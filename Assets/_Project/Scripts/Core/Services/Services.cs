using System;
using System.Collections.Generic;
using UnityEngine;

namespace TLA.Core.Services
{
    /// <summary>
    /// Central registry where the GameManager stores the game's services, so other code can find them:
    ///
    ///   var saves = Services.Get&lt;SaveService&gt;();
    ///
    /// WHY instead of many singletons: there is ONE place that creates and owns every system
    /// (the GameManager). Replacing an implementation later, e.g. a mock AI provider with an
    /// online one, means changing one registration line instead of hunting down static references.
    ///
    /// Only register long-lived systems here, not individual scene objects.
    /// </summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> Registry = new Dictionary<Type, object>();

        /// <summary>Registers a service under type <typeparamref name="T"/>. Replaces (with a warning) any previous one.</summary>
        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            if (Registry.ContainsKey(typeof(T)))
                Debug.LogWarning($"[Services] Replacing existing registration for {typeof(T).Name}.");

            Registry[typeof(T)] = service;
        }

        public static void Unregister<T>() where T : class
        {
            Registry.Remove(typeof(T));
        }

        /// <summary>Returns the service, or throws a clear error if it isn't registered yet.</summary>
        public static T Get<T>() where T : class
        {
            if (Registry.TryGetValue(typeof(T), out object service))
                return (T)service;

            throw new InvalidOperationException(
                $"[Services] {typeof(T).Name} is not registered. Is the Bootstrap scene (GameManager) loaded?");
        }

        /// <summary>Safe version of <see cref="Get{T}"/> for code that can run before the game has booted.</summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            if (Registry.TryGetValue(typeof(T), out object found))
            {
                service = (T)found;
                return true;
            }

            service = null;
            return false;
        }

        public static void Clear()
        {
            Registry.Clear();
        }

        // See EventBus.ResetStatics: keeps things clean when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
        }
    }
}
