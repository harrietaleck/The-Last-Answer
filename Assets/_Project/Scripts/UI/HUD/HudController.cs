using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Systems;
using TLA.Systems.Flow;
using TLA.Systems.Investigation;
using TMPro;
using UnityEngine;

namespace TLA.UI.HUD
{
    /// <summary>
    /// The investigation HUD: current case (top-left), current objective under it, and the
    /// evidence counter (top-right). Visible only while investigating.
    ///
    /// Reads CaseSessionService when it appears and then reacts to its events. It never changes
    /// game state. A changed objective briefly shows a small red "NEW OBJECTIVE" tag.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup caseBlock;
        [SerializeField] private TMP_Text caseLabel;
        [SerializeField] private TMP_Text objectiveLabel;
        [SerializeField] private TMP_Text objectiveTag;
        [SerializeField] private CanvasGroup evidenceBlock;
        [SerializeField] private TMP_Text evidenceCount;

        [Tooltip("Seconds the NEW OBJECTIVE tag stays visible.")]
        [SerializeField] private float newObjectiveSeconds = 4f;

        private CanvasGroup _root;
        private float _tagUntil;

        private void Awake()
        {
            _root = GetComponent<CanvasGroup>();
            _root.interactable = false;
            _root.blocksRaycasts = false;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<CaseStartedEvent>(OnCaseStarted);
            EventBus.Subscribe<ObjectiveChangedEvent>(OnObjectiveChanged);
            EventBus.Subscribe<EvidenceAddedEvent>(OnEvidenceAdded);
            RefreshAll();
            SetVisible(Services.TryGet(out GameStateManager state) && state.Current == GameState.Investigating);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<CaseStartedEvent>(OnCaseStarted);
            EventBus.Unsubscribe<ObjectiveChangedEvent>(OnObjectiveChanged);
            EventBus.Unsubscribe<EvidenceAddedEvent>(OnEvidenceAdded);
        }

        private void Update()
        {
            if (objectiveTag != null)
                objectiveTag.alpha = Mathf.MoveTowards(objectiveTag.alpha, Time.unscaledTime < _tagUntil ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        }

        private void OnStateChanged(GameStateChangedEvent evt) => SetVisible(evt.Current == GameState.Investigating);
        private void OnCaseStarted(CaseStartedEvent evt) => RefreshAll();
        private void OnEvidenceAdded(EvidenceAddedEvent evt) => RefreshEvidence();

        private void OnObjectiveChanged(ObjectiveChangedEvent evt)
        {
            RefreshAll();
            if (!string.IsNullOrEmpty(evt.Objective)) _tagUntil = Time.unscaledTime + newObjectiveSeconds;
        }

        private void SetVisible(bool visible)
        {
            _root.alpha = visible ? 1f : 0f;
        }

        private void RefreshAll()
        {
            CaseSessionService session = Services.TryGet(out GameManager game) ? game.Case : null;
            bool hasCase = session != null && session.HasActiveCase;

            if (caseBlock != null) caseBlock.alpha = hasCase ? 1f : 0f;
            if (evidenceBlock != null) evidenceBlock.alpha = hasCase ? 1f : 0f;
            if (!hasCase) return;

            CaseDefinition c = session.CurrentCase;
            caseLabel.text = $"CASE {c.caseNumber}  ·  {c.title.ToUpperInvariant()}";
            objectiveLabel.text = string.IsNullOrEmpty(session.Objective) ? "-" : session.Objective;
            RefreshEvidence();
        }

        private void RefreshEvidence()
        {
            CaseSessionService session = Services.TryGet(out GameManager game) ? game.Case : null;
            if (session == null || !session.HasActiveCase) return;
            int total = session.CurrentCase.EvidenceTotal;
            evidenceCount.text = total > 0 ? $"{session.Evidence.Count} / {total}" : session.Evidence.Count.ToString();
        }
    }
}
