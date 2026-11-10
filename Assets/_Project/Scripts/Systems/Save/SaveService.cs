using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Config;
using UnityEngine;

namespace TLA.Systems.Save
{
    /// <summary>
    /// Saves and loads game progress in numbered slots plus one autosave slot (<see cref="AutoSlot"/>).
    ///
    /// SAVING:  asks every registered <see cref="ISaveable"/> for its state, adds a header (version,
    ///          scene, summary, checksum of the sections) and writes it through the storage, which
    ///          writes safely (temporary file, then swap, keeping the previous file as a backup).
    /// LOADING: reads and checks the file (JSON, version, checksum). If it's damaged, the backup is
    ///          tried instead. Older versions are migrated. Then every system is restored; if any of
    ///          them fails, ALL of them are rolled back to how they were and the load is refused, so a
    ///          bad file can never leave the game half-loaded or crash it.
    ///
    /// The service never knows WHAT the systems save, so new systems can be added without changing it.
    /// </summary>
    public sealed class SaveService : IGameService
    {
        /// <summary>The autosave slot (written automatically, never by the player).</summary>
        public const int AutoSlot = -1;

        // TypeNameHandling.None (the default) is deliberate: it stops a tampered save file from
        // making the game create arbitrary .NET types, which is a known security risk.
        // DateParseHandling.None keeps text exactly as written, so the checksum stays stable.
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Include,
            DateParseHandling = DateParseHandling.None,
            MaxDepth = 64
        };

        private readonly ISaveStorage _storage;
        private readonly SaveConfig _config;
        private readonly List<ISaveable> _saveables = new List<ISaveable>(); // restored in registration order

        public SaveService(ISaveStorage storage, SaveConfig config)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>Number of manual slots (0 … SlotCount-1). The autosave slot is extra.</summary>
        public int SlotCount => _config.SlotCount;

        public string StorageLocation => _storage.Location;

