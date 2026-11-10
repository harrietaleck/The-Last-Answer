using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TLA.Systems.Save
{
    /// <summary>
    /// The full contents of one save file. It has two parts:
    ///  - a small header (version, time, scene) used for loading and for showing save slots;
    ///  - one "section" per <see cref="ISaveable"/> system, keyed by its SaveKey.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>
        /// Format version of the save file. Increase this when the structure changes in a way old
        /// files can't be read as-is, and add a migration step in SaveService.
        /// </summary>
        public const int CurrentSaveVersion = 1;

        public int saveVersion = CurrentSaveVersion;

        /// <summary>Game build that wrote the file (Application.version). Informational only.</summary>
        public string gameVersion;

        /// <summary>UTC time the save was written, ISO 8601 text (e.g. "2026-11-10T14:03:00Z").</summary>
        public string savedAtUtc;

        /// <summary>Content scene to load when this save is loaded.</summary>
        public string sceneName;

        public Dictionary<string, JToken> sections = new Dictionary<string, JToken>();
    }
}
