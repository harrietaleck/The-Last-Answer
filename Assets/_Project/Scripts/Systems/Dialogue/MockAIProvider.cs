using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TLA.Data.Cases;

namespace TLA.Systems.Dialogue
{
    /// <summary>
    /// Offline provider: builds believable replies from AUTHORED lines only, so the game works with
    /// no internet and no AI model. Uses:
    ///   - the plan the rules decided (answer / follow-up / pressured / admission / denial…)
    ///   - the suspect's knowledge asset (topic answers, reactions, fallbacks)
    ///   - their temperament (generic lines and tone when the writer left a list empty)
    ///   - their composure (stage directions: calm, strained, rattled)
    ///   - what was asked before (variant index → no repeated lines; "I already told you…")
    /// It never invents facts: every sentence comes from the knowledge asset or a neutral generic
    /// line that contains no case information.
    /// </summary>
    public sealed class MockAIProvider : IAIProvider
    {
        private readonly int _minDelayMs;
        private readonly int _maxDelayMs;
        private readonly System.Random _random;

        /// <param name="minDelayMs">Fake "thinking" time so the UI behaves like an online reply. 0 = instant (tests).</param>
        public MockAIProvider(int minDelayMs = 600, int maxDelayMs = 1400, int seed = 0)
        {
            _minDelayMs = minDelayMs;
            _maxDelayMs = maxDelayMs < minDelayMs ? minDelayMs : maxDelayMs;
            _random = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        public string ProviderId => "mock";

        public async Task<AIResponse> GenerateResponseAsync(DialogueContext context, CancellationToken cancellationToken)
        {
            if (_maxDelayMs > 0)
                await Task.Delay(_random.Next(_minDelayMs, _maxDelayMs + 1), cancellationToken);
            return Compose(context);
        }

        /// <summary>The actual line choice (synchronous, used directly by tests).</summary>
        public static AIResponse Compose(DialogueContext c)
        {
            ResponsePlan plan = c.Plan;
            KnowledgeTopic topic = plan.Topic;
            SuspectKnowledge k = c.Knowledge;
            string text;

            switch (plan.Kind)
            {
                case ResponseKind.Greeting:
                    text = Has(k?.greeting) ? k.greeting : Pick(GenericLines.Greeting(c.Temperament), 0);
                    break;

                case ResponseKind.TopicAnswer:
                    text = topic != null && topic.answers != null && topic.answers.Length > 0
                        ? topic.answers[Clamp(plan.VariantIndex, topic.answers.Length)]
                        : StanceFallback(topic, c);
                    break;

                case ResponseKind.TopicRepeat:
                    if (topic == null) { text = StanceFallback(null, c); break; }
                    string recap = topic.answers != null && topic.answers.Length > 0 ? " " + topic.answers[topic.answers.Length - 1] : "";
                    text = Pick(Has(k?.repeatedQuestion) ? k.repeatedQuestion : GenericLines.Repeat(c.Temperament), plan.VariantIndex) + recap;
                    break;

                case ResponseKind.TopicFollowUp:
                    if (topic == null) { text = StanceFallback(null, c); break; }
                    text = Has(topic.followUpAnswer) && plan.VariantIndex == 0
                        ? topic.followUpAnswer
                        : Pick(GenericLines.FollowUp(topic.stance, c.Temperament), plan.VariantIndex);
                    break;

                case ResponseKind.TopicPressured:
                    text = Has(topic?.pressuredAnswer)
                        ? topic.pressuredAnswer
                        : Pick(GenericLines.Cornered(c.Temperament), plan.VariantIndex);
                    break;

                case ResponseKind.TopicAdmitted:
                case ResponseKind.EvidenceContradiction:
                    text = Has(topic?.admissionAnswer) ? topic.admissionAnswer : Pick(GenericLines.Cornered(c.Temperament), plan.VariantIndex);
                    break;

                case ResponseKind.Challenge:
                    text = Pick(GenericLines.Challenge(c.Temperament), plan.VariantIndex);
                    break;

                case ResponseKind.Accusation:
                    text = Pick(Has(k?.accusationReplies) ? k.accusationReplies : GenericLines.Accusation(c.Temperament), plan.VariantIndex);
                    break;

                case ResponseKind.ConfessionRefusal:
                    text = Pick(GenericLines.ConfessionRefusal(c.Temperament), plan.VariantIndex);
                    break;

                case ResponseKind.SmallTalk:
                    text = Pick(GenericLines.SmallTalk(c.Temperament), plan.VariantIndex);
                    break;

                case ResponseKind.EvidenceReaction:
                    EvidenceReaction reaction = k != null ? k.FindReaction(plan.EvidenceId) : null;
                    text = Has(reaction?.reaction)
                        ? reaction.reaction
                        : Pick(Has(k?.irrelevantEvidence) ? k.irrelevantEvidence : GenericLines.IrrelevantEvidence(c.Temperament), plan.VariantIndex);
                    break;

                case ResponseKind.ChallengeRejected:
                    text = Pick(GenericLines.ChallengeRejected(c.Temperament), plan.VariantIndex);
                    break;

                case ResponseKind.ChallengeUnproven:
                    text = Pick(GenericLines.ChallengeUnproven(c.Temperament), plan.VariantIndex);
                    break;

                case ResponseKind.EvidenceRepeated:
                    text = Pick(GenericLines.EvidenceRepeated, plan.VariantIndex);
                    break;

                case ResponseKind.EvidenceIrrelevant:
                    text = Pick(Has(k?.irrelevantEvidence) ? k.irrelevantEvidence : GenericLines.IrrelevantEvidence(c.Temperament), plan.VariantIndex);
                    break;

                default: // Unknown question
                    text = Pick(Has(k?.unknownQuestion) ? k.unknownQuestion : GenericLines.Unknown(c.Temperament), plan.VariantIndex);
                    break;
            }

            return AIResponse.Ok(text, Narration(c));
        }

        /// <summary>A topic with no written answer: speak only in terms of the stance, never facts.</summary>
        private static string StanceFallback(KnowledgeTopic topic, DialogueContext c)
        {
            if (topic == null || topic.stance == TopicStance.Unknown) return Pick(GenericLines.DontKnow, c.TurnNumber);
            return Pick(GenericLines.FollowUp(topic.stance, c.Temperament), c.TurnNumber);
        }

        /// <summary>Stage direction reflecting composure and what just happened. Empty most calm turns.</summary>
        private static string Narration(DialogueContext c)
        {
            string name = c.ShortName;
            switch (c.Plan.Kind)
            {
                case ResponseKind.EvidenceContradiction:
                    return Pick(new[] { $"{name} stares at it for a long moment.", $"The colour drains from {name}'s face.", $"{name} goes very still." }, c.TurnNumber);
                case ResponseKind.Accusation:
                    return Pick(new[] { $"{name}'s chair scrapes back an inch.", $"{name} lets out a short, disbelieving laugh." }, c.TurnNumber);
                case ResponseKind.TopicPressured:
                    return Pick(new[] { $"{name} looks down at the table.", $"{name} rubs the bridge of the nose and exhales." }, c.TurnNumber);
                case ResponseKind.ChallengeRejected:
                    return Pick(new[] { $"{name} almost smiles.", $"{name} sits back, relaxed now." }, c.TurnNumber);
                case ResponseKind.ChallengeUnproven:
                    return Pick(new[] { $"{name} hesitates, just for a second.", $"{name}'s eyes flick to the evidence, then away." }, c.TurnNumber);
            }

            switch (c.Level)
            {
                case ComposureLevel.Rattled:
                    return Pick(new[] { $"{name}'s hands won't stay still.", $"{name} glances at the mirror, then away.", $"{name} swallows hard.", "" }, c.TurnNumber);
                case ComposureLevel.Strained:
                    return c.TurnNumber % 3 == 0 ? Pick(new[] { $"{name}'s jaw tightens.", $"{name} shifts in the chair." }, c.TurnNumber) : "";
                default:
                    return "";
            }
        }

        private static string Pick(IReadOnlyList<string> lines, int index) =>
            lines == null || lines.Count == 0 ? "..." : lines[(index % lines.Count + lines.Count) % lines.Count];

        private static int Clamp(int index, int length) => index < 0 ? 0 : index >= length ? length - 1 : index;

        private static bool Has(string s) => !string.IsNullOrWhiteSpace(s);

        private static bool Has(string[] lines) => lines != null && lines.Length > 0 && Has(lines[0]);
    }

