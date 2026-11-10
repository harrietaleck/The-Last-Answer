using System;
using System.Collections.Generic;
using System.IO;
using TLA.Data.Audio;
using TLA.Data.Config;
using UnityEditor;
using UnityEngine;

namespace TLA.EditorTools.Audio
{
    /// <summary>
    /// Makes the game's PLACEHOLDER audio by synthesising it (no downloads, no paid assets):
    ///   Assets/_Project/Audio/Placeholder/UI     clicks, ticks, page turns, chimes
    ///   Assets/_Project/Audio/Placeholder/SFX    footsteps, doors, chair, evidence, contradiction stings
    ///   Assets/_Project/Audio/Placeholder/Music  slow ambient pads for each mood, plus a tension drone
    /// and the AudioLibrary (Data/Settings/AudioLibrary.asset) that maps every cue to them.
    ///
    /// Your own sounds: drag them into the AudioLibrary entries. The generator only fills entries
    /// that are EMPTY, so it never replaces clips you assigned. Menu: The Last Answer → Audio →
    /// Generate Placeholder Sounds (also runs as part of Rebuild All Locations / Rebuild UI).
    /// </summary>
    internal static class PlaceholderAudioGenerator
    {
        public const string Root = "Assets/_Project/Audio/Placeholder";
        public const string LibraryPath = "Assets/_Project/Data/Settings/AudioLibrary.asset";
        private const string GameConfigPath = "Assets/_Project/Data/Settings/GameConfig.asset";
        private const int SfxRate = 44100;
        private const int MusicRate = 22050;

        [MenuItem("The Last Answer/Audio/Generate Placeholder Sounds", priority = 60)]
        public static void GenerateFromMenu()
        {
            AudioLibrary library = EnsureAll(force: true);
            Selection.activeObject = library;
            Debug.Log($"[Audio] Placeholder sounds written to {Root}; AudioLibrary at {LibraryPath}.");
        }

