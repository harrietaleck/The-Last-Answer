using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TLA.Systems.Save
{
    /// <summary>
    /// The full contents of one save file:
    ///  - a header (version, time, scene, checksum, summary) used for loading and for the slot list;
    ///  - one "section" per <see cref="ISaveable"/> system, keyed by its SaveKey.
    ///
    /// VERSION HISTORY (bump <see cref="CurrentSaveVersion"/> and add a step in SaveMigrations):
    ///   1  header + sections
    ///   2  + checksum of the sections (detects damaged / incomplete / edited files)
    ///      + summary for the slot list (case, objective, status…)
    ///      + interview and player-location sections
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentSaveVersion = 2;

        public int saveVersion = CurrentSaveVersion;

        /// <summary>Game build that wrote the file (Application.version). Informational only.</summary>
        public string gameVersion;

        /// <summary>UTC time the save was written, ISO 8601 text (e.g. "2026-11-10T14:03:00Z").</summary>
        public string savedAtUtc;

        /// <summary>Content scene to load when this save is loaded.</summary>
        public string sceneName;

        /// <summary>SHA-256 (hex) of the compact JSON of <see cref="sections"/>. Empty in version 1 files.</summary>
        public string checksum;

        /// <summary>What the slot list shows, without loading the whole save.</summary>
        public SaveSummary summary = new SaveSummary();

        public Dictionary<string, JToken> sections = new Dictionary<string, JToken>();
    }

    /// <summary>Human-readable description of a save for the slot list. Plain text only, never credentials.</summary>
    [Serializable]
    public sealed class SaveSummary
    {
        public string caseId = "";
        public string caseTitle = "";
        public string objective = "";
        public string status = "";
        public string locationName = "";
        public int evidenceCollected;
        public int evidenceTotal;
        public float playTimeSeconds;
    }

    /// <summary>What the slot list knows about one slot (read from the header only).</summary>
    public sealed class SaveSlotInfo
    {
        public int Slot;
        public bool IsAutosave;
        public bool Exists;

        /// <summary>True if the save can be loaded (possibly from its backup).</summary>
        public bool IsValid;

        /// <summary>The main file was damaged and the backup is what would be loaded.</summary>
        public bool UsesBackup;

        /// <summary>Why the slot can't be loaded (null when valid or empty).</summary>
        public string Problem;

        public int Version;
        public DateTime SavedAtUtc;
        public string SceneName;
        public SaveSummary Summary;
    }
}
