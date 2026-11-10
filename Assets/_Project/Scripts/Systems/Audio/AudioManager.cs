using System.Collections.Generic;
using TLA.Core.Events;
using TLA.Core.Services;
using TLA.Data.Audio;
using TLA.Data.Config;
using TLA.Systems.Cases;
using TLA.Systems.Config;
using TLA.Systems.Dialogue;
using TLA.Systems.Flow;
using TLA.Systems.Investigation;
using UnityEngine;

namespace TLA.Systems.Audio
{
    /// <summary>
    /// All game audio in one place (lives on the GameManager object, so it persists across scenes).
    ///
    ///  EFFECTS  <see cref="Play"/>(cue). A fixed pool of AudioSources is reused (no allocations, no
    ///           runaway voices). Each cue has a cooldown and a maximum number of copies; when every
    ///           voice is busy a higher-priority sound replaces the oldest lower-priority one, otherwise
    ///           the new sound is simply skipped.
    ///  MUSIC    one mood per game state (menu, investigation, interrogation, ending), crossfaded.
    ///           A quiet tension layer rises as the suspect's composure falls and briefly after a
    ///           contradiction is exposed. Music and ambience dip while paused.
    ///  AMBIENCE scene room tones read <see cref="AmbienceGain"/> (volume setting × scene fade × pause).
    ///  VOLUME   master (AudioListener) × music / effects / ambience from the player's settings.
    ///
    /// Most sounds are triggered here from game events (evidence, objectives, contradictions, saves,
    /// case closed…), so gameplay code rarely needs to know about audio. Clips come from the
    /// AudioLibrary asset; nothing is hard-coded.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour, IGameService
    {
        private sealed class Voice
        {
            public AudioSource Source;
            public SoundCue Cue;
            public int Priority;
            public float StartedAt;
            public bool Busy => Source.isPlaying;
        }

        private AudioLibrary _library;
        private AudioConfig _config;
        private readonly List<Voice> _voices = new List<Voice>();
        private readonly Dictionary<SoundCue, SoundCueEntry> _cues = new Dictionary<SoundCue, SoundCueEntry>();
        private readonly Dictionary<SoundCue, float> _lastPlayed = new Dictionary<SoundCue, float>();
        private readonly Dictionary<SoundCue, int> _lastClip = new Dictionary<SoundCue, int>();

        private AudioSource _musicA, _musicB, _tension;
        private AudioSource _currentMusic;
        private MusicState _musicState = MusicState.None;
        private float _currentMusicVolume;

        private float _music = 0.7f, _effects = 0.9f, _ambience = 0.8f;
        private float _pauseDuck = 1f, _pauseDuckTarget = 1f;
        private float _sceneFade = 1f, _sceneFadeTarget = 1f, _sceneFadeSpeed = 2f;
        private float _tensionBase, _tensionSpike;
        private bool _interrogating;

        /// <summary>Gain for scene ambience (room tones): setting × scene fade × pause dip. Read on the audio thread too.</summary>
        public static float AmbienceGain { get; private set; } = 1f;

        public MusicState CurrentMusic => _musicState;

        /// <summary>Called by the GameManager right after AddComponent.</summary>
        public void Configure(AudioConfig config)
        {
            _config = config;
            _library = config?.Library;
            _cues.Clear();
            if (_library != null)
                foreach (SoundCueEntry e in _library.cues)
                    if (e != null && e.cue != SoundCue.None) _cues[e.cue] = e;

            int voices = config != null ? config.MaxSimultaneousEffects : 12;
            for (int i = _voices.Count; i < voices; i++)
            {
                // Each voice is its own child object, so it can be placed in the world for spatial sounds.
                var child = new GameObject("SfxVoice_" + i);
                child.transform.SetParent(transform, false);
                AudioSource s = child.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 1.5f;
                s.maxDistance = 20f;
                s.dopplerLevel = 0f;
                _voices.Add(new Voice { Source = s });
            }

            _musicA = _musicA != null ? _musicA : NewLoopSource();
            _musicB = _musicB != null ? _musicB : NewLoopSource();
            _tension = _tension != null ? _tension : NewLoopSource();
            if (_library != null && _library.tensionLoop != null)
            {
                _tension.clip = _library.tensionLoop;
                _tension.volume = 0f;
            }
            if (_library == null) Debug.LogWarning("[Audio] No AudioLibrary assigned in GameConfig → Audio; the game will be silent apart from room tones.");
        }

        private AudioSource NewLoopSource()
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        // ------------------------------------------------------------------ IGameService

        public void Initialize()
        {
            EventBus.Subscribe<UserSettingsChangedEvent>(OnSettings);
            EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<SceneLoadStartedEvent>(OnSceneLoadStarted);
            EventBus.Subscribe<SceneLoadedEvent>(OnSceneLoaded);
            EventBus.Subscribe<EvidenceAddedEvent>(OnEvidenceAdded);
            EventBus.Subscribe<EvidenceStateChangedEvent>(OnEvidenceChanged);
            EventBus.Subscribe<EvidenceInspectRequestedEvent>(OnInspect);
            EventBus.Subscribe<FactEstablishedEvent>(OnFact);
            EventBus.Subscribe<EvidenceConnectedEvent>(OnConnected);
            EventBus.Subscribe<ObjectiveCompletedEvent>(OnObjective);
            EventBus.Subscribe<ContradictionEvaluatedEvent>(OnContradiction);
            EventBus.Subscribe<InterviewStartedEvent>(OnInterviewStarted);
            EventBus.Subscribe<SuspectComposureChangedEvent>(OnComposure);
            EventBus.Subscribe<CaseConcludedEvent>(OnCaseConcluded);
            EventBus.Subscribe<GameSavedEvent>(OnSaved);
            EventBus.Subscribe<NotificationEvent>(OnNotification);
            ReadSettings();
        }

        public void Shutdown()
        {
            EventBus.Unsubscribe<UserSettingsChangedEvent>(OnSettings);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<SceneLoadStartedEvent>(OnSceneLoadStarted);
            EventBus.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
            EventBus.Unsubscribe<EvidenceAddedEvent>(OnEvidenceAdded);
            EventBus.Unsubscribe<EvidenceStateChangedEvent>(OnEvidenceChanged);
            EventBus.Unsubscribe<EvidenceInspectRequestedEvent>(OnInspect);
            EventBus.Unsubscribe<FactEstablishedEvent>(OnFact);
            EventBus.Unsubscribe<EvidenceConnectedEvent>(OnConnected);
            EventBus.Unsubscribe<ObjectiveCompletedEvent>(OnObjective);
            EventBus.Unsubscribe<ContradictionEvaluatedEvent>(OnContradiction);
            EventBus.Unsubscribe<InterviewStartedEvent>(OnInterviewStarted);
            EventBus.Unsubscribe<SuspectComposureChangedEvent>(OnComposure);
            EventBus.Unsubscribe<CaseConcludedEvent>(OnCaseConcluded);
            EventBus.Unsubscribe<GameSavedEvent>(OnSaved);
            EventBus.Unsubscribe<NotificationEvent>(OnNotification);
        }

        // ------------------------------------------------------------------ effects

        /// <summary>
        /// Plays a sound effect. <paramref name="position"/> places it in the world (for cues marked spatial).
        /// Returns false when it was skipped (no clip, on cooldown, too many copies, or no free voice).
        /// </summary>
        public bool Play(SoundCue cue, Vector3? position = null, float volumeScale = 1f)
        {
            if (cue == SoundCue.None || !_cues.TryGetValue(cue, out SoundCueEntry e) || e.clips == null || e.clips.Length == 0) return false;

            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(cue, out float last) && now - last < e.cooldown) return false;

            int copies = 0;
            foreach (Voice v in _voices) if (v.Busy && v.Cue == cue) copies++;
            if (copies >= e.maxInstances) return false;

            Voice voice = FreeVoice(e.priority);
            if (voice == null) return false;

            AudioClip clip = PickClip(cue, e.clips);
            if (clip == null) return false;

            AudioSource s = voice.Source;
            s.Stop();
            s.clip = clip;
            s.volume = Mathf.Clamp01(e.volume * volumeScale) * _effects;
            s.pitch = Random.Range(Mathf.Min(e.pitch.x, e.pitch.y), Mathf.Max(e.pitch.x, e.pitch.y));
            bool spatial = e.spatial && position.HasValue;
            s.spatialBlend = spatial ? 1f : 0f;
            s.transform.position = spatial ? position.Value : transform.position;
            s.Play();
            voice.Cue = cue;
            voice.Priority = e.priority;
            voice.StartedAt = now;
            _lastPlayed[cue] = now;
            return true;
        }

