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

        /// <summary>Evidence progress: discovered, collected, examined, presented, facts and connections.</summary>
        public EvidenceService Evidence { get; private set; }

        /// <summary>Statements, contradictions, observations and progress.</summary>
        public InvestigationService Investigation { get; private set; }

        /// <summary>Objectives, suspects questioned, decisions, status, conclusion and ending.</summary>
        public Cases.CaseProgressService Progress { get; private set; }

        /// <summary>Every suspect's interview (history and state), saved with the game.</summary>
        public Dialogue.InterviewService Interviews { get; private set; }

        /// <summary>Where the player stands, saved so a load puts them back there.</summary>
        public World.PlayerLocationService PlayerLocation { get; private set; }

        /// <summary>Sound effects, music, tension layer and volume (lives on this object, persists across scenes).</summary>
        public Audio.AudioManager Audio { get; private set; }

        private bool _autosaveRequested;
        private bool _restoringSave; // between loading a save and its scene finishing loading

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
            EventBus.Subscribe<Cases.ObjectiveCompletedEvent>(OnAutosaveMoment);
            EventBus.Subscribe<Cases.CaseConcludedEvent>(OnAutosaveMoment);
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
            EventBus.Unsubscribe<Cases.ObjectiveCompletedEvent>(OnAutosaveMoment);
            EventBus.Unsubscribe<Cases.CaseConcludedEvent>(OnAutosaveMoment);

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
                PlayerLocation.ClearPending(); // a new game starts at the scene's spawn
                Case.StartCase(caseDefinition);
                // Every case-level system resets itself; none of them is specific to a case.
                foreach (IGameService service in _services)
                    if (service is ICaseScopedService scoped) scoped.BeginCase();
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
            if (!CanSave(out string reason))
            {
                Debug.LogWarning($"[GameManager] Can't save now: {reason}");
                return false;
            }

            return Saves.Save(slot, Scenes.CurrentSceneName);
        }

        /// <summary>Saving needs a loaded content scene and an active case, and no scene change in progress.</summary>
        public bool CanSave(out string reason)
        {
            reason = null;
            if (Scenes.IsLoading) reason = "a scene is loading";
            else if (string.IsNullOrEmpty(Scenes.CurrentSceneName) || Scenes.CurrentSceneName == config.Scenes.MainMenuScene) reason = "no investigation is in progress";
            else if (!Case.HasActiveCase) reason = "no case is active";
            return reason == null;
        }

        /// <summary>Loads the newest valid save (autosave or manual). False if there is none.</summary>
        public bool ContinueGame()
        {
            int? slot = Saves.MostRecentSlot();
            return slot.HasValue && LoadGame(slot.Value);
        }

        /// <summary>Restores all systems from the save, then loads the scene the save was made in.</summary>
        public bool LoadGame(int slot)
        {
            if (Scenes.IsLoading) return false;
            if (!Saves.Load(slot, out SaveData data)) return false; // on failure nothing has changed

            _restoringSave = true;
            _autosaveRequested = false;
            _ = Scenes.LoadSceneAsync(string.IsNullOrEmpty(data.sceneName) ? config.Scenes.NewGameScene : data.sceneName);
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
            Scenes.FadeOutSeconds = config.Audio.SceneFadeOutSeconds; // picture and ambience fade before a scene change
            Audio = gameObject.AddComponent<Audio.AudioManager>();
            Audio.Configure(config.Audio);
            AddService(Audio);
            Saves = AddService(new SaveService(saveStorage, config.Save));
            Case = AddService(new CaseSessionService(config.CaseCatalog));
            Saves.SummaryProvider = BuildSaveSummary;
            Saves.Register(Case); // case progress is part of every save
            Evidence = AddService(new EvidenceService(Case));
            Saves.Register(Evidence); // restored after Case (registration order)
            Investigation = AddService(new InvestigationService(Case, Evidence));
            Saves.Register(Investigation);
            Progress = AddService(new Cases.CaseProgressService(Case, Evidence, Investigation));
            Saves.Register(Progress); // re-evaluates objectives from the restored evidence and statements
            Interviews = AddService(new Dialogue.InterviewService());
            Saves.Register(Interviews);
            PlayerLocation = AddService(new World.PlayerLocationService());
            Saves.Register(PlayerLocation);
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

            // Arriving somewhere new during an investigation is a natural checkpoint (not right after loading a save).
            if (_restoringSave) _restoringSave = false;
            else if (evt.SceneName != config.Scenes.MainMenuScene && Case.HasActiveCase) _autosaveRequested = true;
        }

        private void OnAutosaveMoment<T>(T evt) where T : struct, IGameEvent
        {
            if (!_restoringSave) _autosaveRequested = true;
        }

        private void Update()
        {
            if (!_initialized) return;

            // Play time counts only while actually investigating.
            if (State.Current == GameState.Investigating || State.Current == GameState.Interrogating)
                Progress.AddPlayTime(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            // Autosave at most once per frame, once whatever triggered it has finished updating.
            if (!_initialized || !_autosaveRequested) return;
            GameState s = State.Current;
            if (s != GameState.Investigating && s != GameState.Interrogating && s != GameState.Ending) return;
            _autosaveRequested = false;
            if (CanSave(out _)) Saves.Save(SaveService.AutoSlot, Scenes.CurrentSceneName);
        }

        private SaveSummary BuildSaveSummary()
        {
            var summary = new SaveSummary();
            if (Case.HasActiveCase)
            {
                summary.caseId = Case.CurrentCase.caseId;
                summary.caseTitle = Case.CurrentCase.DisplayName;
                summary.objective = Case.Objective;
                summary.evidenceCollected = Evidence.GetCollected().Count;
                summary.evidenceTotal = Case.CurrentCase.EvidenceTotal;
            }
            summary.status = Progress.Status == Cases.InvestigationStatus.Concluded
                ? "Closed" + (Progress.EndingId != null ? " · " + EndingTitle() : "")
                : Progress.Status == Cases.InvestigationStatus.ReadyToConclude ? "Ready to close" : "Investigating";
            summary.locationName = Readable(Scenes.CurrentSceneName);
            summary.playTimeSeconds = Progress.PlayTimeSeconds;
            return summary;
        }

        private string EndingTitle()
        {
            foreach (var e in Case.CurrentCase.endings)
                if (e != null && e.endingId == Progress.EndingId) return e.title;
            return "";
        }

        /// <summary>"InterrogationRoom" → "Interrogation Room".</summary>
        private static string Readable(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < sceneName.Length; i++)
            {
                if (i > 0 && char.IsUpper(sceneName[i]) && !char.IsUpper(sceneName[i - 1])) sb.Append(' ');
                sb.Append(sceneName[i]);
            }
            return sb.ToString();
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
