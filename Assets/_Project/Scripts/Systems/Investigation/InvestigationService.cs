using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Data.Evidence;
using TLA.Data.Investigation;
using TLA.Systems.Save;

namespace TLA.Systems.Investigation
{
    public enum StatementStatus
    {
        Unverified = 0,          // recorded, nothing proven against it (yet)
        ContradictionFound = 1,  // the player found a contradiction in the notebook, not yet put to the suspect
        Exposed = 2              // the suspect was confronted with a valid contradiction
    }

    /// <summary>The player's progress with one statement.</summary>
    [Serializable]
    public sealed class StatementState
    {
        public string id;
        public bool recorded;
        public StatementStatus status;

        /// <summary>The suspect's actual words when they said it (most recent last).</summary>
        public List<string> quotes = new List<string>();

        /// <summary>Evidence id that exposed it (status Exposed / ContradictionFound).</summary>
        public string contradictedWith;
    }

    /// <summary>A note the player wrote in the notebook.</summary>
    [Serializable]
    public sealed class ObservationRecord
    {
        public string text;
        public int order;
    }

    /// <summary>Snapshot of how far the investigation has got (notebook "progress" panel).</summary>
    public readonly struct InvestigationProgress
    {
        public readonly int EvidenceCollected, EvidenceTotal;
        public readonly int FactsConfirmed, FactsTotal;
        public readonly int StatementsRecorded, StatementsTotal;
        public readonly int ContradictionsExposed, ContradictionsFound, ContradictionsTotal;
        public readonly int FailedChallenges, Observations;

        public InvestigationProgress(int evC, int evT, int fC, int fT, int sR, int sT, int cE, int cF, int cT, int failed, int obs)
        {
            EvidenceCollected = evC; EvidenceTotal = evT; FactsConfirmed = fC; FactsTotal = fT;
            StatementsRecorded = sR; StatementsTotal = sT; ContradictionsExposed = cE; ContradictionsFound = cF; ContradictionsTotal = cT;
            FailedChallenges = failed; Observations = obs;
        }

        /// <summary>0-100: evidence, facts and exposed contradictions weighted equally.</summary>
        public int Percent
        {
            get
            {
                float e = EvidenceTotal > 0 ? (float)EvidenceCollected / EvidenceTotal : 0f;
                float f = FactsTotal > 0 ? (float)FactsConfirmed / FactsTotal : 0f;
                float c = ContradictionsTotal > 0 ? (float)ContradictionsExposed / ContradictionsTotal : 0f;
                int parts = (EvidenceTotal > 0 ? 1 : 0) + (FactsTotal > 0 ? 1 : 0) + (ContradictionsTotal > 0 ? 1 : 0);
                return parts == 0 ? 0 : (int)Math.Round((e + f + c) / parts * 100f);
            }
        }
    }

    /// <summary>
    /// The investigation logic: which statements the player has heard, which have been proven false,
    /// the player's own observations, and overall progress. Contradictions are judged ONLY by
    /// <see cref="ContradictionChecker"/> against authored rules, confirmed facts and collected
    /// evidence. Dialogue (mock or online AI) words the suspect's reaction; this service decides the
    /// outcome and applies it.
    /// </summary>
    public sealed class InvestigationService : IGameService, ISaveable, IInvestigationFacts, ICaseScopedService
    {
        private readonly CaseSessionService _session;
        private readonly EvidenceService _evidence;
        private readonly Dictionary<string, StatementState> _statements = new Dictionary<string, StatementState>();
        private readonly List<ObservationRecord> _observations = new List<ObservationRecord>();
        private int _failedChallenges;

        public InvestigationService(CaseSessionService session, EvidenceService evidence)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        }

        public string SaveKey => "investigation";

        public void Initialize() { }

        public void Shutdown() { }

        private CaseDefinition Case => _session.CurrentCase;

        public IReadOnlyList<SuspectStatement> Statements =>
            Case?.statements != null ? Case.statements : (IReadOnlyList<SuspectStatement>)Array.Empty<SuspectStatement>();

        public IReadOnlyList<ObservationRecord> Observations => _observations;

        public int FailedChallenges => _failedChallenges;

        /// <summary>Clears progress and records the statements already known at the start (e.g. given to officers).</summary>
        public void BeginCase()
        {
            _statements.Clear();
            _observations.Clear();
            _failedChallenges = 0;
            foreach (SuspectStatement s in Statements)
                if (s != null && s.recordedFromStart) StateFor(s).recorded = true;
            Changed();
        }

