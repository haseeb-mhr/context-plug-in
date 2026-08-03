# Context Plugins Hackathon — the guide (canonical reference)

**This file is the end goal.** Everything in this repo is judged against the bar recorded here.

| | |
| --- | --- |
| **Source** | https://work.ahshaikh.com/work-226798669733/cp-hackathon-guide-54628041/ |
| **Plugin catalog** | https://hub.contextplugins-hub.pages.dev/ |
| **Retrieved** | 2026-08-03 |
| **Event** | Monday, 3 August 2026, 4:00 PM – 6:30 PM |
| **Deliverables due** | 6:30 PM |
| **Channel** | `#testing-party` — public only |

> Reproduced from the source guide. This is a *reference copy*: if it disagrees with the live guide,
> the live guide wins. Status tracking lives in [`end-goal-checklist.md`](./end-goal-checklist.md) so
> this file can stay a stable quotation.

---

## 1. What is actually being measured

> "What we are actually measuring is whether the plugin made your agent **write correct integration
> code the first time**."

Two evaluations run simultaneously:

1. **Plugin skill adequacy and timing** — did the skill supply the right contract, at the right moment?
2. **Hub usability** — can someone go from browsing the catalog to working API calls without assistance?

The build is the instrument, not the point. A polished app with an empty findings log scores worse
than a rough one with five real misses.

---

## 2. The bar for "done" — five requirements

| # | Requirement | Detail |
| --- | --- | --- |
| 1 | **3+ endpoints across 2+ controllers, composed into one flow** | "not three unrelated buttons." Endpoints from two combined APIs both count. |
| 2 | **One non-trivial request body** | Nested models, an enum, an optional field, or a `oneOf` union — "agents frequently hallucinate field names here." |
| 3 | **One error path handled deliberately** | A real failure the API returns, **caught by type**. |
| 4 | **Live API calls** | "It works against the real API. Live sandbox calls, not mocks." |
| 5 | **Reproducible and cloneable** | "Someone else can clone it and run it. A CLI is fine. A bot is fine." |

---

## 3. Deliverables — due 6:30 PM

### 3.1 GitHub repository

- README listing the required environment variables
- A single command to start
- **No credentials in history**
- `.env.example` committed

### 3.2 Demo video

- Under three minutes
- Screen recording is fine
- **Must include one failure case**
- Show the workflow actually running

### 3.3 Findings log

Every point where the agent produced incorrect API integration code. Four-part structure per entry:

1. What you requested
2. What the agent produced
3. The actual correct approach
4. Which skill should have prevented it, or what was missing

Rules:

- **Start logging immediately, not at the deadline.**
- **Specific reproducible errors are prioritised over vague observations.**
- > "A findings log that says 'the plugin was great, no issues' gives us nothing we can act on."

> **Five specific reproducible misses beat thirty vague ones.**

---

## 4. Ground rules

1. **Public channel only** — "`#testing-party` for questions, blockers, credentials, findings, and
   deliverables. Not DMs."
   > "Someone else has already hit your problem, and the answer is only useful if the room sees it."
2. **Confirm the host before the first write** — "Log the resolved base URL once at startup."
3. **Personal workspace only** — for anything a plugin installs into: bots, apps, webhooks.
   *Not* the APIMatic Slack.
4. **No committed credentials** — environment variables and `.env.example` only; verify diffs before push.
5. **Delete cloned SDK references** when complete.
6. **Honest findings required.**

---

## 5. Worked examples that clear the bar

### Example 1 — Match-Day Alerts Bot

| | |
| --- | --- |
| **APIs** | `sportsdata` + `telegram-bot` |
| **Flow** | Follow a live game; push score changes and odds movement to a Telegram channel with an inline refresh button. |
| **Coverage** | League scores and odds controllers, plus Telegram send / edit / callback handling. |
| **Why it qualifies** | Two APIs combined; nested odds payload prone to error; a real error path (dropped callback); ~1-minute credential setup; SportsData replay mode. |

### Example 2 — Subscription Dunning Console

| | |
| --- | --- |
| **API** | `paypal` |
| **Flow** | Create a plan, subscribe a sandbox buyer, handle a payment failure with retry scheduling, pause, resume, reconcile against transaction search. |
| **Coverage** | Subscriptions, payments, transaction-search controllers. |
| **Why it qualifies** | A genuine state machine across 800+ model definitions; minimal external documentation, which tests the agent's fallback reliance; plugin necessity is clear; the PayPal SDK pre-points at sandbox. |

