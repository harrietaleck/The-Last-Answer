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
    /// PAUSE MENU (global, in Bootstrap). Appears whenever the game state is Paused (Esc), and
    /// opens straight on the notebook when the player presses J. Left column: Resume · Settings ·
    /// Save Investigation · Main Menu · Exit Game. Right side: the Investigation Notes page.
    ///
    /// It never pauses or un-pauses by itself except through GameStateManager, so the state machine
    /// stays the single source of truth.
    /// </summary>
    public sealed class PauseMenuController : UIScreen
    {
        private const int SaveSlot = 0;

        [SerializeField] private NotebookView notebook;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button exitButton;

        protected override void Awake()
        {
            base.Awake();
            resumeButton.onClick.AddListener(OnResume);
            settingsButton.onClick.AddListener(OnSettings);
            saveButton.onClick.AddListener(OnSave);
            mainMenuButton.onClick.AddListener(OnMainMenu);
            exitButton.onClick.AddListener(OnExit);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<NotebookRequestedEvent>(OnNotebookRequested);
            EventBus.Subscribe<SettingsClosedEvent>(OnSettingsClosed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<NotebookRequestedEvent>(OnNotebookRequested);
            EventBus.Unsubscribe<SettingsClosedEvent>(OnSettingsClosed);
        }

        private void OnDestroy()
        {
            resumeButton.onClick.RemoveListener(OnResume);
            settingsButton.onClick.RemoveListener(OnSettings);
            saveButton.onClick.RemoveListener(OnSave);
            mainMenuButton.onClick.RemoveListener(OnMainMenu);
            exitButton.onClick.RemoveListener(OnExit);
        }

        private void OnStateChanged(GameStateChangedEvent evt) => SetVisible(evt.Current == GameState.Paused);

        private void OnNotebookRequested(NotebookRequestedEvent evt)
        {
            if (!Services.TryGet(out GameStateManager state)) return;
            if (state.IsPaused) state.Resume();   // J again closes it
            else state.Pause();                   // opens on the notes page (refused in menus)
        }

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

        private void OnSave()
        {
            if (Services.TryGet(out GameManager game)) game.SaveGame(SaveSlot); // shows "INVESTIGATION SAVED"
        }

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
