using System;
using TLA.Data.Cases;
using TLA.Data.Evidence;
using UnityEngine;

namespace TLA.Data.Investigation
{
    /// <summary>
    /// One way a statement can be proven false: showing <see cref="evidence"/> once every fact in
    /// <see cref="requiresConfirmed"/> is confirmed. Evaluated by the C# ContradictionChecker only.
    /// </summary>
    [Serializable]
    public sealed class ContradictionRule
    {
        [Tooltip("The evidence that contradicts the statement (the player must have collected it).")]
        public EvidenceDefinition evidence;

        [Tooltip("Facts that must be confirmed first for the contradiction to hold. Without them the challenge is 'unproven'.")]
        public CaseFact[] requiresConfirmed = Array.Empty<CaseFact>();

        [TextArea(2, 4)]
        [Tooltip("Why it's a contradiction, shown when it's identified.")]
        public string explanation = "";

        [TextArea(1, 3)]
        [Tooltip("Shown when the evidence is right but the facts aren't confirmed yet, e.g. \"A hidden face proves nothing on its own.\"")]
        public string unprovenHint = "";

        [Tooltip("Fact confirmed when the suspect is confronted with this contradiction (optional).")]
        public CaseFact confirmsFact;
    }

    /// <summary>
    /// A claim a suspect makes, which the player can review, compare with evidence and challenge.
    /// It's recorded in the notebook when the suspect says it (an interview topic) or from the start
    /// if it's already in the case file. Contradictions come ONLY from its authored rules, never from AI.
    /// Create via Assets → Create → The Last Answer → Investigation → Suspect Statement.
    /// </summary>
    [CreateAssetMenu(fileName = "STM_New", menuName = "The Last Answer/Investigation/Suspect Statement", order = 40)]
    public sealed class SuspectStatement : ScriptableObject
    {
        [Tooltip("Unique, stable id, e.g. \"STM_Marlowe_Home\". Never change after release.")]
        public string statementId = "STM_New";

        public SuspectProfile suspect;

        [Tooltip("The interview topic that produces this statement (SuspectKnowledge topic id). Answering it records the statement.")]
        public string topicId = "";

        [TextArea(2, 4)]
        [Tooltip("The claim in the suspect's own words (shown in quotes), e.g. \"I was at home, alone, all evening.\"")]
        public string claim = "";

        [Tooltip("Where it was said, e.g. \"Statement to officers\" or \"Interview\".")]
        public string source = "Interview";

        [Tooltip("Already known when the case starts (e.g. given to the first officers).")]
        public bool recordedFromStart;

        [Tooltip("Ways it can be proven false. Empty = it can't be contradicted with the case's evidence.")]
        public ContradictionRule[] contradictions = Array.Empty<ContradictionRule>();

        public bool CanBeContradicted => contradictions != null && contradictions.Length > 0;
    }
}
