# nyt-unlock

A micro-paywall CLI: search New York Times articles, pay USD 0.99 through PayPal sandbox to unlock
one, receive a signed 24-hour access token, reconcile the payment, and refund it. Six operations
across three controllers — NYT `Search`, PayPal `Orders` and `Payments` — composed into a single
flow, against live sandbox APIs.

Built in C#/.NET as a test of the [Context Plugins](https://hub.contextplugins-hub.pages.dev/)
`paypal` and `nytimes` plugins. The evaluation write-up is [`FINDINGS.md`](./FINDINGS.md); the
contract the code was built from is [`docs/contract.md`](./docs/contract.md).

## Prerequisites

- **.NET SDK 10** (`dotnet --version`) — the project targets `net10.0`; both SDKs are `netstandard2.0`
- **Node 18+** — only for the plugin installer (`npx context-plugins`)
- **git** — the SDKs are consumed from source clones, not NuGet
- **A PayPal sandbox REST app** — Developer Dashboard → Apps & Credentials → **Sandbox** → create app
- **A PayPal sandbox personal (buyer) account** — Testing Tools → Sandbox Accounts. You log in as
  this buyer to approve the order.
- **An NYT API key** with **Article Search** enabled — developer.nytimes.com

## Environment variables

Copy `.env.example` to `.env` and fill it in. `.env` is gitignored.

| Variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `PAYPAL_CLIENT_ID` | for `buy`/`claim`/`status`/`refund` | — | Sandbox REST app client id |
| `PAYPAL_CLIENT_SECRET` | for `buy`/`claim`/`status`/`refund` | — | Sandbox REST app secret |
| `PAYPAL_ENV` | no | `Sandbox` | `Sandbox` or `production`; case-insensitive, unknown values are refused |
| `NYT_API_KEY` | for `search` | — | NYT key with Article Search enabled |
| `UNLOCK_SIGNING_SECRET` | for `claim`/`verify` | — | Any random string; signs the access token |
| `RETURN_URL` | no | `https://example.com/unlock/return` | Post-approval redirect |
| `CANCEL_URL` | no | `https://example.com/unlock/cancel` | Cancelled-approval redirect |
| `ALLOW_PRODUCTION` | no | unset | Must be `true` to run against PayPal production |

Credentials are validated **per command**: `search` needs only `NYT_API_KEY` and will not ask for
PayPal credentials it never uses.

## Install and run

```bash
git clone https://github.com/haseeb-mhr/context-plug-in
cd context-plug-in

# Neither SDK is published to NuGet — both are consumed from a source clone.
git clone --depth 1 --branch main https://github.com/context-plugins/paypal-csharp-sdk  sdk/paypal-csharp-sdk
git clone --depth 1 --branch main https://github.com/context-plugins/nytimes-csharp-sdk sdk/nytimes-csharp-sdk

cp .env.example .env      # then fill it in
dotnet run --project src/NytUnlock -- --help
```

## Walkthrough

Every command prints the resolved base URL of each client before it does anything else, read off the
client configuration rather than hardcoded — the hackathon's "confirm the host before the first
write" ground rule.

```
$ dotnet run --project src/NytUnlock -- search "artificial intelligence"
nyt-unlock — resolved hosts
  NYT     https://api.nytimes.com/svc/search/v2  (env: production)

10 of 12841 hits for "artificial intelligence" (page 0)

[0] How A.I. Is Changing the Way We Work
    2026-07-28  https://www.nytimes.com/2026/07/28/...
    A shift that began in software has reached every desk in the building.
...
Cached to .cache\search.json — buy with: nyt-unlock buy <index>
```

```
$ dotnet run --project src/NytUnlock -- buy 0
nyt-unlock — resolved hosts
  PayPal  https://api-m.sandbox.paypal.com  (env: Sandbox)
  note    SDK maps BOTH PayPal environments to the sandbox host.

index 0 -> How A.I. Is Changing the Way We Work
           nyt://article/xxxxxxxx-...   (from search "artificial intelligence", saved ...)

Order created — CREATED
  order id       5O190127TN364715T
  invoice id     unlock-a1b2c3d4e5f6
  amount         USD 0.99
  approve        https://www.sandbox.paypal.com/checkoutnow?token=5O190127TN364715T

Approve in the browser as your sandbox buyer, then run: nyt-unlock claim 0
```

Approve the link as your sandbox buyer, then:

```
$ dotnet run --project src/NytUnlock -- claim 0
Access captured.
  token          bnl0Oi8vYXJ0aWNsZS8...1794518400.Xq7mK...
  article        https://www.nytimes.com/2026/07/28/...
  capture id     3C679366HH908993F
  expires        2026-08-04 18:30:00Z

$ dotnet run --project src/NytUnlock -- status 0     # reconcile order + capture
$ dotnet run --project src/NytUnlock -- refund 0     # full refund, revokes access
$ dotnet run --project src/NytUnlock -- verify <token>
INVALID — access revoked by refund
```

## The two error paths

Both are handled by **type and issue code**, never by matching message text.

**1. Double capture → `ORDER_ALREADY_CAPTURED`, recovered.** Run `claim` twice:

```
$ dotnet run --project src/NytUnlock -- claim 0
already captured - access restored from order 5O190127TN364715T, capture 3C679366HH908993F
Access restored.
```

The second call raises `SdkException<CaptureOrderError>`; the handler reads
`Error.Details[0].Issue`, recognises `ORDER_ALREADY_CAPTURED`, re-reads the order with `GetOrder`,
recovers the existing capture id, and exits 0 with access intact.

> This path only works because `claim` sends a **fresh** `PayPal-Request-Id` per attempt. Reusing one
> idempotency key — as the original plan specified — turns the second call into an idempotent replay
> that returns the first success, silently removing the error path. See `BUGS.md` BUG-11.

**2. Deterministic hard failure via PayPal's negative-testing header.**

```
$ dotnet run --project src/NytUnlock -- claim 0 --mock INSTRUMENT_DECLINED
The buyer's instrument was declined. PayPal's guidance is to restart the order: run `buy` again.
# exit 6
```

`--mock` sends `{"mock_application_codes":"..."}` as `PayPal-Mock-Response`. This is still a **live
sandbox call** — PayPal returns a real failure response — so it does not violate "not mocks".
`INSTRUMENT_DECLINED` on capture presupposes an approved order; on an unapproved one you will get
`ORDER_NOT_APPROVED` (exit 5) instead.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success |
| 1 | Token invalid (`verify`) |
| 2 | Usage or configuration error |
| 3 | NYT rate limit (429) |
| 4 | NYT auth failure (401) |
| 5 | `ORDER_NOT_APPROVED` |
| 6 | `INSTRUMENT_DECLINED` |
| 7 | Other handled PayPal error |
| 8 | Unhandled API or transport error |

## Notes

- **PayPal defaults to sandbox** and refuses to leave it without `ALLOW_PRODUCTION=true`. Note that
  the SDK maps *both* environments to the sandbox host — `FINDINGS.md` Finding 6.
- **NYT has no sandbox.** The SDK's `ServerEnvironment` exposes only `Production`, so Article Search
  calls hit production. They are read-only.
- `ledger.json` and `.cache/` are local state and are gitignored.
- Deviations from the original build plan — a `verify` command, partial refunds keeping access,
  per-command credential checks, and the idempotency change above — are each annotated in the source
  with the `BUGS.md` entry that motivated them.