    /// <summary>
    /// Neutral in-character lines used when a writer hasn't supplied specific ones. They contain
    /// NO case facts, so they can never reveal or invent anything.
    /// </summary>
    internal static class GenericLines
    {
        public static readonly string[] DontKnow =
        {
            "I don't know anything about that.",
            "I honestly couldn't tell you.",
            "No idea. You'd have to ask someone else."
        };

        public static readonly string[] EvidenceRepeated =
        {
            "You've already shown me that.",
            "Showing it to me twice won't change my answer.",
        };

        public static string[] Greeting(SuspectTemperament t) => t switch
        {
            SuspectTemperament.Nervous => new[] { "I- yes. Hello. Is this going to take long?" },
            SuspectTemperament.Defensive => new[] { "Let's get this over with." },
            SuspectTemperament.Arrogant => new[] { "I assume you have actual questions this time." },
            SuspectTemperament.Evasive => new[] { "Long night for everyone, isn't it?" },
            _ => new[] { "Detective." },
        };

        public static string[] SmallTalk(SuspectTemperament t) => t switch
        {
            SuspectTemperament.Nervous => new[] { "Hi. Sorry. I'm just... can we start?", "Hello again." },
            SuspectTemperament.Defensive => new[] { "Skip the pleasantries.", "We're not friends. Ask your questions." },
            SuspectTemperament.Arrogant => new[] { "Charming. Can we move on?", "Is this the part where you build rapport?" },
            SuspectTemperament.Evasive => new[] { "Hello to you too.", "Nice of you to finally join me." },
            _ => new[] { "Evening.", "Hello." },
        };

