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

        public void Set(DialogueSpeaker who, string speakerName, string text)
        {
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
