using TMPro;
using UnityEngine;

namespace TLA.UI.Components
{
    /// <summary>One notification card. Driven by <see cref="NotificationFeed"/>.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class NotificationToast : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text message;
        [SerializeField] private float fadeSeconds = 0.25f;

        private CanvasGroup _group;
        private float _endTime;
        private float _startTime;

        public bool IsPlaying { get; private set; }

        public void Play(string titleText, string messageText, float seconds)
        {
            _group ??= GetComponent<CanvasGroup>();
            title.text = titleText;
            message.text = messageText;
            message.gameObject.SetActive(!string.IsNullOrEmpty(messageText));
            _startTime = Time.unscaledTime;
            _endTime = _startTime + seconds;
            _group.alpha = 0f;
            IsPlaying = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!IsPlaying) return;
            float now = Time.unscaledTime;
            float fadeIn = Mathf.Clamp01((now - _startTime) / fadeSeconds);
            float fadeOut = Mathf.Clamp01((_endTime - now) / fadeSeconds);
            _group.alpha = Mathf.Min(fadeIn, fadeOut);

            if (now >= _endTime)
            {
                IsPlaying = false;
                gameObject.SetActive(false);
            }
        }
    }
}
