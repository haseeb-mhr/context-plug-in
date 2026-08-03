I read the actual plugin skills and both generated SDKs on GitHub, so the plan below is grounded in the real surface rather than guesses. Everything in the "ground truth" block came from doc/ in the two SDK repos and the plugins' typescript-getting-started skills — tell your agent to re-confirm each fact against its own clone, since that habit is exactly what the plugin is meant to enforce.

The app: nyt-unlock — a micro-paywall CLI

Search New York Times articles, pay $0.99 through PayPal sandbox to unlock one, get a signed access token, reconcile the payment, and refund it. Five commands over three controllers and six operations, one deeply nested request body, two deliberate error paths, all against live sandbox. Ships as a CLI so there's no UI to build.

Ground truth to hand the agent

Both SDKs are APIMatic v3.0 generic libraries, so they share one shape: a single Client constructed with new Client({...}), controllers you instantiate yourself as new XxxApi(client), every operation returning Promise<ApiResponse<T>>, and non-2xx throwing ApiError or a typed subclass. PayPal's SDK is installed from https://github.com/context-plugins/paypal-typescript-sdk#main and imports from paypallib; NYT's from https://github.com/context-plugins/nytimes-typescript-sdk#main, importing from nytimeslib. PayPal's Configuration takes { timeout, environment, clientCredentialsAuthCredentials: { oauthClientId, oauthClientSecret } } and its Environment enum has Production = 'production' and Sandbox = 'Sandbox' — note the inconsistent casing, which is a genuine trap worth logging. NYT authenticates with an API key as a custom query parameter. Retries are disabled by default in these SDKs and only GET/PUT are retried when enabled.

The operations you'll use: SearchApi.returnsAnArrayOfArticles(beginDate?, endDate?, fq?, page?, q?, sort?) returning result.response.docs[]; OrdersApi.createOrder(body, payPalMockResponse?, payPalRequestId?, payPalPartnerAttributionId?, payPalClientMetadataId?, prefer?, payPalAuthAssertion?); OrdersApi.getOrder(id, ...); OrdersApi.captureOrder(id, payPalMockResponse?, payPalRequestId?, prefer?, ...); PaymentsApi.getCapturedPayment(captureId, ...); and PaymentsApi.refundCapturedPayment(captureId, payPalMockResponse?, payPalRequestId?, prefer?, payPalAuthAssertion?, body?: RefundRequest). PayPal's documented 400/401/422 responses arrive as CustomError (a subclass of ApiError carrying .result); NYT's 429 arrives as plain ApiError. Every PayPal operation accepts a payPalMockResponse header that puts sandbox into a negative-testing state — that's your reproducible failure demo.

The order body field names are camelCase in TS: OrderRequest { intent, purchaseUnits[], paymentSource, applicationContext }, where PurchaseUnitRequest has amount: AmountWithBreakdown (with breakdown.itemTotal, shipping, taxTotal …), plus items[], customId, invoiceId, description, referenceId. ItemRequest needs name, unitAmount, and quantity as a string matching ^[1-9][0-9]{0,9}$, with optional category: ItemCategory.DigitalGoods and url. Experience settings live at paymentSource.paypal.experienceContext as PayPalWalletExperienceContext { brandName, shippingPreference: ApplicationContextShippingPreference, userAction: PayPalExperienceUserAction, returnUrl, cancelUrl } — the old top-level applicationContext equivalents are marked deprecated, which is precisely the kind of thing agents get wrong.

Phase 0 — prep (you, not the agent)

Export GITHUB_TOKEN with any classic PAT to avoid the shared rate limit, then run npx context-plugins install paypal and npx context-plugins install nytimes in the repo, followed by npx context-plugins doctor. Create a PayPal sandbox REST app for the client ID and secret, note a sandbox personal buyer login from Testing Tools → Sandbox Accounts, and get an NYT key with Article Search enabled. Create FINDINGS.md now and write your first entry about whether the second plugin install disturbed the first.

Phase 1 — grounding prompt (no feature code yet)
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
   PaymentsApi.getCapturedPayment, PaymentsApi.refundCapturedPayment — including full
   positional parameter order, since several take optional headers BEFORE the body.
4. From doc/models/, write out the field trees for OrderRequest, PurchaseUnitRequest,
   AmountWithBreakdown, Money, ItemRequest, PaymentSource, PayPalWallet,
   PayPalWalletExperienceContext, RefundRequest, and the members of CheckoutPaymentIntent,
   ItemCategory, ApplicationContextShippingPreference, PayPalExperienceUserAction.
   Mark which fields are required and note any field the docs flag as DEPRECATED.

Stop after docs/contract.md. Write no src/ code yet.