---

## 6. Troubleshooting and resources

| Need | Route |
| --- | --- |
| Anything broken | `npx context-plugins doctor` — the primary diagnostic |
| Installs failing | Set `GITHUB_TOKEN`. Unauthenticated requests are limited to **60/hour across the whole network** |
| Fastest route to operations | The SDK's generated `doc/` folder — `controllers/`, `models/`, `auth/` |
| Package name, client class, configuration, environment enum, base error type | The plugin's `getting-started` skill |
| Blockers | `#testing-party` — and log the finding at the same time |

---

## 7. Plugin catalog — check the language column before you commit

> "Browse the catalog — 29 plugins, filterable by category and language. **Check the language column
> before you commit.**"

Retrieved from the hub on 2026-08-03:

| Plugin | Category | Supported languages |
| --- | --- | --- |
| Adyen API | Payments | C#/.NET, TypeScript, Java, Python, Go |
| Alpaca Trading API | Fintech | C#/.NET, TypeScript, Java, Python, PHP, Ruby, +1 |
| Binance API | Fintech | C#/.NET, TypeScript |
| CellPoint API | Payments | C#/.NET, Java, Python, PHP, Go |
| CoinGecko API | Fintech | C#/.NET, TypeScript, Java, Python, PHP, Ruby, +1 |
| Deepgram API | Media | C#/.NET, TypeScript, Java, Python, PHP, Go |
| Discourse API | Communities | C#/.NET, TypeScript, Java, Python, PHP, Ruby, +1 |
| eBay Sell API | Commerce | C#/.NET, TypeScript, Java, Python, PHP, Ruby |
| Google Maps Platform | Data | C#/.NET, TypeScript, Java, Python, PHP, Ruby, +1 |
| Klarna Payments API | Payments | C#/.NET, Java, Python, PHP, Ruby, Go |
| Kubernetes | DevTools | C#/.NET, Go |
| Maxio Advanced Billing | Billing | C#/.NET, TypeScript, Java, Python, PHP, Ruby, +1 |
| **New York Times API** | Media | **C#/.NET, TypeScript**, Java, Python, PHP, Ruby, +1 |
| Notion API | Productivity | C#/.NET, Java, Python, Ruby, Go |
| **PayPal API** | Payments | **C#/.NET, TypeScript**, Java, Python, PHP, Ruby, +1 |
| Paze Checkout API | Payments | C#/.NET, TypeScript, Java, Python, PHP, Go |
| PokéAPI | Data | C#/.NET, TypeScript, Java, Python, PHP, Ruby, +1 |
| Shutterstock API | Media | C#/.NET, TypeScript, Java, Python, PHP, Ruby, +1 |
| Slack Web API | Communications | C#/.NET, TypeScript, Java, Python, PHP, Ruby, +1 |
| Spotify Web API | Media | C#/.NET, TypeScript, Java, Python, PHP, Ruby, +1 |
| Tesla Fleet Management API | Mobility | C#/.NET, TypeScript, Java, Python, PHP, Ruby, +1 |
| Tesser API | Data | C#/.NET, TypeScript, Java, Python, PHP, Ruby, +1 |

### Shortlist result for this repo — use C#, not TypeScript

**Organiser guidance (2026-08-03):**

> "I recommend everyone to work with in C# as it has the updated sdk and updated skills."

**Decision: `paypal` + `nytimes` in C#/.NET.** The catalog's language column is a *weaker* signal than
it looks, and this is the single most important thing on this page:

- The hub lists **TypeScript** for both PayPal and NYT, so a naive language-column check **passes** for
  TypeScript. It should not.
- What the column does not tell you is which languages have an **updated SDK and updated skills**.
  Per the guidance above, that is **C# only**.
- `C#/.NET` is the only language listed for **all 22** plugins, and it always appears first.

This corroborates `FINDINGS.md` Finding 0, which recorded that the per-language *SDK map* — the folder
enumerating every operation signature, model and typed error — exists **only for .NET**. The catalog
language column and the SDK map are different artifacts; a green column does not imply a usable
operation contract.

> **Consequence for this repo:** the [build plan](../nyt-unlock-build-plan.md) is written entirely in
> TypeScript, i.e. the language with a stale SDK and no SDK map. It contradicts both the guidance and
> the repo's own Finding 0. Tracked as `BUGS.md` **BUG-03**, escalated to Critical.

