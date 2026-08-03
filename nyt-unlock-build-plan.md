
# nyt-unlock — Context Plugins Hackathon Build Plan

An NYT article micro-paywall CLI built on two Context Plugins: **paypal** (primary) and **nytimes** (thin read-only edge).
Search NYT articles, pay USD 0.99 through PayPal sandbox to unlock one, receive a signed access token, reconcile the
payment, and refund it.

Grounded in the real generated SDKs (context-plugins/paypal-typescript-sdk and context-plugins/nytimes-typescript-sdk)
and the plugins' typescript-getting-started skills. Re-confirm every fact from your own clone as you go.

---

## 1. Why this clears the "done" bar

| Requirement | How it is met |
| --- | --- |
| 3+ endpoints, 2+ controllers, one flow | 6 operations across 3 controllers: SearchApi (NYT), OrdersApi, PaymentsApi (PayPal), composed as search -> buy -> claim -> status -> refund |
| One non-trivial request body | OrderRequest: purchaseUnits[] with amount.breakdown that must sum exactly, items[] with a string quantity and an ItemCategory enum, customId/invoiceId, plus paymentSource.paypal.experienceContext nested three levels deep |
| One deliberate error path | Double capture -> 422 CustomError, issue ORDER_ALREADY_CAPTURED, recovered by re-reading the order; second path via the PayPal-Mock-Response negative-testing header |
| Works against the real API | Live PayPal sandbox and live NYT Article Search; no mocks |
| Someone else can clone and run it | CLI, README with env table, one command to start |

---

## 2. Ground truth for the agent

Both SDKs are APIMatic v3.0 generic libraries and share one shape:

* Single exported `Client` class, constructed as `new Client({ ... })` with a `Partial<Configuration>` options object.
* Controllers are instantiated by you: `new OrdersApi(client)` — there are no controller accessors on the client.
* Every operation returns `Promise<ApiResponse<T>>`; the parsed body is `response.result`.
* Non-2xx **throws**: `ApiError`, or a typed subclass from `src/errors/`.
* Retries are disabled by default (maxNumberOfRetries = 0) and only GET/PUT are retried when enabled.

### PayPal SDK

* Install: `npm install "https://github.com/context-plugins/paypal-typescript-sdk#main"`
* Imports come from the package root (docs show `paypallib`) — confirm the "name" field in package.json.
* Configuration: `{ timeout, environment, clientCredentialsAuthCredentials: { oauthClientId, oauthClientSecret }, httpClientOptions, logging }`
* Environment enum: `Production = 'production'`, `Sandbox = 'Sandbox'` — mixed casing, use the enum member, never a literal.
* Documented 400/401/422 responses arrive as `CustomError` (extends ApiError, carries `.result` with details[].issue and debug_id).
* Controllers present: orders, payments, subscriptions, transaction-search, vault.

Operations used (note the optional **headers come before the body**):

    OrdersApi.createOrder(body, payPalMockResponse?, payPalRequestId?, payPalPartnerAttributionId?,
                          payPalClientMetadataId?, prefer?, payPalAuthAssertion?, requestOptions?)
    OrdersApi.getOrder(id, payPalMockResponse?, payPalAuthAssertion?, fields?, requestOptions?)
    OrdersApi.captureOrder(id, payPalMockResponse?, payPalRequestId?, prefer?,
                           payPalClientMetadataId?, payPalAuthAssertion?, body?, requestOptions?)
    PaymentsApi.getCapturedPayment(captureId, ...)
    PaymentsApi.refundCapturedPayment(captureId, payPalMockResponse?, payPalRequestId?, prefer?,
                                      payPalAuthAssertion?, body?: RefundRequest, requestOptions?)

Model field names are camelCase:

