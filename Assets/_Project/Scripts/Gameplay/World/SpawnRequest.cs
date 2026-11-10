using UnityEngine;

namespace TLA.Gameplay.World
{
    /// <summary>
    /// Remembers which spawn point the player should use in the NEXT scene. A scene door sets it
    /// just before loading; the player reads ("consumes") it once the new scene has loaded.
    /// Deliberately tiny: it only carries one spawn id across a scene change.
    /// </summary>
    public static class SpawnRequest
    {
        private static string _pendingSpawnId;

        public static void Set(string spawnId)
        {
            _pendingSpawnId = spawnId;
        }

        /// <summary>Returns the requested spawn id (or null) and clears it.</summary>
        public static string Consume()
        {
            string id = _pendingSpawnId;
            _pendingSpawnId = null;
            return id;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _pendingSpawnId = null;
        }
    }
}
