using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Audio;
using TLA.Systems;
using TLA.Systems.Dialogue;
using TLA.UI.Components;
using TMPro;
using UnityEngine;

namespace TLA.UI.Dialogue
{
    /// <summary>
    /// A brief, quiet card over the interview scene when the player presents evidence or challenges a
    /// statement with it: "EVIDENCE PRESENTED · CCTV still, 21:40". Fades in, holds, fades out.
    /// Never blocks clicks.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class EvidencePresentationCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text evidenceName;
        [SerializeField] private float fadeIn = 0.3f;
        [SerializeField] private float hold = 1.6f;
        [SerializeField] private float fadeOut = 0.7f;

        private CanvasGroup _group;
        private float _shownAt = -100f;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PlayerPresentedEvidenceEvent>(OnPresented);
            EventBus.Subscribe<PlayerChallengedStatementEvent>(OnChallenged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerPresentedEvidenceEvent>(OnPresented);
            EventBus.Unsubscribe<PlayerChallengedStatementEvent>(OnChallenged);
        }

        private void Update()
        {
            float t = Time.unscaledTime - _shownAt;
            float a = t < fadeIn ? t / fadeIn : t < fadeIn + hold ? 1f : 1f - (t - fadeIn - hold) / fadeOut;
            _group.alpha = Mathf.Clamp01(a);
        }

        private void OnPresented(PlayerPresentedEvidenceEvent evt) => ShowCard("EVIDENCE PRESENTED", evt.EvidenceName);

        private void OnChallenged(PlayerChallengedStatementEvent evt)
        {
            string name = evt.EvidenceId;
            if (Services.TryGet(out GameManager game) && game.Evidence != null)
            {
                var e = game.Evidence.Find(evt.EvidenceId);
                if (e != null) name = e.displayName;
            }
            ShowCard("STATEMENT CHALLENGED", name);
        }

        private void ShowCard(string heading, string name)
        {
            label.text = heading;
            evidenceName.text = name;
            _shownAt = Time.unscaledTime;
            UISound.Play(SoundCue.EvidencePresented);
        }
    }
}
