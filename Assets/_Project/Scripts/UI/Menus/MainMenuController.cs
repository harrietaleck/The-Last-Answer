using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Systems;
using TLA.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Menus
{
    /// <summary>
    /// Main menu. Presentation only: each button calls the GameManager or opens another screen.
    ///   New Investigation      → starts the first available case
    ///   Continue Investigation → loads save slot 0 (disabled when there's no save)
    ///   Case Files             → the case list screen
    ///   Settings               → the global Settings screen (in Bootstrap)
    ///   Exit Game              → quits (stops Play Mode in the Editor)
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private const int ContinueSlot = 0;

        [SerializeField] private UIScreen mainPanel;
        [SerializeField] private CaseFilesScreen caseFiles;

        [SerializeField] private Button newInvestigationButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button caseFilesButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        [Tooltip("Optional label under Continue, e.g. the saved case name.")]
        [SerializeField] private TMP_Text continueDetail;
        [SerializeField] private TMP_Text versionLabel;

        private void OnEnable()
        {
            newInvestigationButton.onClick.AddListener(OnNewInvestigation);
            continueButton.onClick.AddListener(OnContinue);
            caseFilesButton.onClick.AddListener(OnCaseFiles);
            settingsButton.onClick.AddListener(OnSettings);
            exitButton.onClick.AddListener(OnExit);
            EventBus.Subscribe<SettingsClosedEvent>(OnSettingsClosed);
        }

        private void OnDisable()
        {
            newInvestigationButton.onClick.RemoveListener(OnNewInvestigation);
            continueButton.onClick.RemoveListener(OnContinue);
            caseFilesButton.onClick.RemoveListener(OnCaseFiles);
            settingsButton.onClick.RemoveListener(OnSettings);
            exitButton.onClick.RemoveListener(OnExit);
            EventBus.Unsubscribe<SettingsClosedEvent>(OnSettingsClosed);
        }

        private void Start()
        {
            if (versionLabel != null) versionLabel.text = "v" + Application.version + "  ·  PROTOTYPE";
            RefreshContinue();
            mainPanel.Show();
            if (caseFiles != null) caseFiles.Closed += ShowMain;
        }

        private void OnDestroy()
        {
            if (caseFiles != null) caseFiles.Closed -= ShowMain;
        }

        private void RefreshContinue()
        {
            bool hasSave = Services.TryGet(out GameManager game) && game.Saves.HasSave(ContinueSlot);
            continueButton.interactable = hasSave;
            if (continueDetail != null) continueDetail.text = hasSave ? "Resume your last saved investigation" : "No saved investigation";
        }

        private void ShowMain()
        {
            mainPanel.Show();
        }

        private void OnNewInvestigation()
        {
            if (Services.TryGet(out GameManager game)) game.StartNewGame();
        }

        private void OnContinue()
        {
            if (Services.TryGet(out GameManager game)) game.LoadGame(ContinueSlot);
        }

        private void OnCaseFiles()
        {
            mainPanel.Hide();
            caseFiles.Show();
        }

        private void OnSettings()
        {
            mainPanel.Hide();
            EventBus.Publish(new SettingsRequestedEvent());
        }

        private void OnSettingsClosed(SettingsClosedEvent evt)
        {
            if (caseFiles == null || !caseFiles.IsVisible) ShowMain();
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
