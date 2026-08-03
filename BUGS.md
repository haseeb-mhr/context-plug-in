# BUGS — defect report for `nyt-unlock` / Context-Plugin-Testing

**Audit date:** 2026-08-03 · **Commit:** `f81c3c7` · **Mode:** report only, nothing fixed.

## Scope and what limits it

At `f81c3c7` this repo contains **no implementation**. `git ls-files` returns five files:
`.env.example`, `.gitignore`, `FINDINGS.md`, `README.md`, `nyt-unlock-build-plan.md`. There is no
`package.json`, `tsconfig.json`, `src/`, `docs/`, `node_modules/`, `.env` or `.cache/`.

So the findings below are defects in **the specification, the repo hygiene, and the evaluation
setup** — not in runtime code, because there is none. Plugin-behaviour findings (the ones this
repo actually exists to collect) are blocked; see **BUG-01**.

Citation legend: `plan:NN` = `nyt-unlock-build-plan.md` line NN · `readme:NN` = `README.md` line NN.

| Severity | Count |
| --- | --- |
| Blocker | 1 |
| Critical | 3 |
| High | 9 |
| Medium | 11 |
| Low | 7 |
| **Total** | **31** |

**Revision log — 2026-08-03**, after fetching the hackathon guide and the plugin hub, and receiving
organiser guidance to build in C#:

- **BUG-03 escalated High → Critical.** The plan's TypeScript target is the stale one; C#/.NET is the
  only language with an updated SDK and skills. One sub-point retracted (see the entry).
- **BUG-02 downgraded High → Low.** The hub renders 22 plugins, matching Finding 0's count, so its
  provenance is the hub website and there is no contradiction with Finding 1.
- **BUG-31 added (High).** The hub's language column overstates support and is the guide's only
  gating check — the root cause of BUG-03.

---

## Resolution status — updated at commit `38cc7bc`, 2026-08-03

The report below was written against `f81c3c7`, when the repo held five doc files and no code. The
C# implementation now exists (`src/NytUnlock`, 8 files) and runs against both live APIs, so the
entries have been triaged. **The original text of each entry is left unedited** — this table is the
only statement of current state.

Legend: ✅ fixed in code · ✔️ resolved by the .NET port or by evidence · ➖ moot after the port ·
🟡 accepted deviation · ❌ still open

