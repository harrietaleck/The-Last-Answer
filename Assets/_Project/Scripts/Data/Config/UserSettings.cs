using System;

namespace TLA.Data.Config
{
    /// <summary>
    /// Settings the PLAYER can change (unlike <see cref="GameConfig"/>, which the developer sets).
    /// Saved to settings.json in the player's save folder, separate from save games, so changing
    /// options never touches story progress.
    ///
    /// Keep this a plain data class with public fields so it serialises to JSON cleanly.
    /// When adding a field, give it a sensible default: old settings files won't contain it.
    /// </summary>
    [Serializable]
    public sealed class UserSettings
    {
        /// <summary>Bump when the meaning of existing fields changes, so old files can be migrated.</summary>
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        /// <summary>0 = silent, 1 = full volume.</summary>
        public float masterVolume = 1f;

        /// <summary>Multiplier for first-person mouse look (used from the player stage onward).</summary>
        public float mouseSensitivity = 1f;

        /// <summary>Mock (offline, scripted) or Online (via our backend, later stage).</summary>
        public AIProviderMode aiMode = AIProviderMode.Mock;

        public static UserSettings CreateDefault(GameConfig config)
        {
            return new UserSettings
            {
                aiMode = config != null ? config.AI.DefaultMode : AIProviderMode.Mock
            };
        }

        /// <summary>Clamps values loaded from disk, in case the file was edited by hand or is corrupted.</summary>
        public void Sanitize()
        {
            masterVolume = Math.Clamp(masterVolume, 0f, 1f);
            mouseSensitivity = Math.Clamp(mouseSensitivity, 0.05f, 10f);
            if (!Enum.IsDefined(typeof(AIProviderMode), aiMode))
                aiMode = AIProviderMode.Mock;
        }
    }
}
