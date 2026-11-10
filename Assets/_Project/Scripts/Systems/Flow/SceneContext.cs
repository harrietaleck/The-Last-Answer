using UnityEngine;

namespace TLA.Systems.Flow
{
    /// <summary>
    /// Put ONE of these in every content scene (MainMenu, HQ, crime scenes, Interrogation...).
    /// It tells the game which <see cref="GameState"/> to enter once the scene has loaded,
    /// so scenes describe themselves and the scene loader doesn't need a hard-coded list.
    ///
    /// The Bootstrap scene does NOT need one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneContext : MonoBehaviour
    {
        [Tooltip("The game state to enter when this scene finishes loading.")]
        [SerializeField] private GameState stateOnLoad = GameState.Investigating;

        public GameState StateOnLoad => stateOnLoad;
    }
}
