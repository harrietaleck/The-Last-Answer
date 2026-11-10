using TLA.Systems.Audio;
using UnityEngine;

namespace TLA.Gameplay.Environment
{
    /// <summary>
    /// Put this next to an AudioSource that plays a looping ambience CLIP (your own recordings): it
    /// keeps the source at its base volume × the player's Ambience setting × scene fades × the pause dip.
    /// The generated ProceduralRoomTone does this by itself.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AmbienceVolume : MonoBehaviour
    {
        [Tooltip("Volume at full Ambience setting.")]
        [Range(0f, 1f)] [SerializeField] private float baseVolume = 0.5f;

        private AudioSource _source;

        private void Awake() => _source = GetComponent<AudioSource>();

        private void Update() => _source.volume = baseVolume * AudioManager.AmbienceGain;
    }
}
