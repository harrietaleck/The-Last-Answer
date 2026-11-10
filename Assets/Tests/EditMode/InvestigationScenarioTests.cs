using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TLA.Core.Events;
using TLA.Data.Cases;
using TLA.Data.Evidence;
using TLA.Data.Investigation;
using TLA.Systems.Dialogue;
using TLA.Systems.Investigation;
using UnityEditor;
using UnityEngine;

namespace TLA.Tests
{
    /// <summary>
    /// The investigation logic on the real Case 0147 content (Marlowe's statements, the Evidence Room items):
    ///   VALID CONTRADICTION   "I was at home" vs the CCTV, once CCTV + access log are connected
    ///   INVALID ACCUSATION    "I was at home" vs the coffee cups
    ///   UNRESOLVED STATEMENT  "My phone was off": nothing in the case contradicts it
    /// plus "not proven yet", determinism, saving, the dialogue side of a challenge, and data checks.
    /// </summary>
    public class InvestigationScenarioTests
    {
        private const string CasePath = "Assets/_Project/Data/Cases/Case0147/CASE_0147.asset";

        private CaseDefinition _case;
        private CaseCatalog _catalog;
        private CaseSessionService _session;
        private EvidenceService _evidence;
        private InvestigationService _investigation;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _case = AssetDatabase.LoadAssetAtPath<CaseDefinition>(CasePath);
            Assert.IsNotNull(_case);
            Assert.GreaterOrEqual(_case.statements.Count, 5, "statements missing: run The Last Answer → Environment → Rebuild All Locations");
            _catalog = ScriptableObject.CreateInstance<CaseCatalog>();
            _catalog.cases.Add(_case);
            _session = new CaseSessionService(_catalog);
            _evidence = new EvidenceService(_session);
            _investigation = new InvestigationService(_session, _evidence);
            _session.StartCase(_case);
            _evidence.BeginCase();
            _investigation.BeginCase();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
            Object.DestroyImmediate(_catalog);
        }

        private SuspectStatement S(string id) => _investigation.FindStatement(id);
        private EvidenceDefinition E(string id) => _evidence.Find(id);
        private CaseFact F(string id) => _evidence.FindFact(id);

        private void CollectAndExamine(params string[] ids)
        {
            foreach (string id in ids)
            {
                Assert.AreEqual(CollectResult.Collected, _evidence.Collect(E(id)), id);
                _evidence.Examine(E(id));
            }
        }

        /// <summary>Everything needed for the valid contradiction: CCTV and access log collected, examined and connected.</summary>
        private void PrepareCodeConnection()
        {
            CollectAndExamine("EVD_CaseFile", "EVD_CCTV", "EVD_KeycardLog");
            Assert.AreEqual(ConnectResult.Connected, _evidence.TryConnect(E("EVD_CCTV"), E("EVD_KeycardLog"), out _));
        }

        // ------------------------------------------------------------------ scenario 1: valid contradiction

        [Test]
        public void Scenario_ValidContradiction_HomeVsCctv()
        {
            SuspectStatement home = S("STM_Marlowe_Home");
            Assert.IsTrue(_investigation.IsStatementRecorded(home), "given to officers, so known from the start");

            PrepareCodeConnection();
            _investigation.RecordStatementsForTopic("SUS_Marlowe", "TOP_CCTV", "It wasn't me."); // he also denied being on camera

            ContradictionResult result = _investigation.Evaluate(home, E("EVD_CCTV"));
            Assert.AreEqual(ContradictionOutcome.Valid, result.Outcome);
            StringAssert.Contains("master code", result.Explanation);

            var feedback = new List<ContradictionEvaluatedEvent>();
            EventBus.Subscribe<ContradictionEvaluatedEvent>(feedback.Add);
            _investigation.ApplyChallenge(result);

            Assert.AreEqual(StatementStatus.Exposed, _investigation.GetState(home).status);
            Assert.AreEqual(StatementStatus.Exposed, _investigation.GetState(S("STM_Marlowe_NotOnCamera")).status, "the same evidence breaks his camera denial too");
            Assert.IsTrue(_evidence.IsFactEstablished(F("FACT_MarloweAtOffice")));
            Assert.IsTrue(feedback.Single().InInterview);
            Assert.AreEqual(ContradictionOutcome.AlreadyResolved, _investigation.Evaluate(home, E("EVD_CCTV")).Outcome);
            Assert.AreEqual(2, _investigation.GetProgress().ContradictionsExposed);
        }

