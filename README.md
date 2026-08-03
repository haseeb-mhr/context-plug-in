# nyt-unlock

**Pay 99 cents to read one New York Times article.**

A command-line micro-paywall. You search NYT headlines, pick one, pay USD 0.99 through PayPal's
sandbox, and get back a signed token that grants 24 hours of access to that single article. You can
then reconcile the payment against PayPal and refund it — which revokes the token.

It models the thing subscription paywalls don't do: buying *one* article, as a one-off, with a real
payment and a real receipt behind it.

> **Nothing here costs money.** Every PayPal call goes to the sandbox, and the app refuses to run
> against live PayPal unless you explicitly set `ALLOW_PRODUCTION=true`.

---

## What it does, step by step

```
  search  ──►  buy  ──►  [approve in browser]  ──►  claim  ──►  status  ──►  refund
    │           │                                     │           │            │
  NYT       PayPal                                 PayPal      PayPal       PayPal
  finds     creates a                              captures    reconciles   refunds and
  articles  $0.99 order                            the money   the payment  revokes access
                                                   + mints
                                                   your token
```

1. **`search "climate"`** — queries NYT Article Search, prints a numbered list of headlines, and
   caches them so later commands can refer to an article by its index.
2. **`buy 0`** — creates a PayPal order for USD 0.99 covering article `0`, with the headline as the
   line item. Prints an approval link.
3. **Approve the link in a browser**, logged in as your PayPal *sandbox buyer*. This is the one step
   the CLI cannot do for you — it is the buyer consenting to pay.
4. **`claim 0`** — captures the money and mints your access token, valid 24 hours.
5. **`status 0`** — reads the order and the capture back from PayPal and reconciles them against the
   local ledger, confirming your article id round-tripped through the payment.
6. **`refund 0`** — refunds the capture. A full refund revokes the token; a partial refund keeps
   access.
7. **`verify <token>`** — checks any token's signature, expiry and revocation status.

State lives in two local files: `ledger.json` (what you bought) and `.cache/search.json` (your last
search). Both are gitignored.

---

## Quickstart

### 1. Install the tools

