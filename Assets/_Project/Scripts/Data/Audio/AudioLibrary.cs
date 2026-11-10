using System;
using System.Collections.Generic;
using UnityEngine;

namespace TLA.Data.Audio
{
    /// <summary>Every sound effect the game asks for. Code plays a cue; the AudioLibrary decides which clip.</summary>
    public enum SoundCue
    {
        None = 0,
        // UI
        UIHover = 1, UIClick = 2, UIOpen = 3, UIClose = 4, UISlider = 5, PageTurn = 6, Notification = 7,
        // World
        Footstep = 20, DoorOpen = 21, DoorClose = 22, Interact = 23, ChairScrape = 24,
        // Investigation
        EvidenceDiscovered = 40, EvidenceCollected = 41, EvidenceExamined = 42, FactConfirmed = 43,
        Connection = 44, ObjectiveComplete = 45, EvidencePresented = 46,
        // Interrogation
        ContradictionExposed = 60, ChallengeFailed = 61, ChallengeUnproven = 62,
        // Case
        CaseClosed = 80, GameSaved = 81
    }

    /// <summary>Background music moods. The AudioManager picks one from the game state.</summary>
    public enum MusicState
    {
        None = 0,
        MainMenu = 1,
        Investigation = 2,
        Interrogation = 3,
        Ending = 4
    }

    /// <summary>How one cue sounds and how often it may play.</summary>
    [Serializable]
    public sealed class SoundCueEntry
    {
        public SoundCue cue;

        [Tooltip("One is picked at random each time (never the same twice in a row when there are several).")]
        public AudioClip[] clips = Array.Empty<AudioClip>();

        [Range(0f, 1f)] public float volume = 0.7f;

        [Tooltip("Random pitch range for variety, e.g. 0.95-1.05. (1,1) = no variation.")]
        public Vector2 pitch = new Vector2(1f, 1f);

        [Tooltip("Minimum seconds between two plays of this cue (stops machine-gun repeats).")]
        [Min(0f)] public float cooldown = 0.05f;

        [Tooltip("At most this many copies at once.")]
        [Min(1)] public int maxInstances = 2;

        [Tooltip("When every voice is busy, higher priority sounds replace lower ones.")]
        [Range(0, 100)] public int priority = 50;

        [Tooltip("Played at a position in the world (doors) rather than flat in both ears (UI).")]
        public bool spatial;
    }

    [Serializable]
    public sealed class MusicEntry
    {
        public MusicState state;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.45f;
    }

    /// <summary>
    /// THE place to put your own sound assets. Each cue and music mood lists its clips; replace the
    /// generated placeholders (Assets/_Project/Audio/Placeholder) with your files by dragging them in.
    /// No code changes needed. Data/Settings/AudioLibrary.asset, referenced by GameConfig → Audio.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "The Last Answer/Audio/Audio Library", order = 50)]
    public sealed class AudioLibrary : ScriptableObject
    {
        public List<SoundCueEntry> cues = new List<SoundCueEntry>();
        public List<MusicEntry> music = new List<MusicEntry>();

        [Header("Tension layer (interrogations)")]
        [Tooltip("A quiet loop that rises as the suspect's composure falls, and briefly after a contradiction.")]
        public AudioClip tensionLoop;
        [Range(0f, 1f)] public float tensionVolume = 0.3f;

        public SoundCueEntry Find(SoundCue cue) => cues.Find(c => c != null && c.cue == cue);
        public MusicEntry FindMusic(MusicState state) => music.Find(m => m != null && m.state == state);
    }
}
