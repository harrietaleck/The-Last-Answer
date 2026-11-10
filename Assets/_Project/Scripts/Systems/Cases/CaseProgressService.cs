using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Systems.Investigation;
using TLA.Systems.Save;

namespace TLA.Systems.Cases
{
    /// <summary>Where the investigation stands.</summary>
    public enum InvestigationStatus
    {
        NotStarted = 0,
        Investigating = 1,
        ReadyToConclude = 2,  // every required objective (except closing the case) is done
        Concluded = 3
    }

    /// <summary>An important choice the player made, e.g. who they charged.</summary>
    [Serializable]
    public sealed class DecisionRecord
    {
        public string id;
        public string choice;
        public string label;
        public int order;
    }

    /// <summary>Numbers for the notebook and the ending screen.</summary>
    public readonly struct CaseProgressSummary
    {
        public readonly InvestigationStatus Status;
        public readonly int ObjectivesCompleted, ObjectivesRequired, OptionalCompleted, OptionalTotal;
        public readonly int EvidenceDiscovered, EvidenceTotal;
        public readonly int SuspectsQuestioned, SuspectsTotal;
        public readonly int ContradictionsExposed, ContradictionsTotal, FailedChallenges;
        public readonly int FactsConfirmed, FactsTotal, Decisions;

        public CaseProgressSummary(InvestigationStatus status, int oc, int or, int optC, int optT, int ed, int et, int sq, int st,
            int ce, int ct, int failed, int fc, int ft, int decisions)
        {
            Status = status; ObjectivesCompleted = oc; ObjectivesRequired = or; OptionalCompleted = optC; OptionalTotal = optT;
            EvidenceDiscovered = ed; EvidenceTotal = et; SuspectsQuestioned = sq; SuspectsTotal = st;
            ContradictionsExposed = ce; ContradictionsTotal = ct; FailedChallenges = failed; FactsConfirmed = fc; FactsTotal = ft; Decisions = decisions;
        }
    }

    // ---- Events
    public readonly struct ObjectiveCompletedEvent : IGameEvent
    {
        public readonly ObjectiveDefinition Objective;
        public ObjectiveCompletedEvent(ObjectiveDefinition objective) { Objective = objective; }
    }

    public readonly struct SuspectQuestionedEvent : IGameEvent
    {
        public readonly SuspectProfile Suspect;
        public SuspectQuestionedEvent(SuspectProfile suspect) { Suspect = suspect; }
    }

    public readonly struct DecisionRecordedEvent : IGameEvent
    {
        public readonly DecisionRecord Decision;
        public DecisionRecordedEvent(DecisionRecord decision) { Decision = decision; }
    }

    /// <summary>Objectives, status or decisions changed (UI refreshes on it).</summary>
    public readonly struct CaseProgressChangedEvent : IGameEvent
    {
    }

    /// <summary>The player closed the case: their conclusion and the ending it led to.</summary>
    public readonly struct CaseConcludedEvent : IGameEvent
    {
        public readonly CaseDefinition Case;
        public readonly ConclusionDefinition Conclusion;
        public readonly EndingDefinition Ending;
        public readonly CaseProgressSummary Summary;
        public CaseConcludedEvent(CaseDefinition c, ConclusionDefinition conclusion, EndingDefinition ending, CaseProgressSummary summary)
        {
            Case = c; Conclusion = conclusion; Ending = ending; Summary = summary;
        }
    }

    /// <summary>
    /// CASE PROGRESSION for whichever case is active. It knows nothing about any specific case: it
    /// reads the CaseDefinition's objectives, conclusions and endings and evaluates their conditions
    /// against the other services (evidence, facts, statements) plus what it tracks itself:
    /// suspects questioned, decisions, completed objectives and the investigation status.
    ///
    /// Objectives complete automatically when their conditions hold (re-checked whenever evidence,
    /// facts or statements change). The current objective (HUD) is the first active, unfinished,
    /// required one. Closing the case records the decision and picks the ending deterministically.
    /// </summary>
    public sealed class CaseProgressService : IGameService, ISaveable, ICaseScopedService
    {
        private readonly CaseSessionService _session;
        private readonly EvidenceService _evidence;
        private readonly InvestigationService _investigation;

