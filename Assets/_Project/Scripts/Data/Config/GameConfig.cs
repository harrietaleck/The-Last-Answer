using System;
using UnityEngine;

namespace TLA.Data.Config
{
    /// <summary>
    /// Design-time configuration for the whole game, stored as ONE asset:
    /// Assets/_Project/Data/Settings/GameConfig.asset
    ///
    /// Edit values in the Inspector instead of hard-coding them in scripts.
    /// The GameManager (in the Bootstrap scene) holds a reference to this asset and shares it
    /// with the other systems through the ConfigService.
    ///
    /// SECURITY: this asset is shipped inside the game build, where players can read it.
    /// NEVER put API keys, passwords or other secrets here.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "The Last Answer/Config/Game Config", order = 0)]
    public sealed class GameConfig : ScriptableObject
    {
        [SerializeField] private SceneConfig scenes = new SceneConfig();
        [SerializeField] private SaveConfig save = new SaveConfig();
        [SerializeField] private AIConfig ai = new AIConfig();
        [SerializeField] private AudioConfig audio = new AudioConfig();
        [SerializeField] private DebugConfig debug = new DebugConfig();

        [Tooltip("Every case in the game (Data/Cases/CaseCatalog.asset).")]
        [SerializeField] private Cases.CaseCatalog caseCatalog;

        public Cases.CaseCatalog CaseCatalog => caseCatalog;
        public SceneConfig Scenes => scenes;
        public SaveConfig Save => save;
        public AIConfig AI => ai;
        public AudioConfig Audio => audio;
        // Named DebugOptions (not "Debug") so it never clashes with UnityEngine.Debug.
        public DebugConfig DebugOptions => debug;
    }

    /// <summary>Names of the scenes the game flow needs. Each must be listed in File > Build Profiles (scene list).</summary>
    [Serializable]
    public sealed class SceneConfig
    {
        [Tooltip("Loaded after the Bootstrap scene when the game starts.")]
        [SerializeField] private string mainMenuScene = "MainMenu";

        [Tooltip("Loaded when the player starts a new game. Points at the Sandbox test scene until real content exists.")]
        [SerializeField] private string newGameScene = "Sandbox";

        public string MainMenuScene => mainMenuScene;
        public string NewGameScene => newGameScene;
    }

    [Serializable]
    public sealed class SaveConfig
    {
        [Tooltip("Save files are named <prefix><slot>.json, e.g. save_0.json.")]
        [SerializeField] private string saveFilePrefix = "save_";

        [Tooltip("How many save slots the player can use.")]
        [Min(1)]
        [SerializeField] private int slotCount = 3;

        [Tooltip("File (without extension) that stores the player's settings.")]
        [SerializeField] private string settingsFileName = "settings";

        public string SaveFilePrefix => saveFilePrefix;
        public int SlotCount => slotCount;
        public string SettingsFileName => settingsFileName;
    }

    /// <summary>
    /// Which dialogue provider the game uses. This is the ONLY place the choice is configured.
    /// Gameplay code never refers to a specific AI vendor or model. In Online mode the game
    /// talks to OUR backend, which picks the vendor/model and keeps the API key secret.
    /// </summary>
    public enum AIProviderMode
    {
        Mock = 0,
        Online = 1
    }

    /// <summary>
    /// Online dialogue settings. Everything here ships inside the game, so it holds only PUBLIC
    /// values: the address of OUR backend and some limits. The backend holds the AI service's API
    /// key and chooses the vendor and model; none of that is ever stored in the game.
    /// </summary>
    [Serializable]
    public sealed class AIConfig
    {
        [Tooltip("Mode used on first launch, before the player changes it in Settings.")]
        [SerializeField] private AIProviderMode defaultMode = AIProviderMode.Mock;

        [Tooltip("Base URL of OUR backend in released builds, e.g. https://dialogue.example.com. Must be https. Never a vendor URL, never a key. Empty = Online mode unavailable.")]
        [SerializeField] private string backendBaseUrl = "";