        public static string[] Unknown(SuspectTemperament t) => t switch
        {
            SuspectTemperament.Nervous => new[] { "I'm sorry, I don't understand what you're asking.", "Is that... important? I don't know what to say.", "I don't know. I really don't." },
            SuspectTemperament.Defensive => new[] { "I don't see what that has to do with anything.", "Ask me something that matters.", "That's not a question I can answer." },
            SuspectTemperament.Arrogant => new[] { "Is that meant to be relevant?", "You're fishing. It's not working.", "I don't follow, and I suspect neither do you." },
            SuspectTemperament.Evasive => new[] { "Why would you want to know that?", "Interesting question. Next.", "Hard to say, really." },
            _ => new[] { "I'm not sure what you mean.", "I can't help you with that.", "Could you be more specific?" },
        };

        public static string[] Repeat(SuspectTemperament t) => t switch
        {
            SuspectTemperament.Nervous => new[] { "I- I already said.", "Like I told you before..." },
            SuspectTemperament.Defensive => new[] { "I already told you.", "How many times?" },
            SuspectTemperament.Arrogant => new[] { "Do you need me to write it down?", "Same question, same answer." },
            SuspectTemperament.Evasive => new[] { "Didn't we cover this?", "Asking again won't change it." },
            _ => new[] { "As I said.", "I've answered that." },
        };

        public static string[] Challenge(SuspectTemperament t) => t switch
        {
            SuspectTemperament.Nervous => new[] { "I'm not lying! Why would I lie?", "Please, I'm telling you what I remember." },
            SuspectTemperament.Defensive => new[] { "Call me a liar again and this conversation is over.", "Believe what you like. It's the truth." },
            SuspectTemperament.Arrogant => new[] { "Prove it.", "If you had anything, you'd have shown me by now." },
            SuspectTemperament.Evasive => new[] { "Everyone lies a little, detective. Not about this.", "Lying? That's a strong word." },
            _ => new[] { "I've told you what I know.", "I'm not lying to you." },
        };

        public static string[] Accusation(SuspectTemperament t) => t switch
        {
            SuspectTemperament.Nervous => new[] { "No! No, I didn't- I would never.", "You can't think that. Please." },
            SuspectTemperament.Defensive => new[] { "That's insane. I didn't kill anyone.", "Say that again and I want a lawyer." },
            SuspectTemperament.Arrogant => new[] { "Is that your theory? It's a bad one.", "You don't have a case, so you're trying to scare me." },
            SuspectTemperament.Evasive => new[] { "That's quite a leap.", "Is that what you've told your boss?" },
            _ => new[] { "I didn't do this.", "You're wrong about me." },
        };