        // ------------------------------------------------------------------ queries

        public SuspectStatement FindStatement(string statementId)
        {
            foreach (SuspectStatement s in Statements)
                if (s != null && s.statementId == statementId) return s;
            return null;
        }

        public StatementState GetState(SuspectStatement s)
        {
            if (s != null && _statements.TryGetValue(s.statementId, out StatementState state)) return state;
            return new StatementState { id = s?.statementId };
        }

        public bool IsStatementRecorded(SuspectStatement s) => s != null && GetState(s).recorded;
        public bool IsStatementResolved(SuspectStatement s) => s != null && GetState(s).status == StatementStatus.Exposed;
        public bool IsEvidenceCollected(EvidenceDefinition e) => _evidence.IsCollected(e);
        public bool IsFactConfirmed(CaseFact f) => _evidence.IsFactEstablished(f);

        /// <summary>Recorded statements, optionally for one suspect, in case order.</summary>
        public List<SuspectStatement> GetRecorded(string suspectId = null)
        {
            var list = new List<SuspectStatement>();
            foreach (SuspectStatement s in Statements)
                if (s != null && IsStatementRecorded(s) && (suspectId == null || (s.suspect != null && s.suspect.suspectId == suspectId)))
                    list.Add(s);
            return list;
        }

        /// <summary>Statements the player has heard that are still standing (nothing proven against them).</summary>
        public List<SuspectStatement> GetUnresolved()
        {
            var list = new List<SuspectStatement>();
            foreach (SuspectStatement s in GetRecorded())
                if (GetState(s).status != StatementStatus.Exposed) list.Add(s);
            return list;
        }

        public InvestigationProgress GetProgress()
        {
            int evidenceTotal = Case != null ? Case.EvidenceTotal : 0;
            int factsTotal = Case?.facts?.Count ?? 0;
            int recorded = 0, exposed = 0, found = 0, contradictable = 0;
            foreach (SuspectStatement s in Statements)
            {
                if (s == null) continue;
                StatementState st = GetState(s);
                if (st.recorded) recorded++;
                if (s.CanBeContradicted) contradictable++;
                if (st.status == StatementStatus.Exposed) exposed++;
                if (st.status != StatementStatus.Unverified) found++;
            }
            return new InvestigationProgress(_evidence.GetCollected().Count, evidenceTotal, _evidence.EstablishedFactIds.Count, factsTotal,
                recorded, Statements.Count, exposed, found, contradictable, _failedChallenges, _observations.Count);
        }

        // ------------------------------------------------------------------ statements

        /// <summary>Records every statement of <paramref name="suspectId"/> tied to <paramref name="topicId"/> (the suspect just answered it).</summary>
        public void RecordStatementsForTopic(string suspectId, string topicId, string quote)
        {
            if (string.IsNullOrEmpty(topicId)) return;
            foreach (SuspectStatement s in Statements)
                if (s != null && s.topicId == topicId && s.suspect != null && s.suspect.suspectId == suspectId)
                    RecordStatement(s, quote);
        }

        public void RecordStatement(SuspectStatement s, string quote = null)
        {
            if (s == null || !IsInCase(s)) return;
            StatementState state = StateFor(s);
            bool first = !state.recorded;
            state.recorded = true;
            bool newQuote = !string.IsNullOrWhiteSpace(quote) && !state.quotes.Contains(quote);
            if (newQuote) state.quotes.Add(quote);
            if (first || newQuote)
            {
                EventBus.Publish(new StatementRecordedEvent(s, first));
                Changed();
            }
        }

        // ------------------------------------------------------------------ contradictions

        /// <summary>The verdict, without changing anything.</summary>
        public ContradictionResult Evaluate(SuspectStatement s, EvidenceDefinition e) => ContradictionChecker.Evaluate(s, e, this);

        /// <summary>
        /// Notebook: compares a statement with a piece of evidence. A valid contradiction is marked as
        /// found (ready to put to the suspect). Wrong comparisons cost nothing here.
        /// </summary>
        public ContradictionResult Compare(SuspectStatement s, EvidenceDefinition e)
        {
            ContradictionResult result = Evaluate(s, e);
            if (result.IsValid)
            {
                StatementState state = StateFor(s);
                if (state.status == StatementStatus.Unverified)
                {
                    state.status = StatementStatus.ContradictionFound;
                    state.contradictedWith = e.evidenceId;
                    Changed();
                }
            }
            EventBus.Publish(new ContradictionEvaluatedEvent(result, false));
            return result;
        }