* `OrderRequest { intent, processingInstruction?, purchaseUnits[], paymentSource?, applicationContext? }`
* `PurchaseUnitRequest { referenceId?, amount, payee?, description?, customId?, invoiceId?, softDescriptor?, items?, shipping? }`
* `AmountWithBreakdown { currencyCode, value, breakdown? { itemTotal, shipping, handling, taxTotal, insurance, ... } }`
* `ItemRequest { name, unitAmount, tax?, quantity (STRING, pattern ^[1-9][0-9]{0,9}$), description?, sku?, url?, category? }`
* `PaymentSource { card?, token?, paypal?: PayPalWallet, ... }`
* `PayPalWallet { ..., experienceContext?: PayPalWalletExperienceContext, ... }`
* `PayPalWalletExperienceContext { brandName?, locale?, shippingPreference?, contactPreference?, returnUrl?, cancelUrl?, landingPage?, userAction?, ... }`
* Enums: `CheckoutPaymentIntent`, `ItemCategory` (DigitalGoods), `ApplicationContextShippingPreference` (NoShipping), `PayPalExperienceUserAction` (PayNow).
* The top-level `applicationContext` equivalents of brandName / shippingPreference / landingPage are marked **DEPRECATED** in favour of experienceContext.
* Every operation accepts `payPalMockResponse` — the sandbox negative-testing header. Use it for reproducible failures.

### NYT SDK

* Install: `npm install "https://github.com/context-plugins/nytimes-typescript-sdk#main"` (docs import from `nytimeslib`).
* Auth: API key as a custom query parameter.
* Controller: `SearchApi.returnsAnArrayOfArticles(beginDate?, endDate?, fq?, page?, q?, sort?, requestOptions?)`
  returning `ReturnsAnArrayOfArticlesResponse` with `result.response.docs[]` (webUrl, snippet, printPage, source, ...)
  and `result.response.meta { hits, offset, time }`.
* Errors are plain `ApiError`: 400 bad query, 401 missing key, 429 rate limit. There is no typed subclass — branch on statusCode.
* The generated example for docs[] omits the headline field: grep the Doc model in src/models/ for the real path before mapping it.

---

## 3. Repo layout

    nyt-unlock/
      package.json
      tsconfig.json
      .env.example
      .gitignore
      README.md
      FINDINGS.md
      docs/contract.md        <- produced in Phase 1, before any src/ code
      src/
        index.ts              command router: search | buy | claim | status | refund
        config.ts             env loading, both clients, startup banner
        nyt.ts                article search + cache
        checkout.ts           createUnlockOrder / captureUnlock / reconcile / refundUnlock
        ledger.ts             ledger.json + HMAC token mint/verify
        errors.ts             typed error mapping (CustomError issues -> exit codes)
        format.ts             table + line printing

Environment variables (five required, two optional):

    PAYPAL_CLIENT_ID          sandbox REST app client id
    PAYPAL_CLIENT_SECRET      sandbox REST app secret
    PAYPAL_ENV                Sandbox | production   (default Sandbox)
    NYT_API_KEY               NYT app key with Article Search enabled
    UNLOCK_SIGNING_SECRET     any random string, signs the access token
    RETURN_URL                optional, default https://example.com/unlock/return
    CANCEL_URL                optional, default https://example.com/unlock/cancel

---

## 4. Phase 0 — prep (human, not the agent)

1. Export `GITHUB_TOKEN` (any classic PAT, no scopes) to avoid the shared GitHub rate limit.
2. `npx context-plugins install paypal`
3. `npx context-plugins install nytimes`
4. `npx context-plugins doctor`
5. PayPal Developer -> Apps & Credentials -> **Sandbox** -> create app -> copy client id + secret.
6. PayPal Developer -> Testing Tools -> Sandbox Accounts -> note the **personal (buyer)** email + password. You will log in as this buyer to approve.
7. NYT Developer portal -> register an app -> enable **Article Search** -> copy the key.
8. Create FINDINGS.md and write entry #1: did installing the second plugin disturb the first?

Do these yourself — credentials should never be pasted into an agent transcript.

---

## 5. Phase 1 — grounding prompt (no feature code)

