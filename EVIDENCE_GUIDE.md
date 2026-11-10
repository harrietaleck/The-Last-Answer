# Evidence system: guide

How evidence works in THE LAST ANSWER, how to test it right now, and how to add your own.

## Contents

1. [Try it](#1-try-it)
2. [How it fits together](#2-how-it-fits-together)
3. [Evidence data](#3-evidence-data)
4. [The player's progress](#4-the-players-progress)
5. [Screens](#5-screens)
6. [Adding evidence](#6-adding-evidence)
7. [The test evidence (Case 0147)](#7-the-test-evidence-case-0147)
8. [Testing](#8-testing)
9. [Known limits](#9-known-limits)

---

## 1. Try it

1. Press Play in the **Bootstrap** scene, then **New Investigation**, and walk into the **Evidence Room**.
2. Look at the stack of folders on the desk and press **E**: the case file opens in the inspect view.
   - **NEXT / PREVIOUS** turn the pages.
   - **EXAMINE** reveals a detail and establishes facts.
   - **COLLECT** takes it.
3. Try the **filing cabinet** before and after examining the case file. It's locked until you know what to look for.
4. Press **I** at any time to open the evidence inventory. The pause menu also has an **EVIDENCE** button. Select an item to see its details.
5. **CONNECT** two related items: select one, press CONNECT, then click the other. Try the CCTV still with the building access log.
6. In the interview with Marlowe, **PRESENT EVIDENCE** shows him an item. Its status then reads "Shown to Elliot Marlowe".

## 2. How it fits together

```
EvidenceDefinition / CaseFact (ScriptableObjects, authored)
        │  listed in CaseDefinition.evidence / .facts
        ▼
EvidenceService (C#: the ONLY place rules live)
  discover · collect · examine · present · connect · facts · save/load
        │  events: EvidenceStateChanged, FactEstablished, EvidenceConnected
        │  collected items also go to CaseSessionService (HUD counter, notebook, interview picker)
        ▼
EvidencePickup (world)        EvidenceInspectScreen (UI)        EvidenceScreen (UI inventory)
```

- **Evidence is controlled by the game, never by AI.** Items, facts, conditions and connections are authored data. Only `EvidenceService` changes their state. The dialogue AI only learns which items were presented, and can't award, invent or reveal evidence.
- **UI and gameplay stay separate.** World objects and screens only call `EvidenceService` and listen to its events.

| Piece | File |
|---|---|
| Evidence data | `Scripts/Data/Evidence/EvidenceDefinition.cs` |
| Fact data | `Scripts/Data/Evidence/CaseFact.cs` |
| Rules, state, saving | `Scripts/Systems/Investigation/EvidenceService.cs` |
| Events | `Scripts/Systems/Investigation/InvestigationEvents.cs`, `Scripts/Systems/UIIntentEvents.cs` |
| World object | `Scripts/Gameplay/Investigation/EvidencePickup.cs` |
| Inspect view | `Scripts/UI/Evidence/EvidenceInspectScreen.cs` |
| Inventory + detail view | `Scripts/UI/Evidence/EvidenceScreen.cs` |
| Pauses while open, hides pause menu | `Scripts/UI/Components/UIOverlay.cs` (`ModalScreen`) |
| Test content generator | `Scripts/Editor/UI/EvidenceDataBuilder.cs` |

## 3. Evidence data

**`EvidenceDefinition`** (Create → The Last Answer → Evidence → Evidence)

| Field | Purpose |
|---|---|
| `evidenceId` | Stable id (e.g. `EVD_CCTV`), used by saves and suspect reactions. Never change it after release |
| `displayName`, `description` | Shown in the list and detail view |
| `category` | Document, Photograph, Object, Digital or Forensic. Decides how the inspect view looks: paper, screen or photo frame |
| `locationFound` | e.g. "Evidence Room · filing cabinet" |
| `interactionPrompt` | World prompt, e.g. "Read case file" → `[E] Read case file` |
| `pages` | Text pages (documents and digital). Optional for photos and objects |
| `image`, `caption` | Photo or object image (optional: a placeholder frame is shown without one) |
| `examinationNotes` | Hidden until the player examines the item |
| `relatedFacts` | Facts established when examined |
| `collectable` | Ticked: taken from the scene. Unticked: logged but left in place (a wall board, a laptop) |
| `noteOnCollect`, `objectiveOnCollect` | Optional notebook entry or objective change when collected |
| `discovery` | Discovery conditions: items that must be collected or examined first, and facts that must be established. Choose *hidden* until then, or show a locked prompt and hint |
| `connections` | Items it can be connected with, plus the explanation and the fact established. Define each link on one side only |

**`CaseFact`** (Create → The Last Answer → Evidence → Case Fact): `factId`, `title`, `description`, `source`, `reliability`, related evidence and statements, `isKeyFact`. See `INVESTIGATION_GUIDE.md`.

Both must be listed on the case (`CaseDefinition → Evidence / Facts`). Only items listed there can be collected.

## 4. The player's progress

Each item tracks: **discovered**, **collected**, **examined** and **presented to** (suspect ids). The service also tracks **established facts** and **connections made**.

| Action | Rule |
|---|---|
| Discover | Opening it in the world. Only once its discovery conditions are met |
| Collect | Needs the conditions met. Adds it to the inventory, the HUD counter and the interview picker |
| Examine | Needs it discovered. Reveals the notes and establishes its facts, once |
| Present | Recorded after the suspect answers in an interview |
| Connect | Both items must be collected, and only authored links succeed. Establishes the link's fact. Wrong pairs just get "don't seem connected", with no penalty |

All of this is saved with the game (save section `evidence`). Saves from before this system keep their collected items.

## 5. Screens

- **Inspect view:** category, title and location, then the content:
  - **Document:** paper pages.
  - **Digital:** a screen.
  - **Photo / object:** an image frame.

  Below the content come the examination notes and **COLLECT** (or **LOG AS EVIDENCE**), **EXAMINE** and **CLOSE**. It opens from the world or from **VIEW** in the inventory.
- **Inventory (I):**
  - **Left:** collected items, newest first, with a category filter and tags: **NEW** (not examined), **EXAMINED**, **SHOWN**.
  - **Right:** the detail view, with status, examination notes, established facts (unestablished ones are counted but not spoiled) and connections made.
  - **Buttons:** VIEW · EXAMINE · CONNECT · BACK.
- **Pausing:** both screens pause the game. Opened during play, closing resumes it; opened from the pause menu, closing returns there. Esc always resumes.
- **Notifications:** EVIDENCE ADDED, NEW FACT, CONNECTION MADE, and NOTHING YET (locked item).

## 6. Adding evidence

1. Create the asset(s) and fill in the fields above. Add them to the case's **Evidence** (and **Facts**) lists.
2. In a scene, add **`EvidencePickup`** to an object with a collider and assign the evidence.
   - For a collectable item, the object's renderers hide once it's taken. Untick **Hide On Collect** when it comes out of a container that stays, like a cabinet.
3. Optional: give suspects a reaction in their knowledge asset (`evidenceReactions`). Or list the id in a topic's `contradictedByEvidence` with an `admissionAnswer`, so presenting it forces a partial admission.
4. Run the tests. `Case0147_EveryItemCanBeReached_AndEveryConnectionMade` is a good model for checking your own case: it fails if an item can never be found.

> The Evidence Room is generated by code. If you rebuild it (**The Last Answer → Environment → Rebuild All Locations**), add your pickups in `EvidenceRoomBuilder` too, or place them in a scene that isn't generated.

## 7. The test evidence (Case 0147)

Six items in the Evidence Room. They're created only if missing, so your edits are never overwritten. Fictional content.

| Item | Category | Where | Unlocks when | Kept / taken |
|---|---|---|---|---|
| Case file 0147 | Document (3 pages) | Folders on the desk | Start | Taken |
| Crime scene photographs | Photograph | Evidence board | Start | Logged |
| CCTV still, 21:40 | Digital | Laptop | Start | Logged |
| Building access log | Document | Filing cabinet | Case file **examined** | Taken (cabinet stays) |
| Two coffee cups | Object | Tray 0147-C, top shelf | Photos **examined** | Taken |
| Draft sale agreement | Document (2 pages) | Open folder on the desk | Case file **collected** | Taken |

**Connections:**
- CCTV + access log → *the 21:40 visitor knew the code*.
- Photos + cups → *Reyes knew his visitor*.
- Sale agreement + case file → *Marlowe stood to lose the firm*.

There are 10 facts in all.

**Marlowe's reactions:** he has new reactions to the log, the cups and the agreement. Presenting the **sale agreement** now forces a partial admission about the firm's sale, like the CCTV does for his whereabouts.

## 8. Testing

- `EvidenceServiceTests` (12 EditMode tests) cover:
  - collecting (with note and objective), and items outside the case
  - discovery conditions; examining and facts
  - presenting
  - connecting (wrong pairs, either side, once)
  - save/load, old saves, a new case
  - the authored Case 0147 data: consistency, and that every item can be reached and every fact established.
- Verified in game:
  - locked items, and inspecting, paging, examining and collecting
  - collectable objects disappearing while fixed ones stay
  - the inventory, and connecting through the UI
  - presenting the agreement to Marlowe, which records "shown"
  - returning to play each time.

## 9. Known limits

- **No real art:** photos and objects show a placeholder frame until you assign images.
- **No 3D inspection:** objects aren't rotated in 3D in the inspect view.
- **Facts are a starting point:** they're established and saved, but contradictions, accusations and endings will use them in a later stage.
- **Only Case 0147 has evidence,** all of it in the Evidence Room.
