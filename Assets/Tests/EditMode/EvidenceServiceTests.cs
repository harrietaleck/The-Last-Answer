using System.Collections.Generic;
using NUnit.Framework;
using TLA.Core.Events;
using TLA.Data.Cases;
using TLA.Data.Evidence;
using TLA.Systems.Investigation;
using UnityEditor;
using UnityEngine;

namespace TLA.Tests
{
    /// <summary>
    /// EvidenceService rules: discovery conditions, collecting, examining (facts), presenting,
    /// connecting, saving. Plus a check of the authored Case 0147 test evidence.
    /// </summary>
    public class EvidenceServiceTests
    {
        private readonly List<Object> _created = new List<Object>();
        private CaseDefinition _case;
        private CaseSessionService _session;
        private EvidenceService _evidence;
        private EvidenceDefinition _file, _photo, _log;
        private CaseFact _factFile, _factLink;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _factFile = Make<CaseFact>(f => { f.factId = "FACT_File"; f.title = "From the file"; });
            _factLink = Make<CaseFact>(f => { f.factId = "FACT_Link"; f.title = "From the link"; });

            _file = Item("EVD_File", EvidenceCategory.Document);
            _file.relatedFacts = new[] { _factFile };
            _file.noteOnCollect = "A note";
            _file.objectiveOnCollect = "Next objective";

            _photo = Item("EVD_Photo", EvidenceCategory.Photograph);
            _photo.collectable = false;

            _log = Item("EVD_Log", EvidenceCategory.Digital);
            _log.discovery = new DiscoveryConditions { requiresExamined = new[] { _file }, lockedHint = "Not yet." };
            _log.connections = new[] { new EvidenceConnection { other = _photo, explanation = "They match.", establishesFact = _factLink } };

