using System;

namespace TLA.Data.Config
{
    /// <summary>
    /// Settings the PLAYER can change (unlike <see cref="GameConfig"/>, which the developer sets).
    /// Saved to settings.json in the player's save folder, separate from save games, so changing
    /// options never touches story progress. Edited through the Settings screen.
    ///
    /// Keep this a plain data class with public fields so it serialises to JSON cleanly.
    /// When adding a field, give it a sensible default: old settings files won't contain it.
    /// </summary>
    [Serializable]
    public sealed class UserSettings
    {
        /// <summary>Bump when the meaning of existing fields changes, so old files can be migrated.</summary>
        public const int CurrentVersion = 1;

        public const float MinFieldOfView = 55f;
        public const float MaxFieldOfView = 90f;

        public int version = CurrentVersion;

        // ---- Audio
        /// <summary>0 = silent, 1 = full volume.</summary>
        public float masterVolume = 1f;

        // ---- Controls
        /// <summary>Multiplier for first-person mouse look.</summary>
        public float mouseSensitivity = 1f;

        /// <summary>Invert vertical mouse look (on top of the developer setting).</summary>
        public bool invertY = false;

        // ---- Display / graphics
        /// <summary>Vertical field of view of the first-person camera, in degrees.</summary>
        public float fieldOfView = 70f;

        /// <summary>Brightness offset in exposure stops (-1 darker … +1 brighter). 0 = as designed.</summary>
        public float brightness = 0f;

        /// <summary>Index into Unity's quality levels. -1 = keep the project default.</summary>
        public int qualityLevel = -1;

        public bool vSync = true;

        /// <summary>UnityEngine.FullScreenMode as an int (0 exclusive, 1 borderless, 3 windowed). -1 = keep current.</summary>
        public int displayMode = -1;

        /// <summary>Screen resolution. 0 = keep the current/native resolution.</summary>
        public int resolutionWidth = 0;
        public int resolutionHeight = 0;

        // ---- AI (later stage)
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
            mouseSensitivity = Math.Clamp(mouseSensitivity, 0.1f, 5f);
            fieldOfView = Math.Clamp(fieldOfView, MinFieldOfView, MaxFieldOfView);
            brightness = Math.Clamp(brightness, -1f, 1f);
            qualityLevel = Math.Max(-1, qualityLevel);
            if (displayMode != -1 && displayMode != 0 && displayMode != 1 && displayMode != 3) displayMode = -1;
            if (resolutionWidth < 0 || resolutionHeight < 0) resolutionWidth = resolutionHeight = 0;
            if (!Enum.IsDefined(typeof(AIProviderMode), aiMode)) aiMode = AIProviderMode.Mock;
        }
    }
}
