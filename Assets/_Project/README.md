# The Last Answer — Foundation Systems

This explains the core architecture added in the **Foundation** stage: what each system is for, how they fit together, and how to use them in later stages.
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
| `Assets/Tests/EditMode` | `TLA.Tests.EditMode` | Automated unit tests (Editor only, never in builds). | all of the above |

Empty folders (`AI`, `Gameplay`, `UI`, `Editor`, art, audio…) are ready for later stages. Their `.gitkeep` files only exist so Git keeps the empty folders; Unity ignores them.

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
Press **F1** in Play Mode. It shows the state, scene and settings, with buttons to change scenes and state, pause, save/load/delete slot 0, change volume and open the save folder. It's compiled **only** in the Editor and Development builds, and turned on by `GameConfig → Debug → Show Debug Overlay`.

---

## Scenes

| Scene | Path | Contents |
|---|---|---|
| Bootstrap | `Scenes/Bootstrap.unity` | `GameManager` object (GameManager + DebugOverlay). No camera. **Build index 0.** |
| MainMenu | `Scenes/MainMenu.unity` | Camera + SceneContext (MainMenu). Real menu UI comes in a later stage. |
| Sandbox | `Scenes/Sandbox/Sandbox.unity` | Camera, light, floor, test cube + SceneContext (Investigating). For experiments. Used as the "New Game" scene for now. |

---

## How to test

**Automated:** *Window → General → Test Runner → EditMode → Run All.* There are 17 tests covering the event bus, the state rules, and save/load including corrupted and newer-version files.

**In Play Mode:**
1. Open `Scenes/Bootstrap.unity` and press **Play**. The Console shows `[GameState] Boot -> MainMenu`.
2. Press **F1** to open the dev overlay.
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