        /// <summary>A free voice, or the oldest one playing something less important than <paramref name="priority"/>.</summary>
        private Voice FreeVoice(int priority)
        {
            Voice steal = null;
            foreach (Voice v in _voices)
            {
                if (!v.Busy) return v;
                if (v.Priority < priority && (steal == null || v.Priority < steal.Priority || (v.Priority == steal.Priority && v.StartedAt < steal.StartedAt)))
                    steal = v;
            }
            return steal;
        }

        private AudioClip PickClip(SoundCue cue, AudioClip[] clips)
        {
            if (clips.Length == 1) return clips[0];
            _lastClip.TryGetValue(cue, out int previous);
            int index = Random.Range(0, clips.Length - 1);
            if (index >= previous) index++; // never the same clip twice in a row
            _lastClip[cue] = index;
            return clips[index];
        }

        /// <summary>Number of effect voices currently playing (for the debug overlay and tests).</summary>
        public int ActiveVoices
        {
            get
            {
                int n = 0;
                foreach (Voice v in _voices) if (v.Busy) n++;
                return n;
            }
        }

        // ------------------------------------------------------------------ music

        /// <summary>Crossfades to the music for <paramref name="state"/> (None = fade out).</summary>
        public void SetMusic(MusicState state)
        {
            if (state == _musicState) return;
            _musicState = state;
            MusicEntry entry = _library != null ? _library.FindMusic(state) : null;

            AudioSource next = _currentMusic == _musicA ? _musicB : _musicA;
            if (entry == null || entry.clip == null)
            {
                _currentMusic = null; // both fade out in Update
                return;
            }
            if (_currentMusic != null && _currentMusic.clip == entry.clip) return;

            next.clip = entry.clip;
            next.volume = 0f;
            next.Play();
            _currentMusic = next;
            _currentMusicVolume = entry.volume;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float crossfade = _config != null ? _config.MusicCrossfadeSeconds : 3f;

            _pauseDuck = Mathf.MoveTowards(_pauseDuck, _pauseDuckTarget, dt * 2f);
            _sceneFade = Mathf.MoveTowards(_sceneFade, _sceneFadeTarget, dt * _sceneFadeSpeed);
            AmbienceGain = _ambience * _sceneFade * Mathf.Lerp(1f, _pauseDuck, 0.8f);

            // Music crossfade: the current track rises to its volume, the other falls and stops.
            float step = dt / Mathf.Max(0.1f, crossfade);
            FadeMusic(_musicA, step);
            FadeMusic(_musicB, step);

            // Tension: quiet, follows composure, a short swell after a contradiction.
            if (_tension != null && _tension.clip != null)
            {
                _tensionSpike = Mathf.MoveTowards(_tensionSpike, 0f, dt / 6f);
                float level = _interrogating ? Mathf.Clamp01(_tensionBase + _tensionSpike) : 0f;
                float target = level * _library.tensionVolume * _music * _pauseDuck;
                _tension.volume = Mathf.MoveTowards(_tension.volume, target, dt * 0.25f);
                if (target > 0f && !_tension.isPlaying) _tension.Play();
                else if (target <= 0f && _tension.volume <= 0.0001f && _tension.isPlaying) _tension.Stop();
            }
        }

