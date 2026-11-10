using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Config;
using UnityEngine;

namespace TLA.Systems.Save
{
    /// <summary>
    /// Saves and loads game progress in numbered slots.
    ///
    /// SAVING:  asks every registered <see cref="ISaveable"/> for its state, bundles everything into
    ///          a <see cref="SaveData"/>, converts it to JSON and hands it to the storage.
    /// LOADING: reads the JSON, checks the version, and gives each ISaveable its section back.
    ///          The caller (GameManager) then loads the scene stored in the save.
    ///
    /// The service never knows WHAT the systems save, so new systems can be added without
    /// changing it.
    /// </summary>
    public sealed class SaveService : IGameService
    {
        // TypeNameHandling.None (the default) is deliberate: it stops a tampered save file from
        // making the game create arbitrary .NET types, which is a known security risk.
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Include
        };

        private readonly ISaveStorage _storage;
        private readonly SaveConfig _config;
        private readonly Dictionary<string, ISaveable> _saveables = new Dictionary<string, ISaveable>();

        public SaveService(ISaveStorage storage, SaveConfig config)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public int SlotCount => _config.SlotCount;

        public string StorageLocation => _storage.Location;

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            _saveables.Clear();
        }

        /// <summary>Adds a system to future saves/loads. Each SaveKey must be unique.</summary>
        public void Register(ISaveable saveable)
        {
            if (saveable == null) throw new ArgumentNullException(nameof(saveable));

            if (_saveables.ContainsKey(saveable.SaveKey))
            {
                Debug.LogError($"[Save] A saveable with key '{saveable.SaveKey}' is already registered.");
                return;
            }

            _saveables.Add(saveable.SaveKey, saveable);
        }

        public void Unregister(ISaveable saveable)
        {
            if (saveable != null) _saveables.Remove(saveable.SaveKey);
        }

        public bool HasSave(int slot)
        {
            return IsValidSlot(slot) && _storage.Exists(KeyFor(slot));
        }

        /// <summary>Writes a save to <paramref name="slot"/>. Returns false if anything went wrong.</summary>
        public bool Save(int slot, string sceneName)
        {
            if (!IsValidSlot(slot)) return false;

            try
            {
                var data = new SaveData
                {
                    gameVersion = Application.version,
                    savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    sceneName = sceneName
                };

                foreach (ISaveable saveable in _saveables.Values)
                    data.sections[saveable.SaveKey] = saveable.CaptureState();

                _storage.Write(KeyFor(slot), JsonConvert.SerializeObject(data, JsonSettings));

                Debug.Log($"[Save] Saved slot {slot} ({data.sections.Count} sections).");
                EventBus.Publish(new GameSavedEvent(slot));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Save] Saving slot {slot} failed: {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads the save in <paramref name="slot"/> and restores every registered system.
        /// On success <paramref name="data"/> holds the header (e.g. which scene to load).
        /// </summary>
        public bool Load(int slot, out SaveData data)
        {
            data = null;
            if (!IsValidSlot(slot)) return false;

            if (!_storage.TryRead(KeyFor(slot), out string json))
            {
                Debug.LogWarning($"[Save] No save found in slot {slot}.");
                return false;
            }

            try
            {
                data = JsonConvert.DeserializeObject<SaveData>(json, JsonSettings);
            }
            catch (JsonException exception)
            {
                Debug.LogError($"[Save] Slot {slot} is corrupted and can't be read: {exception.Message}");
                return false;
            }

            if (data == null)
            {
                Debug.LogError($"[Save] Slot {slot} is empty or invalid.");
                return false;
            }

            if (data.saveVersion > SaveData.CurrentSaveVersion)
            {
                Debug.LogError($"[Save] Slot {slot} was made by a newer version of the game " +
                               $"(save format {data.saveVersion}, this build supports {SaveData.CurrentSaveVersion}).");
                data = null;
                return false;
            }

            // FUTURE: if (data.saveVersion < SaveData.CurrentSaveVersion) MigrateToCurrent(data);

            data.sections ??= new Dictionary<string, Newtonsoft.Json.Linq.JToken>();

            foreach (ISaveable saveable in _saveables.Values)
            {
                if (data.sections.TryGetValue(saveable.SaveKey, out var section))
                    saveable.RestoreState(section);
                else
                    Debug.LogWarning($"[Save] Slot {slot} has no '{saveable.SaveKey}' section; that system keeps its defaults.");
            }

            Debug.Log($"[Save] Loaded slot {slot} (saved {data.savedAtUtc}).");
            EventBus.Publish(new GameLoadedEvent(slot));
            return true;
        }

        public void Delete(int slot)
        {
            if (IsValidSlot(slot)) _storage.Delete(KeyFor(slot));
        }

        private bool IsValidSlot(int slot)
        {
            if (slot >= 0 && slot < _config.SlotCount) return true;

            Debug.LogError($"[Save] Slot {slot} is out of range (0 to {_config.SlotCount - 1}).");
            return false;
        }

        private string KeyFor(int slot)
        {
            return _config.SaveFilePrefix + slot.ToString(CultureInfo.InvariantCulture);
        }
    }
}
