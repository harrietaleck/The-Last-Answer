using System.Collections.Generic;
using System.Text;
using TLA.Data.Cases;

namespace TLA.Systems.Dialogue
{
    /// <summary>What kind of thing the player said.</summary>
    public enum IntentKind
    {
        Topic,          // a question about a known topic ("Where were you that night?")
        FollowUp,       // short follow-up on the previous topic ("Why?", "Are you sure?")
        Challenge,      // "You're lying." / "I don't believe you."
        Accusation,     // "You killed him."
        ConfessionRequest, // "Confess." / "Tell the truth."
        Greeting,       // "Hello."
        Unknown         // nothing the suspect can connect to what they know
    }

    /// <summary>Result of interpreting the player's input.</summary>
    public readonly struct DialogueIntent
    {
        public readonly IntentKind Kind;
        /// <summary>The topic concerned (for Topic, FollowUp and sometimes Challenge), else null.</summary>
        public readonly KnowledgeTopic Topic;

        public DialogueIntent(IntentKind kind, KnowledgeTopic topic = null)
        {
            Kind = kind;
            Topic = topic;
        }

        public override string ToString() => Topic != null ? $"{Kind}({Topic.topicId})" : Kind.ToString();
    }

    /// <summary>
    /// Turns free text (or a clicked suggestion) into an intent, using each topic's keywords.
    /// Deliberately simple and predictable: lower-case, strip punctuation, score keyword hits
    /// (phrases count double, words also match longer forms: "phone" matches "phones").
    /// The online provider stage can add smarter classification later, but it must still map onto
    /// these same topics, so the rules stay in C#.
    /// </summary>
    public static class IntentMatcher
    {
        private static readonly string[] AccusationPhrases =
            { "you killed", "you kill", "did you kill", "kill him", "you murdered", "you did it", "you did this", "killed him", "murdered him", "it was you", "you re the killer", "you are the killer", "you shot", "you stabbed" };

        private static readonly string[] ChallengePhrases =
            { "you re lying", "you are lying", "liar", "don t believe", "do not believe", "not true", "lying to me", "that s a lie", "that is a lie", "stop lying", "bullshit", "nonsense" };

        private static readonly string[] ConfessionPhrases =
            { "confess", "admit it", "admit what you did", "tell the truth", "tell me the truth", "come clean", "own up" };

        private static readonly string[] FollowUpWords =
            { "why", "really", "sure", "what else", "and then", "then what", "what do you mean", "explain", "how come", "prove", "details", "go on", "elaborate", "anything else", "such as", "like what", "who", "how", "so you", "you were there", "were you there", "so it was you" };

        private static readonly string[] GreetingWords = { "hello", "hi", "hey", "good evening", "good morning", "good afternoon" };

        public static DialogueIntent Match(string input, string suggestionId, SuspectKnowledge knowledge, string lastTopicId)
        {
            KnowledgeTopic last = knowledge != null && !string.IsNullOrEmpty(lastTopicId) ? knowledge.FindTopic(lastTopicId) : null;

            // A clicked suggestion names its topic directly: no guessing needed.
            if (!string.IsNullOrEmpty(suggestionId) && knowledge != null)
            {
                KnowledgeTopic direct = knowledge.FindTopic(suggestionId);
                if (direct != null) return new DialogueIntent(IntentKind.Topic, direct);
            }

            string text = Normalize(input);
            if (text.Trim().Length == 0) return new DialogueIntent(IntentKind.Unknown);
            int wordCount = text.Trim().Split(' ').Length;

            // A short greeting ("Good evening.") is small talk, even if a word in it is also a keyword.
            if (wordCount <= 3 && ContainsAny(text, GreetingWords)) return new DialogueIntent(IntentKind.Greeting);

            KnowledgeTopic topic = BestTopic(text, knowledge, last);

            if (ContainsAny(text, AccusationPhrases)) return new DialogueIntent(IntentKind.Accusation, topic);
            if (ContainsAny(text, ConfessionPhrases)) return new DialogueIntent(IntentKind.ConfessionRequest, topic);
            if (ContainsAny(text, ChallengePhrases)) return new DialogueIntent(IntentKind.Challenge, topic ?? last);

            if (topic != null) return new DialogueIntent(IntentKind.Topic, topic);

            if (last != null && wordCount <= 6 && ContainsAny(text, FollowUpWords))
                return new DialogueIntent(IntentKind.FollowUp, last);

            if (wordCount <= 4 && ContainsAny(text, GreetingWords)) return new DialogueIntent(IntentKind.Greeting);

            return new DialogueIntent(IntentKind.Unknown);
        }

        private static KnowledgeTopic BestTopic(string text, SuspectKnowledge knowledge, KnowledgeTopic last)
        {
            if (knowledge == null) return null;

            KnowledgeTopic best = null;
            int bestScore = 0;
            foreach (KnowledgeTopic topic in knowledge.topics)
            {
                if (topic == null || topic.keywords == null) continue;
                int score = Score(text, topic.keywords);
                if (score == 0) continue;

                bool better = score > bestScore
                              || (score == bestScore && best != null && topic.priority < best.priority)
                              || (score == bestScore && best != null && topic.priority == best.priority && topic == last);
                if (best == null || better)
                {
                    best = topic;
                    bestScore = score;
                }
            }
            return best;
        }

        private static int Score(string text, IEnumerable<string> keywords)
        {
            int score = 0;
            foreach (string raw in keywords)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string keyword = Normalize(raw).Trim();
                if (keyword.Contains(" "))
                {
                    if (text.Contains(" " + keyword + " ")) score += 2;
                }
                else if (WordMatch(text, keyword))
                {
                    score += 1;
                }
            }
            return score;
        }

        /// <summary>Whole-word match; keywords of 4+ letters also match longer words ("phone" → "phones").</summary>
        private static bool WordMatch(string text, string keyword)
        {
            if (text.Contains(" " + keyword + " ")) return true;
            if (keyword.Length < 4) return false;
            int i = text.IndexOf(" " + keyword, System.StringComparison.Ordinal);
            return i >= 0;
        }

        private static bool ContainsAny(string text, IEnumerable<string> phrases)
        {
            foreach (string p in phrases)
                if (text.Contains(" " + p + " ")) return true;
            return false;
        }

        /// <summary>Lower case, letters/digits only, single spaces, padded with spaces for whole-word matching.</summary>
        public static string Normalize(string input)
        {
            var sb = new StringBuilder(" ");
            bool lastSpace = true;
            foreach (char c in (input ?? string.Empty).ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                    lastSpace = false;
                }
                else if (!lastSpace)
                {
                    sb.Append(' ');
                    lastSpace = true;
                }
            }
            if (!lastSpace) sb.Append(' ');
            return sb.ToString();
        }
    }
}