```
Two Context Plugins are installed in this repo: paypal and nytimes, both APIMatic v3.0
TypeScript SDKs. Before writing any code:

1. Load the typescript-getting-started skill for EACH plugin and clone both SDK sources to
   the system temp dir as the skills instruct (paypal-typescript-sdk, nytimes-typescript-sdk).
   Do not copy SDK source into this project.
2. Produce a contract sheet in docs/contract.md with, for each SDK: npm package id from
   package.json, the Client constructor signature, the full Configuration interface, every
   member of the Environment enum with its exact string value, the auth credential object
   name and its fields, and the base error type plus any typed subclasses in src/errors/.
3. From doc/controllers/, list the exact method signature of: SearchApi.returnsAnArrayOfArticles,
   OrdersApi.createOrder, OrdersApi.getOrder, OrdersApi.captureOrder,
   PaymentsApi.getCapturedPayment, PaymentsApi.refundCapturedPayment - including full
   positional parameter order, since several take optional headers BEFORE the body.
4. From doc/models/, write out the field trees for OrderRequest, PurchaseUnitRequest,
   AmountWithBreakdown, Money, ItemRequest, PaymentSource, PayPalWallet,
   PayPalWalletExperienceContext, RefundRequest, and the members of CheckoutPaymentIntent,
   ItemCategory, ApplicationContextShippingPreference, PayPalExperienceUserAction.
   Mark which fields are required and note any field the docs flag as DEPRECATED.

Stop after docs/contract.md. Write no src/ code yet.
```

**Acceptance:** the sheet names the real package ids, shows Sandbox = 'Sandbox', lists CustomError, and types quantity as a string.
Anything wrong here is your highest-value finding — the agent had both the source and the skill.

---

## 6. Phase 2 — skeleton and startup safety

```
Scaffold a Node 20 + TypeScript CLI named nyt-unlock in this repo. ESM, strict mode,
tsx for dev, no framework, no test runner yet. Files:

  src/index.ts    command router: search | buy | claim | status | refund, plus --help
  src/config.ts   env loading and both clients
  src/nyt.ts      src/checkout.ts  src/ledger.ts  src/errors.ts  src/format.ts  (stubs)

src/config.ts must:
- read PAYPAL_CLIENT_ID, PAYPAL_CLIENT_SECRET, PAYPAL_ENV, NYT_API_KEY,
  UNLOCK_SIGNING_SECRET from process.env via dotenv and fail fast with a list of any missing
  ones - never print their values
- construct ONE PayPal Client with clientCredentialsAuthCredentials and environment resolved
  from PAYPAL_ENV, defaulting to the Sandbox enum member; use the enum member, never a
  hardcoded string
- construct ONE NYT Client with the API-key credential object
- export logStartupBanner() that prints the resolved base URL of each client (read it off the
  client/configuration, not a hardcoded literal) and the PayPal environment name, and refuses
  to continue if PAYPAL_ENV is production unless ALLOW_PRODUCTION=true

Call logStartupBanner() first in every command. Add package.json scripts: dev, build, start.
Write .env.example with the keys and empty values. Add .gitignore covering .env, node_modules,
dist, ledger.json, .cache.
```

**Acceptance:** `npm run dev -- --help` prints a PayPal **sandbox** base URL and the NYT base URL before anything else runs.
That banner is a hackathon ground rule and belongs in the demo video.

---

## 7. Phase 3 — the read side

```
Implement src/nyt.ts and the search command:

  nyt-unlock search "<query>" [--page N] [--sort newest|oldest|relevance]

Use SearchApi.returnsAnArrayOfArticles with the correct positional argument order - pass
undefined for parameters you are not using rather than reordering. Map the Sort enum from the
--sort flag using real enum members read from the SDK.

From result.response.docs[], print a numbered table: index, headline (grep the Doc model for
the real headline field path - the generated example omits it), publication date, web URL and a
one-line snippet. Cache the raw docs array to .cache/search.json with the query and a timestamp
so later commands can resolve an index to an article without re-searching.

Error handling: catch ApiError. On 429 print "NYT rate limit hit - wait 60s" and exit 3; on 401
print a credential hint and exit 4. Do not string-match error messages; branch on
error.statusCode.
```