        private readonly List<string> _completed = new List<string>();
        private readonly List<string> _questioned = new List<string>();
        private readonly List<DecisionRecord> _decisions = new List<DecisionRecord>();
        private InvestigationStatus _status;
        private string _endingId;
        private string _conclusionId;
        private bool _evaluating;
        private float _playTime;

        public CaseProgressService(CaseSessionService session, EvidenceService evidence, InvestigationService investigation)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
            _investigation = investigation ?? throw new ArgumentNullException(nameof(investigation));
        }

        public string SaveKey => "caseProgress";

        public void Initialize()
        {
            EventBus.Subscribe<EvidenceStateChangedEvent>(OnWorldChanged);
            EventBus.Subscribe<FactEstablishedEvent>(OnWorldChanged);
            EventBus.Subscribe<InvestigationChangedEvent>(OnWorldChanged);
        }

        public void Shutdown()
        {
            EventBus.Unsubscribe<EvidenceStateChangedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<FactEstablishedEvent>(OnWorldChanged);
            EventBus.Unsubscribe<InvestigationChangedEvent>(OnWorldChanged);
        }

        private void OnWorldChanged<T>(T evt) where T : struct, IGameEvent => Evaluate();

        private CaseDefinition Case => _session.CurrentCase;

        public InvestigationStatus Status => _status;
        public IReadOnlyList<string> CompletedObjectives => _completed;
        public IReadOnlyList<string> QuestionedSuspects => _questioned;
        public IReadOnlyList<DecisionRecord> Decisions => _decisions;
        public string EndingId => _endingId;
        public string ConclusionId => _conclusionId;

        /// <summary>Seconds spent investigating this case (shown in the save slots).</summary>
        public float PlayTimeSeconds => _playTime;

        public void AddPlayTime(float seconds)
        {
            if (seconds > 0f && Case != null && _status != InvestigationStatus.Concluded) _playTime += seconds;
        }

        public void BeginCase()
        {
            _completed.Clear();
            _questioned.Clear();
            _decisions.Clear();
            _endingId = null;
            _conclusionId = null;
            _playTime = 0f;
            _status = Case != null ? InvestigationStatus.Investigating : InvestigationStatus.NotStarted;
            Evaluate(force: true);
        }

        // ------------------------------------------------------------------ queries

        public bool IsObjectiveCompleted(string id) => _completed.Contains(id);

        public bool IsObjectiveActive(ObjectiveDefinition o)
        {
            if (o == null || IsObjectiveCompleted(o.objectiveId)) return false;
            if (o.prerequisites != null)
                foreach (string p in o.prerequisites)
                    if (!string.IsNullOrEmpty(p) && !IsObjectiveCompleted(p)) return false;
            return true;
        }

        public bool WasQuestioned(SuspectProfile s) => s != null && _questioned.Contains(s.suspectId);

        public DecisionRecord FindDecision(string id) => _decisions.Find(d => d.id == id);

        /// <summary>Checks one condition against the current state of every investigation system.</summary>
        public bool IsMet(CaseCondition c)
        {
            if (c == null) return true;
            switch (c.type)
            {
                case CaseConditionType.EvidenceCollected: return _evidence.IsCollected(c.evidence);
                case CaseConditionType.EvidenceExamined: return _evidence.IsExamined(c.evidence);
                case CaseConditionType.FactConfirmed: return _evidence.IsFactEstablished(c.fact);
                case CaseConditionType.StatementRecorded: return _investigation.IsStatementRecorded(c.statement);
                case CaseConditionType.StatementExposed: return _investigation.IsStatementResolved(c.statement);
                case CaseConditionType.SuspectQuestioned: return WasQuestioned(c.suspect);
                case CaseConditionType.ObjectiveCompleted: return IsObjectiveCompleted(c.id);
                case CaseConditionType.DecisionMade:
                    DecisionRecord d = FindDecision(c.id);
                    return d != null && (string.IsNullOrEmpty(c.choice) || d.choice == c.choice);
                default: return false;
            }
        }

