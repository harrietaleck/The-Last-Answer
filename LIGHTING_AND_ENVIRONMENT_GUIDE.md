# THE LAST ANSWER — Lighting & Environment Guide

How the game's locations are built and lit, and how to change them safely. Written for a solo developer or a small team.
Visual direction: **cinematic investigative thriller, dark but readable, stylised-realistic, subtle red accents.**

---

## Contents
1. [Rendering setup (URP)](#1-rendering-setup-urp)
2. [The four locations](#2-the-four-locations)
3. [Visual rules](#3-visual-rules)
4. [Lighting recipes](#4-lighting-recipes)
5. [Post-processing ("the look")](#5-post-processing-the-look)
6. [Interrogation room: cinematic cameras and the one-way window](#6-interrogation-room-cinematic-cameras-and-the-one-way-window)
7. [Moving between scenes](#7-moving-between-scenes)
8. [Sound](#8-sound)
9. [Performance budget](#9-performance-budget)
10. [Workflow: generator vs. hand-editing](#10-workflow-generator-vs-hand-editing)
11. [Replacing placeholders with real art](#11-replacing-placeholders-with-real-art)
12. [Baking lighting later](#12-baking-lighting-later)
13. [Testing](#13-testing)
14. [Troubleshooting](#14-troubleshooting)

---

## 1. Rendering setup (URP)

| Setting | Value | Why (plain language) |
|---|---|---|
| Render pipeline | **URP** (Universal Render Pipeline) 17.3, asset `Settings/Rendering/URP_PC.asset` | Unity's modern renderer: good lighting, built-in post-processing, fast on PCs. |
| Rendering path | **Forward+** (renderer `URP_PC_Renderer.asset`) | Lets many small lights (lamps, signs, monitors) light the same object. Ideal for interiors. |
| Colour space | **Linear** | Physically correct light maths: light falls off and mixes realistically. Gamma looks flat. |
| HDR | On | Lets bright things (bulbs, red signs) go "brighter than white" so bloom can glow. |
| Anti-aliasing | MSAA 4× on the pipeline + SMAA on cameras | Clean edges on geometry and thin objects like evidence string. |
| Shadows | Soft, 30 m distance, 4096 additional-light atlas | Interiors are small, so a short distance means sharper shadows. |

All of this is created by **The Last Answer → Environment → Setup Render Pipeline (URP)**. It's safe to run again: it updates the assets instead of duplicating them.

**Light intensities.** URP lights fade with distance much more realistically than Unity's old renderer. The environment generator therefore uses a simple 0–5 "feel" scale (1 = a normal room light) and multiplies by `Blockout.UrpIntensityScale = 8`. **The values you see on lights in the scenes are the real ones.** Tune those directly.

---

## 2. The four locations

All scenes are in `Assets/_Project/Scenes/` (locations in `Scenes/Locations/`).

### Main Menu — `MainMenu.unity`
A small vignette behind the title: a detective's desk under one warm lamp, an out-of-focus evidence board with red string, and moonlit blinds. The **camera drifts slowly** (`CameraDrift`), and depth of field blurs the background. The menu UI is on the left: **New Game** (→ police station), **Continue** (save slot 0; dimmed when there's no save), and **Quit**. The camera is aimed left of the desk, so the set sits on the right and the left side stays clear for the UI.

### Police Station Hub — `Locations/PoliceStation.unity` (16 × 12 m, night shift)
```
z=6  ┌─────window──window──window────────┬──[INTERROGATION]──┐
     │  BULLPEN: 4 desks, monitors,      │    CORRIDOR       │
     │  cabinets, whiteboard             ⌂ (opening)    [EVIDENCE]  (east wall)
z=-2 ├────────────archway────────────────┘                   │
     │  LOBBY: front desk, benches, precinct sign            │
z=-6 └────────────────[EXIT → menu]──────────────────────────┘
    x=-8                                 x=3               x=8
```
Red accents: the **EXIT** sign over the front door and the **"in session" lamp** above the interrogation door. The corridor light **flickers** now and then (`FlickeringLight`).
Spawn points: `Entrance` (default), `FromInterrogation`, `FromEvidence`.

### Interrogation Room — `Locations/InterrogationRoom.unity`
```
z=3  ┌──────────────────────────┬──────────┐
     │ INTERVIEW ROOM   suspect ║ OBSERV-  │  ║ = one-way window
     │ acoustic panels  [table] ║ ATION    │
     │            investigator  ║ (dark,   │
z=-2 ├──door──────────────────────┴──door─────┤     red strip)
     │ CORRIDOR  bench          ← MAIN OFFICE │
z=-4 └────────────────────────────────────────┘
```
- **Interview table** (steel, with a restraint bar), **two chairs**, a **placeholder suspect** (primitive mannequin), and `InvestigatorPosition` / `SuspectPosition` markers for future systems.
- **Overhead pendant** (warm spot): the single hard key light, giving a pool of light on the table with dark edges.
- **One-way window** into a dark observation room (desk, monitor, red accent strip).
- **Security camera** with a red recording LED.
- **Six cinematic shots** (see §6). Sit in the investigator's chair (**E**) to use them.

Spawn point: `FromHub` (default, in the corridor).

### Evidence Room — `Locations/EvidenceRoom.unity` (7 × 6 m)
- **North wall:** the main **evidence board**. Cork, with victim/suspect/scene **photographs**, **documents** ("STATEMENT", "AUTOPSY"), **index cards** ("ALIBI", "MOTIVE", "21:40 ?"), red pins and **red string** connections. Two track spotlights wash it.
- **West wall:** **timeline** whiteboard (19:00 → 23:50).
- **Centre:** the **investigation desk** with a stack of **case files**, an open folder of documents, scattered photos, a laptop, a mug, and a **desk lamp**.
- **East wall:** evidence shelving with labelled boxes (`0147-A`…), plus filing cabinets.
- **Red accents:** a glow under the board and a "secure room" lamp over the door.
- The board, case files and laptop respond to **E** with a placeholder message until the evidence system is built.

Spawn point: `FromHub` (default).

---

## 3. Visual rules

1. **Dark but readable.** Every scene has one clear light source the eye goes to first (the lamp pool, the board, the table). Faces and interactive objects must always be readable. If you can't read it, add a *motivated* fill light, i.e. one that could plausibly exist (a table bounce, a monitor glow, a window), rather than raising ambient light everywhere.
2. **Cool world, warm humans.** Walls, floors and fluorescents are cool blue-grey. Lamps, paper, wood and faces are warm. That contrast is most of the "thriller" look.
3. **Red is precious.** Use red **only** for signals: signs, recording lights, "in session" lamps, evidence string. If red is everywhere it stops meaning danger or attention. Keep red lights small and dim (feel-scale 0.06–1.2).
4. **Practical lights.** Every light should have a visible source (fixture, lamp, screen, sign) with an emissive material. It sells realism and gives bloom something to glow on.
5. **Shape the darkness.** Don't light corners. Let fog and vignette swallow the edges of rooms.

### Material palette (`Art/Materials/Environment/`, URP Lit, flat colours)
| Group | Materials |
|---|---|
| Surfaces | `M_ConcreteFloor`, `M_Linoleum`, `M_WallPaint`, `M_WallLower`, `M_InterrogationWall`, `M_AcousticPanel`, `M_Ceiling`, `M_Trim` |
| Furniture | `M_SteelTable`, `M_ChairPlastic`, `M_DarkWood`, `M_DeskLaminate`, `M_MetalCabinet`, `M_DoorSteel`, `M_Cardboard` |
| Paperwork | `M_Cork`, `M_Paper`, `M_PaperAged`, `M_ManilaFolder`, `M_PhotoCool`, `M_PhotoWarm`, `M_PhotoBorder`, `M_Whiteboard` |
| Emissive | `M_RedAccent`, `M_RedString`, `M_LightPanelCool`, `M_LightBulbWarm`, `M_ScreenGlow`, `M_NightWindow` |
| Glass | `M_OneWayMirror` (dark mirror), `M_GlassTint` (transparent, no highlights) |

**Smoothness matters more than colour** for realism: paper ~0.1, painted walls ~0.1–0.15, laminate ~0.3, steel ~0.55, glass/screens 0.9+.

---

## 4. Lighting recipes

Values are the generator's feel-scale numbers (×8 in the scene). ★ = casts shadows.

### Police station hub
| Light | Type | Purpose |
|---|---|---|
| Ceiling panels (lobby ×3, bullpen ×2, corridor ×1) | Point, cool white, 0.9–1.1 | Base fluorescent light. Only `Ceiling_Bullpen_W` ★ |
| Front-desk lamp ★ | Spot, warm, 1.6 | Warm "human" pool at reception |
| Moonlight fill | Point, blue, 0.45 | Motivated by the night windows |
| Exit-sign glow / In-session glow | Point, red, 0.6 / 0.8 | Red accents |
| Ambient | Flat (0.09, 0.10, 0.12) | Darkest shadow level |
| Fog | Exp², density 0.02 | Depth along the corridor |

### Interrogation room
| Light | Type | Purpose |
|---|---|---|
| `Pendant_Light` ★ | Spot 85°, warm, 4 | **Key light.** Hard pool on the table |
| `Bounce_Table` | Point, warm, 0.25, range 1.3 | Fakes light bouncing up off the table onto the suspect's face |
| `Rim_Suspect` | Point, cool, 0.35 | Separates the suspect's silhouette from the dark wall |
| `Fill_Cool` | Point, cool, 0.45 | Keeps the room's shape just visible |
| `RecGlow` | Point, red, 0.06 | Recording LED |
| Observation `Accent_Red` / `Monitor_Glow` | Point, red 0.55 / blue 0.3 | The observation room stays darker than the interview room, so the room reads clearly through the glass |
| Corridor panel (flickers) | Point, cool, 0.8 | Unease |
| Ambient | Flat (0.036, 0.04, 0.048) | Very dark |
| Reflection probe | Realtime, updates once on load | Gives the one-way mirror real reflections |

### Evidence room
| Light | Type | Purpose |
|---|---|---|
| `BoardSpot_L` ★ / `BoardSpot_R` | Spot 55°, warm white, 2.6 | Makes the board the focal point |
| Desk lamp ★ | Spot, warm, 2.6 | Pool on the case files |
| Ceiling panels ×2 | Point, cool, 0.55–0.6 | Dim base light |
| `Accent_Board` / `SecureLamp_Glow` | Point, red, 0.35 / 0.45 | Red accents |

### Main menu
Desk lamp ★ (warm, 1.1), window fill (blue, 0.6), board spot (warm white, 1.4), and a red rim accent behind (1.2).

**To brighten a scene:** first raise its key light (lamp, pendant, board spots), then the fills. Raise ambient (Window → Rendering → Lighting → Environment) only as a last resort, because it flattens everything.

---

## 5. Post-processing ("the look")

Each scene has a **Global Volume** using one of three profiles in `Settings/Rendering/`:

| Profile | Used in | Character |
|---|---|---|
| `VP_Station` | Police station, evidence room, Sandbox | Neutral-cool, contrast +12, saturation −18 |
| `VP_Interrogation` | Interrogation room | Harder: contrast +20, saturation −26, stronger vignette and grain. Pressure |
| `VP_MainMenu` | Main menu | Plus depth of field (background blur) and stronger bloom |

All three use: **ACES tonemapping** (filmic highlight roll-off), **split toning** (cool shadows, warm highlights), **bloom** (threshold 1.0, so only emissive objects glow), **vignette**, and **thin film grain**.
Select a profile asset and edit it in the Inspector. Changes apply live in Play Mode and are kept.

---

## 6. Interrogation room: cinematic cameras and the one-way window

### Shots
`InterviewCameraRig` holds a `CinematicShotDirector`, a disabled `CinematicCamera`, and six `CinematicShot` markers under `Shots`:

| Shot id | Framing | FOV | Use for |
|---|---|---|---|
| `Establishing` | High corner, whole room | 56 | Start of interview, tension beats |
| `OverShoulder_Investigator` | Behind the investigator, onto the suspect | 34 | Default conversation shot |
| `Suspect_CloseUp` | Suspect's face | 28 | Lies, reactions, confessions |
| `Profile_TwoShot` | Side-on, both chairs | 36 | Confrontations, presenting evidence |
| `Through_OneWayGlass` | From the observation room | 30 | "Being watched", cutaways |
| `TableTop_Evidence` | Top-down on the table | 42 | Laying out evidence |

Each shot has a tiny **push-in** (slow forward drift), which makes still shots feel alive.

**Try it:** walk to the investigator's chair → **[E] Sit down** → **Tab** cycles shots → **E** stands up.
**Adjust a shot:** select `Shot_…`. Its view cone shows in the Scene view. Frame the Scene view the way you want, then use **GameObject → Align With View**.
**Add a shot:** duplicate a `Shot_…`, give it a new **Shot Id**, and add it to the director's **Shots** list.
Later, the dialogue system will call `director.ShowShot("Suspect_CloseUp")` to cut to angles based on the conversation.

### One-way window (a free trick)
The interview side is a **single-sided quad** with a dark, mirror-like material (`M_OneWayMirror`). Unity doesn't draw the back of a single-sided quad, so **from the observation room it's invisible** and you see straight through. A second, faintly tinted transparent quad faces the observation room for a glass feel. Keep the observation room **darker** than the interview room, as in real life, or the illusion weakens. An invisible box collider stops anyone walking through.

---

## 7. Moving between scenes

- **`SceneDoor`** (on a door panel): **Target Scene** + **Target Spawn Id**. E → the screen cuts to black → the scene loads → it fades in (`ScreenFader` in Bootstrap).
- **`PlayerSpawnPoint`**: where the player appears. Its gizmo arrow shows the facing direction. Exactly one per scene must be **Default** (used for new games, loading saves, and any door without a spawn id).
- **`PlayerSpawnPlacement`** (on the Player prefab) moves the player to the requested spawn when a scene starts.
- Normal doors inside a scene use **`DoorInteractable`** (swings open, no loading).

**Adding a new location:** create the scene → add a `SceneContext` (Investigating) → add the Player prefab and a default `PlayerSpawnPoint` → add a Global Volume → add it to **File → Build Profiles → Scene List** → put a `SceneDoor` in an existing scene that points to it, and one in the new scene that points back. The scene validation tests (§13) will catch missing pieces.

---

## 8. Sound

No audio files are included (none were downloaded). Instead, every location has an **`Audio_RoomTone`** object with **`ProceduralRoomTone`**, which generates soft, low rumble plus an optional mains hum in code:

| Scene | Volume | Hum |
|---|---|---|
| Main menu | 0.05 | none |
| Police station | 0.07 | 50 Hz, light |
| Interrogation | 0.09 | 50 Hz, stronger (fluorescent buzz) |
| Evidence room | 0.06 | 50 Hz, faint |

**Atmospheric sound opportunities** (where real recordings would add the most):
- **Hub:** distant phones, muffled radio chatter, a printer, rain on the windows, the HVAC drone.
- **Interrogation:** fluorescent buzz, chair creaks, the pendant lamp's electrical tick, a door buzzer, a muffled corridor. Silence is a tool here: let the room tone carry long pauses.
- **Evidence room:** paper rustle, a ticking clock, the laptop fan.
- **Doors:** open/close sounds and the heavy interrogation door latch.
- **Menu:** a low drone and distant thunder.

To use a real recording: assign a looping clip to the room-tone object's **AudioSource** and untick `ProceduralRoomTone`. For spot sounds (a ticking clock), add an AudioSource with **Spatial Blend = 1 (3D)** on the object itself.

---

## 9. Performance budget

These rooms run comfortably on mid-range PCs. Keep to these budgets as you add detail:

| Item | Budget per scene | Notes |
|---|---|---|
| Shadow-casting lights | **≤ 3** | A shadowed **point** light costs **6 shadow maps** (one per cube face). Prefer shadowed **spots** (1 map). |
| Realtime lights | ~12–15 | Forward+ handles this; keep small accents at short ranges |
| Reflection probes | 1 realtime (on awake) | Bake extra probes instead of making them realtime |
| Props | Static where possible | The generator marks geometry Batching Static |
| Transparent surfaces | Few | Glass is expensive when layered |

If Unity logs *"Reduced additional punctual light shadows resolution…"*, too many shadowed lights are on screen. Turn shadows off on some fills.

---

## 10. Workflow: generator vs. hand-editing

The locations are produced by an editor tool, so they're consistent and easy to change in code:
- **The Last Answer → Environment → Rebuild All Locations** regenerates MainMenu, PoliceStation, InterrogationRoom and EvidenceRoom. It asks first.
- Code: `Assets/_Project/Scripts/Editor/Environment/` (`PoliceStationBuilder.cs`, `InterrogationRoomBuilder.cs`, `EvidenceRoomBuilder.cs`, `MainMenuBuilder.cs`, plus helpers `Blockout`, `SceneKit`, `EnvMaterials`, `RenderPipelineSetup`).

> ⚠️ **Rebuilding overwrites those scenes.** Treat the generator as a *blockout* tool. Once you start decorating a location by hand (real models, hand-placed lights), **stop rebuilding that scene**. Edit it directly in Unity from then on. Materials, profiles and the URP asset are shared assets and aren't lost on rebuild.

---

## 11. Replacing placeholders with real art

1. **Materials first.** Add textures (albedo, normal, roughness) to the existing `M_…` materials. Every object using them updates at once. Free CC0 texture and model libraries exist if you want them, but nothing here requires them.
2. **Swap hero props.** The table, chairs, evidence board and desk lamp have the biggest visual impact. Replace a primitive group with a model and keep the same position.
3. **Replace the suspect mannequin** with a character model at `SuspectPosition`. Keep the light setup: pendant, bounce, rim.
4. Keep **colliders simple** (boxes) even when the visuals get detailed.
5. Keep **every light motivated** by a visible fixture when you rearrange props.

---

## 12. Baking lighting later

Everything is **realtime** right now, so iteration is instant. When a room's layout is final:
1. Mark walls, floors and large furniture **Contribute GI** (Static dropdown).
2. Set ceiling panels and fills to **Mixed**; keep the pendant, lamps and flickering lights **Realtime**.
3. **Window → Rendering → Lighting:** create a Lighting Settings asset, enable **Baked Global Illumination**, then Generate. This gives soft bounce light, so you can lower or remove the fake bounce and fill lights.
4. Add a few **baked reflection probes** and **light probes** around props.

Don't bake while layouts are still changing. Rebakes take minutes, and generator rebuilds discard them.

---

## 13. Testing

**Automated** (*Window → General → Test Runner*):
- **EditMode → SceneValidationTests**: every scene has one SceneContext and a Global Volume. Gameplay scenes have one Player, exactly one default spawn and unique spawn ids. Every SceneDoor points at a real scene and spawn. Bootstrap has the GameManager and ScreenFader.
- **PlayMode → LocationFlowTests**: walks Station → Evidence Room → Station → Interrogation, checks the player arrives at the right spawn facing the right way, then sits down, cycles shots and stands up.

**By hand:**
1. Open `Scenes/Bootstrap.unity` → **Play**. The menu fades in.
2. **New Game** → you're in the station lobby. Walk to the corridor.
3. **Evidence Room** door → look at the board, the desk files and the laptop (E shows a placeholder message). Go back out.
4. **Interrogation** door → open the "Interview 1" door → sit in the investigator's chair (**E**) → **Tab** through the camera angles → **E** to stand. Visit the observation room and look through the glass.
5. Return to the lobby → **Leave station** → back at the main menu.

---

## 14. Troubleshooting

| Problem | Fix |
|---|---|
| Everything is pink/magenta | A material isn't URP. Run **Setup Render Pipeline (URP)** (it converts Standard materials), or switch the material's shader to *Universal Render Pipeline/Lit*. |
| A scene is far too dark | Check the Global Volume's profile is assigned, then raise the key light. Remember the ×8 scale if you add lights by hand: a plain point light at intensity 1 is very dim in URP. |
| Black screen after loading | The scene has no camera (Player missing) or the fader is stuck. Check the Console and that the scene has a SceneContext. |
| Keyboard/mouse do nothing in Play Mode | Restart Unity once after switching Active Input Handling to the Input System. |
| *"Invalid Signature"* notification | Unity flags packages that aren't signed by Unity. Here that's the MCP for Unity package installed from GitHub (an editor tool). Review it in the Package Manager. It isn't part of the game build. |
| Shadow atlas warning in the Console | Too many shadowed lights at once. See §9. |
