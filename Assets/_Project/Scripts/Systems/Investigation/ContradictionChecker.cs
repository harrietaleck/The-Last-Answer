using System.Collections.Generic;
using TLA.Data.Evidence;
using TLA.Data.Investigation;

namespace TLA.Systems.Investigation
{
    /// <summary>The checker's verdict on "does this evidence contradict this statement?".</summary>
    public enum ContradictionOutcome
    {
        Valid,              // a contradiction: the rule's evidence is shown and every required fact is confirmed
        Unproven,           // the right evidence, but the facts that make it a contradiction aren't confirmed yet
        Invalid,            // this evidence doesn't contradict the statement
        AlreadyResolved,    // this statement was already exposed
        NotRecorded,        // the player hasn't heard this statement yet
        EvidenceNotCollected,
        Unknown             // not part of the current case
    }

    /// <summary>Everything the UI and dialogue need to know about one comparison.</summary>
    public sealed class ContradictionResult
    {
        public ContradictionOutcome Outcome;
        public SuspectStatement Statement;
        public EvidenceDefinition Evidence;

        /// <summary>The rule that matched (Valid / Unproven), else null.</summary>
        public ContradictionRule Rule;

        /// <summary>Facts still needed (Unproven).</summary>
        public readonly List<CaseFact> MissingFacts = new List<CaseFact>();

        /// <summary>Player-facing explanation of the verdict.</summary>
        public string Explanation;

        public bool IsValid => Outcome == ContradictionOutcome.Valid;

        public string Title
        {
            get
            {
                switch (Outcome)
                {
                    case ContradictionOutcome.Valid: return "CONTRADICTION";
                    case ContradictionOutcome.Unproven: return "NOT PROVEN YET";
                    case ContradictionOutcome.Invalid: return "NO CONTRADICTION";
                    case ContradictionOutcome.AlreadyResolved: return "ALREADY EXPOSED";
                    default: return "CAN'T COMPARE";
                }
            }
        }
    }

    /// <summary>What the checker needs to know about the player's progress.</summary>
    public interface IInvestigationFacts
    {
        bool IsStatementRecorded(SuspectStatement statement);
        bool IsStatementResolved(SuspectStatement statement);
        bool IsEvidenceCollected(EvidenceDefinition evidence);
        bool IsFactConfirmed(CaseFact fact);
    }

    /// <summary>
    /// Decides, deterministically, whether a piece of evidence contradicts a suspect's statement.
    /// A contradiction exists ONLY if the statement has an authored <see cref="ContradictionRule"/>
    /// for that evidence AND every fact the rule requires is confirmed. Nothing else (and no AI)
    /// can produce one. Same inputs, same verdict, every time.
    /// </summary>
    public static class ContradictionChecker
    {
        public static ContradictionResult Evaluate(SuspectStatement statement, EvidenceDefinition evidence, IInvestigationFacts progress)
        {
            var result = new ContradictionResult { Statement = statement, Evidence = evidence };

            if (statement == null || evidence == null || progress == null)
                return Done(result, ContradictionOutcome.Unknown, "Choose a statement and a piece of evidence.");
            if (!progress.IsStatementRecorded(statement))
                return Done(result, ContradictionOutcome.NotRecorded, "You haven't heard this statement yet.");
            if (!progress.IsEvidenceCollected(evidence))
                return Done(result, ContradictionOutcome.EvidenceNotCollected, $"You haven't collected \"{evidence.displayName}\" yet.");
            if (progress.IsStatementResolved(statement))
                return Done(result, ContradictionOutcome.AlreadyResolved, "This statement has already been exposed.");

            ContradictionRule rule = FindRule(statement, evidence);
            if (rule == null)
                return Done(result, ContradictionOutcome.Invalid, $"Nothing in \"{evidence.displayName}\" contradicts this statement.");

            result.Rule = rule;
            if (rule.requiresConfirmed != null)
                foreach (CaseFact fact in rule.requiresConfirmed)
                    if (fact != null && !progress.IsFactConfirmed(fact))
                        result.MissingFacts.Add(fact);

            if (result.MissingFacts.Count > 0)
            {
                string hint = string.IsNullOrWhiteSpace(rule.unprovenHint)
                    ? "Something here doesn't add up, but you can't prove it yet."
                    : rule.unprovenHint;
                return Done(result, ContradictionOutcome.Unproven, hint);
            }

            return Done(result, ContradictionOutcome.Valid, rule.explanation);
        }

        /// <summary>The statement's rule for this evidence, or null.</summary>
        public static ContradictionRule FindRule(SuspectStatement statement, EvidenceDefinition evidence)
        {
            if (statement?.contradictions == null || evidence == null) return null;
            foreach (ContradictionRule r in statement.contradictions)
                if (r != null && r.evidence == evidence) return r;
            return null;
        }

        private static ContradictionResult Done(ContradictionResult r, ContradictionOutcome outcome, string explanation)
        {
            r.Outcome = outcome;
            r.Explanation = explanation;
            return r;
        }
    }
}