Acceptance: the contract sheet names paypallib/nytimeslib, shows Sandbox = 'Sandbox', lists CustomError, and puts quantity as string. Anything it got wrong here is your highest-value findings entry, because it had the source and the skill in hand.

Phase 2 — skeleton and startup safety
Scaffold a Node 20 + TypeScript CLI named nyt-unlock in this repo. ESM, strict mode,
tsx for dev, no framework, no test runner yet. Files:

  src/index.ts    command router: search | buy | claim | status | refund, plus --help
  src/config.ts   env loading and both clients
  src/nyt.ts      src/checkout.ts  src/ledger.ts  src/errors.ts  src/format.ts  (stubs)

src/config.ts must:
- read PAYPAL_CLIENT_ID, PAYPAL_CLIENT_SECRET, PAYPAL_ENV, NYT_API_KEY,
  UNLOCK_SIGNING_SECRET from process.env via dotenv and fail fast with a list of any missing
  ones — never print their values
- construct ONE paypallib Client with clientCredentialsAuthCredentials and environment
  resolved from PAYPAL_ENV, defaulting to the Sandbox enum member; use the enum member,
  never a hardcoded string
- construct ONE nytimeslib Client with the API-key credential object
- export a logStartupBanner() that prints the resolved base URL of each client (read it off
  the client/configuration, not a hardcoded literal) and the PayPal environment name, and
  refuses to continue if PAYPAL_ENV is production unless ALLOW_PRODUCTION=true

Call logStartupBanner() first in every command. Add package.json scripts: dev, build, start.
Write .env.example with the five keys and empty values. Add .gitignore covering .env,
node_modules, dist, ledger.json, .cache.

Acceptance: npm run dev -- --help prints a sandbox base URL for PayPal and api.nytimes.com for NYT before anything else happens. That banner is a ground rule from the guide and it goes in the video.

Phase 3 — the read side
Implement src/nyt.ts and the `search` command:

  nyt-unlock search "<query>" [--page N] [--sort newest|oldest|relevance]

Use SearchApi.returnsAnArrayOfArticles with the correct positional argument order — pass
undefined for parameters you are not using rather than reordering. Map the Sort enum from
the --sort flag using real enum members read from the SDK.

From result.response.docs[], print a numbered table: index, headline (grep the Doc model for
the real headline field path — the doc example omits it, so confirm it in src/models/),
publication date, web URL, and a one-line snippet. Cache the raw docs array to
.cache/search.json with the query and a timestamp so later commands can resolve an index to
an article without re-searching.

Error handling: catch ApiError. On 429 print "NYT rate limit hit — wait 60s" and exit 3;
on 401 print a credential hint and exit 4. Do not string-match error messages; branch on
error.statusCode.

Acceptance: real headlines and URLs appear, and .cache/search.json contains a stable article identifier you can use as customId.

Phase 4 — the order body (the step that tests the plugin)
Implement createUnlockOrder() in src/checkout.ts and the `buy` command:

  nyt-unlock buy <index> [--price 0.99]

Resolve <index> from .cache/search.json. If the ledger already has a granted unlock for that
article, print the existing token and exit 0 without calling PayPal.

Build an OrderRequest with EVERY field name and enum taken from the cloned SDK models:
- intent: the Capture member of CheckoutPaymentIntent
- purchaseUnits: exactly one PurchaseUnitRequest with
    amount: { currencyCode: 'USD', value: price, breakdown: { itemTotal: { currencyCode:
      'USD', value: price } } }   // breakdown MUST sum exactly to amount.value
    items: [ { name: headline truncated to 127 chars, unitAmount: { currencyCode: 'USD',
      value: price }, quantity: '1', category: the DigitalGoods member of ItemCategory,
      url: article web URL } ]
    customId: the NYT article identifier, truncated to 255
    invoiceId: `unlock-${shortHash(articleId)}-${epochSeconds}`, max 127, unique per attempt
    description: headline truncated to 127
- paymentSource.paypal.experienceContext (NOT the deprecated top-level applicationContext):
    brandName: 'NYT Unlock'
    shippingPreference: the NO_SHIPPING member of ApplicationContextShippingPreference
    userAction: the PAY_NOW member of PayPalExperienceUserAction
    returnUrl / cancelUrl: from RETURN_URL / CANCEL_URL env vars, defaulting to
      https://example.com/unlock/return and .../cancel

Call OrdersApi.createOrder with the body first, then undefined for payPalMockResponse, a
deterministic payPalRequestId (uuid v5 of articleId + price + UTC date) for idempotency, then
undefined for the remaining headers, and prefer 'return=representation'.

Persist { articleId, orderId, invoiceId, status: 'CREATED', createdAt } to ledger.json. Print
the order id and the approve URL, resolved by finding the link whose rel is payer-action —
falling back to approve — from the order's links array. Never hardcode a checkout URL.

