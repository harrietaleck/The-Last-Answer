# Online AI dialogue: guide

How THE LAST ANSWER talks to an online AI service, what is already built, and what you need to choose before switching it on.

**Status:** the game side and a reference backend are built and tested. **No AI service has been chosen**, and nothing in the project needs an account yet. Mock mode remains the default and the game is fully playable offline.

## Contents

1. [How it fits together](#1-how-it-fits-together)
2. [Security model](#2-security-model)
3. [What the game sends and receives](#3-what-the-game-sends-and-receives)
4. [Reliability](#4-reliability)
5. [Switching between Mock and Online](#5-switching-between-mock-and-online)
6. [Running the reference backend](#6-running-the-reference-backend)
7. [Choosing an AI service: options, costs, requirements](#7-choosing-an-ai-service-options-costs-requirements)
8. [Hosting the backend](#8-hosting-the-backend)
9. [Connecting a service once chosen](#9-connecting-a-service-once-chosen)
10. [Testing](#10-testing)
11. [Known limits](#11-known-limits)

---

## 1. How it fits together

```
Player question
   │
   ▼  C# (in the game)
IntentMatcher → ResponsePlanner ──► decides WHAT happens: reply type, stance, composure,
   │                                 contradictions, evidence. Nothing below can change it.
   ▼
MockAIProvider.Compose ──► the authored "baseline" line for this turn
   │
   ▼  Online mode only
OnlineAIProvider ── HTTPS ──► YOUR backend (/Backend) ── secret key ──► AI service
   │                               builds prompt, rate-limits,          (not chosen yet)
   │                               cleans the reply
   ▼
ResponseValidator ──► schema, requestId, length, markup, invented names/numbers
   │        │
   │        └── any failure ──► MockAIProvider answers instead (offline line)
   ▼
DialogueSession records the line and applies the C# plan's state changes
```

The online AI is a **voice**, not a brain. The rules and the authored knowledge decide what the suspect means. The AI rewords that meaning in the suspect's personality and reacts to how the player phrased the question.

| Piece | File |
|---|---|
| Provider interface + failure kinds | `Scripts/Systems/Dialogue/IAIProvider.cs` |
| Online provider (timeouts, duplicates, pause after failures) | `Scripts/Systems/Dialogue/OnlineAIProvider.cs` |
| Provider choice (Mock / Online) | `Scripts/Systems/Dialogue/AIProviderFactory.cs` |
| Request/response format + request builder | `Scripts/Systems/Dialogue/Online/DialogueWireFormat.cs` |
| Reply validation | `Scripts/Systems/Dialogue/Online/ResponseValidator.cs` |
| Network layer + URL safety rules | `Scripts/Systems/Dialogue/Online/IDialogueTransport.cs` |
| Settings (URL, limits) | `GameConfig.asset` → **AI** |
| Player switch | Settings → **DIALOGUE → AI DIALOGUE** |
| Reference backend | `/Backend` (Node.js, no dependencies) |

## 2. Security model

**Rule: no API key, token or password ever exists in the game.** Not in C#, not in ScriptableObjects, not in config files, not in the build. Anything shipped in a game can be extracted by players.

- **What the game holds:** only the backend's public address (`GameConfig → AI → Backend Base Url`) and some limits. The vendor and model names never appear in the game.
- **What the backend holds:** the AI service's key, in an environment variable (`TLA_AI_API_KEY`) on the server. `.env` is git-ignored, and `.env.example` contains no values.
- **Safety rules the game enforces on the address:**
  - It must be **https**. Plain http is only allowed for `localhost` and `127.0.0.1` while developing.
  - Addresses containing `user:password@` are refused.
  - Redirects are not followed.
- **Development vs release:** the Editor and Development builds use **Development Backend Url** (default `http://localhost:8787`). Release builds use only **Backend Base Url**, which is empty until you deploy a backend, so Online mode can't run there yet.
- **Protecting the backend from abuse:** the game can't hold a secret, so the backend can't verify that a request really came from the game. It protects itself instead:
  - Limits requests per IP address (`TLA_RATE_LIMIT_PER_MINUTE`, default 30).
  - Limits body size (64 KB) and validates the request schema.
  - Keeps its own prompt, so a modified game can't rewrite the rules.
  - Caps reply length.
- **Spending caps:** set a monthly spending limit in the AI service's dashboard too. That's the real cost safety net.
- **What the AI never receives:**
  - Any topic's hidden **truth**.
  - Admissions the rules haven't unlocked.
  - Other topics' answers.
  - Evidence the player hasn't shown.
  - Other suspects' knowledge.

  It can't leak what it doesn't know.

## 3. What the game sends and receives

`POST {backend}/v1/dialogue`, JSON, schema version 1.

**Request** (only what this one reply needs):

| Field | What |
|---|---|
| `requestId` | Unique per request; must be echoed back |
| `limits` | `maxReplyChars`, `maxNarrationChars` |
| `case` | Public details: title, location, briefing (as the player reads it) |
| `suspect` | Name, age, occupation, relation to case, background, temperament, speech style, composure, composure level |
| `turn` | Player's latest question; reply type decided by the rules (e.g. `TopicAnswer`, `Accusation`, `EvidenceContradiction`); topic name and stance (`Lie`, `Partial`…); evidence shown now; evidence shown so far |
| `allowed.baseline` | The authored line for this turn: **the only case content the reply may express** |
| `history` | The last N lines of this interview (N = `History Lines Sent`, default 12) |

**Response:**

```json
{ "schemaVersion": 1, "requestId": "<same id>", "text": "Spoken line.", "narration": "Optional stage direction." }
```

Unity ignores every other field. A reply that tries to award evidence, end the case or change composure has no effect. A test proves this.

**Checks before a reply is shown** (`ResponseValidator`):

1. Valid JSON object, correct `schemaVersion` and `requestId`, non-empty `text`.
2. Rich-text tags, control characters, wrapping quotes and a leading `Name:` are stripped.
3. Out-of-character text is rejected: "as an AI", "language model", "system prompt"…
4. Length: anything over **Max Reply Characters** (default 320) is cut at the last sentence that fits, or rejected.
5. **Invented details** are rejected: any digits, or any capitalised name mid-sentence, that appeared nowhere in the request. This catches new times, amounts, places and people.
6. Narration that fails any check is dropped, and the line is kept.

## 4. Reliability

| Situation | What happens |
|---|---|
| No internet | Detected immediately (no wait); the mock answers |
| Backend down / connection refused | Mock answers |
| Slow backend | Request timeout (`Request Timeout Seconds`, default 10). Then the mock answers |
| Server error 5xx / bad request 4xx | Mock answers |
| Rate limited (429) | Mock answers |
| Invalid JSON, wrong schema, wrong requestId | Mock answers |
| Reply invents details or breaks character | Mock answers |
| Player submits again while waiting | Refused: "Wait for Marlowe to answer." (session) and Busy (provider). Only one request reaches the network |
| Repeated failures | After `Failures Before Pause` failures in a row (default 2), Online pauses for `Pause After Failures Seconds` (default 60). The mock answers **instantly** meanwhile, then online is retried |
| Leaving the interview mid-request | Request cancelled; not counted as a failure |

The player sees one notification, "ONLINE DIALOGUE UNAVAILABLE: Using offline dialogue for now", then "Connection restored" when it recovers. Each failure is logged with its reason, for example `[Dialogue] Online reply failed (Timeout: …)`.

Case state only changes when **a** reply succeeds, whether online or from the mock. The change is always the C# plan's, so a fallback never changes the outcome of a turn.

## 5. Switching between Mock and Online

- **Player:** Settings → DIALOGUE → **AI DIALOGUE**: `OFFLINE (SCRIPTED)` / `ONLINE`. The change applies on the next question, even mid-interview. It shows `ONLINE (NOT SET UP)` when this build has no usable backend address. Picking it then simply keeps offline dialogue.
- **Developer:** `GameConfig → AI → Default Mode` sets the first-launch default (keep it **Mock**).
- **Code:** only `AIProviderFactory` constructs providers. Nothing else in the game refers to a mode, vendor or model.

## 6. Running the reference backend

Requires **Node.js 18+** (this PC has Node 24). The backend has no npm packages to install.

```bash
cd Backend
node server.js
```

It listens on `http://127.0.0.1:8787` (this computer only) with the **stub** provider. The stub returns the game's baseline line unchanged. It needs no account and has no cost, and it lets you test the whole online path.

Then in Unity, set Settings → AI DIALOGUE → **ONLINE** and interview Marlowe. The console shows `[Dialogue] Dialogue provider: online`.

To simulate failures, start it with `TLA_FAULT` set, then watch the game fall back:

```bash
TLA_FAULT=error node server.js     # also: slow, invalid, wrongid, invent, ratelimit
```

In PowerShell: `$env:TLA_FAULT="invent"; node server.js`.

Backend tests: `node test.js`.

## 7. Choosing an AI service: options, costs, requirements

> Prices change often. The figures below were gathered in November 2026 and are for estimating only. Check each vendor's pricing page before deciding. Prices are per **million tokens** (≈ 750,000 words).

### What one turn costs

One interview question sends about **1,300 input tokens** (rules, profile, briefing, 12 history lines, baseline) and gets back about **80 output tokens**. Some models also produce hidden "thinking" tokens, billed as output. For this task, set thinking or effort to its lowest level.

### Integration options

| Option | How | Pros | Cons |
|---|---|---|---|
| **A. A major AI vendor directly** (Anthropic, OpenAI, Google…) | Backend calls the vendor's API with your key | Best quality, simplest, official SDKs, spending limits | Needs an account with billing; per-token cost |
| **B. A model aggregator** (one key, many vendors' models) | Backend calls the aggregator | Switch models without code changes; compare easily | Extra middle-man (small markup, another dependency) |
| **C. Hosted open-weight models** (cloud GPU inference providers) | Backend calls their API | Often cheapest per token; fast | Quality varies; more tuning to keep strict rules |
| **D. Self-hosted model on your own server** | Run a model on a rented GPU | No per-token fees | GPU servers are expensive to keep running; you maintain it. Not recommended for this project |
| **E. Stay on Mock** | Nothing | Free, offline, fully deterministic | Answers are limited to authored lines |

You **don't** need a local model on players' PCs for any of these. Option D runs on your server, not theirs.

### Price comparison (a selection)

| Model | Input / Output per 1M tokens | ≈ cost per question | ≈ 1,000 questions | Source |
|---|---|---|---|---|
| Claude Haiku 5.5 | $0.10 / $0.50 | $0.0002 | $0.20 | Anthropic model table |
| Claude Sonnet 5.5 | $2.00 / $10.00 | $0.0034 | $3.40 | Anthropic model table |
| Claude Opus 5.5 | $4.00 / $20.00 | $0.0068 | $6.80 | Anthropic model table |
| GPT-5 mini | $0.25 / $2.00 | $0.0005 | $0.50 | third-party tracker |
| GPT-5.4 mini | $0.75 / ~$4.50 | $0.0013 | $1.30 | OpenAI page + tracker |
| Gemini 2.5 Flash | $0.30 / $2.50 | $0.0006 | $0.60 | third-party guide |
| Gemini 2.5 Flash-Lite | $0.10 / $0.40 | $0.0002 | $0.16 | third-party guide |

Costs per question exclude thinking tokens. Prompt caching (the rules part of every request is identical) can cut input cost further on vendors that support it.

**Rough budget:** a full case is about 40 questions per player.

| Usage | Haiku-class / Flash-Lite | Sonnet-class |
|---|---|---|
| You, testing (2,000 questions / month) | ≈ $0.40 | ≈ $7 |
| 1,000 players × 3 cases | ≈ $25–40 | ≈ $400+ |

### What you need for any online option

1. **An account** with the chosen vendor, with a payment card. Some offer free tiers or trial credit with low rate limits, enough for testing.
2. **An API key**, created in the vendor's console, stored only on the backend server.
3. **A monthly spending limit**, set in that console.
4. **Somewhere to run the backend** with HTTPS (see §8). It's needed even for local testing, but `node server.js` on your PC is enough until release.
5. **For release:** a privacy policy line saying interview text is sent to an AI service when Online mode is on. Keep Online opt-in.

### Recommendation

- **Develop on Mock + stub** (free) until the interview design settles. Online mode adds voice, not content, so nothing is blocked.
- **When you want real AI replies:** start with a **small, cheap model** (Haiku-class or Flash-Lite-class). The job is rewording one line under strict rules, which small models handle well, and it keeps costs near zero while testing.
- Only move up a tier if the voice isn't good enough.
- You don't have to decide now. Tell me which vendor and I'll add its adapter. The game needs no changes.

## 8. Hosting the backend

`server.js` is a plain Node HTTP server, so it runs unchanged on any host that runs Node:

| Kind | Examples of the category | Typical cost | Notes |
|---|---|---|---|
| Your own PC | `node server.js` | Free | Development only (localhost) |
| App platforms | Render-, Railway-, Fly-type services | Free tier to ~$5–7/month | Easiest: connect the repo, set env vars, get an https URL |
| Small VPS | Any $4–6/month Linux server | ~$5/month | You manage HTTPS (e.g. a reverse proxy) and updates |
| Serverless functions | Cloudflare Workers, Vercel/Netlify functions, cloud functions | Usually free at indie scale | Needs a small wrapper (move `handleDialogue` into the platform's handler) |

Set the AI key and model as **environment variables in the hosting dashboard**, never in files in the repository. Then put the https address in `GameConfig → AI → Backend Base Url`.

## 9. Connecting a service once chosen

1. Add `Backend/providers/<name>.js` following `Backend/providers/README.md`. It takes the vendor-neutral prompt from `prompt.js`, calls the vendor, and returns `{text, narration}`. Use the vendor's official SDK.
2. On the server, set `TLA_PROVIDER=<name>`, `TLA_AI_API_KEY=…` and `TLA_AI_MODEL=…`.
3. Deploy, then set **Backend Base Url** in GameConfig.
4. Play with Online on and watch the console for rejected replies. If the validator rejects too much, improve `prompt.js` first; it's server-side, so no game rebuild is needed.

Nothing in Unity changes when you switch vendors or models.

## 10. Testing

- **Unity EditMode** (`OnlineAIProviderTests`, 30 tests, fake network):
  - Request content, including the hidden truth never being sent.
  - Validation and ignored extra fields.
  - Timeout, offline, 500/503, 429.
  - Truncated, empty and HTML bodies; wrong requestId or schema.
  - Invented names and times; AI disclaimers.
  - Length cut; duplicate refusal; pause and retry; provider switching; URL rules.
- **Backend** (`node test.js`): health, contract, malformed requests, simulated server error.
- **Manual:** run the backend (stub or a `TLA_FAULT`), switch Settings to Online, and interview Marlowe.

## 11. Known limits

- **The invented-detail check is a heuristic.** Spelled-out numbers ("ten o'clock") and new names at the start of a sentence can slip through. The server prompt is the first defence; the validator is the second.
- **The AI only rewords the authored line.** It doesn't write new answers to questions the knowledge asset doesn't cover; those still get the authored "I don't know" or deflection, reworded. That's deliberate: it keeps facts in C#.
- **Replies arrive in one piece, not streamed.** That's fine for single short lines.
- **Rate limiting is per IP and in memory.** Fine for one server instance; use the host's rate limiting if you run several.