| # | Sev | State | How |
| --- | --- | --- | --- |
| 01 | Blocker | ✔️ | Both plugins installed; marketplace `context-plugins` registered. Evidenced as `FINDINGS.md` Findings 2–4 |
| 02 | Low | ❌ | Wording only — "the marketplace" still reads as the local install where the public catalog is meant |
| 03 | Critical | ✔️ | Built in C#/.NET. Contract re-derived from the .NET SDK map into `docs/contract.md`, not hand-ported |
| 04 | Medium | ✅ | All 10 `FINDINGS.md` entries now carry *Asked for / Produced / Actually correct / Should have been prevented by / Reproducible* |
| 05 | High | ✔️ | Settled from `map/models/enums.md`: `CheckoutPaymentIntent.Capture`, `ItemCategory.DigitalGoods`, `ApplicationContextShippingPreference.NoShipping`, `PayPalExperienceUserAction.PayNow`. The plan's SCREAMING_SNAKE spellings are the **wire values**, not C# members — so neither spelling was invented, they were conflated |
| 06 | Medium | ✔️ | **Inverted for .NET.** The C# `CreateOrder` signature puts five nullable header params *before* `body`, so the plan's "body first" claim is wrong for the target language, not the general rule |
| 07 | Medium | ✅ | `docs/contract.md` is now the single authority and says so; `README.md` rewritten as a real README (see 26) |
| 08 | Medium | ➖ | C# named arguments — `prefer:` is passed by name, so positional drift cannot occur |
| 09 | Medium | ✔️ | NYT auth is `options.Apikey`, a plain `string?`. No wrapper type |
| 10 | Critical | ✅ | `buy` derives request id **and** invoice id from one fingerprint (`articleUri\|price\|UTC date`), so a same-day retry sends an identical body under an identical key |
| 11 | Critical | ✅ | `claim` sends a **fresh** `PayPal-Request-Id` per attempt, keeping the `ORDER_ALREADY_CAPTURED` 422 reachable. `Checkout.cs` carries the rationale inline |
| 12 | High | ✅ | Demo re-scripted: `--mock` runs against the *same* bought index, not an unbought one. Documented in `README.md` along with the caveat that an unapproved order returns `ORDER_NOT_APPROVED` instead |
| 13 | High | ✅ | The ledger is keyed on `ArticleUri`, never the index. `Nyt.Resolve` prints the resolved uri plus the query and timestamp that produced it, so a stale cache is visible rather than silent |
| 14 | High | ✅ | `LedgerEntry` now stores `Token` and `ExpiresAtUnix`; the early-exit path reads state that is actually written |
| 15 | High | ✅ | `verify <token>` command added — checks signature, expiry **and** ledger status |
| 16 | High | ✅ | Partial refund → `PARTIALLY_REFUNDED` (access kept); only a full refund → `REVOKED` |
| 17 | Medium | ✅ | `CAPTURE_FULLY_REFUNDED` and `REFUND_AMOUNT_EXCEEDED` handled by issue code, exit 7 |
| 18 | Medium | ✅ | `status` guards a null `CaptureId` and reports partial reconciliation. **Verified live** on a `CREATED` entry |
| 19 | Medium | ✅ | `NormalisePrice` rejects non-positive and >2-decimal values before any call |
| 20 | Medium | ✅ | Credentials validated per command. **Verified live:** `search` with no `.env` exits 2 naming only `NYT_API_KEY` |
| 21 | Medium | ✅ | Token subject is base64url-encoded, which contains no `.`, so the expiry boundary is unambiguous. The HMAC input is stated exactly as the `<b64uri>.<exp>` prefix |
| 22 | Low | ✅ | NYT 400 branch added alongside 429/401, exit 8 |
| 23 | High | ✅ | `ResolvePaypalEnvironment` normalises case and **refuses unrecognised values**. **Verified live:** `PAYPAL_ENV=Production` → *"requires ALLOW_PRODUCTION=true. Refusing to continue."*, exit 2; `PAYPAL_ENV=staging` → refused |
| 24 | High | ✅ | Never handled literal secret values. Hygiene check greps **variable names** only and uses `git check-ignore`, exactly the safe form this entry proposed |
| 25 | Low | 🟡 | `.env.example` still ships `PAYPAL_ENV`/`RETURN_URL`/`CANCEL_URL` defaults. Kept deliberately — the BUG-23 fix makes the `PAYPAL_ENV` default safe, and blank URLs are a worse first-run experience |
| 26 | High | ✅ | `README.md` replaced: description, prerequisites, 8-row env table, one-command install, walkthrough with real output, both error paths, exit-code table |
| 27 | Low | ✅ | One authority now — the README env table lists all 8 variables including `ALLOW_PRODUCTION` |
| 28 | Low | ➖ | The banner reads base URLs off `options.Server.*`; no literal is asserted. Live output: `https://api.nytimes.com/svc/search/v2` |
| 29 | Low | ❌ | Retry behaviour is still neither configured nor observed. `RetryOptions` members are all `required`; the .NET default is **not** stated in the map, so the plan's "retries off by default" claim remains unverified for C# |
| 30 | Low | ✔️ | `docs/contract.md` exists and was produced before any `src/` code, as Phase 1 required |
| 31 | High | ✔️ | Promoted to `FINDINGS.md` **Finding 1** with on-disk evidence, which is where it has external value |

**Net:** 1 Blocker and 3 Criticals closed; 19 fixed in code; 2 still open (BUG-02 wording, BUG-29
retry) and 1 accepted deviation (BUG-25).

### Defects this report did not and could not predict

All three were only reachable by running against the live APIs, and all three are in the SDKs rather
than in this repo. They are the highest-value entries in `FINDINGS.md`:

- **Finding 8** — `ArticleSearchArticle.PrintPage` is `int?` where the API sends a string, so *no*
  Article Search response deserializes. Thrown by `System.Text.Json`, not as an `SdkException`, so a
  correct catch ladder does not intercept it.
- **Finding 9** — `Response1.Meta` is bound to `"meta"`; the API sends `"metadata"`. Fails silently.
- **Finding 6** — PayPal's `ProductionOptions.BaseUrl` is the sandbox host, so no environment member
  reaches live PayPal.