            _case = Make<CaseDefinition>(c =>
            {
                c.caseId = "CASE_T";
                c.initialObjective = "Start";
                c.evidence.AddRange(new[] { _file, _photo, _log });
                c.facts.AddRange(new[] { _factFile, _factLink });
            });
            var catalog = Make<CaseCatalog>(c => c.cases.Add(_case));
            _session = new CaseSessionService(catalog);
            _evidence = new EvidenceService(_session);
            _session.StartCase(_case);
            _evidence.BeginCase();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
            foreach (Object o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // ------------------------------------------------------------------ collecting

        [Test]
        public void Collect_RecordsInSession_RunsNoteAndObjective_Once()
        {
            int added = 0;
            EventBus.Subscribe<EvidenceAddedEvent>(_ => added++);

            Assert.AreEqual(CollectResult.Collected, _evidence.Collect(_file));
            Assert.AreEqual(CollectResult.AlreadyCollected, _evidence.Collect(_file));

            EvidenceState s = _evidence.GetState("EVD_File");
            Assert.IsTrue(s.discovered && s.collected);
            Assert.IsTrue(_session.HasEvidence("EVD_File"), "the HUD/notebook/interview picker list is updated");
            Assert.AreEqual(1, added);
            Assert.AreEqual("Next objective", _session.Objective);
            Assert.AreEqual("A note", _session.Notes[0].text);
            CollectionAssert.AreEqual(new[] { _file }, _evidence.GetCollected());
        }

        [Test]
        public void ItemsOutsideTheCase_CannotBeCollected()
        {
            EvidenceDefinition stranger = Item("EVD_Stranger", EvidenceCategory.Object);
            Assert.AreEqual(CollectResult.Unknown, _evidence.Collect(stranger));
            Assert.IsFalse(_session.HasEvidence("EVD_Stranger"));
        }

        // ------------------------------------------------------------------ discovery conditions

        [Test]
        public void LockedItem_BecomesAvailable_OnlyAfterItsCondition()
        {
            Assert.IsFalse(_evidence.IsAvailable(_log));
            Assert.AreEqual(CollectResult.NotAvailable, _evidence.Collect(_log));
            Assert.IsFalse(_evidence.Discover(_log));

            _evidence.Collect(_file);
            Assert.IsFalse(_evidence.IsAvailable(_log), "collecting isn't enough: the file must be examined");

            _evidence.Examine(_file);
            Assert.IsTrue(_evidence.IsAvailable(_log));
            Assert.AreEqual(CollectResult.Collected, _evidence.Collect(_log));
        }

        // ------------------------------------------------------------------ examining

        [Test]
        public void Examine_RequiresDiscovery_ThenEstablishesFactsOnce()
        {
            ExamineResult early = _evidence.Examine(_file);
            Assert.IsFalse(early.Success, "can't examine something not found yet");

            var facts = new List<string>();
            EventBus.Subscribe<FactEstablishedEvent>(e => facts.Add(e.Fact.factId));

            _evidence.Discover(_file);
            ExamineResult first = _evidence.Examine(_file);
            ExamineResult again = _evidence.Examine(_file);

            Assert.IsTrue(first.Success && first.FirstTime);
            CollectionAssert.AreEqual(new[] { _factFile }, first.NewFacts);
            Assert.IsTrue(again.Success);
            Assert.IsFalse(again.FirstTime);
            Assert.IsEmpty(again.NewFacts);
            CollectionAssert.AreEqual(new[] { "FACT_File" }, facts);
            Assert.IsTrue(_evidence.IsFactEstablished(_factFile));
            Assert.IsTrue(_evidence.IsExamined(_file));
        }

        // ------------------------------------------------------------------ presenting

        [Test]
        public void MarkPresented_RemembersEachSuspectOnce()
        {
            _evidence.Collect(_file);
            _evidence.MarkPresented("EVD_File", "SUS_A");
            _evidence.MarkPresented("EVD_File", "SUS_A");
            _evidence.MarkPresented("EVD_File", "SUS_B");
            _evidence.MarkPresented("EVD_Nope", "SUS_A"); // unknown ids are ignored

            EvidenceState s = _evidence.GetState("EVD_File");
            Assert.IsTrue(s.WasPresented);
            CollectionAssert.AreEqual(new[] { "SUS_A", "SUS_B" }, s.presentedTo);
        }

        // ------------------------------------------------------------------ connecting

        [Test]
        public void Connect_OnlyAuthoredLinks_BetweenCollectedItems()
        {
            Assert.AreEqual(ConnectResult.NotCollected, _evidence.TryConnect(_photo, _file, out _));

            _evidence.Collect(_file);
            _evidence.Collect(_photo);
            Assert.AreEqual(ConnectResult.SameItem, _evidence.TryConnect(_file, _file, out _));
            Assert.AreEqual(ConnectResult.NotRelated, _evidence.TryConnect(_file, _photo, out _));
            Assert.IsEmpty(_evidence.GetConnections(_file));
        }

        [Test]
        public void Connect_WorksFromEitherSide_EstablishesFact_Once()
        {
            _evidence.Collect(_file);
            _evidence.Examine(_file);
            _evidence.Collect(_photo);
            _evidence.Collect(_log);

            int connected = 0;
            EventBus.Subscribe<EvidenceConnectedEvent>(_ => connected++);

            // The link is authored on the log, but connecting photo → log works too.
            Assert.AreEqual(ConnectResult.Connected, _evidence.TryConnect(_photo, _log, out EvidenceConnection link));
            Assert.AreEqual("They match.", link.explanation);
            Assert.AreEqual(ConnectResult.AlreadyConnected, _evidence.TryConnect(_log, _photo, out _));
            Assert.AreEqual(1, connected);
            Assert.IsTrue(_evidence.IsFactEstablished(_factLink));
            Assert.AreEqual(_log, _evidence.GetConnections(_photo)[0].Other);
            Assert.AreEqual(_photo, _evidence.GetConnections(_log)[0].Other);
        }

        // ------------------------------------------------------------------ saving

        [Test]
        public void SaveAndRestore_RoundTripsEverything()
        {
            _evidence.Collect(_file);
            _evidence.Examine(_file);
            _evidence.Collect(_photo);
            _evidence.Collect(_log);
            _evidence.TryConnect(_log, _photo, out _);
            _evidence.MarkPresented("EVD_Log", "SUS_A");
            var sessionSave = _session.CaptureState();
            var evidenceSave = _evidence.CaptureState();

            var catalog = Make<CaseCatalog>(c => c.cases.Add(_case));
            var session = new CaseSessionService(catalog);
            var evidence = new EvidenceService(session);
            session.RestoreState(sessionSave);
            evidence.RestoreState(evidenceSave);

            Assert.IsTrue(evidence.IsExamined(_file));
            Assert.IsTrue(evidence.IsCollected(_photo));
            Assert.IsTrue(evidence.IsFactEstablished(_factFile));
            Assert.IsTrue(evidence.IsFactEstablished(_factLink));
            Assert.AreEqual(1, evidence.GetConnections(_photo).Count);
            CollectionAssert.AreEqual(new[] { "SUS_A" }, evidence.GetState("EVD_Log").presentedTo);
            Assert.AreEqual(3, evidence.GetCollected().Count);
        }

        [Test]
        public void OldSave_WithOnlyCollectedRecords_KeepsItemsCollected()
        {
            _session.AddEvidence("EVD_File", "File"); // as an older save would contain, with no evidence section
            var evidence = new EvidenceService(_session);
            Assert.IsTrue(evidence.IsCollected(_file));
            Assert.IsFalse(evidence.IsExamined(_file));
        }

        [Test]
        public void BeginCase_ClearsProgress()
        {
            _evidence.Collect(_file);
            _evidence.Examine(_file);
            _session.StartCase(_case);
            _evidence.BeginCase();
            Assert.IsFalse(_evidence.IsCollected(_file));
            Assert.IsFalse(_evidence.IsFactEstablished(_factFile));
        }

        // ------------------------------------------------------------------ authored test content

        [Test]
        public void Case0147_TestEvidence_IsConsistent()
        {
            var c = AssetDatabase.LoadAssetAtPath<CaseDefinition>("Assets/_Project/Data/Cases/Case0147/CASE_0147.asset");
            Assert.IsNotNull(c);
            Assert.GreaterOrEqual(c.evidence.Count, 6, "test evidence missing: run The Last Answer → Environment → Rebuild All Locations");

            var ids = new HashSet<string>();
            foreach (EvidenceDefinition e in c.evidence)
            {
                Assert.IsNotNull(e);
                Assert.IsTrue(ids.Add(e.evidenceId), "duplicate id " + e.evidenceId);
                Assert.IsNotEmpty(e.displayName, e.evidenceId);
                Assert.IsNotEmpty(e.description, e.evidenceId);
                Assert.IsNotEmpty(e.locationFound, e.evidenceId);
                Assert.IsNotEmpty(e.examinationNotes, e.evidenceId);
                foreach (CaseFact f in e.relatedFacts) Assert.IsTrue(c.facts.Contains(f), $"{e.evidenceId}: fact {f?.factId} not in the case");
                foreach (EvidenceConnection link in e.connections)
                {
                    Assert.IsTrue(c.evidence.Contains(link.other), $"{e.evidenceId}: connection to an item outside the case");
                    Assert.IsNotEmpty(link.explanation);
                    if (link.establishesFact != null) Assert.IsTrue(c.facts.Contains(link.establishesFact));
                }
                foreach (EvidenceDefinition req in e.discovery.requiresCollected) Assert.IsTrue(c.evidence.Contains(req));
                foreach (EvidenceDefinition req in e.discovery.requiresExamined) Assert.IsTrue(c.evidence.Contains(req));
            }
        }

        [Test]
        public void Case0147_EveryItemCanBeReached_AndEveryConnectionMade()
        {
            var c = AssetDatabase.LoadAssetAtPath<CaseDefinition>("Assets/_Project/Data/Cases/Case0147/CASE_0147.asset");
            var catalog = Make<CaseCatalog>(x => x.cases.Add(c));
            var session = new CaseSessionService(catalog);
            var evidence = new EvidenceService(session);
            session.StartCase(c);
            evidence.BeginCase();

            // Keep collecting and examining whatever is available until nothing changes.
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (EvidenceDefinition e in c.evidence)
                {
                    if (!evidence.IsAvailable(e) || evidence.IsExamined(e)) continue;
                    evidence.Collect(e);
                    evidence.Examine(e);
                    progress = true;
                }
            }

            foreach (EvidenceDefinition e in c.evidence) Assert.IsTrue(evidence.IsCollected(e), e.evidenceId + " can never be found");
            foreach (EvidenceDefinition e in c.evidence)
                foreach (EvidenceConnection link in e.connections)
                    Assert.AreNotEqual(ConnectResult.NotRelated, evidence.TryConnect(e, link.other, out _));
            // Every fact that evidence can produce (examining or connecting) gets established.
            // (Facts confirmed by exposing a statement are covered by InvestigationScenarioTests.)
            foreach (EvidenceDefinition e in c.evidence)
            {
                foreach (CaseFact f in e.relatedFacts) Assert.IsTrue(evidence.IsFactEstablished(f), f.factId);
                foreach (EvidenceConnection link in e.connections)
                    if (link.establishesFact != null) Assert.IsTrue(evidence.IsFactEstablished(link.establishesFact), link.establishesFact.factId);
            }
        }

        // ------------------------------------------------------------------ helpers

        private EvidenceDefinition Item(string id, EvidenceCategory category) => Make<EvidenceDefinition>(e =>
        {
            e.evidenceId = id;
            e.displayName = id;
            e.category = category;
        });

        private T Make<T>(System.Action<T> init) where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            init(o);
            _created.Add(o);
            return o;
        }
    }
}