        private void FadeMusic(AudioSource s, float step)
        {
            if (s == null) return;
            float target = s == _currentMusic ? _currentMusicVolume * _music * _pauseDuck : 0f;
            s.volume = Mathf.MoveTowards(s.volume, target, step);
            if (s != _currentMusic && s.isPlaying && s.volume <= 0.0001f) s.Stop();
        }

        // ------------------------------------------------------------------ settings and state

        private void OnSettings(UserSettingsChangedEvent evt) => ReadSettings();

        private void ReadSettings()
        {
            if (!Services.TryGet(out ConfigService config) || config.User == null) return;
            _music = config.User.musicVolume;
            _effects = config.User.effectsVolume;
            _ambience = config.User.ambienceVolume;
            foreach (Voice v in _voices)
                if (v.Busy && _cues.TryGetValue(v.Cue, out SoundCueEntry e)) v.Source.volume = e.volume * _effects;
        }

        private void OnStateChanged(GameStateChangedEvent evt)
        {
            _pauseDuckTarget = evt.Current == GameState.Paused ? (_config != null ? _config.PausedVolume : 0.45f) : 1f;
            _interrogating = evt.Current == GameState.Interrogating || (evt.Current == GameState.Paused && evt.Previous == GameState.Interrogating);
            switch (evt.Current)
            {
                case GameState.MainMenu: SetMusic(MusicState.MainMenu); break;
                case GameState.Briefing:
                case GameState.Investigating: SetMusic(MusicState.Investigation); break;
                case GameState.Interrogating: SetMusic(MusicState.Interrogation); break;
                case GameState.Ending: SetMusic(MusicState.Ending); break;
            }
        }

