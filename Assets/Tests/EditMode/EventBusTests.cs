using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TLA.Core.Events;
using UnityEngine;
using UnityEngine.TestTools;

namespace TLA.Tests
{
    public class EventBusTests
    {
        private readonly struct TestEvent : IGameEvent
        {
            public readonly int Value;
            public TestEvent(int value) => Value = value;
        }

        [SetUp]
        public void SetUp() => EventBus.Clear();

        [TearDown]
        public void TearDown() => EventBus.Clear();

        [Test]
        public void Publish_DeliversEventToSubscriber()
        {
            int received = 0;
            EventBus.Subscribe<TestEvent>(e => received = e.Value);

            EventBus.Publish(new TestEvent(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            int calls = 0;
            Action<TestEvent> handler = _ => calls++;
            EventBus.Subscribe(handler);
            EventBus.Unsubscribe(handler);

            EventBus.Publish(new TestEvent(1));

            Assert.AreEqual(0, calls);
            Assert.AreEqual(0, EventBus.SubscriberCount<TestEvent>());
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNothing()
        {
            Assert.DoesNotThrow(() => EventBus.Publish(new TestEvent(1)));
        }

        [Test]
        public void Publish_HandlerThatThrows_DoesNotStopOtherHandlers()
        {
            bool secondCalled = false;
            EventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));
            EventBus.Subscribe<TestEvent>(_ => secondCalled = true);

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            EventBus.Publish(new TestEvent(1));

            Assert.IsTrue(secondCalled);
        }
    }
}
