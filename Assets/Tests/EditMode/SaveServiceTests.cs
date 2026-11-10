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
            public string Location => "memory";
            public bool Exists(string key) => Files.ContainsKey(key);
            public void Write(string key, string content) => Files[key] = content;
            public bool TryRead(string key, out string content) => Files.TryGetValue(key, out content);
            public void Delete(string key) => Files.Remove(key);
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
    }
}