        private void OnSceneLoadStarted(SceneLoadStartedEvent evt)
        {
            float fade = _config != null ? _config.SceneFadeOutSeconds : 0.4f;
            _sceneFadeTarget = 0f;
            _sceneFadeSpeed = 1f / Mathf.Max(0.05f, fade);
        }

        private void OnSceneLoaded(SceneLoadedEvent evt)
        {
            _sceneFadeTarget = 1f;
            _sceneFadeSpeed = 1f / 1.2f; // ambience comes back gently with the picture
        }

        // ------------------------------------------------------------------ game events → sounds

        private void OnEvidenceAdded(EvidenceAddedEvent evt) => Play(SoundCue.EvidenceCollected);

        private void OnEvidenceChanged(EvidenceStateChangedEvent evt)
        {
            if (evt.Change == EvidenceChange.Examined) Play(SoundCue.EvidenceExamined);
        }

        private void OnInspect(EvidenceInspectRequestedEvent evt) => Play(evt.FromWorld ? SoundCue.EvidenceDiscovered : SoundCue.PageTurn);

        private void OnFact(FactEstablishedEvent evt) => Play(SoundCue.FactConfirmed);

        private void OnConnected(EvidenceConnectedEvent evt) => Play(SoundCue.Connection);

        private void OnObjective(ObjectiveCompletedEvent evt) => Play(SoundCue.ObjectiveComplete);

        private void OnContradiction(ContradictionEvaluatedEvent evt)
        {
            if (!evt.InInterview || evt.Result == null) return; // notebook comparisons stay quiet
            switch (evt.Result.Outcome)
            {
                case ContradictionOutcome.Valid:
                    Play(SoundCue.ContradictionExposed);
                    _tensionSpike = 0.6f;
                    break;
                case ContradictionOutcome.Unproven: Play(SoundCue.ChallengeUnproven); break;
                case ContradictionOutcome.Invalid: Play(SoundCue.ChallengeFailed); break;
            }
        }

        private void OnInterviewStarted(InterviewStartedEvent evt)
        {
            _tensionBase = 0.15f;
            if (evt.Suspect != null) OnComposure(new SuspectComposureChangedEvent(evt.Suspect.baseComposure));
        }

        private void OnComposure(SuspectComposureChangedEvent evt)
        {
            // Calm suspect: barely there. Rattled suspect: clearly present, never loud.
            _tensionBase = Mathf.Lerp(0.15f, 0.85f, Mathf.InverseLerp(75f, 15f, evt.Composure));
        }

        private void OnCaseConcluded(CaseConcludedEvent evt) => Play(SoundCue.CaseClosed);

        private void OnSaved(GameSavedEvent evt)
        {
            if (evt.Slot >= 0) Play(SoundCue.GameSaved); // autosaves stay silent
        }

        private void OnNotification(NotificationEvent evt) => Play(SoundCue.Notification);
    }
}