        [Tooltip("Backend used in the Editor and Development builds (e.g. the reference backend in /Backend running on this PC). http is allowed only for localhost.")]
        [SerializeField] private string developmentBackendUrl = "http://localhost:8787";

        [Tooltip("Seconds to wait for an online reply before the offline mock answers instead.")]
        [Range(2f, 30f)]
        [SerializeField] private float requestTimeoutSeconds = 10f;

        [Tooltip("Longest spoken line accepted from the backend (characters). Longer replies are cut at a sentence end, or rejected.")]
        [Range(80, 1000)]
        [SerializeField] private int maxReplyCharacters = 320;

        [Tooltip("Longest stage direction accepted from the backend (characters). Longer ones are dropped.")]
        [Range(0, 300)]
        [SerializeField] private int maxNarrationCharacters = 120;

        [Tooltip("How many recent lines of the conversation are sent with each request.")]
        [Range(0, 40)]
        [SerializeField] private int historyLinesSent = 12;

        [Tooltip("After this many failures in a row, stop trying online for a while (the mock answers instantly instead of waiting for timeouts).")]
        [Range(1, 10)]
        [SerializeField] private int failuresBeforePause = 2;

        [Tooltip("Seconds to wait before trying the backend again after repeated failures.")]
        [Range(5f, 600f)]
        [SerializeField] private float pauseAfterFailuresSeconds = 60f;

        public AIProviderMode DefaultMode => defaultMode;
        public string BackendBaseUrl => backendBaseUrl;
        public string DevelopmentBackendUrl => developmentBackendUrl;
        public float RequestTimeoutSeconds => requestTimeoutSeconds;
        public int MaxReplyCharacters => maxReplyCharacters;
        public int MaxNarrationCharacters => maxNarrationCharacters;
        public int HistoryLinesSent => historyLinesSent;
        public int FailuresBeforePause => failuresBeforePause;
        public float PauseAfterFailuresSeconds => pauseAfterFailuresSeconds;

        /// <summary>
        /// The backend this build should use: the development URL in the Editor and Development
        /// builds (if set), otherwise the release URL. Empty when Online mode isn't available.
        /// </summary>
        public string ActiveBackendUrl
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!string.IsNullOrWhiteSpace(developmentBackendUrl)) return developmentBackendUrl.Trim();
#endif
                return (backendBaseUrl ?? string.Empty).Trim();
            }
        }
    }

    /// <summary>Sound and transition timing. Clips themselves live in the AudioLibrary asset.</summary>
    [Serializable]
    public sealed class AudioConfig
    {
        [Tooltip("Which clip plays for each sound cue and music mood (Data/Settings/AudioLibrary.asset).")]
        [SerializeField] private Audio.AudioLibrary library;

        [Tooltip("Seconds to fade to black (and fade the ambience) before a scene change.")]
        [Range(0f, 2f)] [SerializeField] private float sceneFadeOutSeconds = 0.4f;

        [Tooltip("Seconds to crossfade between music moods.")]
        [Range(0.1f, 8f)] [SerializeField] private float musicCrossfadeSeconds = 3f;

        [Tooltip("Most sound effects that can play at the same moment. Extra ones are skipped or replace quieter ones.")]
        [Range(4, 32)] [SerializeField] private int maxSimultaneousEffects = 12;

        [Tooltip("Music and ambience volume while paused (fraction of normal).")]
        [Range(0f, 1f)] [SerializeField] private float pausedVolume = 0.45f;

        public Audio.AudioLibrary Library => library;
        public float SceneFadeOutSeconds => sceneFadeOutSeconds;
        public float MusicCrossfadeSeconds => musicCrossfadeSeconds;
        public int MaxSimultaneousEffects => maxSimultaneousEffects;
        public float PausedVolume => pausedVolume;
    }

    [Serializable]
    public sealed class DebugConfig
    {
        [Tooltip("Allow the developer overlay (press F1 to show/hide). Only exists in the Editor and Development builds.")]
        [SerializeField] private bool showDebugOverlay = true;

        public bool ShowDebugOverlay => showDebugOverlay;
    }
}
