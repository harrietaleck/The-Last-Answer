using UnityEngine;

namespace TLA.Gameplay.Environment
{
    /// <summary>
    /// Generates a quiet "room tone" in code, so every location has a sense of space and tension
    /// without needing any audio files: soft rumbling noise (ventilation / distant city) plus an
    /// optional electrical hum (fluorescent lights).
    ///
    /// PLACEHOLDER: when you have real ambience recordings, assign a looping clip to this
    /// object's AudioSource and untick this component (or remove it).
    ///
    /// How it works: Unity calls OnAudioFilterRead on the audio thread for every block of
    /// samples, and we fill the block with filtered random noise and a sine wave.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ProceduralRoomTone : MonoBehaviour
    {
        [Tooltip("Overall loudness (0-1). Room tone should sit just above silence: ~0.05-0.15.")]
        [Range(0f, 1f)] [SerializeField] private float volume = 0.08f;

        [Tooltip("Higher = brighter, more 'air conditioning'; lower = deeper rumble.")]
        [Range(0.001f, 0.2f)] [SerializeField] private float noiseBrightness = 0.02f;

        [Tooltip("Mains hum frequency in Hz (50 in Europe/Africa, 60 in the Americas). 0 = no hum.")]
        [SerializeField] private float humFrequency = 50f;

        [Tooltip("Hum loudness relative to the noise.")]
        [Range(0f, 1f)] [SerializeField] private float humLevel = 0.25f;

        // Audio-thread state. System.Random (not UnityEngine.Random) because this runs off the main thread.
        private System.Random _random;
        private double _sampleRate;
        private double _phase;
        private float _lowPass;
        private float _volume, _brightness, _hum, _humLevel;

        private void Awake()
        {
            _random = new System.Random(GetInstanceID());
            _sampleRate = AudioSettings.outputSampleRate;

            var source = GetComponent<AudioSource>();
            if (source.clip == null)
            {
                source.loop = true;
                source.spatialBlend = 0f; // 2D: ambience is everywhere in the room
                source.playOnAwake = true;
                if (!source.isPlaying) source.Play();
            }
        }

        private void Update()
        {
            // Copy settings for the audio thread (Inspector changes apply live).
            _volume = volume;
            _brightness = noiseBrightness;
            _hum = humFrequency;
            _humLevel = humLevel;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_random == null || _sampleRate <= 0) return;

            double phaseStep = _hum * 2.0 * Mathf.PI / _sampleRate;
            for (int i = 0; i < data.Length; i += channels)
            {
                // One-pole low-pass on white noise = soft, deep rumble.
                float white = (float)(_random.NextDouble() * 2.0 - 1.0);
                _lowPass += (white - _lowPass) * _brightness;
                float sample = _lowPass * 6f;

                if (_hum > 0f)
                {
                    _phase += phaseStep;
                    if (_phase > 2.0 * Mathf.PI) _phase -= 2.0 * Mathf.PI;
                    // Fundamental + a quieter 2nd harmonic sounds like a real transformer hum.
                    sample += (float)(System.Math.Sin(_phase) * 0.6 + System.Math.Sin(_phase * 2.0) * 0.3) * _humLevel * 0.3f;
                }

                sample *= _volume;
                for (int c = 0; c < channels; c++) data[i + c] += sample;
            }
        }
    }
}
