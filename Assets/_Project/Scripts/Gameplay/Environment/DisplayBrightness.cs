using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Systems;
using TLA.Systems.Config;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TLA.Gameplay.Environment
{
    /// <summary>
    /// Applies the player's Brightness setting to every scene. Dark games need this: monitors and
    /// rooms vary a lot.
    ///
    /// Lives in the Bootstrap scene and creates a high-priority global Volume at runtime that only
    /// adjusts GAMMA (Lift Gamma Gain). The scene profiles never use gamma, so this shifts the
    /// midtones on top of each location's designed look instead of replacing it.
    /// </summary>
    public sealed class DisplayBrightness : MonoBehaviour
    {
        [Tooltip("How strongly the -1..+1 setting shifts gamma. 0.35 is a noticeable but safe range.")]
        [SerializeField] private float strength = 0.35f;

        private VolumeProfile _profile;
        private LiftGammaGain _gamma;

        private void Awake()
        {
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;

            _profile = ScriptableObject.CreateInstance<VolumeProfile>(); // runtime-only, never saved
            _gamma = _profile.Add<LiftGammaGain>(true);
            volume.profile = _profile;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<UserSettingsChangedEvent>(OnSettingsChanged);
        }

        private void Start()
        {
            Apply();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UserSettingsChangedEvent>(OnSettingsChanged);
        }

        private void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
        }

        private void OnSettingsChanged(UserSettingsChangedEvent evt)
        {
            Apply();
        }

        private void Apply()
        {
            float brightness = Services.TryGet(out ConfigService config) ? config.User.brightness : 0f;
            // Gamma's W component is the overall offset; positive = brighter midtones.
            _gamma.gamma.Override(new Vector4(1f, 1f, 1f, brightness * strength));
        }
    }
}