        public bool AllMet(CaseCondition[] conditions)
        {
            if (conditions == null) return true;
            foreach (CaseCondition c in conditions)
                if (!IsMet(c)) return false;
            return true;
        }

        /// <summary>True while the case is open and its conclusion conditions are met.</summary>
        public bool CanConclude => Case != null && _status != InvestigationStatus.Concluded && AllMet(Case.conclusionUnlockedBy);

        /// <summary>The objective the HUD shows: first active, unfinished, required (else optional) objective.</summary>
        public ObjectiveDefinition CurrentObjective
        {
            get
            {
                if (Case?.objectives == null) return null;
                ObjectiveDefinition optional = null;
                foreach (ObjectiveDefinition o in Case.objectives)
                {
                    if (!IsObjectiveActive(o)) continue;
                    if (!o.optional) return o;
                    optional ??= o;
                }
                return optional;
            }
        }

        public CaseProgressSummary GetSummary()
        {
            int required = 0, done = 0, optionalTotal = 0, optionalDone = 0;
            if (Case?.objectives != null)
                foreach (ObjectiveDefinition o in Case.objectives)
                {
                    if (o == null) continue;
                    if (o.optional) { optionalTotal++; if (IsObjectiveCompleted(o.objectiveId)) optionalDone++; }
                    else { required++; if (IsObjectiveCompleted(o.objectiveId)) done++; }
                }
            InvestigationProgress inv = _investigation.GetProgress();
            int suspects = Case?.suspects?.Count ?? 0;
            return new CaseProgressSummary(_status, done, required, optionalDone, optionalTotal,
                inv.EvidenceCollected, inv.EvidenceTotal, _questioned.Count, suspects,
                inv.ContradictionsExposed, inv.ContradictionsTotal, inv.FailedChallenges, inv.FactsConfirmed, inv.FactsTotal, _decisions.Count);
        }

        // ------------------------------------------------------------------ actions

        /// <summary>The player questioned a suspect (asked at least one question).</summary>
        public void MarkQuestioned(SuspectProfile suspect)
        {
            if (suspect == null || _questioned.Contains(suspect.suspectId)) return;
            _questioned.Add(suspect.suspectId);
            EventBus.Publish(new SuspectQuestionedEvent(suspect));
            Evaluate();
        }

