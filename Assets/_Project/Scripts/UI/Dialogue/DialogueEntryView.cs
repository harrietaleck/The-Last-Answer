using TLA.Systems.Dialogue;
using TMPro;
using UnityEngine;

namespace TLA.UI.Dialogue
{
    /// <summary>
    /// One line in the conversation history. Styling depends on who speaks:
    ///   Suspect      name in RED caps, text white (the person under pressure is the focus)
    ///   Investigator "YOU" in grey caps, text light grey
    ///   System       no name, italic muted text (stage directions: "Marlowe looks away.")
    /// </summary>
    public sealed class DialogueEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text speaker;
        [SerializeField] private TMP_Text body;
        [SerializeField] private Color suspectNameColor = new Color32(0xC4, 0x1E, 0x1A, 0xFF);
        [SerializeField] private Color investigatorNameColor = new Color32(0x9A, 0x9F, 0xA6, 0xFF);
        [SerializeField] private Color primaryText = new Color32(0xEE, 0xEE, 0xEF, 0xFF);
        [SerializeField] private Color secondaryText = new Color32(0xC2, 0xC5, 0xCA, 0xFF);
        [SerializeField] private Color mutedText = new Color32(0x6E, 0x73, 0x7A, 0xFF);

        [Tooltip("Seconds for a new line to fade in. 0 = appear instantly.")]
        [SerializeField] private float fadeInSeconds = 0.35f;

        private CanvasGroup _group;
        private float _fadeStart = -1f;

        private void Awake()
        {
            if (!TryGetComponent(out _group)) _group = gameObject.AddComponent<CanvasGroup>();
        }

        private void Update()
        {
            if (_fadeStart < 0f || _group == null) return;
            float t = (Time.unscaledTime - _fadeStart) / Mathf.Max(0.01f, fadeInSeconds);
            _group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            if (t >= 1f) _fadeStart = -1f;
        }

        public void Set(DialogueSpeaker who, string speakerName, string text)
        {
            // Each new line fades in gently (no movement, no typewriter: easy to read).
            if (_group == null) Awake();
            if (fadeInSeconds > 0f)
            {
                _group.alpha = 0f;
                _fadeStart = Time.unscaledTime;
            }

            switch (who)
            {
                case DialogueSpeaker.Suspect:
                    speaker.gameObject.SetActive(true);
                    speaker.text = (speakerName ?? "").ToUpperInvariant();
                    speaker.color = suspectNameColor;
                    body.text = text;
                    body.color = primaryText;
                    body.fontStyle = FontStyles.Normal;
                    break;

                case DialogueSpeaker.Investigator:
                    speaker.gameObject.SetActive(true);
                    speaker.text = "YOU";
                    speaker.color = investigatorNameColor;
                    body.text = text;
                    body.color = secondaryText;
                    body.fontStyle = FontStyles.Normal;
                    break;

                default:
                    speaker.gameObject.SetActive(false);
                    body.text = text;
                    body.color = mutedText;
                    body.fontStyle = FontStyles.Italic;
                    break;
            }
        }
    }
}