**Acceptance:** real headlines and URLs appear; .cache/search.json holds a stable article identifier usable as customId.

---

## 8. Phase 4 — the order body (where the plugin earns its keep)

```
Implement createUnlockOrder() in src/checkout.ts and the buy command:

  nyt-unlock buy <index> [--price 0.99]

Resolve <index> from .cache/search.json. If the ledger already has a granted unlock for that
article, print the existing token and exit 0 without calling PayPal.

Build an OrderRequest with EVERY field name and enum taken from the cloned SDK models:
- intent: the Capture member of CheckoutPaymentIntent
- purchaseUnits: exactly one PurchaseUnitRequest with
    amount: { currencyCode: 'USD', value: price, breakdown: { itemTotal: { currencyCode: 'USD',
      value: price } } }        // breakdown MUST sum exactly to amount.value
    items: [ { name: headline truncated to 127 chars, unitAmount: { currencyCode: 'USD',
      value: price }, quantity: '1', category: the DigitalGoods member of ItemCategory,
      url: article web URL } ]
    customId: the NYT article identifier, truncated to 255
    invoiceId: 'unlock-' + shortHash(articleId) + '-' + epochSeconds, max 127, unique per attempt
    description: headline truncated to 127
- paymentSource.paypal.experienceContext (NOT the deprecated top-level applicationContext):
    brandName: 'NYT Unlock'
    shippingPreference: the NO_SHIPPING member of ApplicationContextShippingPreference
    userAction: the PAY_NOW member of PayPalExperienceUserAction
    returnUrl / cancelUrl: from RETURN_URL / CANCEL_URL env vars with the documented defaults

Call OrdersApi.createOrder with the body first, then undefined for payPalMockResponse, a
deterministic payPalRequestId (uuid v5 of articleId + price + UTC date) for idempotency, then
undefined for the remaining headers, and prefer 'return=representation'.

Persist { articleId, orderId, invoiceId, status: 'CREATED', createdAt } to ledger.json. Print
the order id and the approve URL, resolved by finding the link whose rel is payer-action -
falling back to approve - in the order's links array. Never hardcode a checkout URL.

Finally print: "Approve in the browser as your sandbox buyer, then run: nyt-unlock claim <index>"
```

**Acceptance:** PayPal returns 201 and the approve link opens a sandbox checkout showing USD 0.99, one digital-goods line item and no shipping section.

**Watch for these four classic misses and log each with the prompt that produced it:**
integer quantity; a breakdown that does not sum; experienceContext placed at the top level; an invented enum member.

---

## 9. Phase 5 — capture, typed recovery, token

```
Implement captureUnlock() and the claim command:

  nyt-unlock claim <index>

Load orderId from the ledger, then call OrdersApi.captureOrder with the id, undefined for
payPalMockResponse (unless --mock is passed, below), the same deterministic payPalRequestId,
and prefer 'return=representation'.

On success read the capture id from purchaseUnits[0].payments.captures[0].id - confirm that
exact path in the Order model before coding it. Update the ledger to { status: 'GRANTED',
captureId, capturedAt }. Mint an access token as base64url(articleId + '.' + exp) + '.' +
HMAC-SHA256 of that payload with UNLOCK_SIGNING_SECRET, 24h expiry. Print token, article URL
and capture id.

Error handling in src/errors.ts, branching on TYPES not strings:
- if the error is an instance of the SDK's CustomError, read error.result and extract
  details[0].issue plus debug_id
- ORDER_ALREADY_CAPTURED: recover - call OrdersApi.getOrder(orderId), pull the existing capture
  id, set the ledger to GRANTED, mint the token, print
  "already captured - access restored from order <id>, capture <id>", exit 0
- ORDER_NOT_APPROVED: print "approve the order first", exit 5
- INSTRUMENT_DECLINED: print PayPal's guidance to restart the order, exit 6
- any other CustomError: print status, issue, description, debug_id, exit 7
- a plain ApiError (e.g. 500): print status, exit 8

Add a --mock <code> flag on claim that sends {"mock_application_codes":"<code>"} as the
payPalMockResponse header so failures are reproducible in sandbox.
```

