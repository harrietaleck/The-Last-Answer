using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TLA.Core.Events;
using TLA.Data.Cases;
using TLA.Data.Evidence;
using TLA.Data.Investigation;
using TLA.Systems.Cases;
using TLA.Systems.Investigation;
using UnityEditor;
using UnityEngine;

namespace TLA.Tests
{
    /// <summary>
    /// Case management on the real Case 0147: validation of every case in the catalog, objectives
    /// completing from game state, the investigation status, every ending, saving, and a check that
    /// the story's facts agree across evidence, dialogue, timeline and crime record.
    /// </summary>
    public class CaseProgressTests
    {
        private const string CasePath = "Assets/_Project/Data/Cases/Case0147/CASE_0147.asset";

        private CaseDefinition _case;
        private CaseCatalog _catalog;
        private CaseSessionService _session;
        private EvidenceService _evidence;
        private InvestigationService _investigation;
        private CaseProgressService _progress;
        private readonly List<string> _completed = new List<string>();

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _case = AssetDatabase.LoadAssetAtPath<CaseDefinition>(CasePath);
            Assert.IsNotNull(_case);
            Assert.Greater(_case.objectives.Count, 0, "case content missing: run The Last Answer → Environment → Rebuild All Locations");
            _catalog = ScriptableObject.CreateInstance<CaseCatalog>();
            _catalog.cases.Add(_case);
            _session = new CaseSessionService(_catalog);
            _evidence = new EvidenceService(_session);
            _investigation = new InvestigationService(_session, _evidence);
            _progress = new CaseProgressService(_session, _evidence, _investigation);
            _progress.Initialize();
            _completed.Clear();
            EventBus.Subscribe<ObjectiveCompletedEvent>(e => _completed.Add(e.Objective.objectiveId));

            _session.StartCase(_case);
            _evidence.BeginCase();
            _investigation.BeginCase();
            _progress.BeginCase();
        }

        [TearDown]
        public void TearDown()
        {
            _progress.Shutdown();
            EventBus.Clear();
            Object.DestroyImmediate(_catalog);
        }

        private EvidenceDefinition E(string id) => _evidence.Find(id);
        private SuspectProfile Marlowe => _case.suspects.First(s => s.suspectId == "SUS_Marlowe");

        private void CollectAndExamine(params string[] ids)
        {
            foreach (string id in ids)
            {
                _evidence.Collect(E(id));
                _evidence.Examine(E(id));
            }
        }

        private void Connect(string a, string b) => Assert.AreEqual(ConnectResult.Connected, _evidence.TryConnect(E(a), E(b), out _));

        private void Expose(string statementId, string evidenceId)
        {
            ContradictionResult r = _investigation.Evaluate(_investigation.FindStatement(statementId), E(evidenceId));
            Assert.IsTrue(r.IsValid, $"{statementId} vs {evidenceId}: {r.Outcome}");
            _investigation.ApplyChallenge(r);
        }

        /// <summary>Opportunity: the alibi broken (CCTV + access log connected, then confronted).</summary>
        private void BreakAlibi()
        {
            _progress.MarkQuestioned(Marlowe);
            CollectAndExamine("EVD_CaseFile", "EVD_ScenePhotos", "EVD_CCTV", "EVD_KeycardLog");
            Connect("EVD_CCTV", "EVD_KeycardLog");
            Expose("STM_Marlowe_Home", "EVD_CCTV");
        }

        [Test]
        public void Objectives_DoneOutOfOrder_CompleteOnceTheirPrerequisitesAre()
        {
            // Expose the alibi before searching the room: the objective waits, then the whole chain completes at once.
            _progress.MarkQuestioned(Marlowe);
            CollectAndExamine("EVD_CaseFile", "EVD_CCTV", "EVD_KeycardLog");
            Connect("EVD_CCTV", "EVD_KeycardLog");
            Expose("STM_Marlowe_Home", "EVD_CCTV");
            Assert.IsFalse(_progress.IsObjectiveCompleted("OBJ_BreakAlibi"), "its prerequisites aren't done yet");

            CollectAndExamine("EVD_ScenePhotos");
            foreach (string id in new[] { "OBJ_SearchEvidenceRoom", "OBJ_TraceEntry", "OBJ_BreakAlibi" })
                Assert.IsTrue(_progress.IsObjectiveCompleted(id), id);
        }

