using System;
using Newtonsoft.Json.Linq;
using TLA.Core.Services;
using TLA.Systems.Save;

namespace TLA.Systems.World
{
    /// <summary>The player in the current scene, as the save system needs it. Implemented in Gameplay.</summary>
    public interface IPlayerLocator
    {
        string SceneName { get; }
        float X { get; }
        float Y { get; }
        float Z { get; }
        float Yaw { get; }
        void PlaceAt(float x, float y, float z, float yaw);
    }

    /// <summary>
    /// Saves where the player was standing, and puts them back there after a save is loaded (instead
    /// of at the scene's default spawn). The player registers itself when its scene starts.
    /// </summary>
    public sealed class PlayerLocationService : IGameService, ISaveable
    {
        [Serializable]
        private sealed class Location
        {
            public string scene;
            public float x, y, z, yaw;
        }

        private IPlayerLocator _player;
        private Location _pending; // from a loaded save, waiting for that scene's player

        public string SaveKey => "playerLocation";

        public void Initialize() { }

        public void Shutdown() { }

        /// <summary>Called by the player when its scene starts. Applies a loaded position if it's for this scene.</summary>
        public void Register(IPlayerLocator player)
        {
            _player = player;
            if (_pending == null || player == null) return;
            if (_pending.scene == player.SceneName) player.PlaceAt(_pending.x, _pending.y, _pending.z, _pending.yaw);
            _pending = null; // only ever applied once, to the scene it was saved in
        }

        public void Unregister(IPlayerLocator player)
        {
            if (ReferenceEquals(_player, player)) _player = null;
        }

        /// <summary>Forget a loaded position (e.g. a new game started).</summary>
        public void ClearPending() => _pending = null;

        public JToken CaptureState()
        {
            if (_player == null) return JValue.CreateNull();
            return JToken.FromObject(new Location { scene = _player.SceneName, x = _player.X, y = _player.Y, z = _player.Z, yaw = _player.Yaw });
        }

        public void RestoreState(JToken state)
        {
            _pending = state == null || state.Type == JTokenType.Null ? null : state.ToObject<Location>();
            if (_pending != null && (float.IsNaN(_pending.x) || float.IsNaN(_pending.y) || float.IsNaN(_pending.z) || string.IsNullOrEmpty(_pending.scene)))
                _pending = null; // nonsense in the file: just use the scene's spawn
        }
    }
}