Findings 8 and 9 are worked around by `scripts/patch-sdk.ps1`, now a required install step.

---

## A. Blocked evaluation

### BUG-01 · Blocker · The plugins under test are not installed
`~/.claude/plugins/known_marketplaces.json` registers exactly one marketplace,
`anthropics/claude-plugins-official`. Its `plugins/` (40 entries) and `external_plugins/`
(15 entries) listings contain no `paypal` and no `nytimes`. `GITHUB_TOKEN` is unset in the
environment.

Phase 0 steps 1–4 (`plan:113-116` — export token, `install paypal`, `install nytimes`, `doctor`)
have not run. Until they do, no statement about plugin or skill behaviour can be produced or
reproduced, which is the stated purpose of the repo.

### BUG-02 · ~~High~~ → **Low** · `FINDINGS.md` is imprecise about provenance (revised)
**Revised 2026-08-03 after fetching the hub — the substance of this finding does not hold.**

Original claim: Finding 0's *"Verified across all 22 plugins in the marketplace"*
(`FINDINGS.md:25-26`) contradicts Finding 1's *"`~/.claude/plugins` is empty, no marketplace
registered"* (`FINDINGS.md:42-43`).

The hub at `hub.contextplugins-hub.pages.dev` renders **exactly 22 plugins**, matching Finding 0's
count precisely. So Finding 0 was verified against the **hub website**, not a local install — which
is fully consistent with Finding 1. There is no contradiction.

What remains is a wording defect only: "the marketplace" reads as the locally-registered
marketplace when it means the public catalog. Worth one word; not a High. Finding 0's count is
corroborated, and its "Reproducible: yes" claim stands.

### BUG-03 · ~~High~~ → **Critical** · The plan is written in the wrong language (escalated)
**Escalated 2026-08-03 on organiser guidance:**

> "I recommend everyone to work with in C# as it has the updated sdk and updated skills."

Finding 0 concluded the SDK map ships for .NET only, that non-.NET languages therefore have no
operation contract, and that *"This is the finding that drives the language choice for the rest of
this build"* (`FINDINGS.md:26-34`). The entire build plan is then written in **TypeScript** — the
language the finding says has no contract, and which the guidance now confirms has a stale SDK and
stale skills.

Finding 0 was right, its stated consequence was correct, and the plan overrode it anyway. Every
implementation detail in Phases 2–7 — camelCase field names, `paypallib`/`nytimeslib` imports,
Node/ESM/`tsx` scaffold, `Promise<ApiResponse<T>>`, positional `undefined` placeholders — targets
the wrong runtime. The flow, controllers and operation set survive the port; the entire Phase 4
body specification does not.

Why the language column did not catch it: the hub lists **TypeScript** for both PayPal and NYT, so
the guide's gating check (*"check the language column before you commit"*) **passes** for
TypeScript. The column is not a currency or completeness signal. See BUG-31.

**Mitigating:** no code exists yet, so the cost is re-planning rather than rewriting. Port table in
`docs/hackathon-guide.md` §8.

**Correction to my own earlier report:** I cited `.gitignore:6-16` (`bin/`, `obj/`, `*.suo`,
`*.nupkg`, `packages/`) and `.gitignore:27` (`.vs/`) as dead .NET residue evidencing a careless
language switch. With C#/.NET as the target those entries are **correct and load-bearing**. Retract
that sub-point; the rest of BUG-03 stands and is now stronger.

### BUG-04 · Medium · Neither findings entry follows the template that file defines
`FINDINGS.md:8-15` defines five fields: *Asked for / Agent produced / Actually correct / Should
have been prevented by / Reproducible*. Finding 0 uses *Asked for / Observed / Consequence /
Reproducible / Status*. Finding 1 uses *Asked for / Status*.

The field that carries the entire evaluative payload — **"Should have been prevented by:
`<skill name>`"** — is absent from both entries. The report format cannot answer the question
the project is asking.

### BUG-31 · High · The hub's language column overstates support, and it is the guide's gating check
*Added 2026-08-03. This is a defect in the product under test, not in this repo — it belongs in
`FINDINGS.md` as a conforming entry. Recorded here because it is the root cause of BUG-03.*

