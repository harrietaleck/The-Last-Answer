using TLA.Core.Events;
using TLA.Systems;
using UnityEngine;

namespace TLA.UI.Transitions
{
    /// <summary>
    /// Full-screen black overlay in the Bootstrap scene. It fades to black when a scene starts loading
    /// (the SceneLoader waits for this; GameConfig → Audio → Scene Fade Out Seconds), stays black while
    /// the new scene loads, and fades back in once it's ready. Reacts only to SceneLoader events.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenFader : MonoBehaviour
    {
        [Tooltip("Seconds to fade from black to the new scene.")]
        [Min(0.05f)] [SerializeField] private float fadeInDuration = 0.8f;

        [Tooltip("Seconds to hold black after loading, so lights/audio settle before the reveal.")]
        [Min(0f)] [SerializeField] private float holdDuration = 0.15f;

        private CanvasGroup _group;
        private float _holdUntil;
        private bool _fadingIn;
        private bool _fadingOut;
        private float _fadeOutDuration = 0.4f;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 1f;               // start black; the first scene load fades in
            _group.blocksRaycasts = false;   // never block menu clicks
            _group.interactable = false;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SceneLoadStartedEvent>(OnLoadStarted);
            EventBus.Subscribe<SceneLoadedEvent>(OnLoaded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SceneLoadStartedEvent>(OnLoadStarted);
            EventBus.Unsubscribe<SceneLoadedEvent>(OnLoaded);
        }

        private void Start()
        {
            // Started directly in a content scene (Editor shortcut): no load event will come, so reveal now.
            BeginFadeIn();
        }

        private void Update()
        {
            if (_fadingOut)
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.unscaledDeltaTime / Mathf.Max(0.05f, _fadeOutDuration));
                if (_group.alpha >= 1f) _fadingOut = false;
                return;
            }
            if (!_fadingIn || Time.unscaledTime < _holdUntil) return;

            _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime / fadeInDuration);
            if (_group.alpha <= 0f) _fadingIn = false;
        }

        private void OnLoadStarted(SceneLoadStartedEvent evt)
        {
            _fadingIn = false;
            _fadeOutDuration = TLA.Core.Services.Services.TryGet(out TLA.Systems.Flow.SceneLoader loader) && loader.FadeOutSeconds > 0f ? loader.FadeOutSeconds : 0f;
            if (_fadeOutDuration > 0f) _fadingOut = true;
            else _group.alpha = 1f;
        }

        private void OnLoaded(SceneLoadedEvent evt)
        {
            BeginFadeIn();
        }

        private void BeginFadeIn()
        {
            _fadingOut = false;
            _holdUntil = Time.unscaledTime + holdDuration;
            _fadingIn = true;
        }
    }
}