        [Test]
        public void Scenario_SameEvidence_BeforeTheFactsAreConfirmed_IsNotProvenYet()
        {
            CollectAndExamine("EVD_CCTV"); // the footage alone: a hidden face

            ContradictionResult result = _investigation.Evaluate(S("STM_Marlowe_Home"), E("EVD_CCTV"));
            Assert.AreEqual(ContradictionOutcome.Unproven, result.Outcome);
            CollectionAssert.Contains(result.MissingFacts, F("FACT_EntrantKnewCode"));
            StringAssert.Contains("hidden", result.Explanation);

            _investigation.ApplyChallenge(result);
            Assert.AreEqual(StatementStatus.Unverified, _investigation.GetState(S("STM_Marlowe_Home")).status);
            Assert.AreEqual(0, _investigation.FailedChallenges, "the right idea, too early, isn't a failed challenge");
            Assert.IsFalse(_evidence.IsFactEstablished(F("FACT_MarloweAtOffice")));
        }

        // ------------------------------------------------------------------ scenario 2: invalid accusation

        [Test]
        public void Scenario_InvalidAccusation_HomeVsCoffeeCups()
        {
            PrepareCodeConnection(); // even with everything else in place...
            CollectAndExamine("EVD_ScenePhotos", "EVD_CoffeeCups");

            ContradictionResult result = _investigation.Evaluate(S("STM_Marlowe_Home"), E("EVD_CoffeeCups"));
            Assert.AreEqual(ContradictionOutcome.Invalid, result.Outcome);
            StringAssert.Contains("contradicts this statement", result.Explanation);

            int factsBefore = _evidence.EstablishedFactIds.Count;
            _investigation.ApplyChallenge(result);
            Assert.AreEqual(StatementStatus.Unverified, _investigation.GetState(S("STM_Marlowe_Home")).status);
            Assert.AreEqual(1, _investigation.FailedChallenges);
            Assert.AreEqual(factsBefore, _evidence.EstablishedFactIds.Count, "a wrong accusation proves nothing");
        }

        // ------------------------------------------------------------------ scenario 3: unresolved statement

        [Test]
        public void Scenario_UnresolvedStatement_PhoneCannotBeContradicted()
        {
            _investigation.RecordStatementsForTopic("SUS_Marlowe", "TOP_Phone", "It was switched off. I switch it off when I go to bed.");
            SuspectStatement phone = S("STM_Marlowe_Phone");
            Assert.IsTrue(_investigation.IsStatementRecorded(phone));
            Assert.AreEqual(1, _investigation.GetState(phone).quotes.Count);

            // Collect and examine EVERYTHING, connect everything: still nothing contradicts it.
            CollectAndExamine("EVD_CaseFile", "EVD_ScenePhotos", "EVD_CCTV", "EVD_KeycardLog", "EVD_CoffeeCups", "EVD_SaleContract");
            foreach (EvidenceDefinition e in _case.evidence)
                foreach (EvidenceConnection link in e.connections) _evidence.TryConnect(e, link.other, out _);

            foreach (EvidenceDefinition e in _case.evidence)
                Assert.AreEqual(ContradictionOutcome.Invalid, _investigation.Compare(phone, e).Outcome, e.evidenceId);

            Assert.AreEqual(StatementStatus.Unverified, _investigation.GetState(phone).status);
            CollectionAssert.Contains(_investigation.GetUnresolved(), phone);
        }

        // ------------------------------------------------------------------ rules