The guide's only instruction for choosing a plugin is *"Check the language column before you
commit."* The hub lists 7 languages for both PayPal and New York Times, including TypeScript.
Per organiser guidance, **C#/.NET is the only language with an updated SDK and updated skills**, and
per `FINDINGS.md` Finding 0 it is the only one shipping an SDK map.

So the column tells you a language *exists* while withholding whether it is current, whether its
skills are complete, and whether it has an operation contract. Following the guide's gating check
exactly — as this repo did — lands you on a stale SDK with no contract, and the check reports
success. The prescribed verification cannot detect the condition that matters.

Contributing signals the hub does not surface: `C#/.NET` is listed for **all 22** plugins and always
appears first; long language lists are truncated to `+1` with no way to see the remainder from the
listing; and no per-language freshness, skill-coverage, or SDK-map indicator exists anywhere in the
column.

Secondary: the guide claims **29 plugins**, the hub renders **22**. One of the two is stale.

---

## B. Ground-truth contradictions

These matter more than ordinary doc drift: the plan's premise is that it supplies authoritative
ground truth (`readme:1`, `plan:8-9`). Where it contradicts itself, it *manufactures* the very
errors it tells the reader to hunt for.

### BUG-05 · High · Enum member names contradict themselves inside one file
`plan:64` — `ItemCategory` (**DigitalGoods**), `ApplicationContextShippingPreference`
(**NoShipping**), `PayPalExperienceUserAction` (**PayNow**) — PascalCase.
`plan:237-238` — the **NO_SHIPPING** member, the **PAY_NOW** member — SCREAMING_SNAKE.
Mirrored at `readme:112-113`.

`plan:255` lists *"an invented enum member"* as one of four classic misses to catch and log. One
of these two spellings is necessarily invented, and the ground-truth block is the source of it.

### BUG-06 · Medium · "Headers before the body" is false for `createOrder`, per the plan's own signature
`plan:44` (*"note the optional **headers come before the body**"*), `plan:142` and `readme:33`
state the rule generally. `plan:46-47` then gives
`createOrder(body, payPalMockResponse?, payPalRequestId?, …)` — body **first**.

`plan:349` (§12 candidate 2) names *"createOrder / captureOrder"* as the header-before-body trap.
For `createOrder` that is backwards. Only `captureOrder` (`plan:49-50`) and
`refundCapturedPayment` (`plan:52-53`) place the body after the headers.

### BUG-07 · Medium · Two divergent copies of the ground truth, no designated authority
`README.md` is the prose draft; `nyt-unlock-build-plan.md` is the structured version. They already
disagree (BUG-05, BUG-06), and neither declares itself canonical. An agent handed both may pick
either, which defeats the purpose of writing ground truth down.

### BUG-08 · Medium · Phase 4's prose argument order for `createOrder` is unfollowable
`plan:241-243`: *"…a deterministic payPalRequestId … then undefined for the remaining headers,
and prefer 'return=representation'."* Per `plan:46-47`, `prefer` is position 6 of 7 — it comes
**before** `payPalAuthAssertion`, not after "the remaining headers". Followed literally, the
prose puts `prefer` in the wrong slot.

