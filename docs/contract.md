# Contract sheet — .NET (Phase 1 deliverable)

Ground truth for `nyt-unlock`, read from the installed plugins' **.NET SDK maps** on 2026-08-03.
Supersedes the TypeScript ground-truth blocks in `../nyt-unlock-build-plan.md` §2 and `../README.md`,
which target the stale language (`BUGS.md` BUG-03).

Sources — both are the plugin-shipped SDK map, not the SDK source:

```
~/.claude/plugins/marketplaces/context-plugins/plugins/paypal/skills/dotnet/dotnet-paypal-getting-started/sdk-map.md
~/.claude/plugins/marketplaces/context-plugins/plugins/nytimes/skills/dotnet/dotnet-nytimes-getting-started/sdk-map.md
```

Everything below is quoted from those maps. **Not yet read:** the `map/operations/*.md` and
`map/models/enums.md` sub-pages that carry per-operation signatures and literal enum member names.
Phase 4 must not be coded until those are read — see *Still required* at the end.

---

## 1. Shared shape (both SDKs)

| Fact | Value |
| --- | --- |
| Generator | APIMatic |
| Target framework | `netstandard2.0`, C# `LangVersion 14`, `Nullable enable` |
| Client construction | `new {Name}Client(HttpClient httpClient, {Name}ClientOptions options)` — **HttpClient is a required first argument** |
| DI alternative | `services.Add{Name}Client(o => { ... })` |
| Controllers | **Properties on the client** (`client.Orders`), not hand-instantiated |
| Error model | Throw-based. `SdkException<TError>` with `.Error` |
| No-throw variants | **Absent** — every operation is throw-only |
| Enums | **Not C# enums.** `StringEnum<T>`/`IntEnum<T>`; build via `Type.FromValue("wire")` or static members |
| Records | Immutable, `init`-only setters; `required` must be set in the initializer; `T?` = optional |

### Error handling

`ApiError` is the abstract base — **43 typed error classes** for PayPal, **9** for NYT.

- **Case A (typed):** `TError` is a generated `…Error : ApiError` with status-specific
  `TryGet…(out …)` accessors, plus inherited `TryGetRawError(out RawError)`.
- **Case B (raw):** `TError` is `RawError` — `StatusCode`, `ReadAsString()`, `ReadAsJson<T>()`,
  `ReadAsBytes()`.

```csharp
try { var resp = await client.{ApiGroup}.{Operation}(body); }
catch (SdkException<{Operation}Error> ex)          // Case A
{
    if (ex.Error.TryGetSomeShape(out var typed))   { }
    else if (ex.Error.TryGetRawError(out var raw)) { }
}
catch (SdkException<RawError> ex)                  // Case B
{
    var status = ex.Error.StatusCode;
}
```

PayPal: 44 operations, 43 Case A, 1 Case B. NYT: 8 operations, 7 Case A, 1 Case B.

---

## 2. PayPal

| Fact | Value |
| --- | --- |
| SDK repo | `github.com/context-plugins/paypal-csharp-sdk` (branch `main`) |
| Spec stamp | `166d107` |
| Root namespace | `Paypal` |
| Client | `PaypalClient(HttpClient, PaypalClientOptions)` |

`PaypalClientOptions`: `Environment: ServerEnvironment` · `Retry: RetryOptions` ·
`Logging: LoggingOptions` · `Server: ServerOptions` · `Oauth2: OAuth2ClientCredentials?` ·
`Oauth2TokenStrategy: IOAuth2TokenStrategy<OAuth2ClientCredentials>?`

**Auth:** `options.Oauth2` of type `OAuth2ClientCredentials?`.

**Environments:** `ServerEnvironment.Production`, `ServerEnvironment.Sandbox` (namespace
`Paypal.Servers`).