        /// <summary>Creates any missing placeholder files and library entries. <paramref name="force"/> rewrites the WAVs.</summary>
        public static AudioLibrary EnsureAll(bool force = false)
        {
            var clips = new Dictionary<string, AudioClip>();
            foreach (var recipe in Recipes())
                clips[recipe.Key] = WriteClip(recipe.Key, recipe.Value, force);

            AudioLibrary library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            AudioClip C(string name) => clips.TryGetValue(name, out AudioClip c) ? c : null;

            // cue, clips, volume, pitch range, cooldown, max copies, priority, spatial
            Cue(library, SoundCue.UIHover, new[] { C("UI/ui_hover") }, 0.25f, 0.98f, 1.02f, 0.06f, 1, 10, false);
            Cue(library, SoundCue.UIClick, new[] { C("UI/ui_click") }, 0.45f, 0.97f, 1.03f, 0.05f, 2, 30, false);
            Cue(library, SoundCue.UIOpen, new[] { C("UI/ui_open") }, 0.35f, 1f, 1f, 0.1f, 1, 30, false);
            Cue(library, SoundCue.UIClose, new[] { C("UI/ui_close") }, 0.3f, 1f, 1f, 0.1f, 1, 30, false);
            Cue(library, SoundCue.UISlider, new[] { C("UI/ui_tick") }, 0.2f, 0.95f, 1.05f, 0.07f, 1, 10, false);
            Cue(library, SoundCue.PageTurn, new[] { C("UI/page_turn") }, 0.45f, 0.95f, 1.05f, 0.12f, 1, 35, false);
            Cue(library, SoundCue.Notification, new[] { C("UI/notify") }, 0.25f, 1f, 1f, 0.6f, 1, 20, false);
            Cue(library, SoundCue.GameSaved, new[] { C("UI/notify") }, 0.3f, 1.1f, 1.1f, 1f, 1, 25, false);

            Cue(library, SoundCue.Footstep, new[] { C("SFX/footstep_1"), C("SFX/footstep_2"), C("SFX/footstep_3"), C("SFX/footstep_4") }, 0.5f, 0.92f, 1.06f, 0.18f, 2, 20, false);
            Cue(library, SoundCue.DoorOpen, new[] { C("SFX/door_open") }, 0.6f, 0.96f, 1.04f, 0.3f, 1, 60, true);
            Cue(library, SoundCue.DoorClose, new[] { C("SFX/door_close") }, 0.6f, 0.96f, 1.04f, 0.3f, 1, 60, true);
            Cue(library, SoundCue.Interact, new[] { C("SFX/interact") }, 0.4f, 0.95f, 1.05f, 0.1f, 1, 30, true);
            Cue(library, SoundCue.ChairScrape, new[] { C("SFX/chair_scrape") }, 0.45f, 0.95f, 1.05f, 0.5f, 1, 40, true);

            Cue(library, SoundCue.EvidenceDiscovered, new[] { C("SFX/paper_rustle") }, 0.5f, 0.95f, 1.05f, 0.2f, 1, 50, false);
            Cue(library, SoundCue.EvidenceCollected, new[] { C("SFX/evidence_collect") }, 0.45f, 1f, 1f, 0.3f, 1, 60, false);
            Cue(library, SoundCue.EvidenceExamined, new[] { C("SFX/evidence_examine") }, 0.35f, 1f, 1f, 0.3f, 1, 55, false);
            Cue(library, SoundCue.FactConfirmed, new[] { C("SFX/fact_confirmed") }, 0.3f, 1f, 1f, 0.8f, 1, 45, false); // several facts at once → one chime
            Cue(library, SoundCue.Connection, new[] { C("SFX/connection") }, 0.45f, 1f, 1f, 0.5f, 1, 65, false);
            Cue(library, SoundCue.ObjectiveComplete, new[] { C("SFX/objective") }, 0.4f, 1f, 1f, 1.2f, 1, 70, false);
            Cue(library, SoundCue.EvidencePresented, new[] { C("SFX/paper_slide") }, 0.45f, 0.97f, 1.03f, 0.3f, 1, 55, false);

            Cue(library, SoundCue.ContradictionExposed, new[] { C("SFX/contradiction_sting") }, 0.55f, 1f, 1f, 1f, 1, 90, false);
            Cue(library, SoundCue.ChallengeFailed, new[] { C("SFX/challenge_failed") }, 0.4f, 1f, 1f, 0.8f, 1, 80, false);
            Cue(library, SoundCue.ChallengeUnproven, new[] { C("SFX/challenge_unproven") }, 0.4f, 1f, 1f, 0.8f, 1, 80, false);
            Cue(library, SoundCue.CaseClosed, new[] { C("SFX/case_closed") }, 0.55f, 1f, 1f, 2f, 1, 100, false);

            Music(library, MusicState.MainMenu, C("Music/music_menu"), 0.45f);
            Music(library, MusicState.Investigation, C("Music/music_investigation"), 0.3f);
            Music(library, MusicState.Interrogation, C("Music/music_interrogation"), 0.32f);
            Music(library, MusicState.Ending, C("Music/music_ending"), 0.42f);
            if (library.tensionLoop == null) library.tensionLoop = C("Music/tension_drone");

            EditorUtility.SetDirty(library);
            AssignToGameConfig(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        // ------------------------------------------------------------------ library helpers

        private static void Cue(AudioLibrary lib, SoundCue cue, AudioClip[] clips, float volume, float pitchMin, float pitchMax,
            float cooldown, int maxInstances, int priority, bool spatial)
        {
            SoundCueEntry e = lib.Find(cue);
            if (e == null)
            {
                e = new SoundCueEntry { cue = cue, volume = volume, pitch = new Vector2(pitchMin, pitchMax), cooldown = cooldown, maxInstances = maxInstances, priority = priority, spatial = spatial };
                lib.cues.Add(e);
            }
            if (e.clips == null || e.clips.Length == 0 || Array.TrueForAll(e.clips, c => c == null))
                e.clips = Array.FindAll(clips, c => c != null); // only fill empty entries: your own clips are never replaced
        }

        private static void Music(AudioLibrary lib, MusicState state, AudioClip clip, float volume)
        {
            MusicEntry e = lib.FindMusic(state);
            if (e == null)
            {
                e = new MusicEntry { state = state, volume = volume };
                lib.music.Add(e);
            }
            if (e.clip == null) e.clip = clip;
        }

        private static void AssignToGameConfig(AudioLibrary library)
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null) return;
            var so = new SerializedObject(config);
            SerializedProperty p = so.FindProperty("audio.library");
            if (p != null && p.objectReferenceValue == null)
            {
                p.objectReferenceValue = library;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ------------------------------------------------------------------ files

        private static AudioClip WriteClip(string name, Func<float[]> render, bool force)
        {
            string path = $"{Root}/{name}.wav";
            bool music = name.StartsWith("Music/");
            if (force || !File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                float[] samples = render();
                WriteWav(path, samples, music ? MusicRate : SfxRate);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                ConfigureImporter(path, music);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>Import settings tuned for playback cost: short effects decompressed in memory, music streamed.</summary>
        private static void ConfigureImporter(string path, bool music)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;
            importer.forceToMono = true;
            importer.loadInBackground = music;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            settings.quality = music ? 0.6f : 1f;
            settings.preloadAudioData = !music;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private static void WriteWav(string path, float[] samples, int rate)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);   // PCM
                w.Write((short)1);   // mono
                w.Write(rate);
                w.Write(rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                foreach (float s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
            }
        }

        // ------------------------------------------------------------------ recipes

        private static IEnumerable<KeyValuePair<string, Func<float[]>>> Recipes()
        {
            KeyValuePair<string, Func<float[]>> R(string n, Func<float[]> f) => new KeyValuePair<string, Func<float[]>>(n, f);

            // UI: short, soft, never shrill.
            yield return R("UI/ui_hover", () => Finish(Tone(0.03f, 1400f, 0.002f, 0.02f), 0.35f));
            yield return R("UI/ui_click", () => Finish(Mix(Tone(0.06f, 950f, 0.001f, 0.035f), Noise(0.06f, 0.002f, 0.012f, 0.25f, 1), 0.35f), 0.55f));
            yield return R("UI/ui_open", () => Finish(Sweep(0.18f, 420f, 640f, 0.01f, 0.14f), 0.4f));
            yield return R("UI/ui_close", () => Finish(Sweep(0.16f, 600f, 380f, 0.005f, 0.12f), 0.35f));
            yield return R("UI/ui_tick", () => Finish(Tone(0.025f, 1800f, 0.001f, 0.012f), 0.25f));
            yield return R("UI/page_turn", () => Finish(Lowpass(Noise(0.32f, 0.04f, 0.2f, 1f, 7), 0.25f), 0.5f));
            yield return R("UI/notify", () => Finish(Chime(new[] { 880f }, 0.6f, 0.45f), 0.35f));

            // Footsteps: muffled heel thumps on a hard floor, four variations.
            for (int i = 1; i <= 4; i++)
            {
                int seed = i;
                float body = 85f + i * 9f;
                yield return R($"SFX/footstep_{i}", () => Finish(Mix(Tone(0.16f, body, 0.002f, 0.05f), Lowpass(Noise(0.16f, 0.001f, 0.04f, 1f, seed * 13), 0.18f), 0.6f), 0.7f));
            }

            yield return R("SFX/door_open", () => Finish(Concat(Mix(Click(0.05f, 3), Tone(0.05f, 220f, 0.001f, 0.03f), 0.5f), Creak(0.75f, 170f, 240f, 21)), 0.65f));
            yield return R("SFX/door_close", () => Finish(Concat(Creak(0.35f, 230f, 180f, 22), Mix(Tone(0.3f, 70f, 0.002f, 0.12f), Lowpass(Noise(0.3f, 0.001f, 0.08f, 1f, 23), 0.1f), 0.8f), Click(0.06f, 24)), 0.75f));
            yield return R("SFX/interact", () => Finish(Mix(Click(0.08f, 31), Tone(0.08f, 520f, 0.001f, 0.04f), 0.4f), 0.5f));
            yield return R("SFX/chair_scrape", () => Finish(Creak(0.55f, 120f, 95f, 41), 0.5f));
            yield return R("SFX/paper_rustle", () => Finish(Lowpass(Noise(0.45f, 0.03f, 0.3f, 1f, 51), 0.3f), 0.5f));
            yield return R("SFX/paper_slide", () => Finish(Lowpass(Noise(0.4f, 0.12f, 0.22f, 1f, 52), 0.12f), 0.55f));
            yield return R("SFX/evidence_collect", () => Finish(Chime(new[] { 659.3f, 987.8f }, 1.1f, 0.18f), 0.45f));
            yield return R("SFX/evidence_examine", () => Finish(Chime(new[] { 440f }, 0.9f, 0f), 0.35f));
            yield return R("SFX/fact_confirmed", () => Finish(Chime(new[] { 1318.5f }, 0.8f, 0f), 0.25f));
            yield return R("SFX/connection", () => Finish(Chime(new[] { 587.3f, 880f }, 1.2f, 0.16f), 0.45f));
            yield return R("SFX/objective", () => Finish(Chime(new[] { 523.3f, 659.3f, 784f }, 1.6f, 0.16f), 0.45f));
            yield return R("SFX/contradiction_sting", () => Finish(Mix(Tone(1.8f, 55f, 0.005f, 0.5f), Pad(1.8f, new[] { 220f, 233.1f, 311.1f }, 0.8f, 1.2f, SfxRate), 0.9f), 0.7f));
            yield return R("SFX/challenge_failed", () => Finish(Mix(Tone(0.5f, 65f, 0.003f, 0.18f), Lowpass(Noise(0.5f, 0.002f, 0.1f, 1f, 61), 0.08f), 0.5f), 0.5f));
            yield return R("SFX/challenge_unproven", () => Finish(Tremolo(Pad(1.1f, new[] { 146.8f, 155.6f }, 0.5f, 0.7f, SfxRate), 6f), 0.4f));
            yield return R("SFX/case_closed", () => Finish(Piano(new[] { 87.3f, 130.8f, 174.6f, 220f, 261.6f }, 3.5f), 0.6f));

            // Music: slow, quiet, seamless loops (rendered with a crossfaded loop point).
            yield return R("Music/music_menu", () => Loop(24f, MusicRate, n => Mix(Pad(n, new[] { 110f, 164.8f, 220f, 261.6f }, 3f, 0f, MusicRate, true), Lowpass(Noise(n, 0f, 0f, 0.25f, 71, MusicRate), 0.01f), 0.4f), 0.5f));
            yield return R("Music/music_investigation", () => Loop(24f, MusicRate, n => Mix(Pad(n, new[] { 73.4f, 110f, 174.6f, 293.7f }, 3f, 0f, MusicRate, true), Plucks(n, new[] { 293.7f, 349.2f, 440f, 392f, 349.2f, 261.6f }, 4f, MusicRate), 0.35f), 0.45f));
            yield return R("Music/music_interrogation", () => Loop(19.2f, MusicRate, n => Mix(Pad(n, new[] { 36.7f, 55f, 73.4f }, 2f, 0f, MusicRate, true), Heartbeat(n, 1.6f, MusicRate), 0.55f), 0.5f));
            yield return R("Music/tension_drone", () => Loop(20f, MusicRate, n => Mix(Pad(n, new[] { 880f, 932.3f, 1318.5f }, 4f, 0f, MusicRate, true), Lowpass(Noise(n, 0f, 0f, 0.3f, 81, MusicRate), 0.05f), 0.5f), 0.35f));
            yield return R("Music/music_ending", () => Loop(20f, MusicRate, n => Pad(n, new[] { 87.3f, 130.8f, 220f, 349.2f }, 4f, 0f, MusicRate, true), 0.45f));
        }

        // ------------------------------------------------------------------ synthesis primitives

        private static float[] Buf(float seconds, int rate = SfxRate) => new float[Mathf.Max(1, (int)(seconds * rate))];

        /// <summary>Sine with a fast attack and exponential decay.</summary>
        private static float[] Tone(float seconds, float freq, float attack, float decay)
        {
            float[] b = Buf(seconds);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SfxRate;
                b[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Env(t, attack, decay);
            }
            return b;
        }

        private static float[] Sweep(float seconds, float from, float to, float attack, float decay)
        {
            float[] b = Buf(seconds);
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SfxRate;
                float f = Mathf.Lerp(from, to, t / seconds);
                phase += 2.0 * Math.PI * f / SfxRate;
                b[i] = (float)Math.Sin(phase) * Env(t, attack, decay);
            }
            return b;
        }

        private static float[] Noise(float seconds, float attack, float decay, float level, int seed, int rate = SfxRate)
        {
            var rng = new System.Random(seed);
            float[] b = Buf(seconds, rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float env = decay > 0f ? Env(t, attack, decay) : 1f;
                b[i] = (float)(rng.NextDouble() * 2 - 1) * env * level;
            }
            return b;
        }

        private static float[] Click(float seconds, int seed) => Lowpass(Noise(seconds, 0.0005f, 0.006f, 1f, seed), 0.5f);

        /// <summary>Wood creak: a low tone with wobbling pitch, rough with filtered noise.</summary>
        private static float[] Creak(float seconds, float from, float to, int seed)
        {
            var rng = new System.Random(seed);
            float[] b = Buf(seconds);
            double phase = 0;
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SfxRate;
                float f = Mathf.Lerp(from, to, t / seconds) * (1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 9f * t));
                phase += 2.0 * Math.PI * f / SfxRate;
                float saw = (float)((phase / (2 * Math.PI)) % 1.0) * 2f - 1f;
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.08f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / seconds));
                b[i] = (saw * 0.35f + lp * 0.8f) * env;
            }
            return Lowpass(b, 0.15f);
        }

        /// <summary>Soft bell: sine partials with long decays, notes staggered by <paramref name="stagger"/> seconds.</summary>
        private static float[] Chime(float[] notes, float seconds, float stagger)
        {
            float[] b = Buf(seconds + stagger * notes.Length);
            for (int n = 0; n < notes.Length; n++)
            {
                int offset = (int)(n * stagger * SfxRate);
                for (int i = offset; i < b.Length; i++)
                {
                    float t = (i - offset) / (float)SfxRate;
                    float env = Env(t, 0.004f, seconds * 0.35f);
                    float f = notes[n];
                    b[i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t) * Env(t, 0.002f, seconds * 0.12f)) * env / notes.Length;
                }
            }
            return b;
        }

        /// <summary>Piano-like chord: decaying harmonics, notes slightly rolled.</summary>
        private static float[] Piano(float[] notes, float seconds)
        {
            float[] b = Buf(seconds);
            for (int n = 0; n < notes.Length; n++)
            {
                int offset = (int)(n * 0.045f * SfxRate);
                for (int i = offset; i < b.Length; i++)
                {
                    float t = (i - offset) / (float)SfxRate;
                    float f = notes[n];
                    float v = 0f;
                    for (int h = 1; h <= 4; h++) v += Mathf.Sin(2f * Mathf.PI * f * h * t) * Env(t, 0.003f, seconds * 0.4f / h) / h;
                    b[i] += v / notes.Length;
                }
            }
            return b;
        }

        /// <summary>Sustained chord with slowly breathing voices. <paramref name="swell"/> = seconds per breath; 0 release = sustained.</summary>
        private static float[] Pad(float seconds, float[] notes, float swell, float release, int rate, bool sustained = false)
        {
            float[] b = Buf(seconds, rate);
            for (int n = 0; n < notes.Length; n++)
            {
                float f = notes[n];
                float rate1 = 1f / (swell * (1.3f + n * 0.37f));
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)rate;
                    float breathe = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * rate1 * t + n);
                    float env = sustained ? 1f : Mathf.Clamp01(t / 0.05f) * (release > 0f ? Mathf.Exp(-t / release) : 1f);
                    float v = Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * f * 1.003f * t + 1f); // detuned pair = warmth
                    b[i] += v * breathe * env / notes.Length;
                }
            }
            return b;
        }

        private static float[] Plucks(float seconds, float[] notes, float every, int rate)
        {
            float[] b = Buf(seconds, rate);
            int count = Mathf.FloorToInt(seconds / every);
            for (int k = 0; k < count; k++)
            {
                float f = notes[k % notes.Length];
                int start = (int)((k * every + 0.7f) * rate);
                for (int i = start; i < b.Length; i++)
                {
                    float t = (i - start) / (float)rate;
                    if (t > 3f) break;
                    b[i] += Mathf.Sin(2f * Mathf.PI * f * t) * Env(t, 0.01f, 0.9f) * 0.5f;
                }
            }
            return b;
        }

        private static float[] Heartbeat(float seconds, float every, int rate)
        {
            float[] b = Buf(seconds, rate);
            int beats = Mathf.FloorToInt(seconds / every);
            for (int k = 0; k < beats; k++)
                foreach (float offsetSec in new[] { 0f, 0.28f })
                {
                    int start = (int)((k * every + offsetSec) * rate);
                    float level = offsetSec == 0f ? 1f : 0.6f;
                    for (int i = start; i < b.Length && i < start + rate / 4; i++)
                    {
                        float t = (i - start) / (float)rate;
                        b[i] += Mathf.Sin(2f * Mathf.PI * 52f * t) * Env(t, 0.004f, 0.07f) * level;
                    }
                }
            return b;
        }

        private static float[] Tremolo(float[] b, float hz)
        {
            for (int i = 0; i < b.Length; i++) b[i] *= 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * hz * i / SfxRate);
            return b;
        }

        private static float[] Lowpass(float[] b, float amount)
        {
            float y = 0;
            for (int i = 0; i < b.Length; i++)
            {
                y += (b[i] - y) * amount;
                b[i] = y;
            }
            return b;
        }

        private static float[] Mix(float[] a, float[] b, float bLevel)
        {
            float[] o = new float[Mathf.Max(a.Length, b.Length)];
            for (int i = 0; i < o.Length; i++) o[i] = (i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] * bLevel : 0f);
            return o;
        }

        private static float[] Concat(params float[][] parts)
        {
            var list = new List<float>();
            foreach (float[] p in parts) list.AddRange(p);
            return list.ToArray();
        }

        private static float Env(float t, float attack, float decay) =>
            (attack > 0f ? Mathf.Clamp01(t / attack) : 1f) * (decay > 0f ? Mathf.Exp(-t / decay) : 1f);

        /// <summary>Normalises to <paramref name="peak"/> and adds 4 ms fades so nothing clicks.</summary>
        private static float[] Finish(float[] b, float peak)
        {
            Normalize(b, peak);
            int fade = Mathf.Min(b.Length / 2, SfxRate / 250);
            for (int i = 0; i < fade; i++)
            {
                float g = i / (float)fade;
                b[i] *= g;
                b[b.Length - 1 - i] *= g;
            }
            return b;
        }

        /// <summary>Renders <paramref name="seconds"/> plus a tail and crossfades the tail into the start: a seamless loop.</summary>
        private static float[] Loop(float seconds, int rate, Func<float, float[]> render, float peak)
        {
            const float overlap = 2f;
            float[] full = render(seconds + overlap);
            int n = (int)(seconds * rate);
            int x = (int)(overlap * rate);
            float[] o = new float[n];
            Array.Copy(full, o, n);
            for (int i = 0; i < x && i < n && n + i < full.Length; i++)
            {
                float g = i / (float)x;
                o[i] = full[i] * g + full[n + i] * (1f - g);
            }
            Normalize(o, peak);
            return o;
        }

        private static void Normalize(float[] b, float peak)
        {
            float max = 0.0001f;
            foreach (float s in b) max = Mathf.Max(max, Mathf.Abs(s));
            float g = peak / max;
            for (int i = 0; i < b.Length; i++) b[i] *= g;
        }
    }
}