### BUG-09 · Medium · The NYT auth credential object is never named
PayPal's is given exactly (`clientCredentialsAuthCredentials: { oauthClientId,
oauthClientSecret }`, `plan:39`). NYT's is only ever *"the API-key credential object"*
(`plan:174`) / *"an API key as a custom query parameter"* (`plan:71`). The one fact needed to
construct the second client is missing from the block that exists to supply such facts.

---

## C. Specification logic bugs — these will ship as real defects

### BUG-10 · Critical · The idempotency key collides with a body required to change every attempt
`plan:241-242`: `payPalRequestId` = uuid v5 of `articleId + price + UTC date` — **constant for a
whole UTC day**.
`plan:233`: `invoiceId` = `unlock-<shortHash>-<epochSeconds>`, *"unique per attempt"* — **changes
every attempt**.

Second `buy` of the same article on the same day therefore sends a *different body* under the
*same* `PayPal-Request-Id`. Either PayPal rejects the reused request id against a changed payload,
or it replays the first order and silently discards the new `invoiceId`. `buy` is broken on retry
in both directions, and the two requirements cannot both be satisfied as written.

### BUG-11 · Critical · The idempotency key suppresses the headline error path the demo depends on
`plan:266-268` reuses *"the same deterministic payPalRequestId"* for `captureOrder`. Two `claim`
runs then send an identical request id **and** an identical body — the canonical idempotent
replay, which returns the original success response rather than a 422.

But `plan:279-281` requires the `ORDER_ALREADY_CAPTURED` recovery branch, `plan:291` makes
*"running claim twice hits the recovery branch"* an acceptance criterion, and `plan:364` makes it
demo step 5. The plan's own idempotency choice is designed to prevent the error it then requires
you to demonstrate.

### BUG-12 · High · Demo step 6 cannot reach PayPal
`plan:292` and `plan:365`: `claim 4 --mock INSTRUMENT_DECLINED`. Index 4 was never bought — the
walkthrough buys index 3 only (`plan:362`). `claim` *"Load[s] orderId from the ledger"*
(`plan:266`), and no ledger entry exists for index 4, so it fails at ledger lookup before any SDK
call.

Secondary defect: even against a bought order, `INSTRUMENT_DECLINED` on capture presupposes an
**approved** order. An unapproved one returns `ORDER_NOT_APPROVED` (`plan:282`) instead — so the
"hard failure, handled by type" step demonstrates the wrong branch.

### BUG-13 · High · The index→article mapping is unstable, and every mutating command keys on it
`plan:202-203` caches search results to `.cache/search.json`, overwritten by each `search`.
All four downstream commands take `<index>`: `buy` (`plan:219`), `claim` (`plan:264`), `status`
(`plan:301`), `refund` (`plan:307`).

Run `search "x"`, `buy 3`, then `search "x" --page 2`, then `claim 3`: index 3 now resolves to a
different `articleId`, so the ledger lookup finds the wrong entry or none. Nothing binds an index
to the search that produced it — no query hash, no page number, no generation counter — even
though the cache does store query and timestamp.

### BUG-14 · High · Phase 4 prints a token the ledger schema never stores
`plan:221-222`: *"If the ledger already has a granted unlock for that article, print the existing
token and exit 0 without calling PayPal."*

The ledger schema is `{ articleId, orderId, invoiceId, status: 'CREATED', createdAt }`
(`plan:245`), updated to `{ status: 'GRANTED', captureId, capturedAt }` (`plan:271-272`). There is
no token field and no `exp` field. The early-exit path reads state that is never written.
Related: with a 24h expiry (`plan:273-274`) and no stored `exp`, an expired token would be
reprinted as if valid.

### BUG-15 · High · No way to verify a token, so the refund acceptance criterion is untestable
`plan:311-312` requires refund to set the ledger to REVOKED *"so token verification fails
afterwards"*; `plan:315` asserts *"a partial refund flips the ledger so the token stops
validating."*

The command set is `search | buy | claim | status | refund` (`plan:91`, `plan:163`). There is no
`verify` command and no other consumer of a token. Nothing in the CLI ever validates one, so the
criterion cannot be exercised, let alone shown in the video.

### BUG-16 · High · A partial refund revokes 100% of access
`plan:307` offers `refund <index> [--amount 0.50]`; `plan:311` sets the entry to REVOKED on
success regardless of amount. A buyer refunded 50c of 99c loses all access. `plan:315` bakes this
into the acceptance criterion, so it is specified behaviour rather than an oversight in passing —
but it is still wrong for a partial refund.

### BUG-17 · Medium · Refund error handling is specified by hand-wave
`plan:312`: *"Handle the over-refund and already-refunded issues explicitly by issue code."* No
issue code is named and no exit code is assigned — in direct contrast to Phase 5, which enumerates
both for five distinct cases (`plan:279-285`). Exit codes 3–8 are allocated (`plan:205-207`,
`plan:282-285`); refund receives none, so "explicitly" is unimplementable as written.

### BUG-18 · Medium · `status` crashes before `claim`
`plan:302` calls `PaymentsApi.getCapturedPayment(captureId)` unconditionally. `captureId` is only
written on a successful claim (`plan:271-272`), so a `CREATED`-state entry has none. No guard,
fallback, or partial-reconciliation path is specified for the ordinary pre-claim state.

### BUG-19 · Medium · `--price` is unvalidated and the sum invariant is only a comment
`plan:219` accepts `--price`, which feeds `amount.value`, `breakdown.itemTotal.value` and
`items[0].unitAmount.value` (`plan:227-231`). `--price 1` or `--price 0.999` violates PayPal's
currency-decimal-precision rule and 400s. The *"breakdown MUST sum exactly to amount.value"*
invariant (`plan:228`) is stated in a code comment only — no validation step is required anywhere.

### BUG-20 · Medium · `search` cannot run without PayPal credentials
`plan:168-170` requires `config.ts` to fail fast listing **all five** missing vars, and
`plan:179` requires `logStartupBanner()` *"first in every command"*. The NYT-only read path is
therefore gated on `PAYPAL_CLIENT_ID` / `PAYPAL_CLIENT_SECRET`, which it never uses. The
first command in the demo (`plan:362`) is the one most likely to be run by someone who has only
an NYT key.

### BUG-21 · Medium · Token payload uses an unescaped delimiter, and the HMAC input is ambiguous
`plan:273-274`: `base64url(articleId + '.' + exp) + '.' + HMAC-SHA256 of that payload`.

Two problems. (1) Parsing splits on `.`, but `articleId` is an arbitrary NYT identifier — any `.`
in it makes the `exp` boundary ambiguous, and two different `(articleId, exp)` pairs can serialise
identically. (2) *"that payload"* does not say whether the HMAC covers the raw
`articleId + '.' + exp` string or its base64url form; the two produce different, non-interoperable
tokens, and the spec is the only reference.

### BUG-22 · Low · NYT 400 is documented then left unhandled
`plan:75` lists *"400 bad query, 401 missing key, 429 rate limit"*. Phase 3 branches only on 429
and 401 (`plan:205-207`). A malformed query surfaces as an unhandled throw.

---

## D. Security and secret handling

### BUG-23 · High · The production guard is defeated by the mixed-case enum the plan itself flags
Guard (`plan:176-177`): refuse to continue *"if PAYPAL_ENV is production unless
ALLOW_PRODUCTION=true"*.
Enum (`plan:40`): `Production = 'production'`, `Sandbox = 'Sandbox'` — *"mixed casing"*, called out
as a genuine trap.

A user who writes `PAYPAL_ENV=Production` — the member spelling, and the capitalisation
`.env.example:7` teaches by shipping `PAYPAL_ENV=Sandbox` — fails a comparison against the literal
`'production'`. The guard passes, and the CLI transacts against **live PayPal** ungated. No
case-normalisation, no allow-list, and no "unrecognised value → refuse" rule is specified.

The blast radius is real money on a real merchant account, reached by a plausible typo that the
repo's own example file encourages.

### BUG-24 · High · Phase 7 instructs the agent to put live secrets in its transcript
`plan:122` (§4): *"Do these yourself — credentials should never be pasted into an agent
transcript."*
`plan:330-332` (Phase 7): *"Verify no secret appears anywhere in the repo: grep the tree for the
literal client id and secret values…"*

An agent cannot grep for literal secret values without being given them. Phase 7 mandates exactly
the leak §4 exists to prevent. The check is also unnecessary in that form — `git check-ignore` and
a scan for the *variable names* achieve the same assurance without ever handling the values.

### BUG-25 · Low · `.env.example` ships non-empty values against its own instruction
`plan:180-181` / `readme:65`: *"Write .env.example with the keys and empty values."* Shipped file
pre-fills `PAYPAL_ENV=Sandbox` (`.env.example:7`),
`RETURN_URL=https://example.com/unlock/return` (`:16`) and `CANCEL_URL=…/cancel` (`:17`).

