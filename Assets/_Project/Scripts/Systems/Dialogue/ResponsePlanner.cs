using System.Collections.Generic;
using TLA.Data.Cases;

namespace TLA.Systems.Dialogue
{
    /// <summary>
    /// THE RULES. Decides what the suspect does with a question or a piece of evidence. Pure C#,
    /// no AI involved, so it's predictable and testable. Key guarantees:
    ///  - Suspects never confess. The strongest reply is a partial admission written by a writer
    ///    (admissionAnswer), and only when evidence that contradicts them has been PRESENTED.
    ///  - Unknown topics stay unknown: stance Unknown always uses its "I don't know" lines.
    ///  - Pressure works gradually: repeats, follow-ups on sore topics, challenges and accusations
    ///    lower composure; a rattled suspect gives the topic's pressuredAnswer (more revealing,
    ///    still not a confession).
    /// </summary>
    public static class ResponsePlanner
    {
        // Composure costs (tune here).
        public const int RepeatCost = 4;
        public const int SensitiveTopicCost = 2;      // asking about a Lie/Partial/Deflect topic
        public const int FollowUpSensitiveCost = 6;
        public const int FollowUpCost = 1;
        public const int ChallengeCost = 8;
        public const int AccusationCost = 10;
        public const int ConfessionRequestCost = 5;
        public const int ContradictionCost = 25;
        public const int UnprovenChallengeCost = 3;
        public const int RejectedChallengeGain = 5;   // a weak challenge makes the suspect MORE confident

        /// <summary>Read-only view of the session state the rules need.</summary>
        public interface IState
        {
            int Composure { get; }
            int TimesAsked(string topicId);
            bool IsContradicted(string topicId);
            bool WasPresented(string evidenceId);
            int NextVariant(string bank); // increments a per-bank counter so lines don't repeat
        }

        public static ResponsePlan PlanQuestion(DialogueIntent intent, IState state)
        {
            var plan = new ResponsePlan { Topic = intent.Topic };
            KnowledgeTopic topic = intent.Topic;

            switch (intent.Kind)
            {
                case IntentKind.Topic:
                    PlanTopic(plan, topic, state);
                    break;

                case IntentKind.FollowUp:
                    bool sensitive = IsSensitive(topic);
                    plan.ComposureDelta = -(sensitive ? FollowUpSensitiveCost : FollowUpCost);
                    if (state.IsContradicted(topic.topicId)) plan.Kind = ResponseKind.TopicAdmitted;
                    else if (sensitive && WouldBeRattled(state, plan) && Has(topic.pressuredAnswer)) plan.Kind = ResponseKind.TopicPressured;
                    else plan.Kind = ResponseKind.TopicFollowUp;
                    plan.VariantIndex = state.NextVariant("followup:" + topic.topicId);
                    break;

                case IntentKind.Challenge:
                    plan.ComposureDelta = -ChallengeCost;
                    if (topic != null && state.IsContradicted(topic.topicId)) plan.Kind = ResponseKind.TopicAdmitted;
                    else if (topic != null && IsSensitive(topic) && WouldBeRattled(state, plan) && Has(topic.pressuredAnswer)) plan.Kind = ResponseKind.TopicPressured;
                    else plan.Kind = ResponseKind.Challenge;
                    plan.VariantIndex = state.NextVariant("challenge");
                    break;

                case IntentKind.Accusation:
                    plan.Kind = ResponseKind.Accusation; // ALWAYS a denial, whatever the composure
                    plan.ComposureDelta = -AccusationCost;
                    plan.VariantIndex = state.NextVariant("accusation");
                    break;

                case IntentKind.ConfessionRequest:
                    plan.Kind = ResponseKind.ConfessionRefusal;
                    plan.ComposureDelta = -ConfessionRequestCost;
                    plan.VariantIndex = state.NextVariant("confession");
                    break;

                case IntentKind.Greeting:
                    plan.Kind = ResponseKind.SmallTalk;
                    plan.VariantIndex = state.NextVariant("smalltalk");
                    break;

                default:
                    plan.Kind = ResponseKind.Unknown;
                    plan.VariantIndex = state.NextVariant("unknown");
                    break;
            }

            return plan;
        }

