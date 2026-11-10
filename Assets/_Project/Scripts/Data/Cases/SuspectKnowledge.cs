using System;
using System.Collections.Generic;
using UnityEngine;

namespace TLA.Data.Cases
{
    /// <summary>
    /// How a suspect treats a topic. Decided by the WRITER, enforced by C#. The AI only phrases it.
    /// </summary>
    public enum TopicStance
    {
        Truthful = 0,   // tells what they know
        Partial = 1,    // tells some of it, holds something back
        Lie = 2,        // says something false (can be broken by contradicting evidence)
        Deflect = 3,    // refuses / changes the subject
        Unknown = 4     // genuinely doesn't know: must never "reveal" anything here
    }

    /// <summary>
    /// One subject a suspect can be asked about, e.g. "Whereabouts on the night".
    /// All lines are authored; the mock provider picks between them, the online provider (later)
    /// uses them as the facts it may express.
    /// </summary>
    [Serializable]
    public sealed class KnowledgeTopic
    {
        [Tooltip("Stable id, e.g. \"TOP_Whereabouts\".")]
        public string topicId = "TOP_New";

        [Tooltip("Short name for notes and debugging, e.g. \"Whereabouts\".")]
        public string displayName = "Topic";

        [Tooltip("Words/phrases that point a typed question at this topic. Lower case. Phrases count double.")]
        public string[] keywords = Array.Empty<string>();

        [Tooltip("Suggested question offered to the player for this topic (empty = never suggested).")]
        public string suggestedQuestion = "";

        public TopicStance stance = TopicStance.Truthful;

        [TextArea(2, 4)]
        [Tooltip("WRITER NOTE / ground truth: what really happened. Never shown or spoken directly.")]
        public string truth = "";

        [Tooltip("Normal answers, used in order (first ask, second ask...). Several = no repetition.")]
        [TextArea(2, 4)] public string[] answers = Array.Empty<string>();

        [Tooltip("Answer to a follow-up on this topic (\"Why?\", \"Are you sure?\"). Empty = generic pushback.")]
        [TextArea(2, 4)] public string followUpAnswer = "";

        [Tooltip("When composure is low: a cracked, more revealing (but still not confessing) answer. Empty = none.")]
        [TextArea(2, 4)] public string pressuredAnswer = "";

        [Tooltip("Evidence that exposes this topic's lie/omission when PRESENTED to the suspect.")]
        public string[] contradictedByEvidence = Array.Empty<string>();

        [Tooltip("What the suspect admits once contradicted. A partial admission, NEVER a confession.")]
        [TextArea(2, 4)] public string admissionAnswer = "";

        [Header("Availability")]
        [Tooltip("Only suggested after this topic has been asked (follow-up chains). Empty = always.")]
        public string requiresTopic = "";

        [Tooltip("Only suggested once the player has DISCOVERED this evidence. Empty = always.")]
        public string requiresEvidence = "";

        [Tooltip("Lower = suggested earlier.")]
        public int priority = 10;
    }

    /// <summary>Reaction when the player shows the suspect a piece of evidence.</summary>
    [Serializable]
    public sealed class EvidenceReaction
    {
        public string evidenceId = "";
        [TextArea(2, 4)] public string reaction = "";
        [Tooltip("Composure lost when shown this (0 = unaffected).")]
        [Range(0, 50)] public int composureImpact = 5;
    }

    /// <summary>
    /// Everything a suspect knows, hides and lies about, plus in-character fallback lines.
    /// The dialogue system can ONLY express what's written here, which is how the game guarantees
    /// suspects never reveal things they don't know or invent evidence.
    /// Create via Assets → Create → The Last Answer → Cases → Suspect Knowledge.
    /// </summary>
    [CreateAssetMenu(fileName = "SUS_New_Knowledge", menuName = "The Last Answer/Cases/Suspect Knowledge", order = 23)]
    public sealed class SuspectKnowledge : ScriptableObject
    {
        public List<KnowledgeTopic> topics = new List<KnowledgeTopic>();
        public List<EvidenceReaction> evidenceReactions = new List<EvidenceReaction>();

        [Header("In-character fallbacks (empty lists use generic lines for the temperament)")]
        [Tooltip("Opening line when the interview starts.")]
        [TextArea(2, 3)] public string greeting = "";

        [Tooltip("Questions the suspect can't connect to anything they know.")]
        [TextArea(2, 3)] public string[] unknownQuestion = Array.Empty<string>();

        [Tooltip("Replies to direct accusations (\"You killed him\"). Denials, never confessions.")]
        [TextArea(2, 3)] public string[] accusationReplies = Array.Empty<string>();

        [Tooltip("Asked the same thing again.")]
        [TextArea(2, 3)] public string[] repeatedQuestion = Array.Empty<string>();

        [Tooltip("Shown evidence they have no reaction written for.")]
        [TextArea(2, 3)] public string[] irrelevantEvidence = Array.Empty<string>();

        public KnowledgeTopic FindTopic(string topicId)
        {
            foreach (KnowledgeTopic t in topics)
                if (t != null && t.topicId == topicId) return t;
            return null;
        }

        public EvidenceReaction FindReaction(string evidenceId)
        {
            foreach (EvidenceReaction r in evidenceReactions)
                if (r != null && r.evidenceId == evidenceId) return r;
            return null;
        }
    }
}