> **Port note — one predicted trap is void.** The TypeScript plan (`plan:40`) warns of mixed-case
> enum values `Production = 'production'` / `Sandbox = 'Sandbox'`. In .NET you pass the member
> `ServerEnvironment.Sandbox`; there is no string literal to mis-case. **BUG-23's attack (a
> `PAYPAL_ENV=Production` typo defeating the production guard) still applies** — it lives in our own
> env parsing, not the SDK, so the guard must normalise case and refuse unrecognised values.

**Controllers (5 groups, 44 ops):** `Orders` (8) · `Payments` (9) · `Subscriptions` (19) ·
`TransactionSearch` (2) · `Vault` (6). We use `client.Orders` and `client.Payments`.

**Models:** 294 records · 0 unions · **85 enums**. Namespaces: `Paypal.Api` (controllers),
`Paypal.Models` (records), `Paypal.Models.Enums`, `Paypal.Errors`.

---

## 3. NYTimes

| Fact | Value |
| --- | --- |
| SDK repo | `github.com/context-plugins/nytimes-csharp-sdk` (branch `main`) |
| Spec stamp | `dfb07f7` |
| Root namespace | `Nytimes` |
| Client | `NytimesClient(HttpClient, NytimesClientOptions)` |

`NytimesClientOptions`: `Environment: ServerEnvironment` · `Retry: RetryOptions` ·
`Logging: LoggingOptions` · `Server: ServerOptions` · `Apikey: string?`

**Auth:** `options.Apikey`, a plain `string?`. *This closes `BUGS.md` BUG-09* — the TypeScript ground
truth never named the NYT credential object. In .NET it is one string property, no wrapper type.

**Environments:** `ServerEnvironment.Production` **only — there is no NYT sandbox.** Every NYT call
in this project hits production. The Phase 2 banner must still print the resolved base URL, but it
cannot claim sandbox for NYT, and the guide's ground rule 2 ("confirm the host before the first
write") is satisfied by the read-only nature of the call rather than by a sandbox host.

**Controllers (5 groups, 8 ops):** `Archive` (1) · `MostPopular` (4) · `Rss` (1) · **`Search` (1)** ·
`Stories` (1).

> **Port note — the plan's method name is wrong for .NET.** The plan specifies
> `SearchApi.returnsAnArrayOfArticles(...)` (`plan:72`). In .NET the controller is `client.Search`
> with exactly one operation, whose C# name must be read from `map/operations/Search.md`. Do not
> carry the camelCase TypeScript name over.

**Models:** 46 records (`Article` … `ViewedArticle`) · 0 unions · **4 enums**.
Namespaces: `Nytimes.Api`, `Nytimes.Models`, `Nytimes.Models.Enums`, `Nytimes.Errors`.

> The plan's warning that the generated docs example omits the headline field (`plan:76`) is
> unverified for .NET — check the `Article` record on `map/models/records-1-Ar-Vi.md`.

---

## 4. Consequences for the build plan

| Plan assumption (TypeScript) | .NET reality |
| --- | --- |
| `new OrdersApi(client)` — controllers instantiated by hand | `client.Orders` — a property |
| Optional headers positional, **before** the body | Named/optional parameters. **BUG-06 and BUG-08 do not survive the port** |
| `Environment.Sandbox` string `'Sandbox'` | `ServerEnvironment.Sandbox` member — no literal |
| `ApiError` / `CustomError` subclass | `SdkException<TError>`; `ApiError` is the base of 43 typed classes, surfaced via `TryGet…` accessors, **not** `catch (CustomError)` |
| `ItemCategory.DigitalGoods` vs `NO_SHIPPING` (BUG-05) | Both spellings unverified. Enums are `StringEnum<T>`; the literal member names are in `map/models/enums.md` and **must be read, not guessed** |
| Client takes only an options object | Takes `HttpClient` **first**, then options |
| NYT auth = unnamed "API-key credential object" | `options.Apikey`, plain `string?` |

