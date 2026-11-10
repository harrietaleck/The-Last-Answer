using System.Collections.Generic;
using TLA.Data.Cases;
using TLA.Data.Evidence;
using TLA.Data.Investigation;

namespace TLA.Systems.Cases
{
    /// <summary>
    /// Checks a CaseDefinition for authoring mistakes: duplicate ids, references to things that
    /// aren't in the case, objectives that can never appear, conclusions without an ending, no way
    /// to solve the case. Used by the tests (every case in the catalog) and the editor menu
    /// The Last Answer → Cases → Validate All Cases. Returns an empty list when the case is fine.
    /// </summary>
    public static class CaseValidator
    {
        public static List<string> Validate(CaseDefinition c)
        {
            var errors = new List<string>();
            if (c == null)
            {
                errors.Add("Case is missing.");
                return errors;
            }
            string P(string msg) => $"{c.caseId}: {msg}";

            if (string.IsNullOrWhiteSpace(c.caseId)) errors.Add("A case has no id.");
            if (string.IsNullOrWhiteSpace(c.title)) errors.Add(P("no title"));
            if (!c.isAvailable) return errors; // locked placeholder cases only need an id and a title

            if (string.IsNullOrWhiteSpace(c.summary)) errors.Add(P("no summary"));
            if (string.IsNullOrWhiteSpace(c.victim?.name)) errors.Add(P("no victim name"));
            if (string.IsNullOrWhiteSpace(c.crime?.crimeType)) errors.Add(P("no crime type"));
            if (c.suspects == null || c.suspects.Count == 0) errors.Add(P("no suspects"));

            Unique(errors, P, "evidence", c.evidence, e => e?.evidenceId);
            Unique(errors, P, "fact", c.facts, f => f?.factId);
            Unique(errors, P, "statement", c.statements, s => s?.statementId);
            Unique(errors, P, "objective", c.objectives, o => o?.objectiveId);
            Unique(errors, P, "conclusion", c.conclusions, x => x?.conclusionId);
            Unique(errors, P, "ending", c.endings, x => x?.endingId);

            // Timeline
            foreach (TimelineEntry t in c.timeline)
                if (t?.revealedBy != null && !c.facts.Contains(t.revealedBy)) errors.Add(P($"timeline '{t.time}' is revealed by a fact not in the case"));

            // Objectives
            var objectiveIds = new HashSet<string>();
            foreach (ObjectiveDefinition o in c.objectives) if (o != null) objectiveIds.Add(o.objectiveId);
            foreach (ObjectiveDefinition o in c.objectives)
            {
                if (o == null) { errors.Add(P("empty objective entry")); continue; }
                if (string.IsNullOrWhiteSpace(o.title)) errors.Add(P($"objective {o.objectiveId} has no title"));
                if (o.prerequisites != null)
                    foreach (string pre in o.prerequisites)
                        if (!objectiveIds.Contains(pre)) errors.Add(P($"objective {o.objectiveId} needs unknown objective '{pre}'"));
                CheckConditions(errors, P, c, o.completion, $"objective {o.objectiveId}", objectiveIds);
            }
            CheckConditions(errors, P, c, c.conclusionUnlockedBy, "conclusion unlock", objectiveIds);

            // Conclusions and endings
            if (c.conclusions.Count > 0 && string.IsNullOrWhiteSpace(c.conclusionDecisionId)) errors.Add(P("conclusions need a conclusionDecisionId"));
            bool solvable = false;
            foreach (ConclusionDefinition con in c.conclusions)
            {
                if (con == null) { errors.Add(P("empty conclusion entry")); continue; }
                if (con.accused != null && !c.suspects.Contains(con.accused)) errors.Add(P($"conclusion {con.conclusionId} accuses someone who isn't a suspect"));
                bool hasFallback = false, hasAny = false;
                foreach (EndingDefinition e in c.endings)
                {
                    if (e == null || e.conclusionId != con.conclusionId) continue;
                    hasAny = true;
                    if (e.conditions == null || e.conditions.Length == 0) hasFallback = true;
                    if (e.outcome == EndingOutcome.Solved) solvable = true;
                }
                if (!hasAny) errors.Add(P($"conclusion {con.conclusionId} has no ending"));
                else if (!hasFallback) errors.Add(P($"conclusion {con.conclusionId} has no fallback ending (one with no conditions, listed last)"));
            }
            foreach (EndingDefinition e in c.endings)
            {
                if (e == null) continue;
                if (c.FindConclusion(e.conclusionId) == null) errors.Add(P($"ending {e.endingId} belongs to unknown conclusion '{e.conclusionId}'"));
                if (string.IsNullOrWhiteSpace(e.epilogue)) errors.Add(P($"ending {e.endingId} has no epilogue"));
                CheckConditions(errors, P, c, e.conditions, $"ending {e.endingId}", objectiveIds);
            }
            if (c.conclusions.Count > 0 && !solvable) errors.Add(P("no ending has the outcome Solved"));

            return errors;
        }

        private static void CheckConditions(List<string> errors, System.Func<string, string> P, CaseDefinition c, CaseCondition[] conditions, string where, HashSet<string> objectiveIds)
        {
            if (conditions == null) return;
            foreach (CaseCondition k in conditions)
            {
                if (k == null) { errors.Add(P($"{where}: empty condition")); continue; }
                switch (k.type)
                {
                    case CaseConditionType.EvidenceCollected:
                    case CaseConditionType.EvidenceExamined:
                        if (k.evidence == null || !c.evidence.Contains(k.evidence)) errors.Add(P($"{where}: {k} - evidence not in the case"));
                        break;
                    case CaseConditionType.FactConfirmed:
                        if (k.fact == null || !c.facts.Contains(k.fact)) errors.Add(P($"{where}: {k} - fact not in the case"));
                        break;
                    case CaseConditionType.StatementRecorded:
                    case CaseConditionType.StatementExposed:
                        if (k.statement == null || !c.statements.Contains(k.statement)) errors.Add(P($"{where}: {k} - statement not in the case"));
                        break;
                    case CaseConditionType.SuspectQuestioned:
                        if (k.suspect == null || !c.suspects.Contains(k.suspect)) errors.Add(P($"{where}: {k} - not a suspect in the case"));
                        break;
                    case CaseConditionType.ObjectiveCompleted:
                        if (!objectiveIds.Contains(k.id)) errors.Add(P($"{where}: {k} - unknown objective"));
                        break;
                    case CaseConditionType.DecisionMade:
                        if (string.IsNullOrWhiteSpace(k.id)) errors.Add(P($"{where}: decision condition without an id"));
                        break;
                }
            }
        }

        private static void Unique<T>(List<string> errors, System.Func<string, string> P, string what, List<T> items, System.Func<T, string> id)
        {
            if (items == null) return;
            var seen = new HashSet<string>();
            foreach (T item in items)
            {
                string key = id(item);
                if (string.IsNullOrWhiteSpace(key)) { errors.Add(P($"a {what} has no id (or the list has an empty entry)")); continue; }
                if (!seen.Add(key)) errors.Add(P($"duplicate {what} id '{key}'"));
            }
        }
    }
}
