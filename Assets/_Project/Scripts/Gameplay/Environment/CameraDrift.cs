using UnityEngine;

namespace TLA.Gameplay.Environment
{
    /// <summary>
    /// Very slow, smooth hand-held style drift for a static camera (used behind the main menu).
    /// It makes a still scene feel alive and cinematic. Keep the amounts tiny.
    /// </summary>
    public sealed class CameraDrift : MonoBehaviour
    {
        [Tooltip("Maximum position offset in metres.")]
        [SerializeField] private float positionAmount = 0.04f;

        [Tooltip("Maximum rotation offset in degrees.")]
        [SerializeField] private float rotationAmount = 0.6f;

        [Tooltip("How fast the drift wanders. Lower = slower, calmer.")]
        [SerializeField] private float speed = 0.08f;

        private Vector3 _basePosition;
        private Quaternion _baseRotation;

        private void Awake()
        {
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
        }

        private void Update()
        {
            float t = Time.unscaledTime * speed;
            // Perlin noise gives smooth, non-repeating wandering; offsets keep each axis independent.
            var offset = new Vector3(Noise(t, 0f), Noise(t, 10f), Noise(t, 20f)) * positionAmount;
            var tilt = new Vector3(Noise(t, 30f), Noise(t, 40f), Noise(t, 50f) * 0.3f) * rotationAmount;

            transform.localPosition = _basePosition + offset;
            transform.localRotation = _baseRotation * Quaternion.Euler(tilt);
        }

        private static float Noise(float t, float seed)
        {
            return Mathf.PerlinNoise(t, seed) * 2f - 1f;
        }
    }
}
