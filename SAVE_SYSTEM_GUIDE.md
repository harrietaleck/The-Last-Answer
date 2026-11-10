# Save system: guide

How THE LAST ANSWER saves and loads, what is stored, and how it protects against damaged files.

## Contents

1. [For players](#1-for-players)
2. [What is saved](#2-what-is-saved)
3. [The save file](#3-the-save-file)
4. [Reliability](#4-reliability)
5. [Versioning and migration](#5-versioning-and-migration)
6. [Adding a system to saves](#6-adding-a-system-to-saves)
7. [Testing](#7-testing)
8. [Known limits](#8-known-limits)

---

## 1. For players

- **Save:** pause (Esc) → **SAVE INVESTIGATION**, then pick Slot 1–3. Overwriting a slot needs a second press.
- **Load:**
  - **CONTINUE INVESTIGATION** on the main menu loads your newest save, autosave or manual. The line under it says which save and from when.
  - **LOAD INVESTIGATION**, on the main menu or in the pause menu, lets you pick any save. Loading over a game in progress needs a second press.
- **Autosave:** made automatically when you arrive in a new location, complete an objective, or close the case. It's its own slot and never overwrites yours.
- **Slot details:** each slot shows the case, current objective, evidence collected, location, status (investigating, ready to close, closed + ending), when it was saved, and time played.
- **Damaged saves** are shown as damaged and can't be loaded. If the previous version of that file is intact, it's loaded instead and the slot says *restored from backup*.
- **Delete:** select a save and press DELETE twice.

Files live in `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\Saves\`: `save_0.json` to `save_2.json` (slots 1–3), `save_auto.json`, plus a `.bak` backup of each.

## 2. What is saved

| Requirement | Section | System |
|---|---|---|
| Current case | `caseSession` | `CaseSessionService`: case id, objective, collected evidence list, notes |
| Current scene | header `sceneName` | `GameManager` |
| Player position | `playerLocation` | `PlayerLocationService` + `PlayerSaveLocator` on the player |
| Collected and examined evidence, presented, connections, facts | `evidence` | `EvidenceService` |
| Investigation notes and observations, statements heard, contradictions, failed challenges | `caseSession` + `investigation` | `CaseSessionService`, `InvestigationService` |
| Conversation history | `interviews` | `InterviewService`: every line of every interview |
| Suspect interaction state | `interviews` | Composure, topics asked, topics exposed, evidence shown, line variety counters |
| Completed objectives, suspects questioned, play time | `caseProgress` | `CaseProgressService` |
| Important decisions | `caseProgress` | Decision records, including the case conclusion |
| Ending status | `caseProgress` | Status, conclusion and ending. Loading a closed case shows its ending screen again |

**Never saved:**
- API keys or any credentials. The online backend URL lives in GameConfig, never in saves, and `SaveFiles_ContainNoCredentials` checks this.
- Unity object references (saves contain ids and plain values only).
- Player settings, which go in `settings.json`, separate from saves.

## 3. The save file

```json
{
  "saveVersion": 2,
  "gameVersion": "1.0",
  "savedAtUtc": "2026-11-10T18:01:12.3Z",
  "sceneName": "InterrogationRoom",
  "checksum": "d1c33ca6…",
  "summary": { "caseId": "CASE_0147", "objective": "…", "evidenceCollected": 3, "evidenceTotal": 6, "playTimeSeconds": 38 },
  "sections": { "caseSession": {…}, "evidence": {…}, "investigation": {…}, "caseProgress": {…}, "interviews": {…}, "playerLocation": {…} }
}
```

- **checksum:** SHA-256 of the sections. It detects damaged, incomplete or edited files.
- **summary:** what the slot list shows, read without loading the save.

## 4. Reliability

| Risk | Protection |
|---|---|
| Crash or power cut while saving | Written to a temporary file, flushed to disk, then swapped in. The old file is kept as `.bak` |
| Truncated, garbled or empty file | Never crashes. Reported as damaged, and the backup is tried |
| Edited or incomplete file | Checksum mismatch → treated as damaged → backup tried |
| A system fails while restoring | **All-or-nothing:** every system is rolled back to its state before the load, the load is refused and the game carries on unchanged |
| A system fails while saving | The save is aborted and the previous file is left untouched |
| Save from a newer game version | Refused with a clear message |
| Save from an older version | Migrated (§5) |
| Nonsense values (e.g. negative counts, NaN position) | Clamped or ignored. A bad position falls back to the scene's spawn point |
| Tampered JSON creating .NET types | `TypeNameHandling.None`, and a nesting limit |
| Saving at a bad moment | Not allowed during scene loading, in the main menu, or with no case |

## 5. Versioning and migration

- `SaveData.CurrentSaveVersion` is **2**:
  - **Version 1:** header + sections.
  - **Version 2:** adds the checksum, the summary, and the interview and player-location sections.
- **Loading a version 1 file:** `SaveMigrations.MigrateToCurrent` upgrades it in memory. Interviews and position, which weren't saved back then, start fresh.
- **When you change the save format:**
  1. Bump `CurrentSaveVersion`.
  2. Add a step to `SaveMigrations`.
  3. Add a test with a file in the old format, like `Version1Save_IsMigratedAndLoads`.

Section keys (`SaveKey`) must never be renamed after release.

## 6. Adding a system to saves

1. Implement `ISaveable` with a unique `SaveKey`, `CaptureState()` returning plain data, and `RestoreState(JToken)`. `RestoreState` should accept `null` and bad values without throwing, though a throw is safe too: it triggers rollback.
2. Register it in `GameManager.CreateServices` with `Saves.Register(...)`. Systems are restored in registration order, so register a system after the ones it depends on.
3. If its state belongs to a case, also implement `ICaseScopedService` so a new investigation resets it.
4. Add it to `SaveRestartTests`.

## 7. Testing

- **`SaveServiceTests`** (17):
  - save/load, file names, empty slot, corrupted file, newer version, slot range, delete
  - fallback to the backup; checksum rejecting an edited file
  - six kinds of garbage that never throw
  - rollback when a system fails
  - version 1 migration
  - slot info, autosave and Continue, and a failed save leaving the previous one intact
- **`SaveRestartTests`** (4), with real files on disk:
  - **Save, close, reopen, load:** plays part of Case 0147, saves, throws away every service, builds brand-new ones from the same folder as a fresh launch would, loads, and checks everything listed in §2.
  - A closed case keeps its ending.
  - A file cut in half on disk loads its backup.
  - Save files contain no credentials.
- **In the Editor:**
  - **Session 1:** new investigation; collected three items; questioned Marlowe; saved to Slot 1 from the pause menu.
  - **Closing:** stopped Play mode, which wipes all code state, like closing the game. Checked the files on disk.
  - **Session 2:** started again. Continue showed "Find out who came in at 21:40 · slot 1, 10 Nov, 18:01" and loaded it. The scene, position and facing, evidence, observation, objectives, Marlowe's 5 conversation lines and his composure all came back, and the conversation replayed when sitting down again.

All 136 EditMode and 12 PlayMode tests pass.

## 8. Known limits

- **Mid-interview saves:** you reload standing in the interview room, next to the chair. The conversation is intact; sit down to continue.
- **Not tested in a built game:** the restart test used Play mode in the Editor. The file code is the same in a Windows build, but it hasn't been run in one.
- **Three manual slots plus one autosave:** change `GameConfig → Save → Slot Count` for more.
- **No cloud saves:** the storage is behind `ISaveStorage`, so Steam Cloud or similar can be added later without touching game code.
