# End-goal checklist — status against the bar

Live status tracker. The bar itself is quoted in [`hackathon-guide.md`](./hackathon-guide.md); this
file only records **where we stand**, so the two never drift into competing copies of the rules.

**Assessed at commit `f81c3c7`, 2026-08-03. Deliverables due 6:30 PM to `#testing-party`.**

Legend: ✅ met · 🟡 partial · ❌ not met · ⛔ blocked

> ### ⚠️ Language decision changed — C#, not TypeScript
>
> Organiser guidance, 2026-08-03: *"I recommend everyone to work with in C# as it has the updated sdk
> and updated skills."*
>
> The entire build plan is TypeScript. The hub's language column lists TypeScript for both plugins, so
> the naive check passed — but the column does not indicate SDK/skill currency, and only C#/.NET has
> both. This confirms `FINDINGS.md` Finding 0 (SDK map ships for .NET only).
>
> **Every implementation row below is now understood as "to be built in C#."** The flow, controllers and
> operations are unchanged; the field casing, package ids, scaffold and error types all change. Port
> table in [`hackathon-guide.md` §8](./hackathon-guide.md). Tracked as `BUGS.md` BUG-03 (Critical).
>
> Silver lining: this arrives *before* any code was written, so the cost is re-planning, not rewriting.

---

## Bar for "done" — 5 requirements

| # | Requirement | Status | Where we stand |
| --- | --- | --- | --- |
| 1 | 3+ endpoints, 2+ controllers, one flow | ❌ | Designed (6 ops / 3 controllers) but **no code exists** — `git ls-files` returns 5 doc files; no `package.json`, `src/` or `node_modules/` |
| 2 | One non-trivial request body | ❌ | `OrderRequest` fully specified in plan Phase 4; not implemented |
| 3 | One error path caught by type | ❌ | `ORDER_ALREADY_CAPTURED` recovery specified in Phase 5; not implemented. **Design defect:** the plan's idempotency key suppresses this very error — `BUGS.md` BUG-11 |
| 4 | Live API calls, not mocks | ⛔ | No credentials obtained; no `.env` on disk. Blocked on Phase 0 |
| 5 | Someone else can clone and run it | ❌ | Nothing to run. README is the planning memo, not a README — `BUGS.md` BUG-26 |

---

## Deliverables — due 6:30 PM

### 1. GitHub repository

| Item | Status | Notes |
| --- | --- | --- |
| README lists required env vars | ❌ | README has no env table. Phase 7 specifies it; not written — BUG-26 |
| Single command to start | ❌ | No `package.json`, so no `npm start` |
| **No credentials in history** | ✅ | Verified: no `.env` on disk; `.env.example` holds empty values for both PayPal credentials and the NYT key; `0b9621d` landed the ignore rule before any secret could |
| `.env.example` committed | ✅ | Present and tracked. Minor deviation: ships 3 pre-filled defaults against Phase 2's "empty values" — BUG-25 |

### 2. Demo video (< 3 min, one failure case)

| Item | Status | Notes |
| --- | --- | --- |
| Recorded | ❌ | Nothing to record yet |
| Running order drafted | ✅ | Plan §13, 7 steps |
| Failure case included | 🟡 | Two are planned, **but both are currently broken by design**: step 5 (double capture) is suppressed by the shared idempotency key (BUG-11) and step 6 (`claim 4 --mock`) hits an empty ledger before reaching PayPal (BUG-12). Fix or re-script before recording. |

### 3. Findings log

| Item | Status | Notes |
| --- | --- | --- |
| Started immediately, not at deadline | 🟡 | `FINDINGS.md` exists with 2 entries |
| Five specific reproducible plugin misses | ❌ | **0 of 5.** See the gap below — this is the highest-priority item |
| Four-part structure per entry | ❌ | Neither existing entry follows the template `FINDINGS.md` itself defines — BUG-04 |
| Honest | ✅ | Both entries are critical, not congratulatory |

---

## The findings-log gap — read this first

The guide is explicit: **"five specific reproducible misses beat thirty vague ones,"** and each entry
must answer *what you requested → what the agent produced → the actual correct approach → which skill
should have prevented it.*

Two things follow, and they are easy to get wrong:

**`BUGS.md` is not the findings log.** It holds 30 findings, but they are defects in *this repo's
plan and hygiene* — not points where a plugin made an agent write wrong integration code. It answers
"is this repo sound?", not "did the plugin work?". Useful, and worth keeping, but it scores zero
against deliverable 3. Do not submit it as the findings log.

**`FINDINGS.md` is the right vehicle and it is nearly empty.** Current tally against the guide's
requirement:

