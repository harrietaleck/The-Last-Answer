using System.Collections.Generic;
using System.IO;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Data.Config;
using TLA.Systems.Config;
using TLA.Systems.Investigation;
using TLA.Systems.Flow;
using TLA.Systems.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TLA.Systems
{
    /// <summary>
    /// The root of the game. Lives in the Bootstrap scene, which stays loaded for the whole session.
    ///
    /// RESPONSIBILITIES (and only these):
    ///  1. Create every core service once, in a known order, and register it in <see cref="Services"/>.
    ///  2. Start the game flow: load the main menu, or adopt a scene you pressed Play in.
    ///  3. Offer a few high-level commands (new game, load scene, save, load) that coordinate
    ///     several services.
    ///  4. Shut everything down cleanly when the game closes.
    ///
    /// Gameplay rules do NOT belong here. They go into their own services, so this class stays small.
    /// </summary>
    [DefaultExecutionOrder(-1000)] // run Awake before any other script, so services exist early
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        [Tooltip("The project's GameConfig asset (Assets/_Project/Data/Settings/GameConfig.asset).")]
        [SerializeField] private GameConfig config;

        private readonly List<IGameService> _services = new List<IGameService>();
        private bool _initialized;

        public GameStateManager State { get; private set; }
        public SceneLoader Scenes { get; private set; }
        public SaveService Saves { get; private set; }
        public ConfigService Config { get; private set; }
        public CaseSessionService Case { get; private set; }

        private void Awake()
        {
            // Only one GameManager may exist (e.g. if Bootstrap was accidentally loaded twice).
            if (Services.TryGet(out GameManager existing) && existing != this)
            {
                Debug.LogWarning("[GameManager] A GameManager already exists. Destroying the duplicate.");
                Destroy(gameObject);
                return;
            }

            if (config == null)
            {
                Debug.LogError("[GameManager] No GameConfig assigned. Select the GameManager in the Bootstrap " +
                               "scene and drag Assets/_Project/Data/Settings/GameConfig.asset into the Config field.");
                enabled = false;
                return;
            }

            CreateServices();

            EventBus.Subscribe<SceneLoadedEvent>(OnSceneLoaded);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            _initialized = true;
        }

        private void Start()
        {
            if (!_initialized) return;

            if (Scenes.TryAdoptLoadedScene(out Scene scene))
            {
                // Editor shortcut: you pressed Play in a content scene. Keep it, enter its state.
                Debug.Log($"[GameManager] Started directly in '{scene.name}'.");
                ApplySceneState(scene);
            }
            else
            {
                GoToMainMenu();
            }
        }

        private void OnDestroy()
        {
            if (!_initialized) return;

            EventBus.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);

            // Shut down in reverse creation order: later services may depend on earlier ones.
            for (int i = _services.Count - 1; i >= 0; i--)
                _services[i].Shutdown();

            _services.Clear();
            Services.Clear();
            EventBus.Clear();
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ public commands

        public void GoToMainMenu()
        {
            _ = Scenes.LoadSceneAsync(config.Scenes.MainMenuScene);
        }

        /// <summary>
        /// Starts a fresh investigation of <paramref name="caseDefinition"/> (default: the first available
        /// case in the catalog) and loads its start scene.
        /// </summary>
        public void StartNewGame(CaseDefinition caseDefinition = null)
        {
            caseDefinition ??= config.CaseCatalog != null ? config.CaseCatalog.DefaultCase : null;
            if (caseDefinition != null)
            {
                Case.StartCase(caseDefinition);
                _ = Scenes.LoadSceneAsync(string.IsNullOrEmpty(caseDefinition.startScene) ? config.Scenes.NewGameScene : caseDefinition.startScene);
                return;
            }

            Debug.LogWarning("[GameManager] No case available in the CaseCatalog; starting without a case.");
            _ = Scenes.LoadSceneAsync(config.Scenes.NewGameScene);
        }

        public void LoadScene(string sceneName)
        {
            _ = Scenes.LoadSceneAsync(sceneName);
        }

        public bool SaveGame(int slot)
        {
            if (string.IsNullOrEmpty(Scenes.CurrentSceneName))
            {
                Debug.LogWarning("[GameManager] Nothing to save: no content scene is loaded.");
                return false;
            }

            return Saves.Save(slot, Scenes.CurrentSceneName);
        }

        /// <summary>Restores all systems from the save, then loads the scene the save was made in.</summary>
        public bool LoadGame(int slot)
        {
            if (Scenes.IsLoading) return false;
            if (!Saves.Load(slot, out SaveData data)) return false;

            _ = Scenes.LoadSceneAsync(data.sceneName);
            return true;
        }

        // ------------------------------------------------------------------ internals

        private void CreateServices()
        {
            // Settings live in the save folder root; save games in a "Saves" sub-folder.
            var settingsStorage = new FileSaveStorage(Application.persistentDataPath);
            var saveStorage = new FileSaveStorage(Path.Combine(Application.persistentDataPath, "Saves"));

            Config = AddService(new ConfigService(config, settingsStorage));
            State = AddService(new GameStateManager());
            Scenes = AddService(new SceneLoader());
            Saves = AddService(new SaveService(saveStorage, config.Save));
            Case = AddService(new CaseSessionService(config.CaseCatalog));
            Saves.Register(Case); // case progress is part of every save
            Services.Register(this);

            // Initialize only after ALL services exist, so services may look each other up.
            foreach (IGameService service in _services)
                service.Initialize();
        }

        private T AddService<T>(T service) where T : class, IGameService
        {
            _services.Add(service);
            Services.Register(service);
            return service;
        }

        private void OnSceneLoaded(SceneLoadedEvent evt)
        {
            ApplySceneState(SceneManager.GetSceneByName(evt.SceneName));
        }

        /// <summary>Switches to the state declared by the scene's <see cref="SceneContext"/>, if it has one.</summary>
        private void ApplySceneState(Scene scene)
        {
            SceneContext context = FindSceneContext(scene);
            if (context == null)
            {
                Debug.LogWarning($"[GameManager] Scene '{scene.name}' has no SceneContext, so the game state was not changed.");
                return;
            }

            GameState target = context.StateOnLoad;
            if (target == State.Current) return;

            // A finished scene load is authoritative: the scene IS now the MainMenu / Interrogation / ...
            // (e.g. loading a save made mid-interrogation from the Ending screen). The transition rules
            // guard gameplay-initiated changes, so here we use them when they allow it and force otherwise.
            if (State.CanChangeTo(target))
                State.TryChangeState(target);
            else
                State.ForceState(target);
        }

        private static SceneContext FindSceneContext(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var context = root.GetComponentInChildren<SceneContext>(true);
                if (context != null) return context;
            }

            return null;
        }

        private static void OnGameStateChanged(GameStateChangedEvent evt)
        {
            // Pausing freezes physics, animations and anything using Time.deltaTime.
            Time.timeScale = evt.Current == GameState.Paused ? 0f : 1f;
        }
    }
}
