# Case authoring guide

How cases work in THE LAST ANSWER, how progress is tracked, and how to write a new case without touching code.

## Contents

1. [What a case contains](#1-what-a-case-contains)
2. [Progression](#2-progression)
3. [Conditions](#3-conditions)
4. [Closing the case and endings](#4-closing-the-case-and-endings)
5. [Writing a new case, step by step](#5-writing-a-new-case-step-by-step)
6. [The test case: Silence on Harlow Street](#6-the-test-case-silence-on-harlow-street)
7. [Keeping the story consistent](#7-keeping-the-story-consistent)
8. [Testing](#8-testing)
9. [Known limits](#9-known-limits)

---

## 1. What a case contains

Everything is one **`CaseDefinition`** asset plus the assets it references. No case is written in code: the GameManager starts whichever case the player picks and resets every case system through `ICaseScopedService`.

| Part | Where | Notes |
|---|---|---|
| Case ID, number, title, location | CaseDefinition | `caseId` must never change after release (saves use it) |
| Summary + briefing | CaseDefinition | Summary: one or two lines. Briefing: what the detective is told |
| Crime information | `crime` | Type, date, time of death, location, cause, weapon, who found the victim, details |
| Victim | `victim` | Name, age, occupation, background |
| Suspects | `suspects` (`SuspectProfile`) | Personality and `SuspectKnowledge` for interviews |
| Witnesses | `witnesses` (`SuspectProfile`) | Same type. A person can be both a witness and a suspect |
| Timeline | `timeline` | Each entry is hidden until its `revealedBy` fact is confirmed (empty = known at the start) |
| Evidence | `evidence` (`EvidenceDefinition`) | See `EVIDENCE_GUIDE.md` |
| Facts | `facts` (`CaseFact`) | Description, source, reliability, related evidence and statements. See `INVESTIGATION_GUIDE.md` |
| Statements | `statements` (`SuspectStatement`) | Claims with contradiction rules |
| Objectives | `objectives` | Prerequisites + completion conditions (§2) |
| Possible conclusions | `conclusions` | What the player can decide when closing the case, usually who to charge |
| Ending conditions | `endings` | For each conclusion, endings checked in order (§4) |
| Writer's notes | `solutionNotes` | The hidden truth. **Never shown in game or sent to AI** |

## 2. Progression

**`CaseProgressService`** tracks, for the active case:

| Tracked | How |
|---|---|
| **Objectives completed** | Automatically, whenever their conditions hold. Re-checked when evidence, facts, statements, interviews or decisions change |
| **Evidence discovered** | From `EvidenceService` |
| **Suspects questioned** | When the player asks or shows something in an interview |
| **Contradictions identified** | From `InvestigationService`: found, exposed, failed challenges |
| **Important decisions** | `RecordDecision(id, choice, label)`. Closing the case records `conclusionDecisionId` |
| **Investigation status** | `Investigating` → `ReadyToConclude` (all required objectives done, except ones waiting for a decision) → `Concluded` |

**How objectives behave:**
- An objective appears once all its `prerequisites` are completed.
- The HUD shows the first active, unfinished, required objective (an optional one only if no required ones are left).
- Objectives completed out of order wait for their prerequisites, then the whole chain completes at once.
- A "Close the case" objective uses a `DecisionMade` condition, so it completes when the player closes the case.

Everything is saved with the game (save section `caseProgress`).

**In game:**
- **HUD:** the current objective, plus an "OBJECTIVE COMPLETE" notification when one completes.
- **Notebook (J):**
  - **CASE:** overview, status, crime, victim, suspects, witnesses, decisions.
  - **OBJECTIVES:** done, current, open and optional; hidden ones are only counted.
  - **TIMELINE:** revealed moments; unknown ones are only counted.
  - **STATEMENTS / FACTS / OBSERVATIONS:** as before.
- **CLOSE THE CASE:** the notebook button appears once the case's `conclusionUnlockedBy` conditions are met.

## 3. Conditions

Objectives, endings and the conclusion unlock all use the same **`CaseCondition`**. Set the type, then fill in only the field it uses:

| Type | Field | True when |
|---|---|---|
| EvidenceCollected | evidence | Collected or logged |
| EvidenceExamined | evidence | Examined |
| FactConfirmed | fact | Confirmed (examining, connecting, or exposing a contradiction) |
| StatementRecorded | statement | The suspect said it, or it was known from the start |
| StatementExposed | statement | Exposed by a valid challenge |
| SuspectQuestioned | suspect | Questioned in an interview |
| ObjectiveCompleted | id | That objective is complete |
| DecisionMade | id (+ choice) | That decision was recorded (with that choice, if set) |

All conditions in a list must hold. All of this is evaluated in C# from game state; AI is never involved.

## 4. Closing the case and endings

1. The player picks a **conclusion**, e.g. "Charge Elliot Marlowe with murder", and confirms it twice. The decision is final.
2. The endings belonging to that conclusion are checked **in list order**. The first whose conditions all hold is the ending. The one listed last should have no conditions: it's the fallback.
3. The **ending screen** shows the outcome (Solved, Partially Solved, Failed or Unsolved), the title, the epilogue and a summary of the investigation. The game moves to the Ending state; **Return to main menu** leaves it.

The validator requires every conclusion to have a fallback ending, and the case to have at least one Solved ending.

## 5. Writing a new case, step by step

1. **The Last Answer → Cases → New Case.** This creates `Data/Cases/Case1000/CASE_1000.asset` with Evidence, Facts, Statements and Suspects sub-folders, and adds it to the CaseCatalog. It starts **locked** (shown as "coming soon").
2. Fill in the overview, crime, victim and the **writer's notes**. Decide the truth first.
3. Create **suspect profiles** and their knowledge (`SuspectKnowledge`): topics, stances, answers and admission lines.
4. Create **evidence** and **facts**, then link them (`relatedFacts`, connections, discovery conditions).
5. Create **statements** with contradiction rules for the suspects' lies.
6. Add the **timeline** and the **objectives**.
7. Add the **conclusions** and **endings**, each conclusion with a fallback ending last. Set `conclusionUnlockedBy`.
8. Set **Start Scene** to an existing scene, and place pickups with `EvidencePickup` and an interview seat with the suspect.
9. **The Last Answer → Cases → Validate All Cases.** Fix everything it reports. The test `EveryCaseInTheCatalog_IsValid` runs the same check.
10. Tick **Is Available**. The case now appears in Case Files, and New Investigation uses the first available case.

## 6. The test case: Silence on Harlow Street

A complete case with one interviewable suspect, six pieces of evidence and nine objectives.

**The truth:** Elliot Marlowe killed his partner Daniel Reyes over the secret sale of the firm. See `solutionNotes` in `CASE_0147.asset`.

**Objectives:**

| # | Objective | Completes when | Needs |
|---|---|---|---|
| 1 | Review the case file | Case file collected | - |
| 2 | Search the Evidence Room | Photos and CCTV collected | 1 |
| 3 | Question Elliot Marlowe | Marlowe questioned | 1 |
| 4 | Find out who came in at 21:40 | Fact: the 21:40 visitor knew the code (connect the CCTV with the access log) | 2 |
| 5 | Break Marlowe's alibi | "I was at home…" exposed | 3, 4 |
| 6 | Find a motive | Fact: Marlowe stood to lose the firm (connect the sale agreement with the case file) | 1 |
| 7 | *Work out who Reyes was expecting* (optional) | Fact: Reyes knew his visitor (connect the photos with the cups) | 2 |
| 8 | *Make Marlowe admit he knew about the sale* (optional) | "The firm's finances are fine" exposed | 3, 6 |
| 9 | Close the case | Case closed | 3 |

**Conclusions and endings** (you can close the case once Marlowe has been questioned):

| Conclusion | Ending | Outcome | When |
|---|---|---|---|
| Charge Marlowe | The Last Answer | Solved | Alibi exposed, motive confirmed, and "Reyes had a visitor" confirmed |
| Charge Marlowe | A Thin File | Partially solved | Alibi exposed |
| Charge Marlowe | Released by Morning | Failed | Alibi never broken |
| Charge Kim | The Wrong Man | Failed | Always |
| Charge Hayes | The Wrong Witness | Failed | Always |
| Charge no one | An Open File | Unsolved | Always |

**Timeline:** 11 moments, from the 12th (Marlowe learns of the sale) to 00:06 (first officer on scene). Seven of them are revealed by facts.

## 7. Keeping the story consistent

The same facts appear in several places. When you change one, change them all:

| Fact | Appears in |
|---|---|
| Master code at 21:38; Hayes 17:58 / 23:47; Marlowe 18:40; alarm 19:10 | Access log pages · timeline · contradiction explanation · solution notes |
| Figure enters at 21:40 | CCTV pages · timeline · Marlowe's admission ("twenty to ten") · statements |
| Found 23:50, called 23:52 | Briefing · case file · crime record · timeline |
| The sale to Calder Holdings, learned on the 12th | Sale agreement · Marlowe's finances admission · timeline · solved epilogue |
| Time of death 21:30–22:30 | Crime record · Hayes ending (rules her out) |
| Kim's card never used | Access log · Kim ending |
| Marlowe is the killer | Solution notes · his death topic is a lie, not "unknown" · the only Solved ending charges him |

The test `StoryFacts_AgreeAcrossEvidenceDialogueTimelineAndCrime` checks these automatically.

**Changes made in this stage for consistency:**
- Marlowe's "I don't know how he died" is now a lie he keeps up, not a true unknown.
- His whereabouts truth now records the killing.
- The case file's one-off objective was removed, because objectives now drive the HUD.

## 8. Testing

**`CaseProgressTests`** (16 EditMode tests) cover:
- every case in the catalog is valid, and the validator catches mistakes
- the first objective, and objectives completing and unlocking from game state
- out-of-order objectives
- reaching "ready to conclude"
- every ending: solved, weak case, collapsed, wrong person (Kim, Hayes), no charge
- closing the case before it's allowed
- saving and restoring
- story consistency

**Played in game:**
- objectives completing as evidence was collected, with the HUD following
- the notebook's Case, Objectives and Timeline pages
- questioning and exposing Marlowe
- "Ready to close"
- Close the Case → Charge Marlowe → confirm → **CASE SOLVED: The Last Answer**
- returning to the main menu

All 119 EditMode and 12 PlayMode tests pass.

## 9. Known limits

- **Only Marlowe can be interviewed.** Hayes and Kim are suspects you can charge, but they have no interviews or statements.
- **One location set:** all evidence is in the Evidence Room, and every case uses the same scenes. A new case can use new scenes through its Start Scene and scene doors.
- **Decisions are final:** there's no "reopen the case". A new investigation starts over.
- **The case file's own text:** Case Files on the main menu still shows the briefing and suspects. The richer details (crime, victim, timeline) are in the in-game notebook.
