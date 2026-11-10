using System;
using NUnit.Framework;
using TLA.Data.Audio;
using TLA.Data.Config;
using UnityEditor;
using UnityEngine;

namespace TLA.Tests
{
    /// <summary>The audio data: every cue and mood has a clip, settings are sane, imports are cheap to play.</summary>
    public class AudioLibraryTests
    {
        private const string LibraryPath = "Assets/_Project/Data/Settings/AudioLibrary.asset";

        private AudioLibrary Library()
        {
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            Assert.IsNotNull(lib, "AudioLibrary missing: run The Last Answer → Audio → Generate Placeholder Sounds");
            return lib;
        }

        [Test]
        public void EverySoundCue_HasAtLeastOneClip()
        {
            AudioLibrary lib = Library();
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                if (cue == SoundCue.None) continue;
                SoundCueEntry e = lib.Find(cue);
                Assert.IsNotNull(e, $"{cue} has no entry");
                Assert.IsTrue(e.clips != null && e.clips.Length > 0 && Array.TrueForAll(e.clips, c => c != null), $"{cue} has no clip");
                Assert.That(e.volume, Is.InRange(0.05f, 1f), cue.ToString());
                Assert.That(e.maxInstances, Is.GreaterThanOrEqualTo(1), cue.ToString());
            }
        }

        [Test]
        public void EveryMusicMood_AndTheTensionLayer_HaveClips()
        {
            AudioLibrary lib = Library();
            foreach (MusicState state in Enum.GetValues(typeof(MusicState)))
            {
                if (state == MusicState.None) continue;
                Assert.IsNotNull(lib.FindMusic(state)?.clip, $"{state} has no music");
            }
            Assert.IsNotNull(lib.tensionLoop);
        }

        [Test]
        public void Footsteps_HaveVariations_AndStingsAreNotLoud()
        {
            AudioLibrary lib = Library();
            Assert.GreaterOrEqual(lib.Find(SoundCue.Footstep).clips.Length, 3, "several footstep variations avoid repetition");
            Assert.LessOrEqual(lib.Find(SoundCue.ContradictionExposed).volume, 0.8f, "no jump scares");
            Assert.LessOrEqual(lib.Find(SoundCue.UIHover).volume, 0.4f, "hover ticks stay subtle");
            Assert.Greater(lib.Find(SoundCue.FactConfirmed).cooldown, 0.3f, "several facts at once make one chime, not a burst");
        }

        [Test]
        public void GameConfig_UsesTheLibrary()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Settings/GameConfig.asset");
            Assert.AreEqual(Library(), config.Audio.Library);
            Assert.Greater(config.Audio.MaxSimultaneousEffects, 3);
        }

        [Test]
        public void PlaceholderMusic_IsStreamed_AndEffectsAreDecompressed()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project/Audio/Placeholder" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                AudioClipLoadType expected = path.Contains("/Music/") ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                Assert.AreEqual(expected, importer.defaultSampleSettings.loadType, path);
            }
        }

        [Test]
        public void UserSettings_ClampAudioVolumes()
        {
            var s = new UserSettings { musicVolume = 3f, effectsVolume = -1f, ambienceVolume = float.NaN };
            s.Sanitize();
            Assert.AreEqual(1f, s.musicVolume);
            Assert.AreEqual(0f, s.effectsVolume);
            Assert.AreEqual(0.8f, s.ambienceVolume, 0.0001f);
        }
    }
}
