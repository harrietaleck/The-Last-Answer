using TLA.Core.Services;
using TLA.Data.Audio;
using TLA.Systems.Audio;
using UnityEngine;

namespace TLA.Gameplay.Player
{
    /// <summary>
    /// Footsteps from real movement: a step every stride length travelled on the ground, quieter when
    /// crouching and slightly louder when running. No steps while airborne, standing still or paused.
    /// The clips are the AudioLibrary's Footstep variations (picked so the same one never repeats).
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        [SerializeField] private float walkStride = 1.5f;
        [SerializeField] private float runStride = 2.1f;
        [SerializeField] private float crouchStride = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float walkVolume = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float runVolume = 0.9f;
        [Range(0f, 1f)] [SerializeField] private float crouchVolume = 0.35f;

        private FirstPersonController _controller;
        private float _travelled;

        private void Awake() => _controller = GetComponent<FirstPersonController>();

        private void Update()
        {
            if (Time.timeScale <= 0f || !_controller.IsGrounded || _controller.HorizontalSpeed < 0.4f)
            {
                _travelled = Mathf.Min(_travelled, 0.5f); // first step comes soon after starting to move
                return;
            }

            _travelled += _controller.HorizontalSpeed * Time.deltaTime;
            float stride = _controller.IsCrouching ? crouchStride : _controller.IsRunning ? runStride : walkStride;
            if (_travelled < stride) return;

            _travelled = 0f;
            float volume = _controller.IsCrouching ? crouchVolume : _controller.IsRunning ? runVolume : walkVolume;
            if (Services.TryGet(out AudioManager audio)) audio.Play(SoundCue.Footstep, null, volume);
        }
    }
}