Retry: `RetryOptions` members are all `required` — build a full instance or start from
`RetryOptions.Default()`. The plan's claim that retries default to off (`plan:33`) is **unverified
for .NET**; `map` does not state the default, so `BUGS.md` BUG-29 stands.

---

## 5. Operation signatures — read from the map, verified by the compiler

All six operations below compiled first try. Parameter names are literal; the leading nullable
parameters have no defaults and **must be passed explicitly** (`null` to skip).

```csharp
// client.Search — NYT. Note: headers/params are all leading, body-less GET.
ReturnsAnArrayOfArticles(string? beginDate, string? endDate, string? fq, int? page,
                         string? q, Sort? sort,
                         RequestOptions? requestOptions = null, CancellationToken ct = default)
    -> ReturnsAnArrayOfArticlesResponse
    throws SdkException<ReturnsAnArrayOfArticlesError>
           .TryGetNoContent(out RawError)  [400, 401, 429 — all three share one accessor,
                                            so branch on RawError.StatusCode]

// client.Orders — PayPal. FIVE header params precede `body`.
CreateOrder(string? payPalMockResponse, string? payPalRequestId,
            string? payPalPartnerAttributionId, string? payPalClientMetadataId,
            string? payPalAuthAssertion, OrderRequest body,
            string? prefer = "return=minimal", ...)                    -> Order
GetOrder(string id, string? fields, string? payPalMockResponse,
         string? payPalAuthAssertion, ...)                             -> Order
CaptureOrder(string id, string? payPalMockResponse, string? payPalRequestId,
             string? payPalClientMetadataId, string? payPalAuthAssertion,
             OrderCaptureRequest? body, string? prefer = "return=minimal", ...)  -> Order

// client.Payments — PayPal.
GetCapturedPayment(string captureId, string? payPalMockResponse, ...)   -> CapturedPayment
RefundCapturedPayment(string captureId, string? payPalMockResponse, string? payPalRequestId,
                      string? payPalAuthAssertion, RefundRequest? body,
                      string? prefer = "return=minimal", ...)          -> Refund
```

PayPal errors are all Case A with `TryGetError(out Error)` plus a `TryGetRawError` fallback.
`Error { Name, Message, DebugId required, Details: IReadOnlyList<ErrorDetails>? }` and
`ErrorDetails { Field?, Value?, Location?, Issue required, Description? }` — so the issue code is
`error.Details[0].Issue`, and branching is on that, never on message text.

### Enum members — settles BUG-05

Members are **PascalCase**; the SCREAMING_SNAKE spellings in the build plan are the *wire values*.
Both spellings appear in the plan because the two were conflated, not because one was invented.

| Enum | Member used | Wire value |
| --- | --- | --- |
| `CheckoutPaymentIntent` | `.Capture` | `CAPTURE` |
| `ItemCategory` | `.DigitalGoods` | `DIGITAL_GOODS` |
| `ApplicationContextShippingPreference` | `.NoShipping` | `NO_SHIPPING` |
| `PayPalExperienceUserAction` | `.PayNow` | `PAY_NOW` |
| `Sort` (NYT) | `.Newest` / `.Oldest` / `.Relevance` / `.Best` | lowercase |

### Model field trees

