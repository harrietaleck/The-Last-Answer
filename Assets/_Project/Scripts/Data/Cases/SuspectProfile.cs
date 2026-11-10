using UnityEngine;

namespace TLA.Data.Cases
{
    /// <summary>How a suspect generally behaves under questioning. Drives tone and fallback lines.</summary>
    public enum SuspectTemperament
    {
        Calm = 0,       // measured, cooperative on the surface
        Nervous = 1,    // hesitant, over-explains, rattles easily
        Defensive = 2,  // irritated, curt, takes questions as attacks
        Arrogant = 3,   // condescending, dismissive
        Evasive = 4     // changes the subject, answers questions with questions
    }

    /// <summary>
    /// Who a suspect is: the public profile shown on the dialogue screen, plus personality settings
    /// used by the dialogue system. What they KNOW, hide and lie about lives in <see cref="knowledge"/>.
    /// Create via Assets → Create → The Last Answer → Cases → Suspect Profile.
    /// </summary>
    [CreateAssetMenu(fileName = "SUS_NewSuspect", menuName = "The Last Answer/Cases/Suspect Profile", order = 21)]
    public sealed class SuspectProfile : ScriptableObject
    {
        [Header("Identity (shown to the player)")]
        [Tooltip("Stable id used by code and save files, e.g. \"SUS_Marlowe\". Never change after release.")]
        public string suspectId = "SUS_New";

        public string displayName = "Unknown";
        public int age = 30;
        public string occupation = "";

        [Tooltip("How they're connected to the case, e.g. \"Business partner of the victim\".")]
        public string relationToCase = "";

        [Tooltip("Status shown on the profile, e.g. \"Person of interest\".")]
        public string status = "Person of interest";

        [TextArea(3, 6)]
        [Tooltip("Short background the investigator already knows before the interview.")]
        public string summary = "";

        [Header("Personality (used by the dialogue system)")]
        public SuspectTemperament temperament = SuspectTemperament.Calm;

        [Tooltip("Starting composure 0-100. High = hard to rattle. Composure falls with pressure, repeats, accusations and contradictions.")]
        [Range(0, 100)] public int baseComposure = 70;

        [TextArea(2, 4)]
        [Tooltip("How they talk, for writers and for the online AI prompt (e.g. \"Clipped, educated, never apologises\").")]
        public string speechStyle = "";

        [Tooltip("What this suspect knows, hides and lies about. Without it, they can only deflect.")]
        public SuspectKnowledge knowledge;

        /// <summary>Initials shown in the portrait placeholder until real portraits exist.</summary>
        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(displayName)) return "?";
                string[] parts = displayName.Split(' ');
                string result = parts[0].Substring(0, 1);
                if (parts.Length > 1 && parts[parts.Length - 1].Length > 0) result += parts[parts.Length - 1].Substring(0, 1);
                return result.ToUpperInvariant();
            }
        }

        /// <summary>First name, used in narration ("Marlowe looks away" uses the last name; see <see cref="ShortName"/>).</summary>
        public string ShortName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(displayName)) return "The suspect";
                string[] parts = displayName.Split(' ');
                return parts[parts.Length - 1];
            }
        }
    }
}
