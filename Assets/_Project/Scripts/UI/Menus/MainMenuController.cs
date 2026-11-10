using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Systems;
using TLA.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TLA.UI.Menus
{
    /// <summary>
    /// Main menu. Presentation only: each button calls the GameManager or opens another screen.
    ///   New Investigation      → starts the first available case
    ///   Continue Investigation → loads the newest valid save, autosave or manual (disabled when there's none)
    ///   Load Investigation     → the save slot screen (choose any save)
    ///   Case Files             → the case list screen
    ///   Settings               → the global Settings screen (in Bootstrap)
    ///   Exit Game              → quits (stops Play Mode in the Editor)
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private UIScreen mainPanel;
        [SerializeField] private CaseFilesScreen caseFiles;

        [SerializeField] private Button newInvestigationButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button caseFilesButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        [Tooltip("Optional label under Continue, e.g. the saved case name.")]
        [SerializeField] private TMP_Text continueDetail;
        [SerializeField] private TMP_Text versionLabel;

        private static readonly Color DetailIdle = new Color(0.36078432f, 0.38039216f, 0.40784314f, 1f);
        private static readonly Color DetailHot = new Color(0.93f, 0.84f, 0.82f, 1f);

        private void OnEnable()
        {
            newInvestigationButton.onClick.AddListener(OnNewInvestigation);
            continueButton.onClick.AddListener(OnContinue);
            if (loadButton != null) loadButton.onClick.AddListener(OnLoad);
            EventBus.Subscribe<SaveSlotsClosedEvent>(OnSlotsClosed);
            caseFilesButton.onClick.AddListener(OnCaseFiles);
            settingsButton.onClick.AddListener(OnSettings);
            exitButton.onClick.AddListener(OnExit);
            EventBus.Subscribe<SettingsClosedEvent>(OnSettingsClosed);
        }

        private void OnDisable()
        {
            newInvestigationButton.onClick.RemoveListener(OnNewInvestigation);
            continueButton.onClick.RemoveListener(OnContinue);
            if (loadButton != null) loadButton.onClick.RemoveListener(OnLoad);
            EventBus.Unsubscribe<SaveSlotsClosedEvent>(OnSlotsClosed);
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

        private void LateUpdate()
        {
            if (continueDetail == null || continueButton == null) return;
            EventSystem events = EventSystem.current;
            bool hot = continueButton.interactable
                && events != null
                && events.currentSelectedGameObject == continueButton.gameObject;
            continueDetail.color = hot ? DetailHot : DetailIdle;
        }

        private void RefreshContinue()
        {
            GameManager game = Services.TryGet(out GameManager g) ? g : null;
            int? slot = game?.Saves.MostRecentSlot();
            continueButton.interactable = slot.HasValue;
            if (loadButton != null) loadButton.interactable = game != null && game.Saves.GetAllSlots().Exists(s => s.Exists);
            if (continueDetail == null) return;
            if (!slot.HasValue)
            {
                continueDetail.text = "No saved investigation";
                return;
            }
            TLA.Systems.Save.SaveSlotInfo info = game.Saves.GetSlotInfo(slot.Value);
            string when = info.SavedAtUtc.ToLocalTime().ToString("d MMM, HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            string what = string.IsNullOrEmpty(info.Summary?.objective) ? info.Summary?.caseTitle : info.Summary.objective;
            continueDetail.text = $"{what}  ·  {(info.IsAutosave ? "autosave" : "slot " + (slot.Value + 1))}, {when}";
        }

        private void OnSlotsClosed(SaveSlotsClosedEvent evt) => RefreshContinue();

        private void OnLoad() => EventBus.Publish(new SaveSlotsRequestedEvent(false));

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
            if (Services.TryGet(out GameManager game) && !game.ContinueGame()) RefreshContinue(); // e.g. the save was damaged meanwhile
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