```
OrderRequest        { Intent !req, PurchaseUnits: IReadOnlyList<PurchaseUnitRequest> !req,
                      PaymentSource?, ApplicationContext?, ProcessingInstruction?, Payer? }
PurchaseUnitRequest { Amount: AmountWithBreakdown !req, Items: IReadOnlyList<ItemRequest>?,
                      CustomId?, InvoiceId?, Description?, ReferenceId?, SoftDescriptor?, ... }
AmountWithBreakdown { CurrencyCode !req, Value !req, Breakdown: AmountBreakdown? }
AmountBreakdown     { ItemTotal: Money?, Shipping?, Handling?, TaxTotal?, Insurance?, ... }
Money               { CurrencyCode !req, Value !req }
ItemRequest         { Name !req, UnitAmount: Money !req, Quantity: STRING !req,
                      Category: ItemCategory?, Url?, Description?, Sku?, Tax? }
PaymentSource       { Paypal: PayPalWallet?, Card?, Token?, ApplePay?, GooglePay?, Venmo?, ... }
PayPalWallet        { ExperienceContext: PayPalWalletExperienceContext?, EmailAddress?, ... }
PayPalWalletExperienceContext
                    { BrandName?, Locale?, ShippingPreference?, UserAction?,
                      ReturnUrl?, CancelUrl?, LandingPage?, ContactPreference?, ... }
RefundRequest       { Amount: Money?, CustomId?, InvoiceId?, NoteToPayer?, PaymentInstruction? }

Order               { Id?, Status: OrderStatus?, PurchaseUnits: IReadOnlyList<PurchaseUnit>?,
                      Links: IReadOnlyList<LinkDescription>?, CreateTime?, ... }
PurchaseUnit        { CustomId?, InvoiceId?, Amount?, Payments: PaymentCollection?, ... }
PaymentCollection   { Captures: IReadOnlyList<OrdersCapture>?, Authorizations?, Refunds? }
OrdersCapture       { Id?, Status: CaptureStatus?, Amount: Money?, CustomId?, InvoiceId?, ... }
LinkDescription     { Href !req, Rel !req, Method? }
```

**Capture id path — confirmed:** `order.PurchaseUnits[0].Payments.Captures[0].Id`.
**Approve link:** the `LinkDescription` whose `Rel` is `payer-action`, falling back to `approve`.
Live output confirms `payer-action` is the one PayPal returns.

`ItemRequest.Quantity` is a **string** (`^[1-9][0-9]{0,9}$`) — the plan predicted this trap and the
map typed it correctly.

### What the map could not settle

Four facts required the source clone the skill itself recommends as the fallback:

| Fact | Why the map was insufficient |
| --- | --- |
| `ApplicationContextShippingPreference` members | The enum's table row was truncated in the rendered markdown |
| `OAuth2ClientCredentials { ClientId, ClientSecret, Scope? }` | Named as a type; fields not listed |
| `ServerOptions { Default, Default1..Default5 }` | Needed to read base URLs for the startup banner |
| NYT docs element is `ArticleSearchArticle` | **Not** the `Article` record a name-based guess lands on. It exposes `Uri` but **no `Id`**, and `Headline` is `ArticleSearchHeadline { Main?, Kicker?, PrintHeadline? }` — so the headline path is `Headline.Main` |

---

## 6. Runtime corrections — where the SDKs disagree with the live APIs

Everything above is what the SDK *declares*. Three facts only surfaced by calling the real endpoints,
and two of them are outright defects requiring `scripts/patch-sdk.ps1`:

| Declared | Live reality | Consequence |
| --- | --- | --- |
| `ArticleSearchArticle.PrintPage: int?` | `print_page` is a **string** (`"3"`) | **No Article Search response deserializes.** `JsonException` from `System.Text.Json`, not an `SdkException` — a correct catch ladder never sees it. `FINDINGS.md` Finding 8 |
| `Response1.Meta` bound to `"meta"` | API sends **`"metadata"`** | `Meta` always null; hit counts silently unavailable. Finding 9 |
| `ServerEnvironment.Production` | `ProductionOptions.BaseUrl` **is the sandbox host** | No environment member reaches live PayPal. Finding 6 |

One further trap, cosmetic but pervasive: `TypedEnum` overrides `ToString()` to return `Value`, but
every generated enum is a `record`, so the compiler-synthesised `ToString()` shadows it. Interpolate
one and you get `OrderStatus { Value = CREATED }`. **Always use `.Value`.** Finding 10.

Also confirmed live: NYT `ServerEnvironment` has only `Production` — there is no NYT sandbox, and
Article Search runs against production. It is read-only.
