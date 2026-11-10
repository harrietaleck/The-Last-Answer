using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Cases;
using TLA.Gameplay.Interaction;
using TLA.Gameplay.Player;
using TLA.Systems.Dialogue;
using TLA.Systems.Flow;
using UnityEngine;

namespace TLA.Gameplay.Cinematics
{
    /// <summary>
    /// The investigator's chair. "[E] Sit down" switches the game to the Interrogating state, hands
    /// the view to the <see cref="CinematicShotDirector"/>, and announces <see cref="InterviewStartedEvent"/>
    /// (the dialogue screen opens). Standing up, via the Interview map's Leave key or the UI's
    /// "End interview" button (<see cref="InterviewLeaveRequestedEvent"/>), returns to first person.
    /// </summary>
    public sealed class InterviewSeat : Interactable
    {
        [SerializeField] private CinematicShotDirector director;

        [Tooltip("Where the player stands after getting up (position + facing).")]
        [SerializeField] private Transform standPoint;

        [Tooltip("The suspect being interviewed in this room.")]
        [SerializeField] private SuspectProfile suspect;

        private FirstPersonController _player;
        private Camera _playerCamera;

        public bool IsSeated { get; private set; }
        public SuspectProfile Suspect => suspect;

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
            EventBus.Subscribe<InterviewLeaveRequestedEvent>(OnLeaveRequested);
            director.Begin(interactor.GetComponent<PlayerInputReader>());
            EventBus.Publish(new InterviewStartedEvent(suspect));
        }

        /// <summary>Ends the seated session. Also callable by other systems (e.g. when an interview ends).</summary>
        public void StandUp()
        {
            if (!IsSeated) return;

            director.LeaveRequested -= StandUp;
            EventBus.Unsubscribe<InterviewLeaveRequestedEvent>(OnLeaveRequested);
            director.End();
            IsSeated = false;

            if (_playerCamera != null) _playerCamera.enabled = true;
            if (_player != null && standPoint != null)
                _player.Teleport(standPoint.position, standPoint.eulerAngles.y);

            EventBus.Publish(new InterviewEndedEvent());

            if (Services.TryGet(out GameStateManager state))
                state.TryChangeState(GameState.Investigating);
        }

        private void OnLeaveRequested(InterviewLeaveRequestedEvent evt)
        {
            StandUp();
        }

        private void OnDisable()
        {
            if (IsSeated) StandUp();
        }
    }
}
