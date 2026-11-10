using System.Collections.Generic;
using TLA.Data.Cases;

namespace TLA.Systems.Dialogue
{
    /// <summary>
    /// Picks which suggested questions to offer: topics with a suggested question that are
    /// available (their required topic was asked / required evidence was discovered), not yet
    /// asked, in priority order. Follow-up chains appear naturally: asking "Where were you?"
    /// unlocks "Can anyone confirm that?".
    /// Suggestions guarantee the player can always make progress, even if typed questions miss.
    /// </summary>
    public static class SuggestionBuilder
    {
        public const int DefaultMax = 3;

        public static List<SuggestedQuestion> Build(SuspectKnowledge knowledge, ResponsePlanner.IState state,
            ICollection<string> discoveredEvidence, int max = DefaultMax)
        {
            var result = new List<SuggestedQuestion>();
            if (knowledge == null) return result;

            var candidates = new List<KnowledgeTopic>();
            foreach (KnowledgeTopic t in knowledge.topics)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.suggestedQuestion)) continue;
                if (state.TimesAsked(t.topicId) > 0) continue;
                if (!string.IsNullOrEmpty(t.requiresTopic) && state.TimesAsked(t.requiresTopic) == 0) continue;
                if (!string.IsNullOrEmpty(t.requiresEvidence) && (discoveredEvidence == null || !discoveredEvidence.Contains(t.requiresEvidence))) continue;
                candidates.Add(t);
            }

            // Unlocked follow-ups and evidence-based questions first (they're what's "new"), then by priority.
            candidates.Sort((a, b) =>
            {
                int na = IsUnlock(a) ? 0 : 1, nb = IsUnlock(b) ? 0 : 1;
                return na != nb ? na.CompareTo(nb) : a.priority.CompareTo(b.priority);
            });

            for (int i = 0; i < candidates.Count && result.Count < max; i++)
                result.Add(new SuggestedQuestion(candidates[i].topicId, candidates[i].suggestedQuestion));
            return result;
        }

        private static bool IsUnlock(KnowledgeTopic t) => !string.IsNullOrEmpty(t.requiresTopic) || !string.IsNullOrEmpty(t.requiresEvidence);
    }
}
