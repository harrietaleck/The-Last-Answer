using TLA.Core.Events;
using TLA.Systems.Investigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TLA.UI.Investigation
{
    /// <summary>
    /// CONTRADICTION FEEDBACK: a banner over the interview scene whenever a challenge is judged by the
    /// ContradictionChecker (notebook comparisons show their verdict inline instead).
    ///   red    CONTRADICTION       the evidence proves the statement false
    ///   amber  NOT PROVEN YET      right idea, but a fact still needs confirming
    ///   grey   NO CONTRADICTION    the evidence doesn't contradict it (a failed challenge in an interview)
    /// Purely a display of the C# verdict.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ContradictionFeedbackView : MonoBehaviour
    {
        [SerializeField] private Image accentBar;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text statement;
        [SerializeField] private TMP_Text explanation;
        [SerializeField] private float visibleSeconds = 6f;

        private static readonly Color Valid = new Color32(0xC8, 0x43, 0x3A, 0xFF);
        private static readonly Color Unproven = new Color32(0xD3, 0x9A, 0x3A, 0xFF);
        private static readonly Color Invalid = new Color32(0x7A, 0x7D, 0x86, 0xFF);

        private CanvasGroup _group;
        private float _hideAt;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void OnEnable() => EventBus.Subscribe<ContradictionEvaluatedEvent>(OnEvaluated);

        private void OnDisable() => EventBus.Unsubscribe<ContradictionEvaluatedEvent>(OnEvaluated);

        private void Update()
        {
            float target = Time.unscaledTime < _hideAt ? 1f : 0f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime * 5f);
        }

        private void OnEvaluated(ContradictionEvaluatedEvent evt)
        {
            ContradictionResult r = evt.Result;
            if (r == null || !evt.InInterview) return; // notebook comparisons show their verdict inline

            Color color = r.IsValid ? Valid : r.Outcome == ContradictionOutcome.Unproven ? Unproven : Invalid;
            string heading = r.Title;
            if (evt.InInterview)
                heading = r.IsValid ? "CONTRADICTION EXPOSED" : r.Outcome == ContradictionOutcome.Invalid ? "CHALLENGE FAILED" : heading;

            accentBar.color = color;
            title.color = color;
            title.text = heading;
            string who = r.Statement?.suspect != null ? r.Statement.suspect.ShortName + ": " : "";
            statement.text = r.Statement != null ? $"{who}\"{r.Statement.claim}\"  vs  {r.Evidence?.displayName}" : "";
            explanation.text = r.Explanation;
            _hideAt = Time.unscaledTime + visibleSeconds;
        }
    }
}