        public static string[] ConfessionRefusal(SuspectTemperament t) => t switch
        {
            SuspectTemperament.Nervous => new[] { "Confess to what? I haven't done anything!", "I don't have anything to confess." },
            SuspectTemperament.Defensive => new[] { "I'm not confessing to something I didn't do.", "Nice try." },
            SuspectTemperament.Arrogant => new[] { "Does that line usually work?", "You'll need more than a stern voice." },
            SuspectTemperament.Evasive => new[] { "The truth? Whose version?", "I've told you the truth. Mostly." },
            _ => new[] { "There's nothing to confess.", "I've told you everything I can." },
        };

        public static string[] IrrelevantEvidence(SuspectTemperament t) => t switch
        {
            SuspectTemperament.Nervous => new[] { "I don't- what am I supposed to see here?", "I've never seen that before. I think." },
            SuspectTemperament.Defensive => new[] { "So? That proves nothing.", "And what does that have to do with me?" },
            SuspectTemperament.Arrogant => new[] { "Am I meant to be impressed?", "Fascinating. Irrelevant, but fascinating." },
            SuspectTemperament.Evasive => new[] { "Huh. Where did you find that?", "Can't say it means anything to me." },
            _ => new[] { "That doesn't mean anything to me.", "I can't help you with that." },
        };

        // A challenge that doesn't hold: no case facts, just confidence.
        public static string[] ChallengeRejected(SuspectTemperament t) => t switch
        {
            SuspectTemperament.Nervous => new[] { "I don't- that doesn't have anything to do with what I said.", "I don't understand. How does that make me a liar?" },
            SuspectTemperament.Defensive => new[] { "That doesn't contradict a word I've said.", "Is that it? That proves nothing." },
            SuspectTemperament.Arrogant => new[] { "That's your evidence? Try again.", "You're grasping, detective. It shows." },
            SuspectTemperament.Evasive => new[] { "I'm not sure what you think that shows.", "Interesting. And irrelevant." },
            _ => new[] { "That doesn't change what I told you.", "I don't see the connection." },
        };

        // The right evidence, not yet enough to prove it: evasive, a little shaken.
        public static string[] ChallengeUnproven(SuspectTemperament t) => t switch
        {
            SuspectTemperament.Nervous => new[] { "That- that could be anyone. Couldn't it?", "I don't know what that is. I really don't." },
            SuspectTemperament.Defensive => new[] { "That could be anyone. Come back when you have something real.", "You can't prove that has anything to do with me." },
            SuspectTemperament.Arrogant => new[] { "Suggestive. Not conclusive. You know the difference, I hope.", "Nice try. You'll need more than that." },
            SuspectTemperament.Evasive => new[] { "Could be. Could be a lot of things.", "I wouldn't read too much into that." },
            _ => new[] { "That doesn't prove anything.", "You're guessing." },
        };

        public static string[] Cornered(SuspectTemperament t) => t switch
        {
            SuspectTemperament.Nervous => new[] { "Okay. Okay. That... that doesn't mean what you think." },
            SuspectTemperament.Defensive => new[] { "Fine. That doesn't make me a killer." },
            SuspectTemperament.Arrogant => new[] { "So I left something out. It's not a crime to keep my private life private." },
            _ => new[] { "There's an explanation for that." },
        };

        public static string[] FollowUp(TopicStance stance, SuspectTemperament t)
        {
            switch (stance)
            {
                case TopicStance.Lie:
                    return t == SuspectTemperament.Nervous
                        ? new[] { "Because it's true. I-I'm sure.", "I don't know what else to tell you." }
                        : new[] { "Because that's what happened.", "I've said what I have to say about that.", "Why does it matter so much to you?" };
                case TopicStance.Partial:
                    return new[] { "That's all there is to it.", "I don't see why I should go into more detail.", "There's nothing more to add." };
                case TopicStance.Deflect:
                    return new[] { "That's my business.", "I'm not going to discuss that.", "Move on, detective." };
                case TopicStance.Unknown:
                    return DontKnow;
                default:
                    return new[] { "I've told you what I know.", "That's really all I know about it.", "Ask the others if you don't believe me." };
            }
        }
    }
}