Defensible as helpful defaults, but it is a spec deviation, and per BUG-23 the `PAYPAL_ENV`
default is precisely the value whose casing the guard mishandles.

---

## E. Hygiene and documentation defects

### BUG-26 · High · `README.md` is not a README — it is the operator's planning memo
`readme:1` opens *"I read the actual plugin skills and both generated SDKs on GitHub, so the plan
below is grounded… tell your agent to re-confirm each fact against its own clone."* It is
second-person instructions to whoever runs the build, committed as the repository's front door.

Phase 7 (`plan:323-329`) specifies the README's required contents: one-paragraph description,
prerequisites, env-var table copied from `.env.example`, one install-and-run command block, the
five-command walkthrough with example output, and the two error-path repro steps. **None** is
present. It also largely duplicates `nyt-unlock-build-plan.md`, which is the root cause of BUG-05
through BUG-08.

### BUG-27 · Low · The environment-variable count is stated three incompatible ways
`plan:99`: *"five required, two optional"* → 7. `.env.example` ships 8 (adds `ALLOW_PRODUCTION`).
`readme:65` and `readme:180`: *"the five keys"* / *"the five env vars"*.

`ALLOW_PRODUCTION` is load-bearing for the Phase 2 guard (`plan:177`) yet appears in no §3 list —
it exists only in `.env.example:19`.