        [Test]
        public void StatementsNotHeardYet_CantBeChallenged()
        {
            CollectAndExamine("EVD_CaseFile", "EVD_SaleContract");
            Assert.AreEqual(ContradictionOutcome.NotRecorded, _investigation.Evaluate(S("STM_Marlowe_Finances"), E("EVD_SaleContract")).Outcome);

            _investigation.RecordStatementsForTopic("SUS_Marlowe", "TOP_Money", "Our finances are none of your business.");
            Assert.AreEqual(ContradictionOutcome.Valid, _investigation.Evaluate(S("STM_Marlowe_Finances"), E("EVD_SaleContract")).Outcome);
        }

        [Test]
        public void EvidenceNotCollected_CantBeUsed()
        {
            Assert.AreEqual(ContradictionOutcome.EvidenceNotCollected, _investigation.Evaluate(S("STM_Marlowe_Home"), E("EVD_CCTV")).Outcome);
        }

        [Test]
        public void NotebookCompare_MarksFound_WithoutPenalty()
        {
            PrepareCodeConnection();
            CollectAndExamine("EVD_ScenePhotos");
            _investigation.Compare(S("STM_Marlowe_Home"), E("EVD_ScenePhotos")); // wrong: free in the notebook
            ContradictionResult r = _investigation.Compare(S("STM_Marlowe_Home"), E("EVD_KeycardLog"));

            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(StatementStatus.ContradictionFound, _investigation.GetState(S("STM_Marlowe_Home")).status);
            Assert.AreEqual(0, _investigation.FailedChallenges);
            Assert.IsFalse(_evidence.IsFactEstablished(F("FACT_MarloweAtOffice")), "facts from a contradiction come from confronting the suspect");
        }