Then print: "Approve in the browser as your sandbox buyer, then run: nyt-unlock claim <index>"

Acceptance: PayPal returns 201 and an approve link that opens a sandbox checkout showing $0.99, one digital-goods line item, and no shipping section. Check the generated code for the four classic misses — integer quantity, a breakdown that doesn't sum, experienceContext placed at the top level, and a fabricated enum member — and log each one you find with the prompt that produced it.

Phase 5 — capture, typed recovery, token
Implement captureUnlock() and the `claim` command:

  nyt-unlock claim <index>

Load orderId from the ledger, then call OrdersApi.captureOrder with the id, undefined for
payPalMockResponse (unless --mock is passed, see below), the same deterministic
payPalRequestId, and prefer 'return=representation'.

On success, read the capture id from the order's purchaseUnits[0].payments.captures[0].id —
confirm that exact path in the Order model before coding it. Update the ledger to
{ status: 'GRANTED', captureId, capturedAt }. Mint an access token as
base64url(articleId + '.' + exp) + '.' + HMAC-SHA256 over that payload with
UNLOCK_SIGNING_SECRET, 24h expiry, and print token + article URL + capture id.

Error handling in src/errors.ts, branching on types not strings:
- if the error is an instance of the SDK's CustomError, read error.result and extract
  details[0].issue plus debug_id
- issue ORDER_ALREADY_CAPTURED: recover — call OrdersApi.getOrder(orderId), pull the existing
  capture id, update the ledger to GRANTED, mint the token, print
  "already captured — access restored from order <id>, capture <id>" and exit 0
- issue ORDER_NOT_APPROVED: print "approve the order first" and exit 5
- issue INSTRUMENT_DECLINED: print PayPal's guidance to restart the order and exit 6
- any other CustomError: print status, issue, description and debug_id, exit 7
- a plain ApiError (e.g. 500): print status and exit 8

Add a --mock <code> flag on claim that passes {"mock_application_codes":"<code>"} as the
payPalMockResponse header so failures are reproducible in sandbox.

Acceptance: the happy path prints a token; running claim twice hits the recovery branch; and claim <n> --mock INSTRUMENT_DECLINED prints the declined branch deterministically. That last one is the failure case for your video.

Phase 6 — reconcile and refund
Implement two more commands:

  nyt-unlock status <index>
    Calls OrdersApi.getOrder(orderId) and PaymentsApi.getCapturedPayment(captureId) and
    prints one reconciliation line: order id, order status, capture id, capture status,
    amount, customId echoed back by PayPal, invoiceId, create time. Assert that the returned
    customId equals the articleId in the ledger and print a MISMATCH warning if not.

  nyt-unlock refund <index> [--amount 0.50] [--note "..."]
    Calls PaymentsApi.refundCapturedPayment with a RefundRequest built from the model:
    amount { currencyCode, value } when --amount is given, otherwise an empty body for a full
    refund; include noteToPayer and invoiceId. Pass a fresh payPalRequestId. On success set
    the ledger entry to REVOKED with refundId and make token verification fail afterwards.
    Handle the over-refund and already-refunded issues explicitly by issue code.

Acceptance: status shows your customId round-tripping through PayPal, and a partial refund flips the ledger so the token stops validating. You're now at six operations across three controllers.

Phase 7 — docs, findings, hygiene
Write README.md with: one-paragraph description, prerequisites (Node 20, PayPal sandbox app,
sandbox buyer account, NYT API key with Article Search enabled), the five env vars in a table
copied from .env.example, install and run in one command block, the full five-command
walkthrough with example output, a section on the two error paths and how to reproduce them
(claim twice, and --mock INSTRUMENT_DECLINED), and a note that the SDKs are installed from
git and point at PayPal sandbox by default.

Then delete the two cloned SDK reference directories from the temp dir. Verify no secret
appears anywhere in the repo: grep the tree for the literal client id and secret values and
for 'sk_' style prefixes, and confirm .env is untracked.

Then finish FINDINGS.md yourself, one entry per miss, using four fields each: what you asked for, what the agent produced, what was actually correct, and which skill should have prevented it or what was missing. Strong candidates from this build: the Environment enum's mixed-case values, the positional headers sitting before the body in createOrder and captureOrder, quantity as a string, experienceContext versus the deprecated applicationContext, the capture-id path inside the order response, the NYT headline field the docs example omits, and whether the agent knew retries are off by default.

Demo video running order, under three minutes

Show the startup banner with the resolved sandbox base URLs, run search "artificial intelligence", run buy 3 and open the approve link, approve as the sandbox buyer, run claim 3 to print the token, run claim 3 again to show the ORDER_ALREADY_CAPTURED recovery, run claim 4 --mock INSTRUMENT_DECLINED for the hard failure, then status 3 and refund 3 to close the loop.