using UnityEngine;

namespace TLA.Gameplay.Environment
{
    /// <summary>
    /// Makes a light (and optionally its emissive fixture) flicker now and then, like an ageing
    /// fluorescent tube. Use it SPARINGLY, on one light per area at most: subtle unease, not a
    /// horror effect.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class FlickeringLight : MonoBehaviour
    {
        [Tooltip("Average seconds between flicker bursts.")]
        [Min(0.5f)] [SerializeField] private float averageInterval = 9f;

        [Tooltip("How dark the light dips during a flicker (0 = off, 1 = no dip).")]
        [Range(0f, 1f)] [SerializeField] private float dipBrightness = 0.35f;

        [Tooltip("Optional emissive mesh (the tube) that should flicker with the light.")]
        [SerializeField] private Renderer fixtureRenderer;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Light _light;
        private float _baseIntensity;
        private Color _baseEmission;
        private MaterialPropertyBlock _block;
        private float _nextBurst;
        private float _burstEnd;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;
            _block = new MaterialPropertyBlock();
            if (fixtureRenderer != null && fixtureRenderer.sharedMaterial != null && fixtureRenderer.sharedMaterial.HasProperty(EmissionColorId))
                _baseEmission = fixtureRenderer.sharedMaterial.GetColor(EmissionColorId);
            ScheduleNext();
        }

        private void Update()
        {
            float level = 1f;
            if (Time.time >= _nextBurst)
            {
                if (_burstEnd < _nextBurst) _burstEnd = Time.time + Random.Range(0.08f, 0.45f);

                // Rapid on/off stutter during the burst.
                level = Random.value > 0.5f ? dipBrightness : 1f;
                if (Time.time >= _burstEnd) ScheduleNext();
            }

            _light.intensity = _baseIntensity * level;

            if (fixtureRenderer != null)
            {
                fixtureRenderer.GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, _baseEmission * level);
                fixtureRenderer.SetPropertyBlock(_block);
            }
        }

        private void ScheduleNext()
        {
            _nextBurst = Time.time + Random.Range(averageInterval * 0.5f, averageInterval * 1.5f);
        }
    }
}
