using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Systems;
using TLA.Systems.Flow;
using TLA.UI.Components;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Menus
{
    /// <summary>
    /// PAUSE MENU (global, in Bootstrap). Appears whenever the game state is Paused (Esc).
    /// Left column: Resume · Notebook · Evidence · Settings · Save Investigation · Main Menu · Exit Game.
    /// Right side: a summary of the investigation notes. The full notebook (J) is its own screen.
    /// Stays hidden while an evidence overlay is open on top of the paused game.
    ///
    /// It never pauses or un-pauses by itself except through GameStateManager, so the state machine
    /// stays the single source of truth.
    /// </summary>
    public sealed class PauseMenuController : UIScreen
    {
        [SerializeField] private NotebookView notebook;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button evidenceButton;
        [SerializeField] private Button notebookButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button exitButton;

        protected override void Awake()
        {
            base.Awake();
            resumeButton.onClick.AddListener(OnResume);
            if (evidenceButton != null) evidenceButton.onClick.AddListener(OnEvidence);
            if (notebookButton != null) notebookButton.onClick.AddListener(OnNotebook);
            settingsButton.onClick.AddListener(OnSettings);
            saveButton.onClick.AddListener(OnSave);
            if (loadButton != null) loadButton.onClick.AddListener(OnLoad);
            mainMenuButton.onClick.AddListener(OnMainMenu);
            exitButton.onClick.AddListener(OnExit);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<SettingsClosedEvent>(OnSettingsClosed);
            EventBus.Subscribe<UIOverlayChangedEvent>(OnOverlayChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<SettingsClosedEvent>(OnSettingsClosed);
            EventBus.Unsubscribe<UIOverlayChangedEvent>(OnOverlayChanged);
        }

        private void OnDestroy()
        {
            resumeButton.onClick.RemoveListener(OnResume);
            if (evidenceButton != null) evidenceButton.onClick.RemoveListener(OnEvidence);
            if (notebookButton != null) notebookButton.onClick.RemoveListener(OnNotebook);
            settingsButton.onClick.RemoveListener(OnSettings);
            saveButton.onClick.RemoveListener(OnSave);
            if (loadButton != null) loadButton.onClick.RemoveListener(OnLoad);
            mainMenuButton.onClick.RemoveListener(OnMainMenu);
            exitButton.onClick.RemoveListener(OnExit);
        }

        private void OnStateChanged(GameStateChangedEvent evt) => SetVisible(evt.Current == GameState.Paused && UIOverlay.OpenCount == 0);

        private void OnOverlayChanged(UIOverlayChangedEvent evt) =>
            SetVisible(evt.OpenCount == 0 && Services.TryGet(out GameStateManager state) && state.IsPaused);

        private void OnEvidence() => EventBus.Publish(new EvidenceInventoryRequestedEvent());

        private void OnNotebook() => EventBus.Publish(new NotebookRequestedEvent());


        protected override void OnShown()
        {
            notebook.Refresh();
        }

        private void OnSettingsClosed(SettingsClosedEvent evt)
        {
            if (Services.TryGet(out GameStateManager state) && state.IsPaused) Show();
        }

        private void OnResume()
        {
            if (Services.TryGet(out GameStateManager state)) state.Resume();
        }

        private void OnSettings()
        {
            Hide();
            EventBus.Publish(new SettingsRequestedEvent());
        }

        private void OnSave() => EventBus.Publish(new SaveSlotsRequestedEvent(true));

        private void OnLoad() => EventBus.Publish(new SaveSlotsRequestedEvent(false));

        private void OnMainMenu()
        {
            if (Services.TryGet(out GameManager game)) game.GoToMainMenu();
        }

        private void OnExit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