        // ------------------------------------------------------------------ validation

        [Test]
        public void EveryCaseInTheCatalog_IsValid()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CaseCatalog>("Assets/_Project/Data/Cases/CaseCatalog.asset");
            foreach (CaseDefinition c in catalog.cases)
                CollectionAssert.IsEmpty(CaseValidator.Validate(c), c != null ? c.caseId : "null case");
        }

        [Test]
        public void Validator_CatchesAuthoringMistakes()
        {
            var broken = ScriptableObject.CreateInstance<CaseDefinition>();
            try
            {
                broken.caseId = "CASE_BROKEN";
                broken.title = "Broken";
                broken.isAvailable = true;
                broken.objectives.Add(new ObjectiveDefinition { objectiveId = "OBJ_A", title = "A", prerequisites = new[] { "OBJ_Missing" } });
                broken.objectives.Add(new ObjectiveDefinition { objectiveId = "OBJ_A", title = "A again" });
                broken.conclusions.Add(new ConclusionDefinition { conclusionId = "CON_X", title = "X" });
                broken.endings.Add(new EndingDefinition { endingId = "END_Y", conclusionId = "CON_Unknown", epilogue = "" });

                string all = string.Join("\n", CaseValidator.Validate(broken));
                StringAssert.Contains("no victim", all);
                StringAssert.Contains("no suspects", all);
                StringAssert.Contains("duplicate objective", all);
                StringAssert.Contains("unknown objective 'OBJ_Missing'", all);
                StringAssert.Contains("CON_X has no ending", all);
                StringAssert.Contains("unknown conclusion", all);
                StringAssert.Contains("no ending has the outcome Solved", all);
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        // ------------------------------------------------------------------ objectives and status

        [Test]
        public void NewCase_StartsOnTheFirstObjective()
        {
            Assert.AreEqual(InvestigationStatus.Investigating, _progress.Status);
            Assert.AreEqual("OBJ_ReviewCaseFile", _progress.CurrentObjective.objectiveId);
            Assert.AreEqual(_progress.CurrentObjective.title, _session.Objective, "the HUD objective comes from the case data");
            Assert.IsFalse(_progress.CanConclude);
            Assert.IsFalse(_progress.IsObjectiveActive(_case.FindObjective("OBJ_BreakAlibi")), "later objectives are hidden until their prerequisites are done");
        }

        [Test]
        public void Objectives_CompleteFromGameState_AndUnlockTheNextOnes()
        {
            _evidence.Collect(E("EVD_CaseFile"));
            CollectionAssert.Contains(_completed, "OBJ_ReviewCaseFile");
            Assert.IsTrue(_progress.IsObjectiveActive(_case.FindObjective("OBJ_SearchEvidenceRoom")));

            _progress.MarkQuestioned(Marlowe);
            CollectionAssert.Contains(_completed, "OBJ_QuestionMarlowe");
            CollectionAssert.Contains(_progress.QuestionedSuspects, "SUS_Marlowe");
            Assert.IsTrue(_progress.CanConclude, "the case can be closed once Marlowe has been questioned");

            CollectAndExamine("EVD_ScenePhotos", "EVD_CCTV");
            CollectionAssert.Contains(_completed, "OBJ_SearchEvidenceRoom");
            Assert.AreEqual("OBJ_TraceEntry", _progress.CurrentObjective.objectiveId);
        }

        [Test]
        public void FullInvestigation_ReachesReadyToConclude()
        {
            BreakAlibi();
            CollectAndExamine("EVD_ScenePhotos", "EVD_CoffeeCups", "EVD_SaleContract");
            Connect("EVD_ScenePhotos", "EVD_CoffeeCups");
            Connect("EVD_SaleContract", "EVD_CaseFile");
            _investigation.RecordStatementsForTopic("SUS_Marlowe", "TOP_Money", "Our finances are none of your business.");
            Expose("STM_Marlowe_Finances", "EVD_SaleContract");

            foreach (ObjectiveDefinition o in _case.objectives)
                if (o.objectiveId != "OBJ_CloseCase") Assert.IsTrue(_progress.IsObjectiveCompleted(o.objectiveId), o.objectiveId);
            Assert.AreEqual(InvestigationStatus.ReadyToConclude, _progress.Status);
            Assert.AreEqual("OBJ_CloseCase", _progress.CurrentObjective.objectiveId);
        }

        // ------------------------------------------------------------------ endings

        [Test]
        public void Ending_Solved_WithOpportunityMotiveAndTheVisitor()
        {
            BreakAlibi();
            CollectAndExamine("EVD_ScenePhotos", "EVD_CoffeeCups", "EVD_SaleContract");
            Connect("EVD_SaleContract", "EVD_CaseFile"); // motive

            CaseConcludedEvent? concluded = null;
            EventBus.Subscribe<CaseConcludedEvent>(e => concluded = e);
            EndingDefinition ending = _progress.Conclude("CON_ChargeMarlowe");

            Assert.AreEqual("END_Solved", ending.endingId);
            Assert.AreEqual(EndingOutcome.Solved, ending.outcome);
            Assert.AreEqual(InvestigationStatus.Concluded, _progress.Status);
            Assert.IsTrue(_progress.IsObjectiveCompleted("OBJ_CloseCase"));
            Assert.AreEqual("CON_ChargeMarlowe", _progress.FindDecision("DEC_CloseCase").choice);
            Assert.IsTrue(concluded.HasValue);
            Assert.AreEqual("END_Solved", concluded.Value.Ending.endingId);
            Assert.IsNull(_progress.Conclude("CON_NoCharge"), "a closed case can't be closed again");
        }

        [Test]
        public void Ending_WeakCase_WithoutAMotive()
        {
            BreakAlibi();
            Assert.AreEqual("END_WeakCase", _progress.Conclude("CON_ChargeMarlowe").endingId);
        }

        [Test]
        public void Ending_Collapses_IfTheAlibiWasNeverBroken()
        {
            _progress.MarkQuestioned(Marlowe);
            CollectAndExamine("EVD_CaseFile", "EVD_SaleContract");
            Connect("EVD_SaleContract", "EVD_CaseFile"); // a motive alone isn't enough
            EndingDefinition e = _progress.Conclude("CON_ChargeMarlowe");
            Assert.AreEqual("END_Collapsed", e.endingId);
            Assert.AreEqual(EndingOutcome.Failed, e.outcome);
        }

        [TestCase("CON_ChargeKim", "END_WrongKim", EndingOutcome.Failed)]
        [TestCase("CON_ChargeHayes", "END_WrongHayes", EndingOutcome.Failed)]
        [TestCase("CON_NoCharge", "END_Unsolved", EndingOutcome.Unsolved)]
        public void Ending_OtherConclusions(string conclusion, string endingId, EndingOutcome outcome)
        {
            BreakAlibi(); // even with Marlowe exposed, the wrong choice gets the wrong ending
            EndingDefinition e = _progress.Conclude(conclusion);
            Assert.AreEqual(endingId, e.endingId);
            Assert.AreEqual(outcome, e.outcome);
        }

        [Test]
        public void CannotConclude_BeforeQuestioningTheSuspect()
        {
            Assert.IsNull(_progress.Conclude("CON_ChargeMarlowe"));
            Assert.AreEqual(InvestigationStatus.Investigating, _progress.Status);
        }

        // ------------------------------------------------------------------ saving

        [Test]
        public void Progress_SavesAndRestores()
        {
            BreakAlibi();
            _progress.RecordDecision("DEC_Test", "yes", "A test decision");
            var saves = new[] { _session.CaptureState(), _evidence.CaptureState(), _investigation.CaptureState(), _progress.CaptureState() };

            var session = new CaseSessionService(_catalog);
            var evidence = new EvidenceService(session);
            var investigation = new InvestigationService(session, evidence);
            var progress = new CaseProgressService(session, evidence, investigation);
            session.RestoreState(saves[0]);
            evidence.RestoreState(saves[1]);
            investigation.RestoreState(saves[2]);
            progress.RestoreState(saves[3]);

            Assert.IsTrue(progress.IsObjectiveCompleted("OBJ_BreakAlibi"));
            Assert.IsTrue(progress.WasQuestioned(Marlowe));
            Assert.AreEqual("yes", progress.FindDecision("DEC_Test").choice);
            Assert.AreEqual(_progress.CurrentObjective.objectiveId, progress.CurrentObjective.objectiveId);
            Assert.AreEqual(_session.Objective, session.Objective);
        }

        // ------------------------------------------------------------------ story consistency

        [Test]
        public void StoryFacts_AgreeAcrossEvidenceDialogueTimelineAndCrime()
        {
            string Pages(string id) => string.Join("\n", E(id).pages);
            string timeline = string.Join("\n", _case.timeline.Select(t => t.time + " " + t.description));
            SuspectKnowledge k = Marlowe.knowledge;

            // Access log times appear on the timeline.
            foreach (string time in new[] { "17:58", "18:40", "19:10", "21:38", "23:47" })
            {
                StringAssert.Contains(time, Pages("EVD_KeycardLog"), "access log");
                StringAssert.Contains(time, timeline, "timeline");
            }
            // The CCTV time matches the timeline, Marlowe's admission ("twenty to ten") and the statement rule.
            StringAssert.Contains("21:40", Pages("EVD_CCTV"));
            StringAssert.Contains("21:40", timeline);
            StringAssert.Contains("twenty to ten", k.FindTopic("TOP_Whereabouts").admissionAnswer);
            // Discovery and the call.
            StringAssert.Contains("23:52", Pages("EVD_CaseFile"));
            StringAssert.Contains("23:52", _case.crime.discoveredBy);
            StringAssert.Contains("23:50", _case.crime.discoveredBy);
            StringAssert.Contains("23:50", _case.briefing);
            // The sale: Marlowe's admission date is on the timeline; the buyer matches the document.
            StringAssert.Contains("12th", k.FindTopic("TOP_Money").admissionAnswer);
            StringAssert.Contains("12th", timeline);
            StringAssert.Contains("Calder", Pages("EVD_SaleContract"));
            StringAssert.Contains("Calder", k.FindTopic("TOP_Money").admissionAnswer);
            // Time of death excludes Hayes (23:47) and includes Marlowe's visit (21:40).
            StringAssert.Contains("21:30", _case.crime.timeOfDeath);
            StringAssert.Contains("22:30", _case.crime.timeOfDeath);
            // The solution: Marlowe is the culprit, so the only Solved ending charges him, and he never
            // treats the death as a true unknown (he lies about it instead). Kim's card was never used.
            foreach (EndingDefinition e in _case.endings.Where(x => x.outcome == EndingOutcome.Solved))
                Assert.AreEqual(Marlowe, _case.FindConclusion(e.conclusionId).accused);
            Assert.AreEqual(TopicStance.Lie, k.FindTopic("TOP_Death").stance);
            StringAssert.Contains("No entries for card 0131 (V. Kim)", Pages("EVD_KeycardLog"));
            StringAssert.Contains("Elliot Marlowe killed Daniel Reyes", _case.solutionNotes);
        }
    }
}
