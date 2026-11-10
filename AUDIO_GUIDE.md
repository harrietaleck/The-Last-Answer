# Audio and atmosphere: guide

How sound, music and the cinematic transitions work in THE LAST ANSWER, and **where to put your own sound files**.

## Contents

1. [Adding your own sounds](#1-adding-your-own-sounds)
2. [What plays when](#2-what-plays-when)
3. [Music and tension](#3-music-and-tension)
4. [Volume settings](#4-volume-settings)
5. [Transitions](#5-transitions)
6. [Performance](#6-performance)
7. [The placeholder sounds](#7-the-placeholder-sounds)
8. [For programmers](#8-for-programmers)
9. [Testing](#9-testing)
10. [Known limits](#10-known-limits)

---

## 1. Adding your own sounds

Everything is in one asset: **`Assets/_Project/Data/Settings/AudioLibrary.asset`**.

1. Put your files in `Assets/_Project/Audio/SFX`, `Audio/Music` (or `Audio/Voice` later). WAV and OGG both work.
2. Select **AudioLibrary**. Find the cue (e.g. *DoorOpen*) or the music mood (e.g. *Interrogation*), and drag your clip into its **Clips** list or **Clip** field, replacing the placeholder.
   - A cue can have **several clips**. One is picked at random, never the same one twice in a row; good for footsteps.
   - **Volume, pitch range, cooldown, max copies, priority** and **spatial** (played at the door's position) are set per cue.
3. Done. No code changes.

**Recommended import settings:**
- **Music:** *Load Type* Streaming, *Compression* Vorbis.
- **Short effects:** *Decompress On Load*, ADPCM or PCM.

**Room ambience:** each location has an `Audio_RoomTone` object that generates its tone in code. To use a recording instead:
1. Put the loop on that object's AudioSource.
2. Add an **AmbienceVolume** component so it follows the Ambience slider.
3. Disable `ProceduralRoomTone`.

The placeholder generator only fills **empty** library entries, so it will never replace a clip you assigned. Delete the `Audio/Placeholder` folder once all your own sounds are in.

## 2. What plays when

| Moment | Cue | Triggered by |
|---|---|---|
| Hover / click a button, step an option | UIHover, UIClick | Every standard button (`UIButtonStyle`), option steppers |
| Drag a slider | UISlider | Settings sliders (rate-limited) |
| Open / close a screen (inventory, notebook, inspect…) | UIOpen, UIClose | `ModalScreen` |
| Turn a page | PageTurn | Inspect view; also opening evidence from the inventory |
| Notification | Notification | `NotificationEvent` |
| Footsteps | Footstep (4 variations) | `PlayerFootsteps` on the player: by stride, softer crouching, louder running |
| Doors | DoorOpen / DoorClose | `DoorInteractable` (at the door), `SceneDoor` |
| Generic interaction | Interact | Any `Interactable` (override `InteractionSound`) |
| Sitting down in the interview | ChairScrape | `InterviewSeat` |
| Evidence found in the world | EvidenceDiscovered (paper) | Inspect view opened from a pickup |
| Evidence collected / examined | EvidenceCollected / EvidenceExamined | Evidence events |
| Fact confirmed | FactConfirmed | Cooldown 0.8 s: several facts make one chime |
| Evidence connected | Connection | |
| Objective complete | ObjectiveComplete | |
| Evidence presented / statement challenged | EvidencePresented | Presentation card |
| Contradiction exposed | ContradictionExposed (low sting) | Valid challenge in an interview |
| Challenge failed / not proven | ChallengeFailed / ChallengeUnproven | |
| Case closed | CaseClosed (piano chord) | |
| Manual save | GameSaved | Autosaves are silent |

Notebook comparisons are silent on purpose; only interview challenges have stings.

## 3. Music and tension

| Game state | Music |
|---|---|
| Main menu | *MainMenu*: slow minor pad |
| Investigating / briefing | *Investigation*: low pad, sparse notes |
| Interrogating | *Interrogation*: low drone, slow heartbeat |
| Ending | *Ending*: warm chord |
| Paused | Music and ambience dip to 45% |

- **Crossfades:** moods crossfade over 3 seconds (`GameConfig → Audio → Music Crossfade Seconds`).
- **Tension layer:** a separate quiet drone plays only during interrogations.
  - **Composure:** it's almost silent while the suspect is calm and rises gently as composure falls.
  - **Contradictions:** an exposed contradiction swells it briefly (about 6 s), then it settles.
  - **Never loud:** its ceiling is `AudioLibrary → Tension Volume`.

## 4. Volume settings

Settings → AUDIO has four sliders. The final volume is master × category × the cue's own volume.

| Slider | Affects | Default |
|---|---|---|
| Master | Everything (AudioListener) | 100% |
| Music | Music and the tension layer | 70% |
| Effects | Sound effects and UI sounds | 90% |
| Ambience | Room tones / ambience loops | 80% |

Changes apply immediately and are saved in `settings.json`. The Settings screen now uses two columns so every option fits.

## 5. Transitions

All transitions are subtle: no shakes, no flashes, no jump scares.

| Transition | What happens |
|---|---|
| **Scene change** | Picture and ambience fade to black over 0.4 s before the old scene unloads, then fade back in. Music continues, or crossfades if the mood changes. `GameConfig → Audio → Scene Fade Out Seconds` |
| **Dialogue** | Each new line fades in over 0.35 s. No typewriter effect, so lines stay easy to read. `DialogueEntryView → Fade In Seconds` |
| **Evidence presentation** | A small card ("EVIDENCE PRESENTED · CCTV still, 21:40") fades in over the interview scene, holds about 1.6 s and fades out, with a paper-slide sound |
| **Case completion** | The ending screen fades in slowly (1.4 s): outcome, then title and rule, then the epilogue, then the summary and button. The piano chord plays and the music crossfades to the ending mood |

## 6. Performance

- **Fixed voice pool:** 12 reusable effect voices (`GameConfig → Audio → Max Simultaneous Effects`). There are no runtime allocations.
- **Per-cue rules:**
  - A **cooldown** stops rapid repeats. Hover ticks, for example, play at most every 0.06 s.
  - **Max copies** caps how many of one cue play at once. Footsteps, for example, overlap at most 2.
- **When every voice is busy:** a higher-priority sound (a contradiction sting) replaces the oldest lower-priority one (a hover tick). Otherwise the new sound is skipped. Nothing ever plays beyond the cap.
- **Bursts collapse:** several facts confirmed at once make one chime, and autosaves are silent.
- **Cheap imports:** music is streamed; short effects are decompressed once and mono.
- **No new sources:** footsteps and doors use the pooled voices.

## 7. The placeholder sounds

- **Generated by an editor tool:** **The Last Answer → Audio → Generate Placeholder Sounds**. It also runs during Rebuild All Locations / Rebuild UI.
- **Synthesised entirely in code:** no downloads, no paid assets.
- **What it writes:** 31 WAV files in `Assets/_Project/Audio/Placeholder` (about 6 MB source, smaller once imported).
  - **UI:** soft clicks, ticks, open/close blips, page turn, notification ping.
  - **SFX:** four footsteps, door open (latch + creak) and close (creak + thud), interaction click, chair scrape, paper rustle and slide, evidence chimes, contradiction sting, failed/unproven tones, case-closed piano chord.
  - **Music:** seamless 19–24 s loops (menu, investigation, interrogation with heartbeat, ending) and a tension drone.

They're intentionally simple and quiet: good enough to judge timing and mix, meant to be replaced.

## 8. For programmers

- **Play a sound:** `Services.Get<AudioManager>().Play(SoundCue.X, position)`. In UI code: `UISound.Play(SoundCue.X)`.
- **Prefer events:** most sounds are triggered by `AudioManager` from existing events (evidence, objectives, contradictions, saves, case closed), so new gameplay usually gets sound by publishing the usual event.
- **New cue:** add it to `SoundCue` (append, don't renumber), give it an entry in the AudioLibrary, and play it. `EverySoundCue_HasAtLeastOneClip` fails until it has a clip.
- **Interactables:** override `InteractionSound` (`SoundCue.None` = silent).
- **Volume:** `AudioManager.AmbienceGain` is the current ambience multiplier for custom ambience scripts.

| Piece | File |
|---|---|
| Cues, music moods, library asset | `Scripts/Data/Audio/AudioLibrary.cs` |
| Playback, music, tension, events | `Scripts/Systems/Audio/AudioManager.cs` |
| Timing settings | `GameConfig → Audio` (`AudioConfig`) |
| Footsteps | `Scripts/Gameplay/Player/PlayerFootsteps.cs` |
| Ambience for your own loops | `Scripts/Gameplay/Environment/AmbienceVolume.cs` |
| UI shortcut | `Scripts/UI/Components/UISound.cs` |
| Evidence card | `Scripts/UI/Dialogue/EvidencePresentationCard.cs` |
| Placeholder generator | `Scripts/Editor/Audio/PlaceholderAudioGenerator.cs` |

## 9. Testing

- **`AudioLibraryTests`** (6 EditMode tests) cover:
  - every cue and music mood has a clip, and the tension loop exists
  - several footstep variations; stings and hovers stay quiet; facts are rate-limited
  - GameConfig uses the library
  - music is streamed and effects decompressed
  - volume settings are clamped
- **`AudioManagerTests`** (5 PlayMode tests, real AudioSources) cover:
  - cooldowns, and per-cue copy limits
  - the voice cap is never exceeded, and a high-priority sting replaces a low-priority sound while a low one can't steal
  - missing clips are skipped quietly
  - music crossfades between moods
- **In game:**
  - menu music at the expected level; scene changes fade out for about 0.4 s then load
  - the investigation and interrogation moods take over
  - chair sound, evidence chime, the presentation card and contradiction sting
  - the tension layer rising after a contradiction (0.056 → 0.117)
  - music ducking while paused
  - the two-column Settings with the new sliders
  - the ending's staged reveal

All 142 EditMode and 17 PlayMode tests pass.

## 10. Known limits

- **Not listened to by the author.** The placeholders were checked by their volumes and timing, not by ear (I can't hear audio). Expect to adjust levels in the AudioLibrary once you listen.
- **No voice acting.** The `Audio/Voice` folder is ready for when lines are recorded.
- **No reverb or audio mixer effects.** Ducking and fades are done in code. An AudioMixer can be added later if you want reverb in the interrogation room.
- **One footstep surface:** footsteps don't change by floor material.