        private static void PlanTopic(ResponsePlan plan, KnowledgeTopic topic, IState state)
        {
            int asked = state.TimesAsked(topic.topicId);
            plan.MarksTopicAsked = true;
            if (IsSensitive(topic)) plan.ComposureDelta -= SensitiveTopicCost;

            if (state.IsContradicted(topic.topicId))
            {
                plan.Kind = ResponseKind.TopicAdmitted;
                return;
            }

            if (asked > 0) plan.ComposureDelta -= RepeatCost;

            if (IsSensitive(topic) && WouldBeRattled(state, plan) && Has(topic.pressuredAnswer))
            {
                plan.Kind = ResponseKind.TopicPressured;
                return;
            }

            int answers = topic.answers != null ? topic.answers.Length : 0;
            if (asked < answers)
            {
                plan.Kind = ResponseKind.TopicAnswer;
                plan.VariantIndex = asked; // a new variant each time it's asked
            }
            else if (asked == 0)
            {
                plan.Kind = ResponseKind.TopicAnswer; // topic with no written answers: provider falls back
            }
            else
            {
                plan.Kind = ResponseKind.TopicRepeat;
                plan.VariantIndex = state.NextVariant("repeat");
            }
        }

        /// <param name="detectContradictions">
        /// True: evidence listed in a topic's contradictedByEvidence exposes it (stand-alone dialogue).
        /// False: the game's investigation system decides contradictions and uses <see cref="PlanChallenge"/> instead.
        /// </param>
        public static ResponsePlan PlanEvidence(string evidenceId, string evidenceName, SuspectKnowledge knowledge, IState state, bool detectContradictions = true)
        {
            var plan = new ResponsePlan { EvidenceId = evidenceId, EvidenceName = evidenceName, MarksEvidencePresented = true };

            if (state.WasPresented(evidenceId))
            {
                plan.Kind = ResponseKind.EvidenceRepeated;
                plan.VariantIndex = state.NextVariant("evidencerepeat");
                return plan;
            }

            // Does this evidence break one of the suspect's lies or omissions?
            if (knowledge != null)
            {
                if (detectContradictions)
                foreach (KnowledgeTopic topic in knowledge.topics)
                {
                    if (topic == null || state.IsContradicted(topic.topicId) || !Contains(topic.contradictedByEvidence, evidenceId)) continue;
                    plan.Kind = ResponseKind.EvidenceContradiction;
                    plan.Topic = topic;
                    plan.MarksTopicContradicted = true;
                    plan.ComposureDelta = -ContradictionCost;
                    return plan;
                }

                EvidenceReaction reaction = knowledge.FindReaction(evidenceId);
                if (reaction != null && Has(reaction.reaction))
                {
                    plan.Kind = ResponseKind.EvidenceReaction;
                    plan.ComposureDelta = -reaction.composureImpact;
                    return plan;
                }
            }

            plan.Kind = ResponseKind.EvidenceIrrelevant;
            plan.VariantIndex = state.NextVariant("irrelevant");
            return plan;
        }

        /// <summary>
        /// The player challenges a statement (on <paramref name="topic"/>) with evidence. The verdict was
        /// decided by the investigation system; this only turns it into a reply plan:
        ///   Valid    → partial admission (the topic's admissionAnswer), big composure loss
        ///   Unproven → the suspect evades, small composure loss
        ///   Invalid  → the suspect brushes it off and regains some composure
        /// </summary>
        public static ResponsePlan PlanChallenge(KnowledgeTopic topic, string evidenceId, string evidenceName, ChallengeVerdict verdict, IState state)
        {
            var plan = new ResponsePlan { Topic = topic, EvidenceId = evidenceId, EvidenceName = evidenceName, MarksEvidencePresented = true };
            switch (verdict)
            {
                case ChallengeVerdict.Valid:
                    plan.Kind = topic != null && state.IsContradicted(topic.topicId) ? ResponseKind.TopicAdmitted : ResponseKind.EvidenceContradiction;
                    plan.MarksTopicContradicted = topic != null;
                    plan.ComposureDelta = plan.Kind == ResponseKind.EvidenceContradiction ? -ContradictionCost : 0;
                    break;
                case ChallengeVerdict.Unproven:
                    plan.Kind = ResponseKind.ChallengeUnproven;
                    plan.ComposureDelta = -UnprovenChallengeCost;
                    plan.VariantIndex = state.NextVariant("unproven");
                    break;
                default:
                    plan.Kind = ResponseKind.ChallengeRejected;
                    plan.ComposureDelta = RejectedChallengeGain;
                    plan.VariantIndex = state.NextVariant("rejected");
                    break;
            }
            return plan;
        }

        /// <summary>Topics the suspect is uncomfortable with.</summary>
        public static bool IsSensitive(KnowledgeTopic topic) =>
            topic != null && (topic.stance == TopicStance.Lie || topic.stance == TopicStance.Partial || topic.stance == TopicStance.Deflect);

        private static bool WouldBeRattled(IState state, ResponsePlan plan) =>
            DialogueContext.LevelFor(state.Composure + plan.ComposureDelta) == ComposureLevel.Rattled;

        private static bool Has(string s) => !string.IsNullOrWhiteSpace(s);

        private static bool Contains(IEnumerable<string> list, string value)
        {
            if (list == null) return false;
            foreach (string s in list) if (s == value) return true;
            return false;
        }
    }
}
