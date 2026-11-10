# THE LAST ANSWER — UI & Dialogue Guide

Covers **Stage 4 (Main menu and game UI)** and **Stage 5 (Suspect dialogue system)**: what each piece does, how they connect, and how to extend them.

---

## Contents
1. [The big idea: UI and gameplay never touch](#1-the-big-idea-ui-and-gameplay-never-touch)
2. [Screens](#2-screens)
3. [Design system: theme and reusable components](#3-design-system-theme-and-reusable-components)
4. [Responsive layout](#4-responsive-layout)
5. [Connecting the UI to game systems](#5-connecting-the-ui-to-game-systems)
6. [The dialogue system](#6-the-dialogue-system)
7. [Writing a suspect](#7-writing-a-suspect)
8. [Switching to an online AI later](#8-switching-to-an-online-ai-later)
9. [Testing](#9-testing)
10. [Known limits and next steps](#10-known-limits-and-next-steps)

---

## 1. The big idea: UI and gameplay never touch

```
 GAMEPLAY / SYSTEMS                     EventBus                         UI
 ──────────────────                     ────────                         ──
 CaseSessionService ── EvidenceAdded, ObjectiveChanged, NoteAdded ──▶ HUD, notifications, notebook
 GameStateManager   ── GameStateChanged ─────────────────────────────▶ HUD / pause / dialogue visibility
 DialogueManager    ── DialogueLine, SuggestedQuestions,
                       SuspectThinking, DialogueError ───────────────▶ Dialogue screen
 InterviewSeat      ── InterviewStarted / InterviewEnded ────────────▶ Dialogue screen
        ▲
        └──── PlayerQuestionSubmitted, PlayerPresentedEvidence,
              InterviewLeaveRequested, TextInputFocusChanged ◀─────── Dialogue screen
```

- **Systems publish facts** ("evidence was added"). **The UI listens** and redraws.
- **The UI publishes requests** ("the player asked X"). **Systems decide** what happens.
- UI scripts read data (e.g. `CaseSessionService.Evidence`) but **never change game state**, except by calling high-level commands such as `GameManager.StartNewGame()` from menus.

That's why the dialogue screen didn't change when the temporary demo responder was replaced by the real dialogue system, and why it won't change when an online AI replaces the mock.

---

## 2. Screens

| Screen | Where | Contents |
|---|---|---|
| **Main menu** | `MainMenu` scene (`MainMenuUI`) | New Investigation · Continue Investigation (save slot 0, disabled without a save) · Case Files · Settings · Exit Game |
| **Case files** | `MainMenu` scene | Case list (from the `CaseCatalog`) with the selected file: number, title, location, status, briefing, persons of interest. **Open Investigation** starts that case; locked cases show *Classified / Coming soon* |
| **HUD** | `UI_GameRoot` (Bootstrap) | Top-left: **current case** + **current objective** (a red NEW tag when it changes). Top-right: **evidence counter** (3 / 6). Centre: crosshair + **interaction prompt**. Bottom-left: key hints. Shown only while investigating |
| **Notifications** | `UI_GameRoot` | Stack under the counter: *EVIDENCE ADDED*, *NOTEBOOK UPDATED*, *INVESTIGATION SAVED*, or any `NotificationEvent` |
| **Dialogue** | `UI_GameRoot` | Right-hand column over the cinematic camera: **suspect profile** (initials portrait, name, age · occupation, status, relation, summary), **conversation history** (scrolls), "*Marlowe is thinking*" indicator, **suggested questions**, **free-text input** + Ask (Enter also sends), validation/error line, **Present Evidence** picker, **End Interview** |
| **Pause + notebook** | `UI_GameRoot` | Esc (or **J** to open on the notes). Left: Resume · Settings · Save Investigation · Main Menu · Exit Game. Right: **Investigation Notes**: case, objective, evidence list (newest first, with descriptions) and notebook entries, including interview statements |
| **Settings** | `UI_GameRoot` (shared by menu and pause) | Controls: mouse sensitivity, invert look. Audio: master volume. Display: display mode, resolution, graphics quality (Low/Medium/High URP tiers), V-Sync, field of view, brightness. Reset to defaults · Back. Changes apply live and save when you leave |

Visual direction followed: charcoal/black panels, white type, **red only for attention** (selected button bar, new evidence, the suspect's name, errors), short fades, and no looping animation except the "thinking" pulse.

---

## 3. Design system: theme and reusable components

- **`Data/Settings/UITheme.asset`**: every colour, the font, text sizes and fade speed. Change the accent colour here and rebuild to restyle everything.
- **Component prefabs** (`Prefabs/UI/Components/`). Screens are built from *instances* of these, so editing a prefab restyles every use:

| Prefab | Script | Used for |
|---|---|---|
| `UI_MenuButton` | `UIButtonStyle` | Big text buttons (menu, pause) with a red bar on hover/selection |
| `UI_ActionButton` | `UIButtonStyle` | Boxed buttons: ASK, BACK, END INTERVIEW, PRESENT EVIDENCE |
| `UI_ListButton` | `UIButtonStyle` | Full-width rows: suggested questions, case list, evidence picker |
| `UI_SliderRow` | `UISliderRow` | Numeric settings: `Bind(title, min, max, value, format, onChanged)` |
| `UI_OptionStepper` | `UIOptionStepper` | Choice settings: `‹ VALUE ›` |
| `UI_ListEntry` | `UIListEntry` | Title + body rows (evidence, notes) |
| `UI_Toast` | `NotificationToast` | One notification card |
| `UI_DialogueEntry` | `DialogueEntryView` | One conversation line, styled by speaker |

- **`UIScreen`** is the base for any show/hide screen: a fade, click blocking, and a first selected element for keyboard navigation.
- **`UIList<T>`** reuses row objects instead of destroying and re-creating them on every refresh.

**Regenerate:** *The Last Answer → UI → Rebuild UI*. It overwrites the component prefabs, `UI_GameRoot`, the MainMenu scene and the Bootstrap UI, but leaves location scenes alone. Once you hand-tune a screen, stop rebuilding it (same rule as the environment generator).

---

## 4. Responsive layout

- Every canvas uses **Scale With Screen Size**, reference **1920×1080**, match **0.5**. Text and spacing scale evenly on 1280×720, 1366×768, 1600×900, 1920×1080, 2560×1440, 3840×2160 and also 16:10 / 21:9.
- Large regions are anchored to **fractions of the screen**: the dialogue column is the right 38%, the pause actions the left 30%. Wide monitors give the 3D view more room instead of stretching panels.
- HUD elements are pinned to **corners** with fixed margins, so they never drift into the centre on ultrawide screens.
- **To check:** in the Game view, pick 1280×720, 1920×1080 and 2560×1080 from the resolution dropdown and open each screen.

---

## 5. Connecting the UI to game systems

### Show something on the HUD
Call the case session from any gameplay code. The HUD, notifications and notebook update by themselves:
```csharp
var game = Services.Get<GameManager>();
game.Case.AddEvidence("EVD_Glove", "Bloody glove", "Found under the desk.");  // counter + toast + notebook
game.Case.SetObjective("Compare the glove with the suspects' hands.");     // HUD objective + NEW tag
game.Case.AddNote("The glove is a left hand.", "Crime scene");             // notebook
EventBus.Publish(new NotificationEvent("LEAD", "Hayes lied about the time.")); // generic toast
```
For level design without code, use the **`InvestigationTrigger`** component: an E-interactable with Evidence / Note / New Objective fields. The Evidence Room's board, case files and laptop use it.

### Start a case
`game.StartNewGame(caseDefinition)` (or with no argument for the first available case). Add new cases to **`Data/Cases/CaseCatalog.asset`**; Case Files lists them automatically.

### Open an interview
Put an **`InterviewSeat`** on a chair, assign a **`SuspectProfile`**, a `CinematicShotDirector` and a stand point, and add a **`DialogueManager`** to the scene. Sitting down publishes `InterviewStartedEvent`: the dialogue screen opens and the manager starts the conversation.

### Add a new screen
1. Create a UI object under `UI_GameRoot` (or your own canvas), add a `CanvasGroup` and a script deriving from **`UIScreen`**.
2. Build its contents from the component prefabs.
3. In the script, subscribe to the events you need in `OnEnable` and unsubscribe in `OnDisable`. To change the game, publish a request event or call `GameManager`.

### Rule of thumb
If you're about to write `FindObjectOfType<SomeUIScript>()` from gameplay code, publish an event instead.

---

## 6. The dialogue system

### Pipeline (every question)
```
"Where were you that night?"
   │
   ▼ ① IntentMatcher         topic? follow-up? challenge? accusation? greeting? unknown?
   ▼ ② ResponsePlanner        C# RULES decide WHAT the suspect does: answer per stance (truth / partial /
   │                          lie / deflect / unknown), repeat, follow-up, pressured answer, admission
   │                          when contradicted, denial; and how composure changes
   ▼ ③ DialogueContext        suspect + knowledge + question + plan + history + evidence shown + composure
   ▼ ④ IAIProvider            WORDS only (MockAIProvider now; OnlineAIProvider via your backend; mock = fallback)
   ▼ ⑤ DialogueSession        applies the state change, records history
   ▼ ⑥ DialogueManager        publishes lines, suggestions, errors → dialogue screen; notes → notebook
```

### Classes
| Class | Role |
|---|---|
| `IAIProvider` | `Task<AIResponse> GenerateResponseAsync(DialogueContext, CancellationToken)` |
| `MockAIProvider` | Offline. Picks authored lines for the plan; tone from temperament; stage directions from composure; variants so lines don't repeat; fake thinking delay |
| `OnlineAIProvider` | Posts the context to **your backend** (`GameConfig → AI → Backend Base Url`) and returns its words |
| `AIProviderFactory` | The only place a provider is chosen (from the player's AI Mode setting) |
| `DialogueManager` | Scene component: events ↔ session; timeout; mock fallback; replays history when you return; writes notes |
| `DialogueSession` | One suspect's interview state and history (pure C#, unit-tested) |
| `DialogueContext` | Everything a provider may use for one reply |
| `DialogueMessage` | One line of history (speaker, text, topic) |
| `IntentMatcher` | Free text → intent (keyword scoring; suggestions map directly to topics) |
| `ResponsePlanner` | The rules (composure costs are constants at the top) |
| `SuggestionBuilder` + `SuggestedQuestion` | Which questions to offer next (unasked, unlocked, by priority) |
| `SuspectProfile` / `SuspectKnowledge` | Data: who they are / what they know, hide and lie about |

### Behaviour the rules guarantee
- **No automatic confessions.** Accusations always get denials. "Confess" gets a refusal. The strongest reply is a writer's *partial admission*, and only after **presenting** evidence that contradicts the lie.
- **Lying, evading, defending, deflecting, partial answers** come from each topic's stance and lines.
- **Pressure is gradual.** Repeats (−4), follow-ups on sore topics (−6), "you're lying" (−8), accusations (−10) and contradictions (−25) lower composure. Below 30 the suspect is *rattled* and gives the topic's cracked `pressuredAnswer`, which reveals more but still isn't a confession.
- **No unknown knowledge.** Topics marked *Unknown* only ever produce "I don't know" lines. Unrelated questions get in-character deflections that contain no case facts.
- **No invented evidence.** Every sentence the mock speaks is authored in the knowledge asset or is a neutral generic line. The online provider is sent only the lines allowed for the reply, plus explicit rules.
- **Follow-ups work.** "Why?", "Are you sure?" and "So you were there." apply to the previous topic.
- **Answers vary.** Asking again gives the next written variant, then impatience ("I already told you…") with a recap.
- **Evidence shown is remembered.** Showing the same item twice gets "You've already shown me that." One item can expose several related lies.

### Player-facing feedback
- **Loading:** "*ELLIOT MARLOWE IS THINKING*" pulses, and Ask, the suggestions and Present Evidence are disabled.
- **Validation:** an empty question shows *"Type a question first."* Asking while he's thinking shows *"Wait for Marlowe to answer."*
- **Errors:** if no provider answers, *"Marlowe didn't respond. Try asking again."* The question stays in the history and no state changes.
- **Review previous answers:** scroll the conversation. Leave and come back, and the earlier conversation is replayed under "— Earlier in this interview —". Key statements and changed stories are written to the **notebook** (J).

---

## 7. Writing a suspect

1. **Profile** (`Data/Cases/<Case>/SUS_Name.asset`): identity, **Temperament** (Calm, Nervous, Defensive, Arrogant, Evasive), **Base Composure**, **Speech Style**, and a **Knowledge** asset.
2. **Knowledge** (*Create → The Last Answer → Cases → Suspect Knowledge*). For each **topic**:
   - `topicId`, `displayName`, `keywords` (lower case; phrases count double; avoid words that are common elsewhere, e.g. "night")
   - `suggestedQuestion` (blank = never suggested)
   - `stance` + `truth` (the writer's note: what really happened)
   - `answers` (2–3 variants), `followUpAnswer`, `pressuredAnswer`
   - `contradictedByEvidence` + `admissionAnswer` (a partial admission, never a confession)
   - `requiresTopic` / `requiresEvidence` to unlock follow-up chains
3. **Evidence reactions** for items they'd react to, plus fallbacks: greeting, unknown question, accusation, repeat, irrelevant evidence. Left empty, generic lines for the temperament are used.

**Worked example (Case 0147):** Elliot Marlowe claims he was home. **CCTV still, 21:40** (found on the Evidence Room laptop) contradicts it. Presenting it makes him admit going to the office to argue with Reyes, while insisting Reyes was alive when he left. He does not know how Reyes died.

**Test a suspect quickly:** copy `SampleInterview_Transcript` in `DialogueSystemTests`, change the script, and read the transcript in the Console.

---

## 8. Online AI

Online dialogue is built (Stage 6): Settings → DIALOGUE → **AI DIALOGUE** switches between offline (scripted) and online. The game only talks to your own backend (`/Backend`), never to an AI vendor, and any failure falls back to the mock. See **[ONLINE_AI_GUIDE.md](ONLINE_AI_GUIDE.md)** for the security model, the request format, reliability, and the service options and costs.

---

## 9. Testing

**Automated** (*Window → General → Test Runner*): **60 tests**.
- `DialogueSystemTests` (EditMode, 19 tests, using the real Marlowe asset): empty input rejected, different questions get different answers, lie variants then impatience, follow-ups, suggestion→topic mapping, greeting vs keyword, "How did Daniel die?" routing, unknown topics reveal nothing, unrelated questions deflected, the evidence question unlocked only after discovery, accusations always denied even when rattled, pressure gives a cracked answer, CCTV exposes both related lies, evidence reactions, the provider receives the full context, fallback to mock, a clean error with no fallback, online provider with no URL, and a full sample transcript.
- `CaseSessionServiceTests`: case start, evidence de-duplication, save/restore round trip.
- Plus the earlier foundation, player, scene validation and location flow tests.

**By hand:**
1. **Play** from `Bootstrap` → **New Investigation** → walk to the **Evidence Room** → examine the board, the case files and the laptop. Watch the HUD counter, toasts and the new objective.
2. **Interrogation Room** → sit (**E**). Try typed questions: *"Where were you that night?"*, *"Why?"*, *"Who had keys?"*, *"Tell me about Victor"*, *"How did he die?"*, *"What's your favourite film?"*, *"You're lying"*, *"You killed him"*.
3. Click a **suggested question**. Press **Ask** with an empty box. Use **Present Evidence → CCTV still, 21:40**.
4. Press **J** (or Esc) to read the **notebook**, then **End Interview**. Sit down again: the history is replayed.

---

## 10. Known limits and next steps

- **Interview history isn't saved to disk yet.** It lasts for the play session (and through scene changes). Notebook entries *are* saved. The planned investigation stage will add interview state to the save.
- Only **Marlowe** has full knowledge. Hayes and Kim have personalities but no interview room yet.
- Keyword matching is deliberately simple. If a typed question misroutes, add or adjust that topic's `keywords`. Suggested questions always work.
- There's no in-game AI Mode toggle yet. It's a single `UIOptionStepper` row away in `SettingsScreen` when an online backend exists.
