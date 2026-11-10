using System.Collections.Generic;
using TLA.Gameplay.Player;
using UnityEngine;

namespace TLA.Gameplay.Cinematics
{
    /// <summary>
    /// Owns a dedicated "cinematic" camera and cuts it between <see cref="CinematicShot"/>s.
    /// Used in the interrogation room while the player is seated: the first-person camera is
    /// switched off, this camera takes over, and Tab cycles through the shots.
    ///
    /// Later, the dialogue system will call <see cref="ShowShot(string)"/> to pick angles that fit
    /// the conversation (a close-up when a suspect hesitates, a wide shot for a confrontation...).
    /// </summary>
    public sealed class CinematicShotDirector : MonoBehaviour
    {
        [Tooltip("The camera this director moves. Keep it disabled in the scene; the director enables it.")]
        [SerializeField] private Camera cinematicCamera;

        [Tooltip("Shots in the order Tab cycles through them. The first is used when the session begins.")]
        [SerializeField] private List<CinematicShot> shots = new List<CinematicShot>();

        private PlayerInputReader _input;
        private CinematicShot _current;
        private int _index;
        private float _pushIn;

        public bool IsActive { get; private set; }
        public CinematicShot CurrentShot => _current;
        public IReadOnlyList<CinematicShot> Shots => shots;

        /// <summary>Raised when the player asks to leave (Leave key). The seat handles standing up.</summary>
        public event System.Action LeaveRequested;

        private void Awake()
        {
            if (cinematicCamera != null) cinematicCamera.enabled = false;
        }

        public void Begin(PlayerInputReader input)
        {
            if (cinematicCamera == null || shots.Count == 0)
            {
                Debug.LogError("[ShotDirector] Needs a camera and at least one shot.", this);
                return;
            }

            _input = input;
            IsActive = true;
            cinematicCamera.enabled = true;
            ShowShot(0);
        }

        public void End()
        {
            IsActive = false;
            if (cinematicCamera != null) cinematicCamera.enabled = false;
            _input = null;
        }

        public void ShowShot(int index)
        {
            if (shots.Count == 0) return;
            _index = (index % shots.Count + shots.Count) % shots.Count;
            _current = shots[_index];
            _pushIn = 0f;
            ApplyShot();
        }

        /// <summary>Cuts to the shot with this id. Returns false if there's no such shot.</summary>
        public bool ShowShot(string shotId)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                if (shots[i] != null && shots[i].ShotId == shotId)
                {
                    ShowShot(i);
                    return true;
                }
            }

            return false;
        }

        public void NextShot()
        {
            ShowShot(_index + 1);
        }

        private void Update()
        {
            if (!IsActive) return;

            if (_input != null)
            {
                if (_input.NextShotPressedThisFrame) NextShot();
                if (_input.LeavePressedThisFrame) LeaveRequested?.Invoke();
            }

            if (_current != null && _current.PushInSpeed > 0f)
            {
                _pushIn += _current.PushInSpeed * Time.deltaTime;
                ApplyShot();
            }
        }

        private void ApplyShot()
        {
            if (_current == null) return;
            Transform shot = _current.transform;
            cinematicCamera.transform.SetPositionAndRotation(shot.position + shot.forward * _pushIn, shot.rotation);
            cinematicCamera.fieldOfView = _current.FieldOfView;
        }
    }
}
