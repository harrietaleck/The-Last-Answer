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
        [SerializeField] private DebugConfig debug = new DebugConfig();

        public SceneConfig Scenes => scenes;
        public SaveConfig Save => save;
        public AIConfig AI => ai;
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
    /// (The providers themselves are built in a later stage.)
    /// </summary>
    public enum AIProviderMode
    {
        Mock = 0,
        Online = 1
    }

    [Serializable]
    public sealed class AIConfig
    {
        [Tooltip("Mode used on first launch, before the player changes it in Settings.")]
        [SerializeField] private AIProviderMode defaultMode = AIProviderMode.Mock;

        [Tooltip("Base URL of OUR backend service (used in Online mode, later). Never a vendor URL, never a key.")]
        [SerializeField] private string backendBaseUrl = "";

        [Tooltip("Seconds to wait for an online reply before falling back to mock dialogue.")]
        [Min(1f)]
        [SerializeField] private float requestTimeoutSeconds = 10f;

        public AIProviderMode DefaultMode => defaultMode;
        public string BackendBaseUrl => backendBaseUrl;
        public float RequestTimeoutSeconds => requestTimeoutSeconds;
    }

    [Serializable]
    public sealed class DebugConfig
    {
        [Tooltip("Allow the developer overlay (press F1 to show/hide). Only exists in the Editor and Development builds.")]
        [SerializeField] private bool showDebugOverlay = true;

        public bool ShowDebugOverlay => showDebugOverlay;
    }
}
