using System.Text.RegularExpressions;
using NUnit.Framework;
using TLA.Core.Events;
using TLA.Systems;
using TLA.Systems.Flow;
using UnityEngine;
using UnityEngine.TestTools;

namespace TLA.Tests
{
    public class GameStateManagerTests
    {
        private GameStateManager _state;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _state = new GameStateManager();
        }

        [TearDown]
        public void TearDown() => EventBus.Clear();

        [Test]
        public void StartsInBoot()
        {
            Assert.AreEqual(GameState.Boot, _state.Current);
        }

        [Test]
        public void AllowedChange_Succeeds_AndPublishesEvent()
        {
            GameStateChangedEvent? received = null;
            EventBus.Subscribe<GameStateChangedEvent>(e => received = e);

            bool changed = _state.TryChangeState(GameState.MainMenu);

            Assert.IsTrue(changed);
            Assert.AreEqual(GameState.MainMenu, _state.Current);
            Assert.IsTrue(received.HasValue);
            Assert.AreEqual(GameState.Boot, received.Value.Previous);
            Assert.AreEqual(GameState.MainMenu, received.Value.Current);
        }

        [Test]
        public void IllegalChange_IsRefused()
        {
            _state.TryChangeState(GameState.MainMenu);

            LogAssert.Expect(LogType.Warning, new Regex("not allowed"));
            bool changed = _state.TryChangeState(GameState.Ending);

            Assert.IsFalse(changed);
            Assert.AreEqual(GameState.MainMenu, _state.Current);
        }

        [Test]
        public void PauseAndResume_ReturnsToPreviousState()
        {
            _state.TryChangeState(GameState.MainMenu);
            _state.TryChangeState(GameState.Interrogating);

            Assert.IsTrue(_state.Pause());
            Assert.IsTrue(_state.IsPaused);

            Assert.IsTrue(_state.Resume());
            Assert.AreEqual(GameState.Interrogating, _state.Current);
        }

        [Test]
        public void WhilePaused_OnlyResumeOrMainMenuAllowed()
        {
            _state.TryChangeState(GameState.MainMenu);
            _state.TryChangeState(GameState.Investigating);
            _state.Pause();

            Assert.IsFalse(_state.CanChangeTo(GameState.Interrogating));
            Assert.IsTrue(_state.CanChangeTo(GameState.Investigating));
            Assert.IsTrue(_state.CanChangeTo(GameState.MainMenu));
        }

        [Test]
        public void ForceState_IgnoresRules()
        {
            _state.ForceState(GameState.Interrogating);

            Assert.AreEqual(GameState.Interrogating, _state.Current);
            Assert.AreEqual(GameState.Boot, _state.Previous);
        }
    }
}
