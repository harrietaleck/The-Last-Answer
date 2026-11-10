using System.Collections;
using System.Collections.Generic;
using TLA.Core.Events;
using TLA.Data.Cases;
using TLA.Systems;
using TLA.Systems.Dialogue;
using TLA.Systems.Flow;
using TLA.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TLA.UI.Dialogue
{
    /// <summary>
    /// The interview screen, a column on the right while the cinematic camera shows the room:
    ///   SUSPECT PROFILE   initials portrait, name, age · occupation, status, relation, summary
    ///   CONVERSATION      scrolling history of lines
    ///   SUGGESTED         clickable questions
    ///   QUESTION INPUT    free text + Ask button (Enter also sends), "thinking…" indicator,
    ///                     empty-input validation and error messages
    ///   PRESENT EVIDENCE  pick a discovered item to show the suspect
    ///   END INTERVIEW
    ///
    /// UI ONLY: it shows what the dialogue system publishes (see DialogueContracts) and publishes
    /// PlayerQuestionSubmittedEvent / InterviewLeaveRequestedEvent. It never decides what the suspect says.
    /// </summary>
    public sealed class DialoguePanel : UIScreen
    {
        [Header("Suspect profile")]
        [SerializeField] private TMP_Text portraitInitials;
        [SerializeField] private TMP_Text suspectName;
        [SerializeField] private TMP_Text suspectMeta;
        [SerializeField] private TMP_Text suspectStatus;
        [SerializeField] private TMP_Text suspectSummary;

        [Header("Conversation")]
        [SerializeField] private ScrollRect historyScroll;
        [SerializeField] private DialogueEntryView entryTemplate;
        [SerializeField] private Transform historyContainer;
        [SerializeField] private CanvasGroup thinkingIndicator;
        [SerializeField] private TMP_Text thinkingLabel;

        [Header("Questions")]
        [SerializeField] private Button suggestionTemplate;
        [SerializeField] private Transform suggestionContainer;
        [SerializeField] private TMP_InputField questionInput;
        [SerializeField] private Button askButton;
        [SerializeField] private Button endInterviewButton;
        [SerializeField] private TMP_Text errorLabel;

        [Header("Present evidence")]
        [SerializeField] private Button presentEvidenceButton;
        [SerializeField] private GameObject evidencePicker;
        [SerializeField] private Button evidenceTemplate;
        [SerializeField] private Transform evidenceContainer;
        [SerializeField] private TMP_Text evidenceEmpty;

        [Tooltip("Seconds an error message stays visible.")]
        [SerializeField] private float errorSeconds = 3.5f;

        [Tooltip("Max lines kept in the history (oldest are recycled).")]
        [SerializeField] private int maxHistory = 60;

        private UIList<DialogueEntryView> _history;
        private UIList<Button> _suggestions;
        private UIList<Button> _evidenceButtons;
        private float _errorUntil;
        private readonly List<SuggestedQuestion> _currentSuggestions = new List<SuggestedQuestion>();
        private bool _interviewActive;
        private bool _thinking;
        private string _suspectDisplayName = "Suspect";

        protected override void Awake()
        {
            base.Awake();
            _history = new UIList<DialogueEntryView>(entryTemplate, historyContainer);
            _suggestions = new UIList<Button>(suggestionTemplate, suggestionContainer);
            _evidenceButtons = new UIList<Button>(evidenceTemplate, evidenceContainer);
            presentEvidenceButton.onClick.AddListener(ToggleEvidencePicker);
            evidencePicker.SetActive(false);
            if (errorLabel != null) errorLabel.alpha = 0f;
            askButton.onClick.AddListener(SubmitTyped);
            endInterviewButton.onClick.AddListener(OnEndInterview);
            questionInput.onSubmit.AddListener(_ => SubmitTyped());
            questionInput.onSelect.AddListener(_ => EventBus.Publish(new TextInputFocusChangedEvent(true)));
            questionInput.onDeselect.AddListener(_ => EventBus.Publish(new TextInputFocusChangedEvent(false)));
            SetThinking(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<InterviewStartedEvent>(OnInterviewStarted);
            EventBus.Subscribe<InterviewEndedEvent>(OnInterviewEnded);
            EventBus.Subscribe<DialogueLineEvent>(OnLine);
            EventBus.Subscribe<SuggestedQuestionsEvent>(OnSuggestions);
            EventBus.Subscribe<SuspectThinkingEvent>(OnThinking);
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<DialogueErrorEvent>(OnError);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InterviewStartedEvent>(OnInterviewStarted);
            EventBus.Unsubscribe<InterviewEndedEvent>(OnInterviewEnded);
            EventBus.Unsubscribe<DialogueLineEvent>(OnLine);
            EventBus.Unsubscribe<SuggestedQuestionsEvent>(OnSuggestions);
            EventBus.Unsubscribe<SuspectThinkingEvent>(OnThinking);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<DialogueErrorEvent>(OnError);
        }

        protected override void Update()
        {
            base.Update();
            // Gentle pulse while waiting for the suspect: the only looping animation in the UI.
            if (_thinking && thinkingIndicator != null)
                thinkingIndicator.alpha = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));

            if (errorLabel != null)
                errorLabel.alpha = Mathf.MoveTowards(errorLabel.alpha, Time.unscaledTime < _errorUntil ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        }

        // ------------------------------------------------------------------ events from the dialogue system

        private void OnInterviewStarted(InterviewStartedEvent evt)
        {
            _interviewActive = true;
            _history.Clear();
            _suggestions.Clear();
            questionInput.text = "";
            evidencePicker.SetActive(false);
            _errorUntil = 0f;
            ShowProfile(evt.Suspect);
            SetThinking(false);
            Show();
        }

        private void OnInterviewEnded(InterviewEndedEvent evt)
        {
            _interviewActive = false;
            Hide();
        }

        // Pausing during an interview hides the panel; resuming brings it back.
        private void OnStateChanged(GameStateChangedEvent evt)
        {
            if (_interviewActive) SetVisible(evt.Current == GameState.Interrogating);
        }

        private void OnLine(DialogueLineEvent evt)
        {
            if (_history.Count >= maxHistory) _history.Clear(); // simple cap; a long interview starts a fresh page
            _history.Add().Set(evt.Speaker, evt.SpeakerName, evt.Text);
            StartCoroutine(ScrollToBottomNextFrame());
        }

        private void OnSuggestions(SuggestedQuestionsEvent evt)
        {
            _currentSuggestions.Clear();
            _suggestions.Clear();
            if (evt.Questions == null) return;

            foreach (SuggestedQuestion q in evt.Questions)
            {
                _currentSuggestions.Add(q);
                Button button = _suggestions.Add();
                button.GetComponentInChildren<TMP_Text>().text = q.Text;
                SuggestedQuestion captured = q;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Submit(captured.Text, captured.Id));
            }
        }

        private void OnThinking(SuspectThinkingEvent evt) => SetThinking(evt.IsThinking);

        // ------------------------------------------------------------------ player actions

        private void SubmitTyped()
        {
            Submit(questionInput.text, null);
            questionInput.text = "";
            questionInput.ActivateInputField(); // keep typing without clicking the box again
        }

        private void Submit(string text, string suggestionId)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowError("Type a question first.");
                return;
            }
            if (_thinking)
            {
                ShowError($"Wait for {_suspectDisplayName} to answer.");
                return;
            }
            EventBus.Publish(new PlayerQuestionSubmittedEvent(text.Trim(), suggestionId));
        }

        private void OnError(DialogueErrorEvent evt) => ShowError(evt.Message);

        private void ShowError(string message)
        {
            if (errorLabel == null) return;
            errorLabel.text = message;
            _errorUntil = Time.unscaledTime + errorSeconds;
        }

        private void ToggleEvidencePicker()
        {
            bool open = !evidencePicker.activeSelf;
            evidencePicker.SetActive(open);
            if (open) BuildEvidenceList();
        }

        /// <summary>Lists the evidence the player has DISCOVERED (from CaseSessionService); clicking one presents it.</summary>
        private void BuildEvidenceList()
        {
            _evidenceButtons.Clear();
            var session = TLA.Core.Services.Services.TryGet(out GameManager game) ? game.Case : null;
            int count = 0;
            if (session != null)
            {
                foreach (TLA.Systems.Investigation.EvidenceRecord e in session.Evidence)
                {
                    Button b = _evidenceButtons.Add();
                    b.GetComponentInChildren<TMP_Text>().text = e.displayName;
                    string id = e.id, name = e.displayName;
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(() => PresentEvidence(id, name));
                    count++;
                }
            }
            if (evidenceEmpty != null) evidenceEmpty.gameObject.SetActive(count == 0);
        }

        private void PresentEvidence(string id, string name)
        {
            if (_thinking)
            {
                ShowError($"Wait for {_suspectDisplayName} to answer.");
                return;
            }
            evidencePicker.SetActive(false);
            EventBus.Publish(new PlayerPresentedEvidenceEvent(id, name));
        }

        private void OnEndInterview()
        {
            EventBus.Publish(new InterviewLeaveRequestedEvent());
        }

        protected override void OnHidden()
        {
            // Make sure gameplay keys work again even if the box still had focus.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == questionInput.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            EventBus.Publish(new TextInputFocusChangedEvent(false));
        }

        // ------------------------------------------------------------------ helpers

        private void ShowProfile(SuspectProfile s)
        {
            _suspectDisplayName = s != null ? s.displayName : "Unknown";
            portraitInitials.text = s != null ? s.Initials : "?";
            suspectName.text = _suspectDisplayName.ToUpperInvariant();
            suspectMeta.text = s != null ? $"{s.age}  ·  {s.occupation}" : "";
            suspectStatus.text = s != null ? s.status.ToUpperInvariant() : "";
            suspectSummary.text = s != null ? $"<color=#9A9FA6>{s.relationToCase}</color>\n{s.summary}" : "";
        }

        private void SetThinking(bool thinking)
        {
            _thinking = thinking;
            if (thinkingIndicator != null) thinkingIndicator.alpha = thinking ? 1f : 0f;
            if (thinkingLabel != null) thinkingLabel.text = $"{_suspectDisplayName.ToUpperInvariant()} IS THINKING";
            askButton.interactable = !thinking;
            presentEvidenceButton.interactable = !thinking;
            foreach (Button b in _suggestions.Items) b.interactable = !thinking;
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null; // wait for the layout to include the new line
            Canvas.ForceUpdateCanvases();
            historyScroll.verticalNormalizedPosition = 0f;
        }
    }
}
