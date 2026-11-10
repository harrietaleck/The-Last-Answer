using TLA.Core.Services;
using TLA.Gameplay.Interaction;
using TLA.Systems;
using UnityEngine;

namespace TLA.Gameplay.World
{
    /// <summary>
    /// A door (or any object) that takes the player to another location, e.g. the hub's
    /// "Evidence Room" door. Press E → the screen fades → the target scene loads → the player
    /// appears at the target spawn point.
    ///
    /// SETUP: put it on an object with a collider, set Target Scene (must be in the Build Profile
    /// scene list) and Target Spawn Id (a PlayerSpawnPoint in that scene).
    /// The automated scene tests check that every SceneDoor points at a real scene and spawn.
    /// </summary>
    public sealed class SceneDoor : Interactable
    {
        [Tooltip("Scene to load, exactly as named in the Build Profile scene list.")]
        [SerializeField] private string targetScene = "PoliceStation";

        [Tooltip("PlayerSpawnPoint id in the target scene. Empty = that scene's default spawn.")]
        [SerializeField] private string targetSpawnId = "";

        public string TargetScene => targetScene;
        public string TargetSpawnId => targetSpawnId;

        public override bool CanInteract => base.CanInteract && !IsLoading();

        protected override void OnInteract(PlayerInteractor interactor)
        {
            if (!Services.TryGet(out GameManager game))
            {
                Debug.LogWarning("[SceneDoor] Game systems aren't running, so the scene can't change.", this);
                return;
            }

            SpawnRequest.Set(targetSpawnId);
            game.LoadScene(targetScene);
        }

        private static bool IsLoading()
        {
            return Services.TryGet(out GameManager game) && game.Scenes.IsLoading;
        }
    }
}
