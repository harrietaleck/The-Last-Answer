using System.Collections.Generic;
using TLA.Data.Cases;

namespace TLA.Systems.Dialogue
{
    /// <summary>How rattled the suspect is. Derived from composure (0-100).</summary>
    public enum ComposureLevel
    {
        Composed = 0,  // 60+
        Strained = 1,  // 30-59
        Rattled = 2    // below 30
    }

    /// <summary>The kind of reply the C# rules decided on. Providers choose the WORDS, never the kind.</summary>
    public enum ResponseKind
    {
        Greeting,
        TopicAnswer,          // normal answer for the topic's stance (truth, partial, lie, deflect, unknown)
        TopicRepeat,          // asked again with no new answer left: "I already told you…"
        TopicFollowUp,        // "Why?" on the last topic
        TopicPressured,       // composure is low: a cracked, more revealing answer (still no confession)
        TopicAdmitted,        // topic already contradicted by evidence: sticks to the admission
        Challenge,            // "You're lying."
        Accusation,           // "You killed him."
        ConfessionRefusal,    // "Confess."
        SmallTalk,            // "Hello."
        Unknown,              // unrelated / unknown question
        EvidenceContradiction,// shown evidence that exposes a lie → partial admission
        EvidenceReaction,     // shown relevant evidence
        EvidenceIrrelevant,   // shown evidence they have nothing to say about
        EvidenceRepeated,     // shown the same evidence again
        ChallengeRejected,    // challenged a statement with evidence that doesn't contradict it: the suspect brushes it off
        ChallengeUnproven     // the right evidence, but the contradiction can't be proven yet: the suspect evades
    }

    /// <summary>
    /// The investigation system's verdict on a challenge (statement + evidence), decided in C# by the
    /// ContradictionChecker BEFORE the suspect answers. The dialogue only words the reaction.
    /// </summary>
    public enum ChallengeVerdict
    {
        Valid,
        Unproven,
        Invalid
    }

    /// <summary>
    /// What the suspect will do this turn, decided by <see cref="ResponsePlanner"/> (C#).
    /// State changes (composure, asked topics, contradictions) are applied only if the reply succeeds.
    /// </summary>
    public sealed class ResponsePlan
    {
        public ResponseKind Kind;
        public KnowledgeTopic Topic;
        public string EvidenceId;
        public string EvidenceName;

        /// <summary>Which variant of a line set to use (keeps answers from repeating).</summary>
        public int VariantIndex;

        // Deltas, applied by DialogueSession after a successful reply.
        public int ComposureDelta;
        public bool MarksTopicAsked;
        public bool MarksTopicContradicted;
        public bool MarksEvidencePresented;

        public override string ToString() => $"{Kind}{(Topic != null ? "(" + Topic.topicId + ")" : "")} Δcomposure={ComposureDelta}";
    }

    /// <summary>
    /// EVERYTHING a provider is allowed to know for one reply: the suspect and their knowledge, the
    /// question, the decided plan, the conversation so far, the evidence shown and the suspect's
    /// state. A provider must work only from this, so a mock and an online AI are interchangeable.
    /// </summary>
    public sealed class DialogueContext
    {
        public SuspectProfile Suspect;
        public SuspectKnowledge Knowledge;

        /// <summary>The case being investigated (public details only are ever sent online). May be null in tests.</summary>
        public CaseDefinition Case;

        /// <summary>The player's words (empty for evidence presentation and greetings).</summary>
        public string PlayerInput;
        public DialogueIntent Intent;
        public ResponsePlan Plan;

        public IReadOnlyList<DialogueMessage> History;
        public IReadOnlyCollection<string> PresentedEvidence;
        public IReadOnlyCollection<string> DiscoveredEvidence;

        /// <summary>Composure AFTER this turn's change (the reply should reflect it).</summary>
        public int Composure;
        public ComposureLevel Level;
        public int TurnNumber;

        public string SuspectName => Suspect != null ? Suspect.displayName : "Suspect";
        public string ShortName => Suspect != null ? Suspect.ShortName : "The suspect";
        public SuspectTemperament Temperament => Suspect != null ? Suspect.temperament : SuspectTemperament.Calm;

        public static ComposureLevel LevelFor(int composure) =>
            composure >= 60 ? ComposureLevel.Composed : composure >= 30 ? ComposureLevel.Strained : ComposureLevel.Rattled;
    }
}
