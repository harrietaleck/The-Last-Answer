using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TLA.Core.Events;
using TLA.Data.Cases;
using TLA.Data.Config;
using TLA.Data.Evidence;
using TLA.Systems.Cases;
using TLA.Systems.Dialogue;
using TLA.Systems.Investigation;
using TLA.Systems.Save;
using TLA.Systems.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TLA.Tests
{
    /// <summary>
    /// "Save, close the game, reopen, continue" with the REAL systems and real files on disk:
    /// play part of Case 0147, save, throw every service away, build brand-new ones (as a fresh
    /// launch would) reading the same save folder, load, and check that everything came back.
    /// </summary>
    public class SaveRestartTests
    {
        private const string CasePath = "Assets/_Project/Data/Cases/Case0147/CASE_0147.asset";

        /// <summary>One "launch" of the game: all case systems registered with a save service, as the GameManager does.</summary>
        private sealed class World
        {
            public CaseSessionService Session;
            public EvidenceService Evidence;
            public InvestigationService Investigation;
            public CaseProgressService Progress;
            public InterviewService Interviews;
            public PlayerLocationService Location;
            public SaveService Saves;

            public World(CaseCatalog catalog, string folder)
            {
                Session = new CaseSessionService(catalog);
                Evidence = new EvidenceService(Session);
                Investigation = new InvestigationService(Session, Evidence);
                Progress = new CaseProgressService(Session, Evidence, Investigation);
                Progress.Initialize();
                Interviews = new InterviewService();
                Location = new PlayerLocationService();
                Saves = new SaveService(new FileSaveStorage(folder), new SaveConfig());
                foreach (ISaveable s in new ISaveable[] { Session, Evidence, Investigation, Progress, Interviews, Location }) Saves.Register(s);
            }

            public void Close() => Progress.Shutdown();
        }

        private sealed class FakePlayer : IPlayerLocator
        {
            public string SceneName { get; set; } = "InterrogationRoom";
            public float X { get; set; } = 1.5f;
            public float Y { get; set; }
            public float Z { get; set; } = -2.25f;
            public float Yaw { get; set; } = 90f;
            public void PlaceAt(float x, float y, float z, float yaw) { X = x; Y = y; Z = z; Yaw = yaw; }
        }

        private CaseDefinition _case;
        private CaseCatalog _catalog;
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _case = AssetDatabase.LoadAssetAtPath<CaseDefinition>(CasePath);
            _catalog = ScriptableObject.CreateInstance<CaseCatalog>();
            _catalog.cases.Add(_case);
            _folder = Path.Combine(Application.temporaryCachePath, "tla_save_test_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
            Object.DestroyImmediate(_catalog);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private SuspectProfile Marlowe => _case.suspects.First(s => s.suspectId == "SUS_Marlowe");

        private static DialogueSession NewSession(SuspectProfile s) =>
            new DialogueSession(s, new MockAIProvider(0, 0)) { DetectContradictionsOnPresent = false };

        /// <summary>Plays a slice of the case: evidence, connections, an interview, a contradiction, notes and a decision.</summary>
        private DialogueSession PlaySomeOfTheCase(World w)
        {
            w.Session.StartCase(_case);
            w.Evidence.BeginCase();
            w.Investigation.BeginCase();
            w.Progress.BeginCase();
            w.Interviews.BeginCase();

            foreach (string id in new[] { "EVD_CaseFile", "EVD_ScenePhotos", "EVD_CCTV", "EVD_KeycardLog" })
            {
                w.Evidence.Collect(w.Evidence.Find(id));
                w.Evidence.Examine(w.Evidence.Find(id));
            }
            w.Evidence.TryConnect(w.Evidence.Find("EVD_CCTV"), w.Evidence.Find("EVD_KeycardLog"), out _);

            DialogueSession session = w.Interviews.GetOrCreate(Marlowe, () => NewSession(Marlowe));
            session.BeginAsync().GetAwaiter().GetResult();
            session.AskAsync("Why didn't you answer your phone?", null, new List<string>()).GetAwaiter().GetResult();
            w.Investigation.RecordStatementsForTopic("SUS_Marlowe", "TOP_Phone", session.History.Last().Text);
            w.Progress.MarkQuestioned(Marlowe);

            ContradictionResult r = w.Investigation.Evaluate(w.Investigation.FindStatement("STM_Marlowe_Home"), w.Evidence.Find("EVD_CCTV"));
            session.ChallengeAsync("TOP_Whereabouts", "home", "EVD_CCTV", "CCTV still", ChallengeVerdict.Valid, new List<string>()).GetAwaiter().GetResult();
            w.Investigation.ApplyChallenge(r);

            w.Investigation.AddObservation("He never asked how Reyes died.");
            w.Progress.RecordDecision("DEC_Test", "pressed", "Pressed Marlowe hard");
            w.Progress.AddPlayTime(754f);
            return session;
        }

        [Test]
        public void SaveCloseReopenLoad_RestoresTheWholeInvestigation()
        {
            // ---- First launch: play, then save.
            var first = new World(_catalog, _folder);
            var player = new FakePlayer();
            first.Location.Register(player);
            DialogueSession before = PlaySomeOfTheCase(first);
            Assert.IsTrue(first.Saves.Save(1, "InterrogationRoom"));

            var expected = new
            {
                Objective = first.Session.Objective,
                Notes = first.Session.Notes.Count,
                History = before.History.Select(m => m.Text).ToList(),
                Composure = before.Composure,
                Completed = first.Progress.CompletedObjectives.ToList(),
                Percent = first.Investigation.GetProgress().Percent
            };
            first.Close();
            EventBus.Clear(); // the game closes

            // ---- Second launch: brand-new systems, same save folder.
            var second = new World(_catalog, _folder);
            Assert.IsTrue(second.Saves.Load(1, out SaveData data));

            Assert.AreEqual("InterrogationRoom", data.sceneName, "current scene");
            Assert.AreEqual("CASE_0147", second.Session.CurrentCase.caseId, "current case");
            Assert.AreEqual(expected.Objective, second.Session.Objective, "current objective");
            Assert.AreEqual(expected.Notes, second.Session.Notes.Count, "investigation notes");
            Assert.IsTrue(second.Evidence.IsCollected(second.Evidence.Find("EVD_KeycardLog")), "collected evidence");
            Assert.IsTrue(second.Evidence.IsExamined(second.Evidence.Find("EVD_CCTV")), "examined evidence");
            Assert.AreEqual(1, second.Evidence.GetConnections(second.Evidence.Find("EVD_CCTV")).Count, "connections");
            Assert.AreEqual(StatementStatus.Exposed, second.Investigation.GetState(second.Investigation.FindStatement("STM_Marlowe_Home")).status, "contradictions");
            Assert.AreEqual("He never asked how Reyes died.", second.Investigation.Observations[0].text, "player observations");
            CollectionAssert.AreEqual(expected.Completed, second.Progress.CompletedObjectives, "completed objectives");
            Assert.IsTrue(second.Progress.WasQuestioned(Marlowe), "suspects questioned");
            Assert.AreEqual("pressed", second.Progress.FindDecision("DEC_Test").choice, "decisions");
            Assert.AreEqual(754f, second.Progress.PlayTimeSeconds, 0.01f, "play time");
            Assert.AreEqual(expected.Percent, second.Investigation.GetProgress().Percent);

            // Interview: history and Marlowe's state come back when he's next interviewed.
            DialogueSession after = second.Interviews.GetOrCreate(Marlowe, () => NewSession(Marlowe));
            CollectionAssert.AreEqual(expected.History, after.History.Select(m => m.Text).ToList(), "conversation history");
            Assert.AreEqual(expected.Composure, after.Composure, "composure");
            Assert.IsTrue(after.IsContradicted("TOP_Whereabouts"), "exposed topic");
            Assert.AreEqual(1, after.TimesAsked("TOP_Phone"), "topics asked");
            Assert.IsTrue(after.PresentedEvidence.Contains("EVD_CCTV"), "evidence shown");

            // Player position is applied when that scene's player appears.
            var newPlayer = new FakePlayer { X = 0, Z = 0, Yaw = 0 };
            second.Location.Register(newPlayer);
            Assert.AreEqual(1.5f, newPlayer.X, 0.001f);
            Assert.AreEqual(-2.25f, newPlayer.Z, 0.001f);
            Assert.AreEqual(90f, newPlayer.Yaw, 0.001f);
            second.Close();
        }

        [Test]
        public void ClosedCase_KeepsItsEnding()
        {
            var first = new World(_catalog, _folder);
            PlaySomeOfTheCase(first);
            EndingDefinition ending = first.Progress.Conclude("CON_ChargeMarlowe");
            Assert.IsNotNull(ending);
            first.Saves.Save(0, "InterrogationRoom");
            first.Close();
            EventBus.Clear();

            var second = new World(_catalog, _folder);
            Assert.IsTrue(second.Saves.Load(0, out _));
            Assert.AreEqual(InvestigationStatus.Concluded, second.Progress.Status);
            Assert.AreEqual(ending.endingId, second.Progress.EndingId);
            CaseConcludedEvent? stored = second.Progress.StoredConclusion();
            Assert.IsTrue(stored.HasValue, "the ending screen can be shown again");
            Assert.AreEqual(ending.endingId, stored.Value.Ending.endingId);
            Assert.IsFalse(second.Progress.CanConclude, "a closed case stays closed");
            second.Close();
        }

        [Test]
        public void DamagedFileOnDisk_LoadsTheBackup_AndNeverThrows()
        {
            var first = new World(_catalog, _folder);
            PlaySomeOfTheCase(first);
            first.Saves.Save(0, "EvidenceRoom");
            first.Evidence.Collect(first.Evidence.Find("EVD_SaleContract"));
            first.Saves.Save(0, "EvidenceRoom"); // previous save kept as save_0.json.bak
            first.Close();

            string path = Path.Combine(_folder, "save_0.json");
            string text = File.ReadAllText(path);
            File.WriteAllText(path, text.Substring(0, text.Length / 2)); // power cut mid-write

            EventBus.Clear();
            var second = new World(_catalog, _folder);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("backup"));
            Assert.DoesNotThrow(() => Assert.IsTrue(second.Saves.Load(0, out _)));
            Assert.IsTrue(second.Evidence.IsCollected(second.Evidence.Find("EVD_CCTV")));
            Assert.IsFalse(second.Evidence.IsCollected(second.Evidence.Find("EVD_SaleContract")), "the older, intact save was loaded");
            second.Close();
        }

        [Test]
        public void SaveFiles_ContainNoCredentials()
        {
            var first = new World(_catalog, _folder);
            PlaySomeOfTheCase(first);
            first.Saves.Save(0, "InterrogationRoom");
            first.Close();

            string text = File.ReadAllText(Path.Combine(_folder, "save_0.json")).ToLowerInvariant();
            foreach (string forbidden in new[] { "apikey", "api_key", "secret", "password", "bearer", "authorization", "backendbaseurl", "token\"" })
                StringAssert.DoesNotContain(forbidden, text);
        }
    }
}