**Acceptance:** happy path prints a token; running claim twice hits the recovery branch; `claim <n> --mock INSTRUMENT_DECLINED`
fails deterministically. That last one is the failure case for the video.

---

## 10. Phase 6 — reconcile and refund

```
Implement two more commands:

  nyt-unlock status <index>
    Calls OrdersApi.getOrder(orderId) and PaymentsApi.getCapturedPayment(captureId) and prints
    one reconciliation line: order id, order status, capture id, capture status, amount,
    customId echoed back by PayPal, invoiceId, create time. Assert the returned customId equals
    the articleId in the ledger and print a MISMATCH warning if not.

  nyt-unlock refund <index> [--amount 0.50] [--note "..."]
    Calls PaymentsApi.refundCapturedPayment with a RefundRequest built from the model: amount
    { currencyCode, value } when --amount is given, otherwise an empty body for a full refund;
    include noteToPayer and invoiceId. Pass a fresh payPalRequestId. On success set the ledger
    entry to REVOKED with refundId so token verification fails afterwards. Handle the
    over-refund and already-refunded issues explicitly by issue code.
```

**Acceptance:** status shows your customId round-tripping through PayPal; a partial refund flips the ledger so the token stops validating.
You are now at six operations across three controllers.

---

## 11. Phase 7 — docs, findings, hygiene

```
Write README.md with: one-paragraph description; prerequisites (Node 20, PayPal sandbox app,
sandbox buyer account, NYT key with Article Search enabled); the env vars in a table copied
from .env.example; install and run in one command block; the five-command walkthrough with
example output; a section on the two error paths and how to reproduce them (claim twice, and
--mock INSTRUMENT_DECLINED); and a note that the SDKs install from git and default to PayPal
sandbox.

Then delete the two cloned SDK reference directories from the temp dir. Verify no secret
appears anywhere in the repo: grep the tree for the literal client id and secret values, and
confirm .env is untracked.
```

---

## 12. FINDINGS.md template

    ## Finding N - <one-line title>
    **Asked for:** <the prompt, verbatim>
    **Agent produced:** <the wrong code, minimal snippet>
    **Actually correct:** <the right code, and where in the SDK it is documented>
    **Should have been prevented by:** <skill name> / <what was missing>
    **Reproducible:** yes/no - <how>

Strong candidates from this build:

1. Environment enum mixed casing (Production = 'production' vs Sandbox = 'Sandbox').
2. Positional optional headers sitting BEFORE the body in createOrder / captureOrder.
3. ItemRequest.quantity typed as a number instead of a string.
4. experienceContext vs the deprecated top-level applicationContext.
5. The capture id path inside the Order response.
6. The NYT headline field the generated docs example omits.
7. Whether the agent knew retries are off by default and only GET/PUT retry.

---

## 13. Demo video running order (under 3 minutes)

1. Startup banner showing both resolved base URLs (proof you are on sandbox).
2. `search "artificial intelligence"`
3. `buy 3`, open the approve link, log in as the sandbox buyer, approve.
4. `claim 3` - token printed.
5. `claim 3` again - ORDER_ALREADY_CAPTURED recovery.
6. `claim 4 --mock INSTRUMENT_DECLINED` - hard failure, handled by type.
7. `status 3` then `refund 3` - reconcile and revoke.

---

## 14. Scope cuts if time runs short

Cut in this order: the refund command, then the --mock flag, then the status reconciliation line.
Even after all three you keep three controllers, four operations, the nested order body and one typed
error path with real recovery - which is the whole bar. Never cut FINDINGS.md.
