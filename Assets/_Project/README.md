# The Last Answer — Systems Guide

This explains the game's systems: what each one is for, how they fit together, and how to use them in later stages.
- **Foundation:** GameManager, game states, scenes, config, save/load, events.
- **First-person player:** movement, mouse look, crouching, interaction, HUD prompt. See [First-person player](#first-person-player).

For the full game plan, see `THE_LAST_ANSWER_DEVELOPMENT_PLAN.md` in the repository root.

---

## The big picture

```
Bootstrap scene  (always loaded)
└── GameManager ─ creates and owns every core service
      ├── ConfigService     GameConfig asset + player settings (settings.json)
      ├── GameStateManager  current game mode (MainMenu, Investigating, Interrogating...)
      ├── SceneLoader       loads/unloads content scenes on top of Bootstrap
      └── SaveService       save slots (save_0.json ...)

+ ONE content scene at a time (MainMenu, Sandbox, later HQ / crime scenes / Interrogation)
  └── SceneContext ─ tells the game which state to enter when this scene loads

Services  (registry)   any code can find a service:  Services.Get<SaveService>()
EventBus  (messages)   systems announce what happened; others listen
```

**Rule of thumb:** services hold the logic and are plain C# classes, so they're easy to test. MonoBehaviours in scenes are thin: they read input, show things and call services.

---

## Code layout and assemblies

Code is split into **assembly definitions** (`.asmdef`). Each one compiles separately, which keeps compile times short, and Unity enforces which layer may use which. A lower layer can never reference a higher one.

| Folder | Assembly | Purpose | May use |
|---|---|---|---|
| `Scripts/Core` | `TLA.Core` | Generic building blocks: `EventBus`, `Services`, `IGameService`. Knows nothing about this game. | — |
| `Scripts/Data` | `TLA.Data` | Data definitions: `GameConfig` asset, `UserSettings`. No logic. | Core |
| `Scripts/Systems` | `TLA.Systems` | The game's systems: GameManager, state, scenes, saving, config, dev overlay. | Core, Data, Newtonsoft JSON |
| `Scripts/Gameplay` | `TLA.Gameplay` | Things in the world: first-person player, interaction, doors. | Core, Data, Systems, Input System |
| `Scripts/UI` | `TLA.UI` | Screens and HUD (interaction prompt). Only listens to events. | Core, Systems, Gameplay, uGUI, TextMeshPro |
| `Assets/Tests/EditMode` | `TLA.Tests.EditMode` | Fast unit tests (Editor only, never in builds). | Core, Data, Systems |
| `Assets/Tests/PlayMode` | `TLA.Tests.PlayMode` | Tests that drive the real Player prefab with simulated keyboard/mouse. | all of the above |

Empty folders (`AI`, `Editor`, art, audio…) are ready for later stages. Their `.gitkeep` files only exist so Git keeps the empty folders; Unity ignores them.

---

## Systems

### GameManager — `Systems/GameManager.cs`
The root of the game, in the **Bootstrap** scene.
- **Creates** each service once, registers it in `Services`, calls `Initialize()` on all of them, and calls `Shutdown()` in reverse order when the game closes.
- **Starts the flow:** loads the main menu. If you pressed Play in another scene, it keeps that scene instead (see *BootstrapLoader*).
- **High-level commands** that coordinate several services: `StartNewGame()`, `GoToMainMenu()`, `LoadScene(name)`, `SaveGame(slot)`, `LoadGame(slot)`.
- Pauses time (`Time.timeScale = 0`) while the state is `Paused`.

Keep gameplay rules **out** of the GameManager. New features get their own service, registered in `CreateServices()`.

### Game State — `Systems/Flow/GameStateManager.cs`, `GameState.cs`
One current state: `Boot, MainMenu, Briefing, Investigating, Interrogating, Deciding, Ending, Paused`.
- `TryChangeState(next)` checks a **rules table** and refuses illegal jumps, such as MainMenu → Ending or opening an interrogation while paused. It logs a warning instead of breaking the game.
- `Pause()` / `Resume()`: Resume returns to whatever state you paused from.
- `ForceState(next)` skips the rules. It's used only by the GameManager when a scene load decides the state.
- Every change publishes `GameStateChangedEvent`.

### Scene Management — `Systems/Flow/SceneLoader.cs`, `SceneContext.cs`, `BootstrapLoader.cs`
- **Additive loading:** Bootstrap stays loaded and holds the services. Content scenes are loaded on top of it and swapped. Because Bootstrap never unloads, services survive every scene change without `DontDestroyOnLoad`.
- `LoadSceneAsync(name)` unloads the old content scene, loads the new one, makes it the **active scene** (new objects and lighting go there), and publishes `SceneLoadStartedEvent` / `SceneLoadedEvent`. Only one load runs at a time.
- **SceneContext:** add one to every content scene and set **State On Load**. After the scene loads, the game switches to that state. MainMenu uses `MainMenu`; Sandbox uses `Investigating`.
- **BootstrapLoader:** if you press Play in any scene, Bootstrap is added automatically. You can test a single scene without opening Bootstrap first.
- Every scene must be listed in **File → Build Profiles → Scene List**, with **Bootstrap first**.

### Configuration — `Data/Config/GameConfig.cs`, `UserSettings.cs`, `Systems/Config/ConfigService.cs`
Two kinds of settings:
| | Who sets it | Where it lives |
|---|---|---|
| **GameConfig** | You, the developer | `Assets/_Project/Data/Settings/GameConfig.asset`, edited in the Inspector. Sections: Scenes, Save, AI, Debug. |
| **UserSettings** | The player | `settings.json` in the save folder. Volume, mouse sensitivity, AI mode. |

```csharp
var config = Services.Get<ConfigService>();
string menu = config.Game.Scenes.MainMenuScene;   // developer setting
config.User.masterVolume = 0.5f;                 // player setting
config.SaveUserSettings();                       // apply + write + UserSettingsChangedEvent
```
**AI settings:** `GameConfig → AI` holds the provider mode (`Mock`/`Online`), the URL of *our own* backend and a timeout. There are **no vendor names, model names or API keys**, and there never should be. The GameConfig ships inside the build, where players can read it.

### Save / Load — `Systems/Save/*`
- **`ISaveable`:** any system with progress to keep implements `SaveKey`, `CaptureState()` and `RestoreState()`, then calls `Services.Get<SaveService>().Register(this)`. The save service doesn't need to know what each system saves.
- **`SaveService`:** `Save(slot, sceneName)`, `Load(slot, out data)`, `HasSave`, `Delete`. A save file has a header (format version, game version, time, scene) plus one section per saveable.
- **`ISaveStorage` / `FileSaveStorage`:** where bytes go. Today that's JSON files in
  `%USERPROFILE%\AppData\LocalLow\<Company>\The-Last-Answer\Saves`. Writes are **crash-safe**: the file is written to a temp file and then swapped in, and the previous file is kept as `.bak`. Swapping storage, for example to cloud saves, doesn't touch game code.
- **Safety:** corrupted files and saves from newer game versions are refused with a clear message. JSON type-name handling is disabled so a tampered save can't create arbitrary objects.
- When the save format changes, bump `SaveData.CurrentSaveVersion` and add a migration step in `SaveService.Load`.

### Event System — `Core/Events/EventBus.cs`
Systems publish small event structs; anything can subscribe.
```csharp
void OnEnable()  => EventBus.Subscribe<GameStateChangedEvent>(OnStateChanged);
void OnDisable() => EventBus.Unsubscribe<GameStateChangedEvent>(OnStateChanged);  // always unsubscribe!
void OnStateChanged(GameStateChangedEvent e) => Debug.Log($"{e.Previous} -> {e.Current}");
```
Current events (`Systems/GameEvents.cs`): `GameStateChangedEvent`, `SceneLoadStartedEvent`, `SceneLoadedEvent`, `GameSavedEvent`, `GameLoadedEvent`, `UserSettingsChangedEvent`.
**Why:** the UI, audio and future systems react to the game without the game knowing about them. If one listener throws an error, the others still run.

### Service registry — `Core/Services/Services.cs`, `IGameService.cs`
`Services.Get<T>()` returns a registered service, or throws a clear error if Bootstrap isn't running. `Services.TryGet<T>(out t)` is the safe version. Only long-lived systems belong here.

### Dev overlay — `Systems/Dev/DebugOverlay.cs`
Press **F1** in Play Mode to show/hide it. It starts hidden. While you're walking around the mouse is locked, so press **Esc** (pause) to get the cursor back and click its buttons. It shows the state, scene and settings, with buttons to change scenes and state, pause, save/load/delete slot 0, change volume and open the save folder. It's compiled **only** in the Editor and Development builds, and allowed by `GameConfig → Debug → Show Debug Overlay`.

---

## Scenes

| Scene | Path | Contents |
|---|---|---|
| Bootstrap | `Scenes/Bootstrap.unity` | `GameManager` object (GameManager + DebugOverlay). No camera. **Build index 0.** |
| MainMenu | `Scenes/MainMenu.unity` | Camera + SceneContext (MainMenu). Real menu UI comes in a later stage. |
| Sandbox | `Scenes/Sandbox/Sandbox.unity` | Test room with the **Player** prefab: walls, a working door, a locked closet door, a table with a test object, a low beam to crouch under, steps and a ramp, plus SceneContext (Investigating). Used as the "New Game" scene for now. |

---

## How to test

**Automated:** *Window → General → Test Runner*.
- **EditMode → Run All:** 17 tests covering the event bus, the state rules, and save/load including corrupted and newer-version files.
- **PlayMode → Run All:** 11 tests that drive the real Player prefab with simulated keys and mouse. They cover walking, running, smooth stopping, crouching, the ceiling check, mouse look and its clamp, interaction, the range limit, wall blocking, doors, and looking down at a table.

**In Play Mode:**
1. Open `Scenes/Bootstrap.unity` and press **Play**. The Console shows `[GameState] Boot -> MainMenu`.
2. Press **F1** to open the dev overlay, and **Esc** to free the mouse so you can click it.
3. **New Game** → Sandbox loads and the state becomes `Investigating`.
4. **Pause** → the state becomes `Paused`. **Interrogate** is refused (Console warning). **Resume** → back to `Investigating`.
5. **Save** (slot 0) → **Main Menu** → **Load** → you're back in Sandbox, `Investigating`.
6. **Open save folder** to see `Saves/save_0.json` and `settings.json`.
7. Stop, open `Scenes/Sandbox/Sandbox.unity` directly and press Play. Bootstrap loads automatically and the state goes straight to `Investigating`.

---

## Adding things later — checklists

- **New service:** plain C# class implementing `IGameService` → create it in `GameManager.CreateServices()` with `AddService(...)`.
- **New saveable progress:** implement `ISaveable` and register with `SaveService`. Save IDs and plain data, never Unity object references.
- **New content scene:** create the scene → add a `SceneContext` with the right state → add it to the Build Profile scene list → load it with `GameManager.LoadScene("Name")`.
- **New event:** add a `readonly struct XxxEvent : IGameEvent` (past-tense name) and publish it from the system that owns the change.

---

## First-person player

### Input — package requirements
The player uses Unity's **Input System** package (`com.unity.inputsystem`, v1.20.1), the supported modern input solution. It replaces the old `Input.GetKey` API.
- **Project Settings → Player → Active Input Handling** is set to **Input System Package (New)**. Unity only applies this setting after **restarting the editor**. Until you restart, no keyboard or mouse is detected in Play Mode.
- The UI uses **Unity UI (uGUI) 2.0**, which includes **TextMeshPro**. TMP's essential resources (default font) were imported into `Assets/TextMesh Pro/`.

**Controls** (from `Data/Settings/PlayerControls.inputactions`):

| Action | Keys |
|---|---|
| Move | W A S D / arrow keys |
| Look | Mouse |
| Run | Hold Left Shift |
| Crouch | C or Left Ctrl (toggle by default) |
| Interact | E |
| Pause / free the mouse | Esc |

To change a key, double-click `PlayerControls.inputactions`, select the binding, and pick a new path. On-screen prompts update automatically, because "[E]" is read from the binding.

There's **no jump**, on purpose. An investigator doesn't need platforming, and it stops players reaching places a scene wasn't built for. Adding one later would be about 10 lines in `FirstPersonController`.

### Player prefab — `Prefabs/Player/Player.prefab`
```
Player            CharacterController · PlayerInputReader · FirstPersonController · PlayerInteractor
├── CameraPivot   moves down when crouching, tilts for looking up/down
│   └── Main Camera
└── HUD           Canvas · InteractionPromptUI
    ├── Crosshair     small centre dot (brightens when something is usable)
    └── Prompt        "[E] Inspect" box (fades in/out)
```
Drop the prefab into any scene that has a floor. The Player's position is at its **feet**.

| Script | Job |
|---|---|
| `PlayerInputReader` | The only script that talks to the Input System. Enables the controls and locks/hides the cursor **only in the Investigating state**, so pausing or a future dialogue frees the mouse automatically. Esc toggles pause. |
| `FirstPersonController` | Walking, running, crouching (with a ceiling check), smooth acceleration, gravity, mouse look. It uses a **CharacterController** instead of a Rigidbody, which gives precise, non-floaty movement with wall sliding, steps and slopes built in. Call `Teleport()` to move the player; don't set `transform.position` directly. |
| `PlayerInteractor` | Each frame, casts a ray from the camera centre, ignoring the player's own body. The nearest thing hit wins, so walls block doors behind them. Pressing E calls `Interact()`. Focus changes are announced as `InteractionFocusChangedEvent`. |
| `InteractionPromptUI` | Listens for that event and shows or hides the prompt. It never calls gameplay code. Hidden whenever you're not investigating. |

### Making things interactive
Every interactive object uses **`IInteractable`**: `Prompt` (verb), `CanInteract`, `Interact()`. Most objects inherit from **`Interactable`**, which adds:
- a **Prompt** text field (`Inspect` → shown as `[E] Inspect`),
- an **Interaction Enabled** checkbox,
- an **On Interacted** event you can wire up in the Inspector with no code,
- `OnFocusGained()` / `OnFocusLost()` hooks, for example for highlighting.

The object (or a child of it) needs a **normal, non-trigger collider**.

| Component | Use |
|---|---|
| `Interactable` | Add directly for simple "press E → something happens" objects via the Inspector event. |
| `DoorInteractable` | Hinged door. Put it on the hinge object, with the panel as a child offset along local +X. It swings open **away from the player**, the prompt switches Open/Close, and **Locked** shows "Locked". |
| `TestInteractable` | Sandbox only: highlights on focus, flashes and logs when used. |
| *(later)* Evidence | Will inherit from `Interactable` and call the Investigation system. The interaction code doesn't need to change. |

### Configuring movement, sensitivity and interaction distance
**Everything is in one asset:** `Assets/_Project/Data/Settings/PlayerMovementSettings.asset`. Select it and edit in the Inspector. You can do this **during Play Mode**: changes apply instantly and, unlike component values, they're **kept when you stop playing**.

| Setting | Default | What it does |
|---|---|---|
| Walk Speed | 2.2 m/s | Normal pace (a relaxed real walk is ~1.4) |
| Run Speed | 4.2 m/s | While holding Shift |
| Crouch Speed | 1.2 m/s | While crouched |
| Acceleration / Deceleration | 12 / 14 m/s² | Higher = snappier start/stop; lower = smoother, heavier feel |
| Gravity | 15 m/s² | Downward pull (real is 9.81; a bit more feels grounded) |
| Standing / Crouch Height | 1.8 / 1.1 m | Body (collision) height. Crouched fits under ~1.2 m obstacles |
| Eye Offset From Top | 0.12 m | Camera height = body height − this (eyes at 1.68 m standing) |
| Crouch Transition Speed | 8 | How fast you crouch/stand (≈ 1/8 s) |
| Crouch Mode | Toggle | Toggle (press once) or Hold |
| **Look Sensitivity** | 0.12 | Degrees turned per pixel of mouse movement |
| Invert Y | off | Invert vertical look |
| Max Look Angle | 85° | How far up/down you can look |
| **Interaction Distance** | 2.5 m | How far from the eyes you can interact |
| Interaction Radius | 0.05 m | Aim forgiveness. Slightly above 0 makes small objects easier to target |
| Interaction Layers | Everything | Physics layers the interaction ray can hit |

**Player-facing sensitivity:** `UserSettings.mouseSensitivity` (saved in `settings.json`, default 1.0) **multiplies** Look Sensitivity. A future Settings menu will change that value. Developer tuning goes in the asset.

### Testing the player by hand
1. **Restart Unity once** if you haven't since installing the Input System (see above).
2. Open `Scenes/Sandbox/Sandbox.unity` → **Play** → click inside the Game view to capture the mouse.
3. Walk (WASD), run (Shift), look around. Crouch (C) and go under the blue beam, then try standing up under it: you'll stay crouched until you're clear.
4. Climb the steps on the left and walk down the ramp.
5. Look at the yellow box on the table: **"[E] Inspect"** appears and the box brightens. Press **E**: it flashes, and the Console logs a message.
6. Look at the door: **"[E] Open"** → E → it swings away from you; the prompt changes to "Close". The brown door on the right wall says **"Locked"**.
7. Turn around: try the small yellow cube on the wall behind the start point from across the room. There's no prompt until you're within 2.5 m.
8. Press **Esc**: the game pauses, the mouse is freed, and the HUD hides. Esc again resumes.