### BUG-28 · Low · The Phase 2 acceptance criterion hardcodes the literal Phase 2 forbids
`readme:68`: the banner must print *"a sandbox base URL for PayPal and api.nytimes.com for NYT."*
`plan:175-177` requires reading the base URL *"off the client/configuration, not a hardcoded
literal."* The test asserts the constant the implementation is barred from using.

### BUG-29 · Low · Ground truth stated, never exercised
`plan:33`: *"Retries are disabled by default (maxNumberOfRetries = 0) and only GET/PUT are retried
when enabled."* `plan:354` (§12 candidate 7) asks whether the agent knew this. No phase configures,
asserts, or observes retry behaviour, so the candidate finding can never be evidenced.

### BUG-30 · Low · Phase 1's sole deliverable is absent
`plan:89` places `docs/contract.md` in the repo layout as the Phase 1 output, gated before any
`src/` code (`plan:149`). `docs/` does not exist and nothing is tracked under it. Phase 1 has not
run — consistent with BUG-01.

---

## F. Checked and cleared — not defects

Recorded so the report's negatives are as trustworthy as its positives.

- **`.gitignore` `.env.*` / `!.env.example` ordering is correct.** `git check-ignore -v
  .env.example` exits 1 — the negation on `.gitignore:4` follows the pattern on `:3`, so the
  example file is trackable while `.env` (`:2`) and `.env.local` etc. stay ignored.
- **`.claude/settings.local.json` is properly excluded.** `git check-ignore` matches
  `.gitignore:29`, and the file is absent from `git ls-files` — it was never committed before the
  rule was added.
- **No secret material in tracked files.** No `.env` exists on disk; `.env.example` holds empty
  values for both PayPal credentials and the NYT key.
- **Commit ordering was handled correctly.** `0b9621d` added the `.env`-ignoring `.gitignore`
  before any secret could land on disk, as its message claims.

---

## Recommended triage order

1. **BUG-03** — decide the language *first*. Every downstream spec defect below is scoped by it, and
   fixing TypeScript details you are about to discard is wasted effort. Target C#/.NET.
2. **BUG-01** — nothing else in the evaluation can proceed. Register the marketplace, install both
   plugins, run `doctor`, then answer Finding 1 and verify BUG-31's claim locally.
3. **BUG-23, BUG-24** — the two security defects. Both are cheap to correct in the spec and both
   are dangerous if code is written from it verbatim. BUG-23 survives the port to C# unchanged.
4. **BUG-10, BUG-11** — the idempotency contradiction. It breaks `buy` retries *and* silently
   removes the error path the whole demo is built around. Language-independent.
5. **BUG-05 through BUG-09** — settle one canonical ground-truth document before any code is
   generated, or the plan will inject the errors it means to detect. Note the port re-opens these:
   the enum spellings must be re-derived from the **.NET** SDK map, not translated from the
   TypeScript guesses.
6. **BUG-31, BUG-04** — the two findings that have external value. BUG-31 is a conforming
   findings-log entry as-is; BUG-04 is what stops the log from being submittable.
7. Everything else, by severity.

**Port sensitivity.** BUG-06 and BUG-08 (positional headers before the body) are artifacts of the
TypeScript signatures and **may not survive** the move to C#, which has named optional parameters.
Re-check them against the .NET SDK map rather than carrying them over. BUG-05 (enum member casing)
gets *worse* in C#, where PascalCase members are idiomatic and both plan spellings are wrong.

Nothing in this repo was modified to produce this report. The guide fetched during this audit is
recorded at `docs/hackathon-guide.md`, with live status at `docs/end-goal-checklist.md`.
