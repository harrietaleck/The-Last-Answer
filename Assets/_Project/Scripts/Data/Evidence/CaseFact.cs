using System;
using System.Collections.Generic;
using TLA.Data.Investigation;
using UnityEngine;
using UnityEngine.Serialization;

namespace TLA.Data.Evidence
{
    /// <summary>How far a fact can be trusted, based on where it comes from.</summary>
    public enum FactReliability
    {
        Low = 0,      // hearsay, a single unconfirmed account
        Medium = 1,   // testimony, circumstantial
        High = 2      // records, documents, forensic results, an admission
    }

    /// <summary>
    /// Something the investigation can confirm as true, e.g. "No forced entry". Facts are confirmed
    /// ONLY by game systems: examining evidence, connecting evidence, or exposing a contradiction.
    /// Whether a fact is confirmed is the player's progress (EvidenceService), not stored here.
    /// Create via Assets → Create → The Last Answer → Evidence → Case Fact.
    /// </summary>
    [CreateAssetMenu(fileName = "FACT_New", menuName = "The Last Answer/Evidence/Case Fact", order = 31)]
    public sealed class CaseFact : ScriptableObject
    {
        [Tooltip("Unique, stable id used by code and save files, e.g. \"FACT_NoForcedEntry\". Never change after release.")]
        public string factId = "FACT_New";

        [Tooltip("Short title shown in lists, e.g. \"No forced entry\".")]
        public string title = "New fact";

        [FormerlySerializedAs("statement")]
        [TextArea(2, 4)]
        [Tooltip("What the investigator now knows, in one or two sentences.")]
        public string description = "";

        [Tooltip("Where it comes from, e.g. \"Building access log\" or \"Marlowe, under questioning\".")]
        public string source = "";

        public FactReliability reliability = FactReliability.Medium;

        [Tooltip("Evidence this fact comes from or relates to.")]
        public EvidenceDefinition[] relatedEvidence = Array.Empty<EvidenceDefinition>();

        [Tooltip("Suspect statements this fact supports or undermines.")]
        public SuspectStatement[] relatedStatements = Array.Empty<SuspectStatement>();

        [Tooltip("Key facts matter for solving the case (shown highlighted).")]
        public bool isKeyFact;

        /// <summary>Ids of <see cref="relatedEvidence"/>.</summary>
        public IEnumerable<string> RelatedEvidenceIds
        {
            get
            {
                if (relatedEvidence == null) yield break;
                foreach (EvidenceDefinition e in relatedEvidence)
                    if (e != null) yield return e.evidenceId;
            }
        }

        public string ReliabilityLabel => reliability == FactReliability.High ? "HIGH" : reliability == FactReliability.Medium ? "MEDIUM" : "LOW";
    }
}