        /// <summary>Fills the summary shown in the slot list (set by the GameManager). Optional.</summary>
        public Func<SaveSummary> SummaryProvider { get; set; }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            _saveables.Clear();
        }

        /// <summary>Adds a system to future saves/loads. Each SaveKey must be unique. Systems are restored in registration order.</summary>
        public void Register(ISaveable saveable)
        {
            if (saveable == null) throw new ArgumentNullException(nameof(saveable));

            if (_saveables.Exists(s => s.SaveKey == saveable.SaveKey))
            {
                Debug.LogError($"[Save] A saveable with key '{saveable.SaveKey}' is already registered.");
                return;
            }

            _saveables.Add(saveable);
        }

        public void Unregister(ISaveable saveable)
        {
            if (saveable != null) _saveables.RemoveAll(s => s.SaveKey == saveable.SaveKey);
        }

        public bool HasSave(int slot)
        {
            return IsValidSlot(slot) && _storage.Exists(KeyFor(slot));
        }

        // ------------------------------------------------------------------ saving

        /// <summary>Writes a save to <paramref name="slot"/>. Returns false if anything went wrong (the previous save is untouched).</summary>
        public bool Save(int slot, string sceneName)
        {
            if (!IsValidSlot(slot)) return false;

            try
            {
                var sections = new Dictionary<string, JToken>();
                foreach (ISaveable saveable in _saveables)
                    sections[saveable.SaveKey] = saveable.CaptureState() ?? JValue.CreateNull();

                SaveSummary summary = null;
                try { summary = SummaryProvider?.Invoke(); }
                catch (Exception e) { Debug.LogWarning($"[Save] Couldn't build the save summary: {e.Message}"); }

                var data = new SaveData
                {
                    gameVersion = Application.version,
                    savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    sceneName = sceneName,
                    summary = summary ?? new SaveSummary(),
                    sections = sections,
                    checksum = Checksum(JObject.FromObject(sections))
                };

                _storage.Write(KeyFor(slot), JsonConvert.SerializeObject(data, JsonSettings));

                Debug.Log($"[Save] Saved {SlotName(slot)} ({data.sections.Count} sections).");
                EventBus.Publish(new GameSavedEvent(slot));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Save] Saving {SlotName(slot)} failed: {exception.Message}");
                return false;
            }
        }

        // ------------------------------------------------------------------ loading

        /// <summary>
        /// Reads the save in <paramref name="slot"/> and restores every registered system.
        /// On success <paramref name="data"/> holds the header (e.g. which scene to load).
        /// On failure nothing in the game has changed.
        /// </summary>
        public bool Load(int slot, out SaveData data)
        {
            data = null;
            if (!IsValidSlot(slot)) return false;

            if (!_storage.Exists(KeyFor(slot)))
            {
                Debug.LogWarning($"[Save] No save found in {SlotName(slot)}.");
                return false;
            }

            if (!ReadSlot(slot, out data, out bool fromBackup, out string problem))
            {
                Debug.LogError($"[Save] {SlotName(slot)} can't be loaded: {problem}");
                data = null;
                return false;
            }
            if (fromBackup) Debug.LogWarning($"[Save] {SlotName(slot)} was damaged; loaded its backup instead.");

            // Remember how everything is now, so a failure part-way can be undone.
            var before = new List<KeyValuePair<ISaveable, JToken>>();
            foreach (ISaveable saveable in _saveables)
            {
                try { before.Add(new KeyValuePair<ISaveable, JToken>(saveable, saveable.CaptureState())); }
                catch (Exception e) { Debug.LogWarning($"[Save] Couldn't snapshot '{saveable.SaveKey}' before loading: {e.Message}"); }
            }

            foreach (ISaveable saveable in _saveables)
            {
                if (!data.sections.TryGetValue(saveable.SaveKey, out JToken section))
                {
                    Debug.LogWarning($"[Save] {SlotName(slot)} has no '{saveable.SaveKey}' section; that system keeps its defaults.");
                    continue;
                }

                try
                {
                    saveable.RestoreState(section);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Save] Restoring '{saveable.SaveKey}' from {SlotName(slot)} failed ({exception.Message}). The load was cancelled and the game is unchanged.");
                    foreach (var pair in before)
                    {
                        try { pair.Key.RestoreState(pair.Value); }
                        catch (Exception e) { Debug.LogError($"[Save] Couldn't roll back '{pair.Key.SaveKey}': {e.Message}"); }
                    }
                    data = null;
                    return false;
                }
            }

            Debug.Log($"[Save] Loaded {SlotName(slot)} (saved {data.savedAtUtc}, format {data.saveVersion}).");
            EventBus.Publish(new GameLoadedEvent(slot));
            return true;
        }

        // ------------------------------------------------------------------ slot list

        /// <summary>Describes one slot without restoring anything.</summary>
        public SaveSlotInfo GetSlotInfo(int slot)
        {
            var info = new SaveSlotInfo { Slot = slot, IsAutosave = slot == AutoSlot };
            if (!IsValidSlot(slot, false)) return info;
            info.Exists = _storage.Exists(KeyFor(slot));
            if (!info.Exists) return info;

            info.IsValid = ReadSlot(slot, out SaveData data, out info.UsesBackup, out info.Problem);
            if (!info.IsValid) return info;

            info.Version = data.saveVersion;
            info.SceneName = data.sceneName;
            info.Summary = data.summary ?? new SaveSummary();
            DateTime.TryParse(data.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out info.SavedAtUtc);
            return info;
        }

        /// <summary>The autosave first, then every manual slot.</summary>
        public List<SaveSlotInfo> GetAllSlots()
        {
            var list = new List<SaveSlotInfo> { GetSlotInfo(AutoSlot) };
            for (int i = 0; i < SlotCount; i++) list.Add(GetSlotInfo(i));
            return list;
        }

        /// <summary>The slot with the newest loadable save (for "Continue"), or null if there is none.</summary>
        public int? MostRecentSlot()
        {
            SaveSlotInfo best = null;
            foreach (SaveSlotInfo info in GetAllSlots())
                if (info.IsValid && (best == null || info.SavedAtUtc > best.SavedAtUtc)) best = info;
            return best?.Slot;
        }

        public void Delete(int slot)
        {
            if (IsValidSlot(slot)) _storage.Delete(KeyFor(slot));
        }

        public static string SlotName(int slot) => slot == AutoSlot ? "the autosave" : $"slot {slot + 1}";

        // ------------------------------------------------------------------ reading and checking

        /// <summary>Reads, checks and migrates a slot; falls back to the backup when the main file is damaged.</summary>
        private bool ReadSlot(int slot, out SaveData data, out bool fromBackup, out string problem)
        {
            fromBackup = false;
            data = null;
            string key = KeyFor(slot);

            if (!_storage.TryRead(key, out string json))
            {
                problem = "no save in this slot";
                return false;
            }

            if (TryParse(json, out data, out problem)) return true;

            string mainProblem = problem;
            if (_storage.TryReadBackup(key, out string backup) && TryParse(backup, out data, out _))
            {
                fromBackup = true;
                problem = null;
                return true;
            }

            problem = mainProblem;
            data = null;
            return false;
        }

        /// <summary>JSON → checked, migrated SaveData. Never throws.</summary>
        private static bool TryParse(string json, out SaveData data, out string problem)
        {
            data = null;
            problem = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                problem = "the file is empty";
                return false;
            }

            JObject root;
            try
            {
                root = JsonConvert.DeserializeObject<JObject>(json, JsonSettings);
                if (root == null) { problem = "the file is empty"; return false; }
                data = root.ToObject<SaveData>(JsonSerializer.Create(JsonSettings));
            }
            catch (Exception exception) // JsonException, or bad values in fields
            {
                problem = "the file is corrupted and can't be read (" + exception.Message + ")";
                data = null;
                return false;
            }

            if (data == null) { problem = "the file is empty or invalid"; return false; }

            if (data.saveVersion <= 0 || data.saveVersion > SaveData.CurrentSaveVersion)
            {
                problem = data.saveVersion > SaveData.CurrentSaveVersion
                    ? $"it was made by a newer version of the game (save format {data.saveVersion}, this build supports {SaveData.CurrentSaveVersion})"
                    : $"unknown save format {data.saveVersion}";
                data = null;
                return false;
            }

            data.sections ??= new Dictionary<string, JToken>();

            // Version 2+: the sections must match the checksum written with them.
            if (data.saveVersion >= 2)
            {
                var sections = root["sections"] as JObject;
                if (sections == null || string.IsNullOrEmpty(data.checksum) || Checksum(sections) != data.checksum)
                {
                    problem = "the file is incomplete or was modified (checksum mismatch)";
                    data = null;
                    return false;
                }
            }

            SaveMigrations.MigrateToCurrent(data);
            return true;
        }

        private static string Checksum(JObject sections)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(sections.ToString(Formatting.None));
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private bool IsValidSlot(int slot, bool log = true)
        {
            if (slot == AutoSlot || (slot >= 0 && slot < _config.SlotCount)) return true;
            if (log) Debug.LogError($"[Save] Slot {slot} is out of range (0 to {_config.SlotCount - 1}).");
            return false;
        }

        private string KeyFor(int slot)
        {
            return slot == AutoSlot ? _config.SaveFilePrefix + "auto" : _config.SaveFilePrefix + slot.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Upgrades older save files in memory, one version at a time, so every system only ever sees
    /// the current format. Add a step here whenever <see cref="SaveData.CurrentSaveVersion"/> goes up.
    /// </summary>
    public static class SaveMigrations
    {
        public static void MigrateToCurrent(SaveData data)
        {
            if (data.saveVersion < 2)
            {
                // 1 → 2: no checksum or summary yet; interviews and player location weren't saved
                // (those systems keep their defaults: conversations start fresh, the player uses the scene's spawn).
                data.summary ??= new SaveSummary();
                data.saveVersion = 2;
            }
        }
    }
}
