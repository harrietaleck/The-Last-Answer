# The Last Answer — manual playtest checklist

Use this after a code change and again on a Windows build started **outside** the Unity Editor. Play with the network disconnected at least once. Default dialogue mode is **Offline (scripted)** (`GameConfig` AI mode Mock). Online mode is optional. If the backend is missing, the interview falls back to the scripted replies.

Mark each line pass or fail. A failed line is a defect, not a note.

## Startup and main menu

- [ ] Launch from Bootstrap (Editor) or the `.exe` (build). The Unity splash may appear, then the main menu. The screen is not stuck black.
- [ ] The menu shows New Investigation, Continue Investigation, Load Investigation, Case Files, Settings, and Exit Game.
- [ ] Click the **empty part** of New Investigation, not only the letters. The police station loads and you can look around.
- [ ] Continue is disabled, with “No saved investigation”, when no save exists. After a save, Continue loads that investigation.
- [ ] Load Investigation lists the slots. Back returns to the menu. Loading an empty slot does nothing harmful.
- [ ] Case Files lists Case 0147 (Silence on Harlow Street) as open and any other case as classified. Open Investigation starts 0147. Back returns to the menu.
- [ ] Settings opens and Back returns. Exit stops Play Mode in the Editor, and closes the window in a build.
- [ ] Mouse, keyboard, and gamepad (if connected) can move the highlight. Enter or Space activates the highlighted row.

## Display, input, and audio

- [ ] Settings → Display: Fullscreen, Borderless, and Windowed all apply. Resolution and quality apply. V-Sync toggles. The window can be resized in Windowed mode.
- [ ] Field of view and brightness change what you see. Mouse sensitivity and Invert Look change aiming.
- [ ] Master, Music, Effects, and Ambience sliders change volume, including the UI click. Reset to defaults restores the sliders.
- [ ] In the station you hear a room tone. Footsteps play while walking. There is one audio listener (no doubled sound).
- [ ] Esc pauses. The mouse is free. Resume returns to the investigation with the mouse captured. Esc does not pause the main menu.

## Movement and interaction

- [ ] WASD moves. Shift runs. C or Left Ctrl crouches, and you cannot stand into a ceiling. There is no jump.
- [ ] Mouse look stays level (does not flip over). The crosshair stays centred.
- [ ] Looking at a door shows a prompt. E uses it. Walls block interaction with things behind them. Out-of-range objects show no prompt.

## Case 0147 flow

- [ ] New Investigation starts at the police station, objective “Review the case files in the Evidence Room.”
- [ ] The evidence-room door loads the evidence room at the arrival spawn, not inside a wall.
- [ ] Case files and physical evidence can be inspected and collected. The evidence screen lists what you collected. Examine and Connect do not throw errors.
- [ ] The interrogation-room door loads the interview room. You can sit and start an interview.
- [ ] Asking a question, using a suggested question, presenting evidence, and challenging a statement all produce a reply **with Wi-Fi off**.
- [ ] A matching contradiction is called out. Repeating the same evidence does not break the interview. Ending the interview returns you to investigation.
- [ ] The notebook opens. Observations can be added. When the case is ready, Close the Case shows endings and confirms one.
- [ ] The ending screen returns to the main menu. Continue or Load restores the case, evidence, and room you saved in.

## Save, load, and exit

- [ ] Save from the pause menu writes a slot. The file is under `%USERPROFILE%\AppData\LocalLow\TheLastAnswer\The-Last-Answer\Saves`.
- [ ] Quit to the main menu, then Continue, and you are back in the same room with the same evidence.
- [ ] Alt-F4 or Exit closes the build without leaving a Unity process running.
- [ ] Saves made before the company name was `TheLastAnswer` live under `DefaultCompany` and are not picked up. That is expected.

## What this checklist does not prove

Automated Edit Mode tests cover state rules, saves, evidence, contradictions, scene wiring, and offline dialogue. They do not prove mouse feel, audio mix, lighting, or that a built `.exe` launches. Do not call the game release-ready until the Windows build section of `BUILD_AND_RELEASE_GUIDE.md` has been run on a machine that is not the Editor.
