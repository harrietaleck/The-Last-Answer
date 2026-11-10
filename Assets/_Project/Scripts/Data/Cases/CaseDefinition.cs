using System.Collections.Generic;
using UnityEngine;

namespace TLA.Data.Cases
{
    /// <summary>
    /// One investigation (a "case"), fully data-driven: overview, crime and victim, people, timeline,
    /// evidence, facts, statements, objectives, possible conclusions and endings. The game's systems
    /// read this asset; adding a case means creating a new one and listing it in the CaseCatalog,
    /// with no code changes. See CASE_AUTHORING_GUIDE.md.
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

        [TextArea(2, 4)]
        [Tooltip("One or two sentences for lists and the notebook's case page.")]
        public string summary = "";

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

        [Tooltip("Witnesses (people involved who aren't suspects, or are both). Same profile type as suspects.")]
        public List<SuspectProfile> witnesses = new List<SuspectProfile>();

        [Header("Crime")]
        public CrimeInfo crime = new CrimeInfo();
        public VictimInfo victim = new VictimInfo();

        [Tooltip("Key moments, revealed as their facts are confirmed.")]
        public List<TimelineEntry> timeline = new List<TimelineEntry>();

        [Header("Evidence")]
        [Tooltip("Every piece of evidence in this case. Only items listed here can be collected.")]
        public List<Evidence.EvidenceDefinition> evidence = new List<Evidence.EvidenceDefinition>();

        [Tooltip("Every fact this case can confirm (by examining or connecting evidence, or exposing a contradiction).")]
        public List<Evidence.CaseFact> facts = new List<Evidence.CaseFact>();

        [Header("Investigation")]
        [Tooltip("Suspect statements the player can review, compare with evidence and challenge.")]
        public List<Investigation.SuspectStatement> statements = new List<Investigation.SuspectStatement>();

        [Header("Progression")]
        [Tooltip("Objectives, in display order. The first active, unfinished, non-optional one is the HUD objective.")]
        public List<ObjectiveDefinition> objectives = new List<ObjectiveDefinition>();

        [Tooltip("When the player may close the case (all must hold). Empty = any time.")]
        public CaseCondition[] conclusionUnlockedBy = System.Array.Empty<CaseCondition>();

        [Tooltip("Decision id recorded when the case is closed (objectives/endings can check it).")]
        public string conclusionDecisionId = "DEC_CloseCase";

        public List<ConclusionDefinition> conclusions = new List<ConclusionDefinition>();

        [Tooltip("Checked in order for the chosen conclusion; the first whose conditions hold is shown.")]
        public List<EndingDefinition> endings = new List<EndingDefinition>();

        [Header("Writer's notes (never shown in game)")]
        [TextArea(4, 12)]
        [Tooltip("The hidden truth of the case, for writers. Never sent to AI or shown to the player.")]
        public string solutionNotes = "";

        /// <summary>"CASE 0147 · Silence on Harlow Street"</summary>
        public string DisplayName => $"CASE {caseNumber}  ·  {title}";

        public ObjectiveDefinition FindObjective(string id) => objectives.Find(o => o != null && o.objectiveId == id);

        public ConclusionDefinition FindConclusion(string id) => conclusions.Find(c => c != null && c.conclusionId == id);

        /// <summary>Evidence count for the HUD: the authored list if present, else <see cref="evidenceTotal"/>.</summary>
        public int EvidenceTotal => evidence != null && evidence.Count > 0 ? evidence.Count : evidenceTotal;
    }
}
