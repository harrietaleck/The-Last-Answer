using TLA.Core.Services;
using TLA.Data.Audio;
using TLA.Systems.Audio;

namespace TLA.UI.Components
{
    /// <summary>Shortcut for UI code: play a UI sound through the AudioManager (silent if it isn't running).</summary>
    public static class UISound
    {
        public static void Play(SoundCue cue)
        {
            if (cue != SoundCue.None && Services.TryGet(out AudioManager audio)) audio.Play(cue);
        }
    }
}
