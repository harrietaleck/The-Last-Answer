using TLA.Core.Services;
using TLA.Gameplay.Interaction;
using TLA.Gameplay.Player;
using TLA.Systems.Flow;
using UnityEngine;

namespace TLA.Gameplay.Cinematics
{
    /// <summary>
    /// The investigator's chair. "[E] Sit down" switches the game to the Interrogating state and
    /// hands the view to the <see cref="CinematicShotDirector"/>. "[E] Stand up" (the Interview
    /// map's Leave key) returns to first person, standing at <see cref="standPoint"/>.
    ///
    /// The future dialogue system will start from here: sitting down is where an interview begins.
    /// </summary>
    public sealed class InterviewSeat : Interactable
    {
        [SerializeField] private CinematicShotDirector director;

        [Tooltip("Where the player stands after getting up (position + facing).")]
        [SerializeField] private Transform standPoint;

        private FirstPersonController _player;
        private Camera _playerCamera;

        public bool IsSeated { get; private set; }

        public override bool CanInteract => base.CanInteract && !IsSeated && director != null;

        protected override void OnInteract(PlayerInteractor interactor)
        {
            if (Services.TryGet(out GameStateManager state) && !state.TryChangeState(GameState.Interrogating))
                return;

            _player = interactor.GetComponent<FirstPersonController>();
            _playerCamera = _player.GetComponentInChildren<Camera>();
            if (_playerCamera != null) _playerCamera.enabled = false;

            IsSeated = true;
            director.LeaveRequested += StandUp;
            director.Begin(interactor.GetComponent<PlayerInputReader>());
        }

        /// <summary>Ends the seated session. Also callable by other systems (e.g. when an interview ends).</summary>
        public void StandUp()
        {
            if (!IsSeated) return;

            director.LeaveRequested -= StandUp;
            director.End();
            IsSeated = false;

            if (_playerCamera != null) _playerCamera.enabled = true;
            if (_player != null && standPoint != null)
                _player.Teleport(standPoint.position, standPoint.eulerAngles.y);

            if (Services.TryGet(out GameStateManager state))
                state.TryChangeState(GameState.Investigating);
        }

        private void OnDisable()
        {
            if (IsSeated) StandUp();
        }
    }
}