        /// <summary>Records an important decision (a later choice with the same id replaces it).</summary>
        public DecisionRecord RecordDecision(string id, string choice, string label)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _decisions.RemoveAll(d => d.id == id);
            var record = new DecisionRecord { id = id, choice = choice, label = label, order = _decisions.Count };
            _decisions.Add(record);
            EventBus.Publish(new DecisionRecordedEvent(record));
            Evaluate(force: true);
            return record;
        }

        /// <summary>The ending this conclusion would lead to right now (first ending whose conditions hold).</summary>
        public EndingDefinition ResolveEnding(string conclusionId)
        {
            if (Case?.endings == null) return null;
            foreach (EndingDefinition e in Case.endings)
                if (e != null && e.conclusionId == conclusionId && AllMet(e.conditions)) return e;
            return null;
        }

        /// <summary>
        /// Closes the case with the player's conclusion. Final: records the decision, picks the ending and
        /// announces it. Returns null if the case can't be concluded now or the conclusion doesn't exist.
        /// </summary>
        public EndingDefinition Conclude(string conclusionId)
        {
            if (!CanConclude) return null;
            ConclusionDefinition conclusion = Case.FindConclusion(conclusionId);
            if (conclusion == null) return null;

            EndingDefinition ending = ResolveEnding(conclusionId); // judged on the evidence BEFORE the decision is recorded
            _conclusionId = conclusionId;
            _endingId = ending?.endingId;
            RecordDecision(Case.conclusionDecisionId, conclusionId, conclusion.title);
            _status = InvestigationStatus.Concluded;
            _session.SetObjective("Case closed.");
            _session.AddNote($"Case closed: {conclusion.title}. {(ending != null ? ending.title : "")}", "Conclusion");
            EventBus.Publish(new CaseProgressChangedEvent());
            EventBus.Publish(new CaseConcludedEvent(Case, conclusion, ending, GetSummary()));
            return ending;
        }

        /// <summary>For a concluded case (e.g. after loading a save): the conclusion and ending as an event to show again. Null otherwise.</summary>
        public CaseConcludedEvent? StoredConclusion()
        {
            if (_status != InvestigationStatus.Concluded || Case == null) return null;
            ConclusionDefinition conclusion = Case.FindConclusion(_conclusionId);
            if (conclusion == null) return null;
            EndingDefinition ending = Case.endings.Find(e => e != null && e.endingId == _endingId);
            return new CaseConcludedEvent(Case, conclusion, ending, GetSummary());
        }

        // ------------------------------------------------------------------ evaluation

        /// <summary>Completes every objective whose conditions now hold, then updates status and the HUD objective.</summary>
        public void Evaluate(bool force = false)
        {
            if (_evaluating || Case == null) return;
            _evaluating = true;
            bool changed = force;
            try
            {
                bool progress = true;
                while (progress) // completing one objective can activate the next
                {
                    progress = false;
                    foreach (ObjectiveDefinition o in Case.objectives)
                    {
                        if (!IsObjectiveActive(o) || o.completion == null || o.completion.Length == 0 || !AllMet(o.completion)) continue;
                        _completed.Add(o.objectiveId);
                        EventBus.Publish(new ObjectiveCompletedEvent(o));
                        progress = changed = true;
                    }
                }

                if (_status != InvestigationStatus.Concluded)
                {
                    InvestigationStatus before = _status;
                    _status = RequiredDoneExceptDecision() && CanConclude ? InvestigationStatus.ReadyToConclude : InvestigationStatus.Investigating;
                    changed |= before != _status;

                    if (Case.objectives.Count > 0)
                    {
                        ObjectiveDefinition current = CurrentObjective;
                        _session.SetObjective(current != null ? current.title : "Close the case.");
                    }
                }
            }
            finally
            {
                _evaluating = false;
            }
            if (changed) EventBus.Publish(new CaseProgressChangedEvent());
        }

        /// <summary>All required objectives are done, ignoring ones that wait for a decision (like closing the case).</summary>
        private bool RequiredDoneExceptDecision()
        {
            foreach (ObjectiveDefinition o in Case.objectives)
            {
                if (o == null || o.optional || IsObjectiveCompleted(o.objectiveId)) continue;
                bool waitsForDecision = false;
                if (o.completion != null)
                    foreach (CaseCondition c in o.completion)
                        if (c != null && c.type == CaseConditionType.DecisionMade) waitsForDecision = true;
                if (!waitsForDecision) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ saving

        private sealed class SaveState
        {
            public List<string> completed = new List<string>();
            public List<string> questioned = new List<string>();
            public List<DecisionRecord> decisions = new List<DecisionRecord>();
            public InvestigationStatus status;
            public string endingId;
            public string conclusionId;
            public float playTime;
        }

        public JToken CaptureState() => JToken.FromObject(new SaveState
        {
            completed = new List<string>(_completed),
            questioned = new List<string>(_questioned),
            decisions = new List<DecisionRecord>(_decisions),
            status = _status,
            endingId = _endingId,
            conclusionId = _conclusionId,
            playTime = _playTime
        });

        public void RestoreState(JToken state)
        {
            var data = state?.ToObject<SaveState>();
            _completed.Clear();
            _questioned.Clear();
            _decisions.Clear();
            if (data != null)
            {
                if (data.completed != null) _completed.AddRange(data.completed);
                if (data.questioned != null) _questioned.AddRange(data.questioned);
                if (data.decisions != null) _decisions.AddRange(data.decisions);
                _status = data.status;
                _endingId = data.endingId;
                _conclusionId = data.conclusionId;
                _playTime = data.playTime > 0f && !float.IsNaN(data.playTime) ? data.playTime : 0f;
            }
            Evaluate(force: true);
        }
    }
}