- **[.NET SDK 10](https://dotnet.microsoft.com/download)** — check with `dotnet --version`
- **git**
- **PowerShell 7+** (`pwsh`) — only to run the one-line SDK patch in step 3

### 2. Clone the app and its two SDKs

Neither SDK is published to NuGet, so both are consumed from a source clone.

```bash
git clone https://github.com/haseeb-mhr/context-plug-in
cd context-plug-in

git clone --depth 1 --branch main https://github.com/context-plugins/paypal-csharp-sdk  sdk/paypal-csharp-sdk
git clone --depth 1 --branch main https://github.com/context-plugins/nytimes-csharp-sdk sdk/nytimes-csharp-sdk
```

### 3. Patch the NYT SDK — required, not optional

```bash
pwsh scripts/patch-sdk.ps1
```

**Skip this and every search crashes.** The generated NYT SDK cannot parse its own Article Search
response: `ArticleSearchArticle.PrintPage` is typed `int?` while the API returns a string (`"3"`), so
the response dies in the JSON deserializer. A second patch rebinds `Response1.Meta` from `meta` to
`metadata` — the key the API actually sends — without which hit counts are always empty.

The script is idempotent, prints what it changed, and **must be re-run if you re-clone `sdk/`**. Both
defects are written up in [`FINDINGS.md`](./FINDINGS.md) as Findings 8 and 9.

### 4. Get your credentials

Three values, from two places. Neither service charges you.

| What | Where |
| --- | --- |
| PayPal **client ID** and **secret** | [developer.paypal.com](https://developer.paypal.com) → Apps & Credentials → **Sandbox** tab → create an app |
| PayPal **sandbox buyer** login | Same dashboard → Testing Tools → **Sandbox Accounts** → the *personal* account. Note its email and password — you log in as this buyer in step 6 |
| NYT **API key** | [developer.nytimes.com](https://developer.nytimes.com) → create an app → enable **Article Search** |

### 5. Configure

```bash
cp .env.example .env
```

Open `.env` and fill in `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET` and `NYT_API_KEY`. Set
`UNLOCK_SIGNING_SECRET` to any random string — it signs your tokens locally. Everything else has a
working default.

`.env` is gitignored and will not be committed.

### 6. Run it

```bash
dotnet run --project src/NytUnlock -- --help
dotnet run --project src/NytUnlock -- search "artificial intelligence"
dotnet run --project src/NytUnlock -- buy 0
# open the printed approve link, log in as your sandbox buyer, approve
dotnet run --project src/NytUnlock -- claim 0
```

> The `--` matters: it separates `dotnet run`'s own arguments from the app's.

---

## Commands

| Command | What it does |
| --- | --- |
| `search "<query>" [--page N] [--sort newest\|oldest\|relevance\|best]` | Search NYT and cache the results |
| `buy <index> [--price 0.99]` | Create a PayPal order for the cached article at `<index>` |
| `claim <index> [--mock <CODE>]` | Capture payment and mint a 24-hour token |
| `status <index>` | Reconcile the order and capture against the ledger |
| `refund <index> [--amount 0.50] [--note "..."]` | Refund; partial keeps access, full revokes it |
| `verify <token>` | Check a token's signature, expiry and revocation |
| `--help` | Full usage |

Already own an article? `buy` prints your existing token and exits without calling PayPal again.

---

## Walkthrough

Every command prints the resolved base URL of each API **before** it does anything else, read off the
client configuration rather than hardcoded. If you are ever unsure which host you are talking to, the
first two lines of output tell you.

The output below is real, captured from an actual run, except where marked.

```
$ dotnet run --project src/NytUnlock -- search "artificial intelligence"
nyt-unlock — resolved hosts
  NYT     https://api.nytimes.com/svc/search/v2  (env: production)

10 of 10000 hits for "artificial intelligence" (page 0)

[0] Is A.I. ‘Scheming’ Against Us?
    2026-08-01  https://www.nytimes.com/2026/08/01/business/ai-scheming.html
    Researchers are sounding the alarm on sneaky artificial intelligence models that stray from hum

[1] If You’re Over 40, You’re Ready to Use A.I.
    2026-07-27  https://www.nytimes.com/2026/07/27/opinion/teaching-kabbalah-ai.html
    A.I. should be treated like the mystical Jewish practice of kabbalah.
...
Cached to .cache\search.json — buy with: nyt-unlock buy <index>
```

```
$ dotnet run --project src/NytUnlock -- buy 0
nyt-unlock — resolved hosts
  PayPal  https://api-m.sandbox.paypal.com  (env: Sandbox)
  note    SDK maps BOTH PayPal environments to the sandbox host.

index 0 -> Is A.I. ‘Scheming’ Against Us?
           nyt://article/474036bc-3c1d-5148-873a-8ed347c0c7ce   (from search "artificial intelligence", saved 2026-08-03 13:53:10Z)

Order created — PAYER_ACTION_REQUIRED
  order id       85A03610WW326725D
  invoice id     unlock-313f12c70fca
  amount         USD 0.99
  approve        https://www.sandbox.paypal.com/checkoutnow?token=85A03610WW326725D

Approve in the browser as your sandbox buyer, then run: nyt-unlock claim 0
```

Open that approve link. You should see **USD 0.99**, **one line item** named after the article and
marked as digital goods, and **no shipping section**. Approve as your sandbox buyer.

If you run `claim` *before* approving, PayPal refuses and the app tells you why:

```
$ dotnet run --project src/NytUnlock -- claim 0
Approve the order first — open the approve link from `buy` and pay as your sandbox buyer.
# exit 5
```

`status` works at any point, and degrades gracefully before there is a capture to read:

```
$ dotnet run --project src/NytUnlock -- status 0
Order
  order id       85A03610WW326725D
  status         PAYER_ACTION_REQUIRED
  custom_id      nyt://article/474036bc-3c1d-5148-873a-8ed347c0c7ce
  invoice_id     unlock-313f12c70fca
  amount         USD 0.99
  create time    2026-08-03T13:51:31Z

No capture yet — order is not claimed. Partial reconciliation only.

Ledger
  status         CREATED
```

That `custom_id` is the NYT article id going out with the payment and coming back from PayPal
unchanged — which is what makes the payment traceable to the article.

Once approved *(output below is illustrative — this path needs an approved order)*:

```
$ dotnet run --project src/NytUnlock -- claim 0
Access captured.
  token          bnl0Oi8vYXJ0aWNsZS80NzQwMzZiYy0...1785766800.Xq7mKp...
  article        https://www.nytimes.com/2026/08/01/business/ai-scheming.html
  capture id     3C679366HH908993F
  expires        2026-08-04 13:55:00Z

$ dotnet run --project src/NytUnlock -- refund 0
Refund COMPLETED — ledger now REVOKED
Access revoked — the token will no longer validate.

$ dotnet run --project src/NytUnlock -- verify bnl0Oi8vYXJ0aWNsZS80NzQwMzZiYy0...
INVALID — access revoked by refund
```

---

## The two failure paths

Both are matched on the error's **type and issue code**, never on its message text — so they keep
working if PayPal rewords a message.

**1. Paying twice → recovered, not crashed.** Run `claim` a second time. PayPal returns 422
`ORDER_ALREADY_CAPTURED`; the app re-reads the order, recovers the capture that already exists,
restores your token, and exits 0:

```
already captured - access restored from order 85A03610WW326725D, capture 3C679366HH908993F
```

**2. A declined card, on demand.** PayPal's sandbox can be told to fail:

```
$ dotnet run --project src/NytUnlock -- claim 0 --mock INSTRUMENT_DECLINED
The buyer's instrument was declined. PayPal's guidance is to restart the order: run `buy` again.
# exit 6
```

`--mock` sends PayPal's `PayPal-Mock-Response` header. This is still a **real** sandbox call
returning a **real** failure — nothing is stubbed locally. Note that `INSTRUMENT_DECLINED` assumes an
approved order; on an unapproved one you will get `ORDER_NOT_APPROVED` (exit 5) instead.

---

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| `JsonException … print_page` | You skipped step 3. Run `pwsh scripts/patch-sdk.ps1` |
| `Missing required environment variable …` | That command needs a value your `.env` doesn't have |
| `NYT rate limit hit - wait 60s` (exit 3) | NYT throttles by key. Wait a minute |
| `NYT rejected the key (401)` (exit 4) | Key is wrong, or Article Search isn't enabled on the app |
| `Approve the order first` (exit 5) | Open the approve link from `buy` and pay as the sandbox buyer |
| `PAYPAL_ENV=… requires ALLOW_PRODUCTION=true` | Intentional guard. Use `Sandbox` |
| `No cached search` | Run `search` first — later commands resolve indexes from its cache |
| Index resolves to the wrong article | A newer `search` overwrote the cache. The resolved uri is printed on every command so you can check |

### Exit codes

| Code | Meaning | | Code | Meaning |
| --- | --- | --- | --- | --- |
| 0 | Success | | 5 | `ORDER_NOT_APPROVED` |
| 1 | Token invalid | | 6 | `INSTRUMENT_DECLINED` |
| 2 | Usage or config error | | 7 | Other handled PayPal error |
| 3 | NYT rate limit | | 8 | Unhandled API or transport error |
| 4 | NYT auth failure | | | |

---

## Configuration reference

| Variable | Required for | Default | Purpose |
| --- | --- | --- | --- |
| `PAYPAL_CLIENT_ID` | `buy` `claim` `status` `refund` | — | Sandbox REST app client id |
| `PAYPAL_CLIENT_SECRET` | `buy` `claim` `status` `refund` | — | Sandbox REST app secret |
| `PAYPAL_ENV` | — | `Sandbox` | `Sandbox` or `production`; case-insensitive, unknown values refused |
| `NYT_API_KEY` | `search` | — | NYT key with Article Search enabled |
| `UNLOCK_SIGNING_SECRET` | `claim` `verify` | — | Any random string; signs access tokens |
| `RETURN_URL` | — | `https://example.com/unlock/return` | Post-approval redirect |
| `CANCEL_URL` | — | `https://example.com/unlock/cancel` | Cancelled-approval redirect |
| `ALLOW_PRODUCTION` | — | unset | Must be `true` to leave sandbox |

Credentials are checked **per command** — `search` asks only for `NYT_API_KEY` and never for PayPal
credentials it doesn't use.

---

## Notes

- **PayPal is sandbox-only here.** The app refuses production without `ALLOW_PRODUCTION=true`, and the
  SDK maps *both* of its environments to the sandbox host anyway ([`FINDINGS.md`](./FINDINGS.md)
  Finding 6).
- **NYT has no sandbox.** Its SDK exposes only `Production`, so searches hit the real API. They are
  read-only.
- **Tokens are local.** `UNLOCK_SIGNING_SECRET` signs them and `ledger.json` records them; there is no
  server. Change the secret and every existing token stops validating.

## About this project

Built in C#/.NET to test whether the [Context Plugins](https://hub.contextplugins-hub.pages.dev/)
`paypal` and `nytimes` plugins let an AI agent write correct API integration code first time. Six
operations across three controllers — NYT `Search`, PayPal `Orders` and `Payments`.

- [`FINDINGS.md`](./FINDINGS.md) — the evaluation: 10 findings, including two SDK defects that make
  Article Search unusable as generated
- [`docs/contract.md`](./docs/contract.md) — the API contract the code was written from
- [`BUGS.md`](./BUGS.md) — defects in the original build plan, and how each was resolved
- [`docs/end-goal-checklist.md`](./docs/end-goal-checklist.md) — status against the hackathon bar