### The hub's language column overstates support — a findings-log candidate

This is a conforming finding in its own right, and it targets the second thing the hackathon is
measuring (hub usability):

- **Requested:** pick a plugin and language by using the catalog's advertised language filter.
- **Produced:** the column lists 7 languages for PayPal and NYT with no currency or completeness
  signal, leading directly to a TypeScript choice.
- **Correct:** C#/.NET is the only language with an updated SDK and updated skills; the SDK map ships
  for .NET alone.
- **Missing:** the hub surfaces no per-language freshness, no skill-coverage indicator, and no marker
  for which languages ship an SDK map. The filter invites a choice it cannot support.

### Count discrepancy

The guide says **29 plugins**; the hub renders **22**, which matches `FINDINGS.md` Finding 0 exactly.
The `+1` suffixes indicate the hub truncates long language lists — confirm the full list on the hub
page before relying on any single language.

### Hub positioning (for context)

> "Context Plugins — Deterministic API Context for AI Coding Tools. Install a Context Plugin so Claude
> Code, VS Code, and Cursor write correct API integration code the first time."

That claim — *correct integration code the first time* — is exactly what the findings log is meant to
test, and it is made without language qualification.

---

## 8. How this repo maps to the bar

The [build plan](../nyt-unlock-build-plan.md) targets all five requirements via `nyt-unlock`:

| Bar | How `nyt-unlock` intends to clear it |
| --- | --- |
| 3+ endpoints, 2+ controllers, one flow | 6 operations across 3 controllers — `SearchApi` (NYT), `OrdersApi`, `PaymentsApi` (PayPal) — composed as `search → buy → claim → status → refund` |
| Non-trivial request body | `OrderRequest`: `purchaseUnits[]` with an exactly-summing `amount.breakdown`, `items[]` with a string `quantity` and an `ItemCategory` enum, plus `paymentSource.paypal.experienceContext` nested three levels deep |
| Deliberate error path, caught by type | Double capture → 422 `CustomError`, issue `ORDER_ALREADY_CAPTURED`, recovered by re-reading the order; second path via the `PayPal-Mock-Response` negative-testing header |
| Live API calls | Live PayPal sandbox and live NYT Article Search |
| Cloneable | CLI, README env table, one command to start |

**Note on requirement 4:** the `--mock <code>` flag sends PayPal's `PayPal-Mock-Response`
negative-testing header. That is still a **live sandbox call** — the API returns the failure — so it
does not violate "not mocks." It is a real response, deterministically triggered.

If time runs short, the plan's §14 cut order is: refund → `--mock` flag → status reconciliation.
Even after all three cuts the bar still clears. **The findings log is never cut.**

### Language port required

The flow above is language-agnostic — the same 6 operations across the same 3 controllers clear the bar
in any language. But the plan's *implementation detail* is TypeScript throughout, and per §7 the target
is now **C#/.NET**. What has to change:

| Plan detail | TypeScript (as written) | C#/.NET (target) |
| --- | --- | --- |
| Package / import | `paypallib`, `nytimeslib` from npm-over-git | NuGet package ids — read from the getting-started skill |
| Field casing | camelCase (`currencyCode`, `purchaseUnits`, `unitAmount`) | PascalCase — **every model field in Phase 4 changes** |
| Runtime / scaffold | Node 20, ESM, `tsx`, `package.json` scripts | .NET 8, `dotnet new console`, `dotnet run` |
| Env loading | `dotenv` | `Microsoft.Extensions.Configuration` or `DotNetEnv` |
| Async surface | `Promise<ApiResponse<T>>` | `Task<ApiResponse<T>>` |
| Error types | `ApiError` / `CustomError` | Confirm the .NET exception names — do not assume they match |
| Optional args | positional `undefined` placeholders | named/optional parameters — **the header-before-body trap may not survive the port** |

Two upsides: the .NET SDK map exists, so Phase 1's contract sheet should be far more reliable; and
`.gitignore` already carries the .NET ignores (`bin/`, `obj/`, `*.suo`, NuGet, `packages/`, `.vs/`),
which I had previously flagged as dead residue — it is now correct and load-bearing.

**Do not port the field names by hand.** Phase 1's contract sheet, generated from the .NET SDK map, is
what Phase 4 must be built from — and any place the agent gets a PascalCase field or enum member wrong
despite having the SDK map in hand is a first-class findings entry.