        /// <summary>
        /// Interview: applies the outcome of challenging a suspect, once their reply has been given.
        /// Valid exposes the statement and confirms the rule's fact; a wrong challenge is counted.
        /// </summary>
        public void ApplyChallenge(ContradictionResult result)
        {
            if (result?.Statement == null) return;
            switch (result.Outcome)
            {
                case ContradictionOutcome.Valid:
                    Expose(result);
                    // The same evidence may also break other statements this suspect made (e.g. "I was home" and "it wasn't me on camera").
                    string suspectId = result.Statement.suspect != null ? result.Statement.suspect.suspectId : null;
                    foreach (SuspectStatement other in GetRecorded(suspectId))
                    {
                        if (other == result.Statement) continue;
                        ContradictionResult also = Evaluate(other, result.Evidence);
                        if (also.IsValid) Expose(also);
                    }
                    break;
                case ContradictionOutcome.Invalid:
                    _failedChallenges++;
                    break;
            }
            EventBus.Publish(new ContradictionEvaluatedEvent(result, true));
            Changed();
        }

        /// <summary>
        /// For plain "present evidence" in an interview: the first recorded, unexposed statement of this
        /// suspect that the evidence VALIDLY contradicts, or null. Lets showing the right evidence expose a lie
        /// without the player having to pick the statement.
        /// </summary>
        public ContradictionResult FindValidContradiction(string suspectId, EvidenceDefinition e)
        {
            foreach (SuspectStatement s in GetRecorded(suspectId))
            {
                ContradictionResult r = Evaluate(s, e);
                if (r.IsValid) return r;
            }
            return null;
        }

        private void Expose(ContradictionResult result)
        {
            StatementState state = StateFor(result.Statement);
            state.status = StatementStatus.Exposed;
            state.contradictedWith = result.Evidence.evidenceId;
            if (result.Rule?.confirmsFact != null) _evidence.ConfirmFact(result.Rule.confirmsFact);
            string suspect = result.Statement.suspect != null ? result.Statement.suspect.displayName : "The suspect";
            _session.AddNote($"{suspect}'s statement \"{result.Statement.claim}\" was exposed by \"{result.Evidence.displayName}\". {result.Explanation}",
                "Contradiction");
        }

        // ------------------------------------------------------------------ observations

        public ObservationRecord AddObservation(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            if (text.Length > 300) text = text.Substring(0, 300);
            var record = new ObservationRecord { text = text, order = _observations.Count };
            _observations.Add(record);
            EventBus.Publish(new ObservationAddedEvent(record));
            Changed();
            return record;
        }

        // ------------------------------------------------------------------ helpers

        private bool IsInCase(SuspectStatement s)
        {
            foreach (SuspectStatement x in Statements)
                if (x == s) return true;
            return false;
        }

        private StatementState StateFor(SuspectStatement s)
        {
            if (!_statements.TryGetValue(s.statementId, out StatementState state))
            {
                state = new StatementState { id = s.statementId };
                _statements[s.statementId] = state;
            }
            return state;
        }

        private static void Changed() => EventBus.Publish(new InvestigationChangedEvent());

        // ------------------------------------------------------------------ saving

        private sealed class SaveState
        {
            public List<StatementState> statements = new List<StatementState>();
            public List<ObservationRecord> observations = new List<ObservationRecord>();
            public int failedChallenges;
        }

        public JToken CaptureState() => JToken.FromObject(new SaveState
        {
            statements = new List<StatementState>(_statements.Values),
            observations = new List<ObservationRecord>(_observations),
            failedChallenges = _failedChallenges
        });

        public void RestoreState(JToken state)
        {
            var data = state?.ToObject<SaveState>();
            _statements.Clear();
            _observations.Clear();
            _failedChallenges = 0;
            if (data != null)
            {
                if (data.statements != null)
                    foreach (StatementState s in data.statements)
                        if (s != null && !string.IsNullOrEmpty(s.id))
                        {
                            s.quotes ??= new List<string>();
                            _statements[s.id] = s;
                        }
                if (data.observations != null) _observations.AddRange(data.observations);
                _failedChallenges = data.failedChallenges;
            }
            Changed();
        }
    }
}
