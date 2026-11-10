# The Last Answer — build and release guide

This is how to produce a Windows PC build of the investigation prototype. The game is **not** release-ready until a build made with these steps has been launched outside Unity and walked through `PLAYTEST_CHECKLIST.md`.

## What the player build is

- Unity 6.3 LTS (`6000.3.0f1`), Universal Render Pipeline.
- First scene: `Assets/_Project/Scenes/Bootstrap.unity`. It loads the main menu. Do not put another scene first.
- Enabled scenes, in order: Bootstrap, MainMenu, PoliceStation, InterrogationRoom, EvidenceRoom, Sandbox.
- Input: the new Input System only (`Active Input Handling` = Input System Package). Keyboard and mouse are `PlayerControls.inputactions`. UI clicks use the Input System UI module on the Bootstrap `EventSystem`.
- Dialogue default: **Mock / Offline**. `GameConfig` → AI → Default Mode is Mock. No API key is in the project. Online mode calls this project’s own backend (`ONLINE_AI_GUIDE.md`) at `http://localhost:8787` unless a production URL is set. If that URL is empty or the request fails, the interview uses the scripted mock replies.
- Graphics default: **High** (the player can pick Very Low through Ultra in Settings). The window starts borderless at the desktop resolution and can be resized or switched to exclusive fullscreen or windowed.
- Saves: `%USERPROFILE%\AppData\LocalLow\TheLastAnswer\The-Last-Answer\Saves\save_0.json` (and slots 1–2). Settings: `settings.json` beside the `Saves` folder. Writes go to a temp file and then replace the previous file. A `.bak` is kept.

## 1. Build the Windows executable

1. Open the project in Unity `6000.3.0f1`.
2. Wait until the Console has no compile errors. `Window → General → Test Runner → EditMode → Run All` should pass.
3. `File → Build Profiles` (Unity 6) or `File → Build Settings`.
4. Select **Windows**. If the platform button says Switch Platform, switch and wait for the reimport. The project is already set up for Windows.
5. Scene list must match the list above, Bootstrap at index 0. That list is `ProjectSettings/EditorBuildSettings.asset`.
6. Architecture: **Intel 64-bit**. Scripting backend can stay **Mono** for a fast test build. Use **IL2CPP** only for a build you intend to hand to other people; the first compile is much slower.
7. Development Build: on while you are still fixing bugs. Off for a demo you give to players. The F1 debug overlay only appears when `GameConfig → Debug → Show Debug Overlay` is on.
8. Choose a folder **outside** `Assets`, for example `Builds/Windows`.
9. Build. Unity writes `The-Last-Answer.exe`, a `The-Last-Answer_Data` folder, and UnityPlayer DLLs. Ship the whole folder, not the exe alone.

The Unity splash screen stays on. Turning it off needs a Unity license that allows it. Leave it on for this prototype.

## 2. Test the build outside Unity

1. Close the Unity Editor so it is not locking files or audio devices.
2. Disconnect from the internet (or disable the adapter).
3. Double-click `The-Last-Answer.exe`. Do not press Play inside the Editor for this pass.
4. Confirm Settings → Dialogue says **Offline (scripted)**. Play the checklist in `PLAYTEST_CHECKLIST.md`, including New Investigation and one interview.
5. Alt-tab, change Windowed / Borderless, then quit with Exit Game. Start the exe again and confirm settings and the save are still there.
6. If you turn Dialogue to Online, the interview must still answer when the backend is down. The session falls back to the scripted line. Do not put a vendor API key in `GameConfig`.

A build that has not been double-clicked outside the Editor has not been tested.

## 3. Package the game

1. Zip the whole build folder (`The-Last-Answer.exe`, `*_Data`, Unity crash handler, and DLLs). Name it `TheLastAnswer-windows-vX.Y.zip` using `ProjectSettings → Player → Version` (currently 1.0).
2. Do not include `Assets`, `Library`, `Saves`, `settings.json`, or the Unity project.
3. On a second Windows PC (or a clean user account), unzip and run the exe. Windows SmartScreen may warn on an unsigned exe; that is expected until the file is code-signed.
4. Check the save folder was created under that user’s `AppData\LocalLow\TheLastAnswer\The-Last-Answer`.

## 4. Prepare a future public demo

Do this only after the outside-Unity checklist passes.

- One case only: Case 0147, Silence on Harlow Street. Leave other cases classified.
- Turn off `GameConfig → Debug → Show Debug Overlay` for the demo build, and turn off Development Build.
- Keep AI mode Mock. Online stays optional and keyless, and only talks to this project’s backend.
- Record a 60-second silent clip: menu → station → evidence room → one interview question → pause → quit.
- Write the player-facing blurb from the case briefing, plus the controls (WASD, mouse, Shift, C, E, Esc).
- Note known limits in the zip’s readme: prototype lighting, placeholder audio, one case, no code signing.

## Review notes from the build preparation

| Item | Decision |
|---|---|
| Scenes | Bootstrap, MainMenu, PoliceStation, InterrogationRoom, EvidenceRoom, Sandbox. Sandbox stays in the list as a movement test room; the menu does not load it. |
| Quality | Default quality level is High, not Ultra, so modest PCs are not forced into the heaviest preset. |
| Display | Borderless fullscreen, native resolution, resizable window, Alt+Enter allowed. The game does not keep running when it loses focus. |
| Input | New Input System only. UI and gameplay both use it. |
| Saves | Company name is `TheLastAnswer`. Older Editor saves under `DefaultCompany` are not migrated. |
| Startup | Bootstrap loads the menu. Exit calls `Application.Quit` (in the Editor, Exit stops Play Mode). |
| Offline AI | Mock provider is the default and the fallback when Online cannot answer. |
| Missing assets | Scene validation tests require a SceneContext, one player, one default spawn, and doors that name a real scene and spawn. Run those tests before building. |
| Performance | The locations are small interior sets. High quality with the URP asset already on each quality tier is the intended PC setting. The Editor frame rate is not the build frame rate. |

This document does not claim a successful player build. Run section 2 and record the result before telling anyone the game is ready.
