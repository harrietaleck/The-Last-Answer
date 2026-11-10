using System;
using System.Collections.Generic;
using UnityEngine;

namespace TLA.Core.Events
{
    /// <summary>
    /// A tiny, type-safe publish/subscribe messaging system.
    ///
    /// WHY: systems announce what happened ("the game state changed") without knowing who
    /// is listening. The UI, audio and other systems subscribe and react. This keeps systems
    /// independent: you can redesign the UI without touching gameplay code.
    ///
    /// USAGE:
    ///   EventBus.Subscribe&lt;GameStateChangedEvent&gt;(OnStateChanged);    // usually in OnEnable
    ///   EventBus.Unsubscribe&lt;GameStateChangedEvent&gt;(OnStateChanged);  // ALWAYS in OnDisable
    ///   EventBus.Publish(new GameStateChangedEvent(previous, current));
    ///
    /// RULE: every Subscribe must have a matching Unsubscribe, otherwise destroyed objects
    /// keep receiving events and cause MissingReferenceExceptions.
    ///
    /// NOTE: the bus is intended for use from Unity's main thread only.
    /// </summary>
    public static class EventBus
    {
        // One multicast delegate (a list of handlers) per event type.
        private static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Handlers.TryGetValue(typeof(T), out Delegate existing);
            Handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null) return;
            if (!Handlers.TryGetValue(typeof(T), out Delegate existing)) return;

            Delegate remaining = Delegate.Remove(existing, handler);
            if (remaining == null)
                Handlers.Remove(typeof(T));
            else
                Handlers[typeof(T)] = remaining;
        }

        /// <summary>
        /// Sends the event to every current subscriber.
        /// If one subscriber throws, the error is logged and the remaining subscribers still run,
        /// so a bug in (for example) the UI can't break core game logic.
        /// </summary>
        public static void Publish<T>(T gameEvent) where T : struct, IGameEvent
        {
            if (!Handlers.TryGetValue(typeof(T), out Delegate existing)) return;

            // GetInvocationList returns a copy, so handlers may safely (un)subscribe while we iterate.
            foreach (Delegate handler in existing.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler).Invoke(gameEvent);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        /// <summary>Number of handlers currently subscribed to an event type (useful for tests/debugging).</summary>
        public static int SubscriberCount<T>() where T : struct, IGameEvent
        {
            return Handlers.TryGetValue(typeof(T), out Delegate existing) ? existing.GetInvocationList().Length : 0;
        }

        /// <summary>Removes every subscription. Called when the game shuts down and between tests.</summary>
        public static void Clear()
        {
            Handlers.Clear();
        }

        // Unity can be configured to skip "domain reload" when entering Play Mode (faster iteration).
        // Static fields then survive between play sessions, so we reset them explicitly here.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
        }
    }
}
