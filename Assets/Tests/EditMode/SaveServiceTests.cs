using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLA.Core.Events;
using TLA.Data.Config;
using TLA.Systems.Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace TLA.Tests
{
    public class SaveServiceTests
    {
        /// <summary>Keeps "files" in a dictionary so tests never touch the real disk.</summary>
        private sealed class InMemorySaveStorage : ISaveStorage
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
            public readonly Dictionary<string, string> Backups = new Dictionary<string, string>();
            public string Location => "memory";
            public bool Exists(string key) => Files.ContainsKey(key);
            public void Write(string key, string content)
            {
                if (Files.TryGetValue(key, out string old)) Backups[key] = old; // like FileSaveStorage's .bak
                Files[key] = content;
            }
            public bool TryRead(string key, out string content) => Files.TryGetValue(key, out content);
            public bool TryReadBackup(string key, out string content) => Backups.TryGetValue(key, out content);
            public void Delete(string key)
            {
                Files.Remove(key);
                Backups.Remove(key);
            }
        }

        /// <summary>A system whose restore always fails (to test rollback).</summary>
        private sealed class BrokenSystem : ISaveable
        {
            public string SaveKey => "broken";
            public JToken CaptureState() => new JObject();
            public void RestoreState(JToken state) => throw new System.InvalidOperationException("boom on restore");
        }

        /// <summary>A pretend game system with some state to save.</summary>
        private sealed class FakeSystem : ISaveable
        {
            public class State
            {
                public int Score;
                public List<string> FoundIds = new List<string>();
            }

            public State Data = new State();
            public string SaveKey => "fake";
            public JToken CaptureState() => JToken.FromObject(Data);
            public void RestoreState(JToken state) => Data = state.ToObject<State>();
        }

        private InMemorySaveStorage _storage;
        private SaveService _saves;
        private FakeSystem _system;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _storage = new InMemorySaveStorage();
            _saves = new SaveService(_storage, new SaveConfig()); // defaults: prefix "save_", 3 slots
            _system = new FakeSystem();
            _saves.Register(_system);
        }

        [TearDown]
        public void TearDown() => EventBus.Clear();

        [Test]
        public void SaveThenLoad_RestoresState_AndScene()
        {
            _system.Data.Score = 7;
            _system.Data.FoundIds.Add("EVD_Glove");

            Assert.IsTrue(_saves.Save(0, "Sandbox"));

            _system.Data = new FakeSystem.State(); // simulate quitting and restarting
            Assert.IsTrue(_saves.Load(0, out SaveData data));

            Assert.AreEqual(7, _system.Data.Score);
            CollectionAssert.AreEqual(new[] { "EVD_Glove" }, _system.Data.FoundIds);
            Assert.AreEqual("Sandbox", data.sceneName);
            Assert.AreEqual(SaveData.CurrentSaveVersion, data.saveVersion);
        }

        [Test]
        public void Save_UsesConfiguredFileName()
        {
            _saves.Save(1, "Sandbox");

            Assert.IsTrue(_storage.Exists("save_1"));
            Assert.IsTrue(_saves.HasSave(1));
        }

        [Test]
        public void Load_EmptySlot_ReturnsFalse()
        {
            LogAssert.Expect(LogType.Warning, new Regex("No save found"));
            Assert.IsFalse(_saves.Load(2, out _));
        }

        [Test]
        public void Load_CorruptedFile_ReturnsFalse()
        {
            _storage.Files["save_0"] = "{ this is not json";

            LogAssert.Expect(LogType.Error, new Regex("corrupted"));
            Assert.IsFalse(_saves.Load(0, out _));
        }

        [Test]
        public void Load_SaveFromNewerGameVersion_IsRefused()
        {
            _storage.Files["save_0"] = "{ \"saveVersion\": 999, \"sceneName\": \"Sandbox\", \"sections\": {} }";

            LogAssert.Expect(LogType.Error, new Regex("newer version"));
            Assert.IsFalse(_saves.Load(0, out _));
        }

        [Test]
        public void OutOfRangeSlot_IsRejected()
        {
            LogAssert.Expect(LogType.Error, new Regex("out of range"));
            Assert.IsFalse(_saves.Save(5, "Sandbox"));
        }

        [Test]
        public void Delete_RemovesSave()
        {
            _saves.Save(0, "Sandbox");
            _saves.Delete(0);

            Assert.IsFalse(_saves.HasSave(0));
        }

        // ------------------------------------------------------------------ reliability (Stage 11)

        [Test]
        public void DamagedSave_FallsBackToTheBackup()
        {
            _system.Data.Score = 1;
            _saves.Save(0, "Sandbox");
            _system.Data.Score = 2;
            _saves.Save(0, "Sandbox"); // the first save becomes the backup
            _storage.Files["save_0"] = _storage.Files["save_0"].Substring(0, 40); // cut off mid-write

            LogAssert.Expect(LogType.Warning, new Regex("backup"));
            Assert.IsTrue(_saves.Load(0, out _));
            Assert.AreEqual(1, _system.Data.Score, "restored from the previous save");
            Assert.IsTrue(_saves.GetSlotInfo(0).UsesBackup);
        }

        [Test]
        public void EditedSave_IsRejectedByTheChecksum()
        {
            _system.Data.Score = 5;
            _saves.Save(0, "Sandbox");
            _storage.Files["save_0"] = _storage.Files["save_0"].Replace("\"Score\": 5", "\"Score\": 9999");
            _system.Data.Score = 0;

            LogAssert.Expect(LogType.Error, new Regex("checksum"));
            Assert.IsFalse(_saves.Load(0, out _));
            Assert.AreEqual(0, _system.Data.Score, "nothing was restored");
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("null")]
        [TestCase("[1,2,3]")]
        [TestCase("{\"saveVersion\": \"two\"}")]
        [TestCase("{\"saveVersion\": 2, \"sections\": {\"fake\": {}}}")]
        public void GarbageSaves_NeverThrow(string content)
        {
            _storage.Files["save_0"] = content;
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => Assert.IsFalse(_saves.Load(0, out _)));
            Assert.IsFalse(_saves.GetSlotInfo(0).IsValid);
        }

        [Test]
        public void FailingSystem_RollsEveryoneBack_AndLoadFails()
        {
            _system.Data.Score = 3;
            _saves.Register(new BrokenSystem());
            _saves.Save(0, "Sandbox");
            _system.Data.Score = 42; // the game moved on

            LogAssert.Expect(LogType.Error, new Regex("cancelled"));
            LogAssert.Expect(LogType.Error, new Regex("roll back 'broken'")); // the broken system fails its rollback too; everyone else is restored
            Assert.IsFalse(_saves.Load(0, out SaveData data));
            Assert.IsNull(data);
            Assert.AreEqual(42, _system.Data.Score, "the half-done load was undone");
        }

        [Test]
        public void Version1Save_IsMigratedAndLoads()
        {
            _storage.Files["save_0"] = "{ \"saveVersion\": 1, \"sceneName\": \"PoliceStation\", \"sections\": { \"fake\": { \"Score\": 11, \"FoundIds\": [\"EVD_A\"] } } }";
            Assert.IsTrue(_saves.Load(0, out SaveData data));
            Assert.AreEqual(SaveData.CurrentSaveVersion, data.saveVersion);
            Assert.AreEqual(11, _system.Data.Score);
            Assert.IsNotNull(data.summary);
        }

        [Test]
        public void SlotInfo_DescribesEmptyDamagedAndValidSlots()
        {
            _saves.SummaryProvider = () => new SaveSummary { caseTitle = "CASE 0147", objective = "Find a motive", evidenceCollected = 3, evidenceTotal = 6 };
            _saves.Save(1, "EvidenceRoom");
            _storage.Files["save_2"] = "{ broken";

            Assert.IsFalse(_saves.GetSlotInfo(0).Exists);
            SaveSlotInfo valid = _saves.GetSlotInfo(1);
            Assert.IsTrue(valid.IsValid);
            Assert.AreEqual("EvidenceRoom", valid.SceneName);
            Assert.AreEqual("Find a motive", valid.Summary.objective);
            Assert.AreEqual(3, valid.Summary.evidenceCollected);
            Assert.AreNotEqual(default(System.DateTime), valid.SavedAtUtc);
            SaveSlotInfo damaged = _saves.GetSlotInfo(2);
            Assert.IsTrue(damaged.Exists);
            Assert.IsFalse(damaged.IsValid);
            Assert.IsNotNull(damaged.Problem);
        }

        [Test]
        public void Autosave_IsItsOwnSlot_AndContinueUsesTheNewest()
        {
            _system.Data.Score = 1;
            _saves.Save(0, "Sandbox");
            System.Threading.Thread.Sleep(20);
            _system.Data.Score = 2;
            Assert.IsTrue(_saves.Save(SaveService.AutoSlot, "Sandbox"));

            Assert.IsTrue(_storage.Exists("save_auto"));
            Assert.AreEqual(SaveService.AutoSlot, _saves.MostRecentSlot());
            Assert.AreEqual(4, _saves.GetAllSlots().Count, "autosave + 3 manual slots");

            _storage.Files["save_auto"] = "{ damaged";
            Assert.AreEqual(0, _saves.MostRecentSlot(), "a damaged save is skipped");
        }

        [Test]
        public void FailedSave_LeavesThePreviousSaveIntact()
        {
            _system.Data.Score = 8;
            _saves.Save(0, "Sandbox");
            string before = _storage.Files["save_0"];
            _saves.Register(new ThrowingCaptureSystem());

            LogAssert.Expect(LogType.Error, new Regex("failed"));
            Assert.IsFalse(_saves.Save(0, "Sandbox"));
            Assert.AreEqual(before, _storage.Files["save_0"]);
        }

        private sealed class ThrowingCaptureSystem : ISaveable
        {
            public string SaveKey => "throwsOnSave";
            public JToken CaptureState() => throw new System.InvalidOperationException("can't capture");
            public void RestoreState(JToken state) { }
        }
    }
}
