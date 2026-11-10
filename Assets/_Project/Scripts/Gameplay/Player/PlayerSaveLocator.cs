using System.Collections;
using TLA.Core.Services;
using TLA.Systems.World;
using UnityEngine;

namespace TLA.Gameplay.Player
{
    /// <summary>
    /// Lets the save system read and restore the player's position. Registers with the
    /// PlayerLocationService one frame after the scene starts (after the spawn point has placed
    /// the player), so a loaded save's position wins over the default spawn.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class PlayerSaveLocator : MonoBehaviour, IPlayerLocator
    {
        private FirstPersonController _controller;
        private PlayerLocationService _service;

        public string SceneName => gameObject.scene.name;
        public float X => transform.position.x;
        public float Y => transform.position.y;
        public float Z => transform.position.z;
        public float Yaw => transform.eulerAngles.y;

        private void Awake() => _controller = GetComponent<FirstPersonController>();

        private IEnumerator Start()
        {
            yield return null; // let PlayerSpawnPlacement run first
            if (Services.TryGet(out _service)) _service.Register(this);
        }

        private void OnDestroy()
        {
            _service?.Unregister(this);
        }

        public void PlaceAt(float x, float y, float z, float yaw) => _controller.Teleport(new Vector3(x, y, z), yaw);
    }
}
