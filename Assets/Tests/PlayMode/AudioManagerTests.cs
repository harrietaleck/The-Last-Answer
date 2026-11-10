using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TLA.Core.Events;
using TLA.Data.Audio;
using TLA.Data.Config;
using TLA.Systems.Audio;
using UnityEngine;
using UnityEngine.TestTools;

namespace TLA.Tests
{
    /// <summary>
    /// The AudioManager's playback rules with real AudioSources: cooldowns, per-cue copy limits, the
    /// voice cap with priority stealing, missing clips, and music crossfades.
    /// </summary>
    public class AudioManagerTests
    {
        private GameObject _go;
        private AudioManager _audio;
        private AudioLibrary _library;
        private AudioClip _clip;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _clip = AudioClip.Create("test_tone", 44100 * 2, 1, 44100, false); // 2 s of silence: long enough to stay "playing"
            _library = ScriptableObject.CreateInstance<AudioLibrary>();
            _library.cues.Add(Entry(SoundCue.UIClick, cooldown: 0.5f, max: 4, priority: 30));
            _library.cues.Add(Entry(SoundCue.Footstep, cooldown: 0f, max: 2, priority: 20));
            _library.cues.Add(Entry(SoundCue.UIHover, cooldown: 0f, max: 50, priority: 10));
            _library.cues.Add(Entry(SoundCue.ContradictionExposed, cooldown: 0f, max: 1, priority: 90));
            _library.cues.Add(new SoundCueEntry { cue = SoundCue.DoorOpen, clips = new AudioClip[0] }); // no clip assigned
            _library.music.Add(new MusicEntry { state = MusicState.MainMenu, clip = _clip, volume = 0.5f });
            _library.music.Add(new MusicEntry { state = MusicState.Interrogation, clip = AudioClip.Create("music_b", 44100, 1, 44100, false), volume = 0.5f });

            var config = new AudioConfig();
            typeof(AudioConfig).GetField("library", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, _library);
            typeof(AudioConfig).GetField("maxSimultaneousEffects", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, 6);

            _go = new GameObject("AudioManagerTest");
            _go.AddComponent<AudioListener>();
            _audio = _go.AddComponent<AudioManager>();
            _audio.Configure(config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_go);
            Object.Destroy(_library);
            EventBus.Clear();
        }

        private SoundCueEntry Entry(SoundCue cue, float cooldown, int max, int priority) =>
            new SoundCueEntry { cue = cue, clips = new[] { _clip }, volume = 0.5f, cooldown = cooldown, maxInstances = max, priority = priority };

        [UnityTest]
        public IEnumerator Cooldown_StopsImmediateRepeats()
        {
            Assert.IsTrue(_audio.Play(SoundCue.UIClick));
            Assert.IsFalse(_audio.Play(SoundCue.UIClick), "within the 0.5 s cooldown");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(_audio.Play(SoundCue.UIClick));
        }

        [UnityTest]
        public IEnumerator CopiesPerCue_AreLimited()
        {
            yield return null;
            Assert.IsTrue(_audio.Play(SoundCue.Footstep));
            Assert.IsTrue(_audio.Play(SoundCue.Footstep));
            Assert.IsFalse(_audio.Play(SoundCue.Footstep), "max 2 footsteps at once");
        }

        [UnityTest]
        public IEnumerator VoiceCap_IsNeverExceeded_AndImportantSoundsStillPlay()
        {
            yield return null;
            int played = 0;
            for (int i = 0; i < 20; i++) if (_audio.Play(SoundCue.UIHover)) played++;
            Assert.AreEqual(6, played, "only 6 voices exist");
            Assert.AreEqual(6, _audio.ActiveVoices);

            Assert.IsTrue(_audio.Play(SoundCue.ContradictionExposed), "a high-priority sting replaces a low-priority hover");
            Assert.AreEqual(6, _audio.ActiveVoices, "still never more than the cap");
            Assert.IsFalse(_audio.Play(SoundCue.UIHover), "a low-priority sound can't steal a voice");
        }

        [UnityTest]
        public IEnumerator MissingClipsOrUnknownCues_AreSkippedQuietly()
        {
            yield return null;
            Assert.IsFalse(_audio.Play(SoundCue.DoorOpen), "no clip assigned");
            Assert.IsFalse(_audio.Play(SoundCue.CaseClosed), "no entry at all");
            Assert.IsFalse(_audio.Play(SoundCue.None));
            Assert.AreEqual(0, _audio.ActiveVoices);
        }

        [UnityTest]
        public IEnumerator Music_SwitchesMood_WithACrossfade()
        {
            _audio.SetMusic(MusicState.MainMenu);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(MusicState.MainMenu, _audio.CurrentMusic);
            _audio.SetMusic(MusicState.Interrogation);
            Assert.AreEqual(MusicState.Interrogation, _audio.CurrentMusic);

            // Both tracks play during the crossfade, then only the new one.
            int playing = 0;
            foreach (AudioSource s in _go.GetComponents<AudioSource>()) if (s.isPlaying && s.loop && s.clip != null) playing++;
            Assert.AreEqual(2, playing, "old and new music overlap while crossfading");
        }
    }
}
