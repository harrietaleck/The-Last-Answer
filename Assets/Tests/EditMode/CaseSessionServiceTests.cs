using NUnit.Framework;
using TLA.Core.Events;
using TLA.Data.Cases;
using TLA.Systems.Investigation;
using UnityEngine;

namespace TLA.Tests
{
    public class CaseSessionServiceTests
    {
        private CaseCatalog _catalog;
        private CaseDefinition _case;
        private CaseSessionService _session;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _case = ScriptableObject.CreateInstance<CaseDefinition>();
            _case.caseId = "CASE_TEST";
            _case.initialObjective = "Start here.";
            _catalog = ScriptableObject.CreateInstance<CaseCatalog>();
            _catalog.cases.Add(_case);
            _session = new CaseSessionService(_catalog);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
            Object.DestroyImmediate(_case);
            Object.DestroyImmediate(_catalog);
        }

        [Test]
        public void StartCase_SetsObjective_AndAnnounces()
        {
            int started = 0;
            EventBus.Subscribe<CaseStartedEvent>(_ => started++);
            _session.StartCase(_case);
            Assert.AreEqual(1, started);
            Assert.AreEqual("Start here.", _session.Objective);
        }

        [Test]
        public void Evidence_IsAddedOnce()
        {
            _session.StartCase(_case);
            int events = 0;
            EventBus.Subscribe<EvidenceAddedEvent>(_ => events++);
            Assert.IsTrue(_session.AddEvidence("EVD_A", "Item A"));
            Assert.IsFalse(_session.AddEvidence("EVD_A", "Item A"), "duplicates are ignored");
            Assert.AreEqual(1, _session.Evidence.Count);
            Assert.AreEqual(1, events);
        }

        [Test]
        public void SaveAndRestore_RoundTrips()
        {
            _session.StartCase(_case);
            _session.AddEvidence("EVD_A", "Item A", "desc");
            _session.AddNote("A note", "Somewhere");
            _session.SetObjective("Next step");
            var saved = _session.CaptureState();

            var restored = new CaseSessionService(_catalog);
            restored.RestoreState(saved);
            Assert.AreSame(_case, restored.CurrentCase);
            Assert.AreEqual("Next step", restored.Objective);
            Assert.AreEqual("Item A", restored.Evidence[0].displayName);
            Assert.AreEqual("A note", restored.Notes[0].text);
        }
    }
}
