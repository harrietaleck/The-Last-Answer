using System.Collections.Generic;
using UnityEngine;

namespace TLA.Data.Cases
{
    /// <summary>
    /// One investigation (a "case"). For now it holds what the menus and HUD show: number, title,
    /// briefing, first objective and suspects. The investigation stage will add facts, evidence,
    /// contradictions and endings to this same asset.
    /// Create via Assets → Create → The Last Answer → Cases → Case Definition.
    /// </summary>
    [CreateAssetMenu(fileName = "CASE_New", menuName = "The Last Answer/Cases/Case Definition", order = 20)]
    public sealed class CaseDefinition : ScriptableObject
    {
        [Tooltip("Stable id used by code and save files, e.g. \"CASE_0147\". Never change after release.")]
        public string caseId = "CASE_0000";

        [Tooltip("Shown as the case number, e.g. \"0147\".")]
        public string caseNumber = "0000";

        public string title = "Untitled Case";

        [Tooltip("Where it happened, e.g. \"Harlow Street, Eastside\".")]
        public string location = "";

        [TextArea(4, 10)]
        public string briefing = "";

        [Tooltip("Objective shown when the case starts.")]
        public string initialObjective = "Review the case files.";

        [Tooltip("Scene the investigation starts in.")]
        public string startScene = "PoliceStation";

        [Tooltip("Unticked = shown as locked / coming soon in Case Files.")]
        public bool isAvailable = true;

        [Tooltip("Total evidence items in this case (for the HUD counter). 0 = don't show a total.")]
        [Min(0)] public int evidenceTotal = 0;

        public List<SuspectProfile> suspects = new List<SuspectProfile>();

        /// <summary>"CASE 0147 · Silence on Harlow Street"</summary>
        public string DisplayName => $"CASE {caseNumber}  ·  {title}";
    }
}