| Entry | Counts toward the five? | Why |
| --- | --- | --- |
| Finding 0 — SDK map ships for .NET only | 🟡 Partially | A real, specific, reproducible observation about plugin *packaging*. But it records no requested-vs-produced code pair, so it is missing two of the four required parts. Still open: the hub's green TypeScript column does **not** refute it (different artifact). |
| Finding 1 — did install #2 disturb install #1? | ❌ No | Explicitly "NOT YET ANSWERED" |

**Net: 0 fully-conforming entries out of 5.** Every conforming entry requires the plugins installed
and an agent generating code against them, which is blocked at Phase 0 — `BUGS.md` BUG-01.

The plan already names seven strong candidates (§12) — the mixed-case `Environment` enum, positional
headers before the body, `quantity` as a string, `experienceContext` vs the deprecated
`applicationContext`, the capture-id path, the omitted NYT headline field, and retry defaults. Each
becomes a conforming entry **only if** you capture the prompt and the wrong output at the moment it
happens. Retrofitting them after the fact loses the "what the agent produced" half, which is the half
that has evidentiary value.

---

## Critical path from here

Ordered by what unblocks the most.

1. **Phase 0 — unblock everything.** Set `GITHUB_TOKEN` (unauthenticated GitHub installs share a
   60/hour network-wide limit), register the context-plugins marketplace, `npx context-plugins install
   paypal`, `install nytimes`, `npx context-plugins doctor`. Nothing measurable exists until this
   lands — BUG-01.
2. **Answer Finding 1 while installing.** Capture the first plugin's skill listing *before* and
   *after* the second install. This is free evidence and it is only obtainable during step 1 — miss
   the window and the finding is gone.
3. **Confirm the C# skills are actually the fresh ones.** While the plugins are installed, list the
   per-language skill folders for both and check that .NET carries an SDK map and TypeScript does not.
   That either promotes Finding 0 to a fully-evidenced entry or refutes it — and it takes one `ls`.
4. **Credentials.** PayPal sandbox REST app (client id + secret), a sandbox *personal buyer* login to
   approve with, and an NYT key with Article Search enabled. Obtain these yourself — the guide bars
   credentials from the shared channel and the plan bars them from the agent transcript.
5. **Phase 1 — `docs/contract.md` from the .NET SDK map, and log as you go.** This is the
   highest-yield findings window: the agent has the skill *and* the SDK map in hand, so anything it
   gets wrong here is maximally damning. Diff its output against the SDK and write each miss up
   immediately, with the prompt that produced it.
6. **Re-specify Phase 4's body in PascalCase from that contract sheet — do not hand-port the
   TypeScript.** Hand-porting would launder the plan's existing enum contradiction (BUG-05) into the
   C# code and destroy the finding, because you could no longer tell whether the agent or the plan
   got it wrong.
7. **Resolve the two blocking design defects before coding Phase 4/5** — BUG-10 and BUG-11. The
   idempotency key both breaks `buy` on retry and deletes the demo's headline failure case. Deciding
   this after the code exists means rewriting the capture path.
8. **Build Phases 2–5.** That alone clears all five bar requirements. Cut in the plan's §14 order if
   time runs short: refund → `--mock` → status reconciliation.
9. **Write the real README** — BUG-26 — then record the video, then submit.

**Scope warning.** The guide allots roughly 90 minutes of build time. The plan is 7 phases, 6
operations and 5 commands, starting from zero code, with Phase 0 not yet done, and now a language port
on top. Phases 2–5 plus a truthful findings log is the realistic target; treat Phase 6 (`status`,
`refund`) as optional from the outset rather than discovering that at 6:15.

**One upside of the C# switch:** the .NET SDK map exists, which is precisely the artifact Phase 1
depends on. Phase 1 should get *faster* and more accurate, not slower — and if it does not, that
discrepancy is itself the most valuable finding available, because .NET is the language the guidance
calls current.

---

## Requirements the plan already satisfies

Worth noting so they are not re-litigated:

- **"Log the resolved base URL once at startup"** (ground rule 2) — Phase 2's `logStartupBanner()`
  reads the base URL off the client configuration and prints it before anything else. One defect
  against it: the acceptance criterion hardcodes `api.nytimes.com`, the literal Phase 2 forbids
  (BUG-28).
- **"Delete cloned SDK references when complete"** (ground rule 5) — Phase 7.
- **"Live sandbox calls, not mocks"** (requirement 4) — the `--mock` flag sends PayPal's
  `PayPal-Mock-Response` negative-testing header, which is a live sandbox call returning a real
  failure. Compliant.
- **Personal workspace only** (ground rule 3) — no workspace-installing plugin is involved; PayPal
  and NYT install nothing into a Slack.