        [Test]
        public void Checker_IsDeterministic()
        {
            PrepareCodeConnection();
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(ContradictionOutcome.Valid, _investigation.Evaluate(S("STM_Marlowe_Home"), E("EVD_CCTV")).Outcome);
                Assert.AreEqual(ContradictionOutcome.Invalid, _investigation.Evaluate(S("STM_Marlowe_Home"), E("EVD_CaseFile")).Outcome);
            }
        }

        [Test]
        public void FindValidContradiction_OnlyReturnsValidOnes()
        {
            CollectAndExamine("EVD_CCTV");
            Assert.IsNull(_investigation.FindValidContradiction("SUS_Marlowe", E("EVD_CCTV")), "unproven isn't enough");
            CollectAndExamine("EVD_CaseFile", "EVD_KeycardLog");
            _evidence.TryConnect(E("EVD_CCTV"), E("EVD_KeycardLog"), out _);
            Assert.AreEqual(S("STM_Marlowe_Home"), _investigation.FindValidContradiction("SUS_Marlowe", E("EVD_CCTV")).Statement);
        }

        [Test]
        public void Observations_AndProgress_AreSavedAndRestored()
        {
            PrepareCodeConnection();
            _investigation.ApplyChallenge(_investigation.Evaluate(S("STM_Marlowe_Home"), E("EVD_CCTV")));
            _investigation.AddObservation("The coat on the CCTV looks expensive.");
            _investigation.RecordStatementsForTopic("SUS_Marlowe", "TOP_Phone", "Off.");

            var sessionSave = _session.CaptureState();
            var evidenceSave = _evidence.CaptureState();
            var investigationSave = _investigation.CaptureState();

            var session = new CaseSessionService(_catalog);
            var evidence = new EvidenceService(session);
            var investigation = new InvestigationService(session, evidence);
            session.RestoreState(sessionSave);
            evidence.RestoreState(evidenceSave);
            investigation.RestoreState(investigationSave);

            Assert.AreEqual(StatementStatus.Exposed, investigation.GetState(investigation.FindStatement("STM_Marlowe_Home")).status);
            Assert.IsTrue(investigation.IsStatementRecorded(investigation.FindStatement("STM_Marlowe_Phone")));
            Assert.AreEqual("The coat on the CCTV looks expensive.", investigation.Observations[0].text);
            Assert.AreEqual(_investigation.GetProgress().Percent, investigation.GetProgress().Percent);
        }

        // ------------------------------------------------------------------ dialogue side

        [Test]
        public void Dialogue_FollowsTheVerdict_NotTheOtherWayRound()
        {
            SuspectProfile marlowe = _case.suspects.First(s => s.suspectId == "SUS_Marlowe");
            var session = new DialogueSession(marlowe, new MockAIProvider(0, 0)) { DetectContradictionsOnPresent = false };
            var none = new List<string>();
            int composure = session.Composure;

            DialogueTurn rejected = session.ChallengeAsync("TOP_Whereabouts", "home", "EVD_CoffeeCups", "Two coffee cups", ChallengeVerdict.Invalid, none).GetAwaiter().GetResult();
            Assert.AreEqual(ResponseKind.ChallengeRejected, rejected.Plan.Kind);
            Assert.Greater(session.Composure, composure, "a failed challenge makes him more confident");
            Assert.IsFalse(session.IsContradicted("TOP_Whereabouts"));

            DialogueTurn unproven = session.ChallengeAsync("TOP_Whereabouts", "home", "EVD_CCTV", "CCTV still", ChallengeVerdict.Unproven, none).GetAwaiter().GetResult();
            Assert.AreEqual(ResponseKind.ChallengeUnproven, unproven.Plan.Kind);
            Assert.IsFalse(session.IsContradicted("TOP_Whereabouts"));

            DialogueTurn valid = session.ChallengeAsync("TOP_Whereabouts", "home", "EVD_CCTV", "CCTV still", ChallengeVerdict.Valid, none).GetAwaiter().GetResult();
            Assert.AreEqual(ResponseKind.EvidenceContradiction, valid.Plan.Kind);
            Assert.IsTrue(session.IsContradicted("TOP_Whereabouts"));
            Assert.AreEqual(marlowe.knowledge.FindTopic("TOP_Whereabouts").admissionAnswer, valid.Responses.Last().Text);
        }

        [Test]
        public void Dialogue_PresentingEvidence_DoesNotDecideContradictionsInGameMode()
        {
            SuspectProfile marlowe = _case.suspects.First(s => s.suspectId == "SUS_Marlowe");
            var session = new DialogueSession(marlowe, new MockAIProvider(0, 0)) { DetectContradictionsOnPresent = false };
            DialogueTurn t = session.PresentEvidenceAsync("EVD_CCTV", "CCTV still", new List<string>()).GetAwaiter().GetResult();
            Assert.AreNotEqual(ResponseKind.EvidenceContradiction, t.Plan.Kind, "only the investigation system can expose a statement");
            Assert.IsFalse(session.IsContradicted("TOP_Whereabouts"));
        }

        // ------------------------------------------------------------------ data

        [Test]
        public void Case0147_Statements_AreConsistent()
        {
            var ids = new HashSet<string>();
            foreach (SuspectStatement s in _case.statements)
            {
                Assert.IsTrue(ids.Add(s.statementId), "duplicate " + s.statementId);
                Assert.IsNotNull(s.suspect, s.statementId);
                Assert.IsNotEmpty(s.claim, s.statementId);
                Assert.IsNotNull(s.suspect.knowledge.FindTopic(s.topicId), $"{s.statementId}: topic {s.topicId} doesn't exist");
                foreach (ContradictionRule r in s.contradictions)
                {
                    Assert.IsTrue(_case.evidence.Contains(r.evidence), $"{s.statementId}: rule evidence not in the case");
                    Assert.IsNotEmpty(r.explanation, s.statementId);
                    foreach (CaseFact f in r.requiresConfirmed) Assert.IsTrue(_case.facts.Contains(f), $"{s.statementId}: required fact not in the case");
                    if (r.confirmsFact != null) Assert.IsTrue(_case.facts.Contains(r.confirmsFact));
                    Assert.IsNotEmpty(s.suspect.knowledge.FindTopic(s.topicId).admissionAnswer, $"{s.statementId}: a contradictable topic needs an admission line");
                }
            }
            foreach (CaseFact f in _case.facts)
            {
                Assert.IsNotEmpty(f.description, f.factId);
                Assert.IsNotEmpty(f.source, f.factId);
                foreach (SuspectStatement s in f.relatedStatements) Assert.IsTrue(_case.statements.Contains(s), f.factId);
            }
        }
    }
}
