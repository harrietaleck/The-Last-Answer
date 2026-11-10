using System;
using TLA.Data.Evidence;
using TLA.Data.Investigation;
using UnityEngine;

namespace TLA.Data.Cases
{
    // =====================================================================================
    // The narrative parts of a case, all authored data inside a CaseDefinition. The game reads
    // them; nothing about a specific case is written in code.
    // =====================================================================================

    /// <summary>What happened, as the police know it from the start.</summary>
    [Serializable]
    public sealed class CrimeInfo
    {
        [Tooltip("e.g. \"Homicide\".")]
        public string crimeType = "Homicide";

        [Tooltip("When it happened, as known, e.g. \"Night of the 14th\".")]
        public string date = "";

        [Tooltip("e.g. \"21:30 - 22:30 (pathologist's estimate)\".")]
        public string timeOfDeath = "";

        public string location = "";

        [Tooltip("e.g. \"Blunt-force injury to the head\".")]
        public string causeOfDeath = "";

        [Tooltip("e.g. \"Not recovered\".")]
        public string weapon = "";

        [Tooltip("Who found the victim, and when.")]
        public string discoveredBy = "";

        [TextArea(2, 5)] public string details = "";
    }

    /// <summary>Who the victim was.</summary>
    [Serializable]
    public sealed class VictimInfo
    {
        public string name = "";
        public int age;
        public string occupation = "";
        [TextArea(2, 5)] public string background = "";
    }

    /// <summary>One moment in the case timeline. Hidden until <see cref="revealedBy"/> is confirmed (empty = known from the start).</summary>
    [Serializable]
    public sealed class TimelineEntry
    {
        [Tooltip("Sort key and label, e.g. \"21:40\" or \"12th\". Entries are shown in list order.")]
        public string time = "";

        [TextArea(1, 3)] public string description = "";

        [Tooltip("The fact that reveals this entry. Empty = known from the start.")]
        public CaseFact revealedBy;

        public bool isKey;
    }

    /// <summary>What a condition checks. Shared by objectives and endings, evaluated in C# only.</summary>
    public enum CaseConditionType
    {
        EvidenceCollected = 0,
        EvidenceExamined = 1,
        FactConfirmed = 2,
        StatementRecorded = 3,
        StatementExposed = 4,
        SuspectQuestioned = 5,
        ObjectiveCompleted = 6,
        DecisionMade = 7
    }

    /// <summary>
    /// One requirement, e.g. "FACT_MarloweAtOffice confirmed" or "objective OBJ_Interview completed".
    /// Fill in only the field the type uses.
    /// </summary>
    [Serializable]
    public sealed class CaseCondition
    {
        public CaseConditionType type;
        public EvidenceDefinition evidence;
        public CaseFact fact;
        public SuspectStatement statement;
        public SuspectProfile suspect;

        [Tooltip("Objective id (ObjectiveCompleted) or decision id (DecisionMade).")]
        public string id = "";

        [Tooltip("DecisionMade only: the choice that must have been made (empty = any choice).")]
        public string choice = "";

        public override string ToString()
        {
            switch (type)
            {
                case CaseConditionType.EvidenceCollected: return $"collected {evidence?.evidenceId}";
                case CaseConditionType.EvidenceExamined: return $"examined {evidence?.evidenceId}";
                case CaseConditionType.FactConfirmed: return $"fact {fact?.factId}";
                case CaseConditionType.StatementRecorded: return $"heard {statement?.statementId}";
                case CaseConditionType.StatementExposed: return $"exposed {statement?.statementId}";
                case CaseConditionType.SuspectQuestioned: return $"questioned {suspect?.suspectId}";
                case CaseConditionType.ObjectiveCompleted: return $"objective {id}";
                default: return $"decision {id}{(string.IsNullOrEmpty(choice) ? "" : "=" + choice)}";
            }
        }
    }

    /// <summary>An investigation objective. Active once its prerequisites are done; complete when all its conditions hold.</summary>
    [Serializable]
    public sealed class ObjectiveDefinition
    {
        [Tooltip("Unique, stable id within the case, e.g. \"OBJ_BreakAlibi\".")]
        public string objectiveId = "OBJ_New";

        [Tooltip("Shown on the HUD and in the notebook, e.g. \"Break Marlowe's alibi\".")]
        public string title = "";

        [TextArea(2, 4)] public string description = "";

        [Tooltip("Optional objectives don't block closing the case, but can change the ending.")]
        public bool optional;

        [Tooltip("Objective ids that must be completed before this one appears.")]
        public string[] prerequisites = Array.Empty<string>();

        [Tooltip("All must hold for the objective to complete. Empty = only completed by code (e.g. the final decision).")]
        public CaseCondition[] completion = Array.Empty<CaseCondition>();
    }

    /// <summary>A conclusion the player can reach when closing the case (usually: who to charge).</summary>
    [Serializable]
    public sealed class ConclusionDefinition
    {
        [Tooltip("Unique, stable id, e.g. \"CON_ChargeMarlowe\".")]
        public string conclusionId = "CON_New";

        [Tooltip("e.g. \"Charge Elliot Marlowe with murder\".")]
        public string title = "";

        [TextArea(2, 4)] public string description = "";

        [Tooltip("The person charged (empty = nobody).")]
        public SuspectProfile accused;
    }

    public enum EndingOutcome
    {
        Solved = 0,           // the right person, a strong case
        PartiallySolved = 1,  // the right person, a weak case
        Failed = 2,           // the wrong person, or a charge that collapses
        Unsolved = 3          // nobody charged
    }

    /// <summary>
    /// An ending. When the player picks <see cref="conclusionId"/>, the endings for it are checked in
    /// list order and the first whose conditions all hold is the one they get.
    /// </summary>
    [Serializable]
    public sealed class EndingDefinition
    {
        public string endingId = "END_New";
        public string title = "";
        public EndingOutcome outcome = EndingOutcome.Unsolved;

        [Tooltip("The conclusion this ending belongs to.")]
        public string conclusionId = "";

        [Tooltip("All must hold. Empty = the fallback ending for this conclusion (put it last).")]
        public CaseCondition[] conditions = Array.Empty<CaseCondition>();

        [TextArea(4, 10)] public string epilogue = "";
    }
}
