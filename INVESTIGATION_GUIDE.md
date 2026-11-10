# Investigation logic: guide

How statements, facts and contradictions work in THE LAST ANSWER, and how to try the three test scenarios.

## Contents

1. [Try the scenarios](#1-try-the-scenarios)
2. [How it fits together](#2-how-it-fits-together)
3. [Data: facts and statements](#3-data-facts-and-statements)
4. [The contradiction checker](#4-the-contradiction-checker)
5. [Challenging a suspect](#5-challenging-a-suspect)
6. [The notebook](#6-the-notebook)
7. [Adding statements](#7-adding-statements)
8. [Testing](#8-testing)
9. [Known limits](#9-known-limits)

---

## 1. Try the scenarios

Start a **New Investigation**. Marlowe's statement to the officers, *"I was at home, alone, all evening"*, is in the notebook from the start.

| Scenario | Steps | Result |
|---|---|---|
| **Not proven yet** | Collect the CCTV still. In Interview Room 1, press **CHALLENGE STATEMENT**, pick *"I was at home…"*, then pick the CCTV still | Amber banner: the face is hidden, so it proves nothing yet. Marlowe evades. Nothing changes |
| **Invalid accusation** | Challenge *"I was at home…"* with the **coffee cups** | Grey "CHALLENGE FAILED" banner. Marlowe brushes it off and gains composure. The failed challenge is counted |
| **Valid contradiction** | Collect and examine the case file, then collect the access log. **Connect** the CCTV still with the access log in the evidence screen (I). Now challenge *"I was at home…"* with the CCTV still, or just **present** it | Red "CONTRADICTION EXPOSED" banner. Marlowe admits he went to the office. The statement is marked **exposed**, and the fact *"Marlowe was at the office that night"* is confirmed |
| **Unresolved statement** | Ask *"Why didn't you answer your phone?"* | *"My phone was switched off"* is recorded. Nothing in the case contradicts it: every comparison says "no contradiction", and it stays **unresolved** |

You can test any comparison in the notebook first (**J** → STATEMENTS → COMPARE WITH EVIDENCE). It's free there; only challenges made in an interview count.

## 2. How it fits together

```
SuspectStatement + ContradictionRule  (authored data: who said what, what can prove it false)
CaseFact                              (authored: description, source, reliability, related evidence/statements)
        │
        ▼
InvestigationService  ── statements heard, their status, observations, progress, saving
        │  uses
        ▼
ContradictionChecker (pure C#)  ── statement + evidence → Valid / Unproven / Invalid / …
        │                           uses: collected evidence, confirmed facts (EvidenceService)
        ▼
DialogueManager → DialogueSession.ChallengeAsync(verdict) → provider words the reply
        │  only after the reply succeeds:
        ▼
InvestigationService.ApplyChallenge → statement exposed, fact confirmed, notebook note
```

**The AI can't invent contradictions:**
- The verdict is decided in C# before the suspect answers.
- The provider (mock or online) only receives the reply type: admission, evasion, or brushing it off. It can't change the outcome.
- Plain "present evidence" no longer exposes anything by itself in the game. It goes through the same checker.

| Piece | File |
|---|---|
| Fact data | `Scripts/Data/Evidence/CaseFact.cs` |
| Statement + contradiction rules | `Scripts/Data/Investigation/SuspectStatement.cs` |
| Checker | `Scripts/Systems/Investigation/ContradictionChecker.cs` |
| Statements, observations, progress, saving | `Scripts/Systems/Investigation/InvestigationService.cs` |
| Challenge reply types | `Scripts/Systems/Dialogue/ResponsePlanner.cs` (`PlanChallenge`), `DialogueSession.ChallengeAsync` |
| Wiring in interviews | `Scripts/Gameplay/Interview/DialogueManager.cs` |
| Notebook screen | `Scripts/UI/Investigation/NotebookScreen.cs` |
| Feedback banner | `Scripts/UI/Investigation/ContradictionFeedbackView.cs` |
| Interview challenge picker | `Scripts/UI/Dialogue/DialoguePanel.cs` |
| Test content | `Scripts/Editor/UI/InvestigationDataBuilder.cs` |

## 3. Data: facts and statements

**`CaseFact`** (Create → The Last Answer → Evidence → Case Fact):

| Field | Meaning |
|---|---|
| `factId` | Unique, stable id |
| `title`, `description` | What it says |
| `source` | Where it comes from, e.g. "Building access log", "Marlowe, confronted with the evidence" |
| `reliability` | Low, Medium or High |
| `relatedEvidence` | The items it relates to. `RelatedEvidenceIds` gives their ids |
| `relatedStatements` | The statements it supports or undermines |
| confirmed? | Player progress, not stored on the asset: `EvidenceService.IsFactEstablished`. A fact is confirmed by examining evidence, connecting evidence, or exposing a contradiction |

**`SuspectStatement`** (Create → The Last Answer → Investigation → Suspect Statement):

| Field | Meaning |
|---|---|
| `statementId`, `suspect` | Who said it |
| `topicId` | The interview topic that produces it. When the suspect answers that topic, the statement is recorded with their exact words |
| `claim` | The claim in their own words |
| `source` | Where it was said, e.g. "Statement to officers" or "Interview" |
| `recordedFromStart` | Known when the case opens |
| `contradictions` | Rules. Each one names the **evidence** that proves the claim false, the **facts that must be confirmed first**, an explanation, a hint for "not proven yet", and a fact to confirm when the suspect is confronted. No rules means it can't be contradicted |

## 4. The contradiction checker

`ContradictionChecker.Evaluate(statement, evidence)` always gives the same answer for the same situation:

| Outcome | When |
|---|---|
| **Valid** | The statement has a rule for this evidence, and every fact the rule requires is confirmed |
| **Unproven** | A rule exists for this evidence, but a required fact isn't confirmed yet. Shows the rule's hint |
| **Invalid** | No rule for this evidence: it doesn't contradict the statement |
| **AlreadyResolved** | The statement was already exposed |
| **NotRecorded** | The player hasn't heard the statement yet |
| **EvidenceNotCollected** | The player doesn't have the evidence |

## 5. Challenging a suspect

**In an interview, CHALLENGE STATEMENT:** pick one of this suspect's open statements, then a piece of evidence. Or just use **PRESENT EVIDENCE**: if the item validly contradicts something the suspect said, it counts as a challenge.

| Verdict | Suspect's reply | What changes (C#) |
|---|---|---|
| Valid | The topic's written `admissionAnswer`: a partial admission, never a confession. Composure −25 | Statement **exposed** and the rule's fact confirmed. Any other statement the same evidence validly contradicts is exposed too. A notebook note is added |
| Unproven | Evasive ("That could be anyone…"). Composure −3 | Nothing |
| Invalid | Brushes it off ("That doesn't contradict a word I've said"). Composure **+5** | Failed challenges +1 |

The outcome is applied only after the suspect's reply arrives, so a failed network reply changes nothing. The feedback banner shows the verdict and the explanation over the interview scene.

## 6. The notebook

Press **J**, or NOTEBOOK in the pause menu. It pauses the game.

- **Progress** (left column):
  - percentage investigated
  - evidence collected
  - facts confirmed
  - statements heard
  - contradictions exposed or still to confront
  - failed challenges
- **STATEMENTS:**
  - **The list:** who said what, with status UNRESOLVED, CONTRADICTION FOUND or EXPOSED.
  - **Details:** where it was said, their exact words, what contradicted it and why, and the related facts.
  - **COMPARE WITH EVIDENCE:** shows the checker's verdict inline. A valid comparison marks the statement *contradiction found*; you then confront the suspect with it.
- **FACTS:** confirmed facts with description, id, source, reliability, related evidence and related statements. Unconfirmed facts are counted but not shown.
- **OBSERVATIONS:** type your own notes and press ADD. Automatic case notes appear here too.

Statements, observations and failed challenges are saved with the game (save section `investigation`).

## 7. Adding statements

1. Create a `SuspectStatement`. Set the suspect, the `topicId` of the interview topic that produces it, and the claim.
2. Add rules for the evidence that can prove it false, with any facts that must be confirmed first.
3. Make sure the suspect's topic has an `admissionAnswer`. That's their line when the contradiction is valid. A test checks this.
4. Add the statement to the case's **Statements** list, and link it from related facts (`relatedStatements`).
5. Run the tests. `Case0147_Statements_AreConsistent` shows the checks to copy for your own case.

## 8. Testing

**`InvestigationScenarioTests`** (13 EditMode tests, on the real Case 0147 content):
- the three scenarios, plus "not proven yet"
- statements not heard yet, and evidence not collected
- comparing in the notebook (free) vs challenging in an interview (counted)
- determinism; finding a valid contradiction when evidence is simply presented
- saving and restoring
- the dialogue following the verdict, and presenting evidence not deciding contradictions on its own
- data consistency

**In game, through the interview panel and notebook UI:**
- not proven (amber banner, evasion)
- invalid (grey banner, Marlowe brushes it off, failed challenge counted)
- contradiction found in the notebook compare
- valid (presenting the CCTV exposed the statement, gave the admission and confirmed the fact)
- unresolved (the phone statement recorded with his words)

All 103 EditMode and 12 PlayMode tests pass.

## 9. Known limits

- **Only Marlowe has statements:** five of them.
- **Facts aren't used for decisions yet:** they're confirmed and shown, but endings and the final accusation come in a later stage.
- **Claims are written by hand:** the statement's claim is authored. The suspect's actual words (mock or AI) are stored as quotes but never used to decide anything.
- **The old auto-contradiction still exists outside the game:** a topic's `contradictedByEvidence` still works in stand-alone dialogue (`DetectContradictionsOnPresent`, on by default for tests). The game switches it off and uses the checker instead. Keep the two consistent when you write a suspect.
