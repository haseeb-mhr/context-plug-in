# FINDINGS — Context Plugins evaluation

Points where the plugins failed to supply the contract an agent needs to write correct integration
code the first time. Every entry below was verified on this machine at the paths shown, after
`npx context-plugins install paypal` and `install nytimes` on 2026-08-03.

Each entry uses the four-part structure the hackathon guide requires:
**Asked for → Produced → Actually correct → Should have been prevented by**, plus reproduction steps.

Marketplace clone under test: `~/.claude/plugins/marketplaces/context-plugins/` (referred to below as
`$MP`). Environment: Windows 11, Node v20.16.0, .NET SDK 10.0.302.

---

## Finding 1 · The SDK map ships for .NET only — the hub's language column cannot detect this

**Asked for:** choose a plugin and language for a PayPal + NYT build, following the guide's only
stated gating check — *"Browse the catalog … Check the language column before you commit."*

**Produced:** the hub lists **7 languages** for both PayPal and New York Times, TypeScript among
them, with no currency or completeness signal. The check passes for TypeScript, so the entire build
plan (`nyt-unlock-build-plan.md`, 375 lines) was written in TypeScript — package ids, camelCase
model fields, `Promise<ApiResponse<T>>`, positional `undefined` placeholders.

**Actually correct:** C#/.NET is the only language shipping an operation contract. Verified on disk —
the `sdk-map.md` file and `map/` directory exist under `dotnet` and under no other language, for
both plugins:

```
$MP/plugins/paypal/skills/dotnet/dotnet-paypal-getting-started/sdk-map.md     8512 bytes
$MP/plugins/paypal/skills/dotnet/dotnet-paypal-getting-started/map/           <dir>
$MP/plugins/nytimes/skills/dotnet/dotnet-nytimes-getting-started/sdk-map.md   7837 bytes
$MP/plugins/nytimes/skills/dotnet/dotnet-nytimes-getting-started/map/         <dir>

$MP/plugins/paypal/skills/typescript/typescript-getting-started/   -> SKILL.md only, no sdk-map.md, no map/
$MP/plugins/nytimes/skills/typescript/typescript-getting-started/  -> SKILL.md only, no sdk-map.md, no map/
```

This matches organiser guidance the same day: *"I recommend everyone to work with in C# as it has the
updated sdk and updated skills."*

**Should have been prevented by:** the hub's language column, which is the guide's prescribed check
and reports success for a language with no operation contract. Secondarily by
`typescript-getting-started/SKILL.md`, which does not declare that it ships no SDK map — an agent
loading it has no way to learn that the operation contract is absent rather than merely unread.
The column tells you a language *exists*; nothing anywhere tells you whether it is usable.

**Reproducible:** yes.
```bash
ls $MP/plugins/*/skills/dotnet/*getting-started/        # sdk-map.md + map/ present
ls $MP/plugins/*/skills/typescript/*getting-started/    # neither present
```

**Cost incurred:** the full build plan targeted the wrong runtime and had to be re-scoped to C#
before any code was written. Tracked as `BUGS.md` BUG-03 (Critical) / BUG-31 (High).

---

## Finding 2 · Installing one plugin silently clones all 22

**Asked for:** build plan Phase 0 step 8 — *"did installing the second plugin disturb the first?"*

**Produced:** the question turns out to be unanswerable as posed, because the two installs do not
have independent payloads. `npx context-plugins install paypal` — a single-plugin request — cloned
the entire marketplace repository: **3,550 files** across **all 22 plugins**, `nytimes` included and
fully populated with all 7 language skill trees, before `nytimes` was ever requested.

The subsequent `npx context-plugins install nytimes` fetched nothing new:

```
Marketplace 'context-plugins' is already registered - updating it.
✓ Updated marketplace 'context-plugins'
✓ Installed nytimes@context-plugins (user scope)
```

**Actually correct:** no disturbance is possible — one shared clone, and the second "install" is a
registration step only. But two things follow that the installer does not disclose: you download 22
plugins' worth of skills to use one, and a plugin is present and readable on disk before you install
it, so "is it installed?" and "is it available?" are different questions with no visible distinction.

**Should have been prevented by:** installer output. It reports `Installing 'paypal'` and
`Fetching marketplace via git`, which reads as fetching the one plugin. Nothing states that the whole
catalog lands on disk. An agent inspecting `$MP/plugins/` after one install will find 21 plugins it
was never asked to consider.

**Reproducible:** yes — on a clean machine, `npx context-plugins install paypal`, then
`ls $MP/plugins/` → 22 entries.

---

## Finding 3 · The installer writes to three unrelated tools without being asked

**Asked for:** install the `paypal` plugin, run from a non-interactive shell in this repo.

**Produced:** the installer detected and wrote into **three** harnesses, and modified a VS Code user
settings file:

```
[Harnesses]
  Non-interactive shell - using every detected harness (--targets to choose).
  Installing into: Claude Code, Cursor, VS Code
...
[Cursor]   ✓ Installed -> ~\.cursor\plugins\local\paypal
[VS Code]  ✓ Installed -> ~\.context-plugins\vscode\paypal
           Registered in chat.pluginLocations
           (~\AppData\Roaming\Code\User\settings.json)
```

**Actually correct:** the target should default to the harness the command was invoked from, or
prompt. `--targets` exists but is only mentioned *after* the decision has been made, in the output of
the run that already performed it.

**Should have been prevented by:** the installer's default. "Non-interactive shell" is treated as
consent to write to every detected editor, which inverts the safe default — a non-interactive shell
is precisely where an unattended tool should do *less*, not more. Editing another application's user
`settings.json` as an undisclosed side effect of a plugin install is the specific concern.

**Reproducible:** yes — deterministic on any machine with Cursor and VS Code present.

---

## Finding 4 · .NET carries a skill TypeScript has no counterpart for

**Asked for:** compare per-language skill coverage for the same plugin, to see whether "supported"
means the same thing across the column.

**Produced:** the `dotnet` tree ships **9** skills, `typescript` **8**. The extra one is
`dotnet-integrate-paypal/SKILL.md` (10,455 bytes) — and `dotnet-integrate-nytimes/SKILL.md`
(10,486 bytes) for the other plugin. There is no `typescript-integrate-*` for either.

Byte-for-byte, several .NET skills are also substantially larger than their TypeScript namesakes —
`dotnet-configuration-resilience` 22,221 vs `typescript-configuration-resilience` 4,789;
`dotnet-error-handling` 20,287 vs `typescript-error-handling` 4,933. Error handling is the skill
requirement 3 of the bar depends on, and the TypeScript version is under a quarter of the size.

**Actually correct:** the asymmetry is real and one-directional. Combined with Finding 1, "supported
language" in the catalog spans at least two tiers: .NET with an SDK map, an integrate skill, and
4× the error-handling guidance; and everything else.

**Should have been prevented by:** any per-language coverage indicator in the catalog. There is none,
so the only way to discover the tier difference is to install a plugin and diff the skill trees —
after committing to a language.

**Reproducible:** yes.
```bash
ls $MP/plugins/paypal/skills/dotnet/ | wc -l       # 9
ls $MP/plugins/paypal/skills/typescript/ | wc -l   # 8
```

---

## Finding 5 · Catalog count disagrees with the guide

**Asked for:** confirm the catalog size before choosing, per the guide's instruction to browse it.

**Produced:** the guide states **29 plugins**; the hub renders **22**; the installed marketplace on
disk contains **22** (`adyen, alpaca, binance, cellpoint, coingecko, deepgram, discourse, ebay-sell,
google-maps-platform, klarna, kubernetes, maxio-advanced-billing, notion, nytimes, paypal, paze,
pokeapi, shutterstock, slack, spotify-web-api, tesla-fleet-management-api, tesser-api-v1`).

**Actually correct:** 22 is the real number — the hub and the on-disk clone agree, so the guide's 29
is stale.

**Should have been prevented by:** the guide, which is the document participants are told to work
from. Minor on its own; recorded because it is the second instance of catalog metadata disagreeing
with the artifact it describes, after Finding 1.

**Reproducible:** yes — count `$MP/plugins/` against the guide text.

---

## Finding 6 · The PayPal SDK routes `Production` to the sandbox host

**Asked for:** build a production guard for the CLI, reading base URLs off the client
configuration rather than hardcoding them (hackathon ground rule 2).

**Produced / observed:** `Servers/DefaultOptions.cs` in `paypal-csharp-sdk` declares both
environments with the **same** base URL:

```csharp
public class ProductionOptions
{
    public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";
}
public class SandboxOptions
{
    public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";
}
```

Corroborating signals in the same file and `Servers/ServerEnvironment.cs`: the XML doc on
`ServerEnvironment.Production` reads *"PayPal Sandbox Environment"* — the same text as `Sandbox` —
and `ServerEnvironment.Default()` returns `Production`.

**Actually correct:** live PayPal is `https://api-m.paypal.com`. As shipped, selecting
`ServerEnvironment.Production` sends traffic to sandbox, and there is no environment member that
reaches production at all — you must override `options.Server.Default.Production.BaseUrl` by hand.

**Should have been prevented by:** the generated server wiring, and secondarily the SDK map, which
states *"`options.Environment` is a `ServerEnvironment` … with members `Production`, `Sandbox`"*
and links `Servers/` without noting the two resolve identically. An agent reading only the map has
no way to learn that `Production` is not production.

**Cuts both ways.** Benign for a hackathon (you cannot accidentally charge a real card), but the
inverse is the real risk: an integration that believes it went live silently did not, and
transactions are missing from the live account with no error anywhere. Our banner now prints a
warning when the two URLs match rather than letting the environment name speak for itself.

**Reproducible:** yes — `git clone --depth 1 --branch main
https://github.com/context-plugins/paypal-csharp-sdk` and read `Servers/DefaultOptions.cs`.

---

## Finding 7 · Where the .NET SDK map succeeded — recorded for balance

The guide asks for honest findings, so the positive result belongs here alongside the defects.

**Asked for:** implement six operations across three controllers — NYT `Search`, PayPal `Orders`
(create/get/capture) and `Payments` (get captured/refund) — in C#, working only from the bundled
`sdk-map.md` and its `map/` sub-pages, cloning SDK source only where the map was silent.

**Produced:** roughly 600 lines across 7 files **compiled on the first `dotnet build`, zero errors
and zero warnings.** No signature guess, enum member, model field name or error accessor needed
correcting.

Facts the map supplied correctly and completely, each verified by the compiler:

| Fact | Map value |
| --- | --- |
| Operation signatures | Full positional order, incl. `payPalMockResponse` … `body` and `prefer = "return=minimal"` defaults |
| Enum member names | `CheckoutPaymentIntent.Capture`, `ItemCategory.DigitalGoods`, `PayPalExperienceUserAction.PayNow` — PascalCase members, SCREAMING_SNAKE wire values |
| `ItemRequest.Quantity` | `string !req` — typed correctly, the trap the plan predicted |
| Error model | `SdkException<TOpError>` + `TryGetError(out Error)` / `TryGetRawError(out RawError)`, with per-status mapping |
| Capture id path | `PurchaseUnits[0].Payments.Captures[0].Id` (`OrdersCapture`) |
| NYT auth | `options.Apikey`, plain `string?` |

**What the map did not settle**, requiring the source clone the skill itself recommends:
`ApplicationContextShippingPreference` members (the enum row was truncated in the rendered table),
the `OAuth2ClientCredentials` field names (`ClientId`/`ClientSecret`), the `ServerOptions` shape
needed to read base URLs for the banner, and `ArticleSearchArticle` — the NYT docs array is
`ArticleSearchArticle`, **not** the `Article` record a name-based guess lands on, and it exposes
`Uri` but no `Id`.

**Bearing on Finding 1:** this is the strongest evidence for the .NET-only concern. The contract
that made a first-try compile possible exists in exactly one language. The same build in TypeScript
would have started from the plan's hand-written ground truth — which `BUGS.md` BUG-05 and BUG-06
show was already self-contradictory and, on two points, wrong for .NET: it claimed `createOrder`
takes the body first (it does not — headers precede `body` in the C# signature) and gave enum
members in SCREAMING_SNAKE (those are wire values).

**Reproducible:** yes — `dotnet build src/NytUnlock` against the committed source.

---

## Finding 8 · The NYT SDK cannot deserialize its own Search response

**This is the most serious finding in the log: the generated model is wrong against the live API,
and it breaks the only operation the `Search` controller has.**

**Asked for:** call `client.Search.ReturnsAnArrayOfArticles` and read `response.Response.Docs`.

**Produced:** the call reaches the API, authenticates, and returns HTTP 200 — then the SDK throws
while parsing its own response:

```
System.Text.Json.JsonException: The JSON value could not be converted to
System.Nullable`1[System.Int32]. Path: $.response.docs[0].print_page
 ---> System.InvalidOperationException: Cannot get the value of a token type 'String' as a number.
   at Nytimes.Core.Response.JsonResponse`1.Map(...)
```

`Models/ArticleSearchArticle.cs:24` declares:

```csharp
[JsonPropertyName("print_page")]
public int? PrintPage { get; init; }
```

**Actually correct:** the live API returns `print_page` as a **string**. Verified directly against
`https://api.nytimes.com/svc/search/v2/articlesearch.json`:

```
print_page      String    "3"
print_section   String    "BU"
word_count      Int64     635
```

So `print_page` must be `string?`. (`print_section` is already `string?` and `word_count` is
genuinely numeric — this is one field, not a systemic type problem.)

**Severity:** total. Every Article Search response carries `print_page`, so *no* query succeeds. The
exception is a `JsonException` from the serializer, **not** an `SdkException`, so it bypasses the
error-handling contract the SDK map documents — a correctly-written `catch (SdkException<…Error>)`
ladder does not catch it, and the process dies with an unhandled exception and exit code 134.

**Should have been prevented by:** the generator, or the spec it consumed — the API's own
documentation types this field as a string. Nothing in the plugin could have saved us: the SDK map
faithfully reports `PrintPage (print_page): int?`, so the map is *correct about the SDK* and the SDK
is wrong about the API. An agent doing everything right — reading the map, trusting the contract —
still produces code that cannot work, and only finds out at runtime against a live endpoint.

**Reproducible:** yes, deterministically — any Article Search query. Workaround applied in
`scripts/patch-sdk.ps1` (patch 1).

---

## Finding 9 · `Response1.Meta` is bound to a wire name the API does not send

**Asked for:** print the total hit count from `response.Response.Meta.Hits`, as the SDK map and the
build plan both describe (`result.response.meta { hits, offset, time }`).

**Produced:** `Meta` was always `null`, so the CLI could only print `10 of ? hits`. No error, no
warning — a silently absent field.

**Actually correct:** the live response has no `meta` key. Its `response` object contains exactly
`docs` and **`metadata`**:

```
=== live response.meta ===
null
=== response top-level keys ===
docs, metadata
```

`Models/Response1.cs` binds `[JsonPropertyName("meta")]`. It has to be `"metadata"`. After patching,
the same query reports `10 of 10000 hits`.

**Should have been prevented by:** the generator/spec, as with Finding 8. This one is worse in one
respect and better in another: it fails **silently** rather than throwing, so an integration ships
believing the field is simply unpopulated — but it degrades one field instead of killing the
operation.

**Reproducible:** yes. Workaround in `scripts/patch-sdk.ps1` (patch 2).

---

## Finding 10 · Enum `ToString()` is overridden on the base but shadowed by every derived record

**Asked for:** log an order status — `$"status {order.Status}"`.

**Produced:** `status OrderStatus { Value = PAYER_ACTION_REQUIRED }` instead of
`status PAYER_ACTION_REQUIRED`. Same for `ServerEnvironment` in the startup banner:
`env: ServerEnvironment { Value = production }`.

**Actually correct:** `Core/Enum/TypedEnum.cs` does override it —

```csharp
public override string ToString() => Value.ToString() ?? string.Empty;
```

— but every generated enum is declared `public record OrderStatus : StringEnum<OrderStatus>`, and
the C# compiler synthesises a `ToString()` for each record, which shadows the base override. The
intended behaviour is silently lost. Use `.Value` explicitly.

**Should have been prevented by:** `dotnet-models`, which explains that these are `StringEnum<T>`
rather than C# enums and how to *construct* them, but not how to render one back to a string. The
base class carrying a deliberate — and defeated — `ToString()` override makes the trap easy to walk
into: the SDK author clearly intended interpolation to work.

**Impact:** cosmetic, but it lands in every log line and console message an integration writes, and
`Value` is not a name you would reach for when the base type advertises a `ToString()`.

**Reproducible:** yes — interpolate any generated enum from either SDK.

---

## Not yet obtainable

The highest-value class of finding — *the agent had the SDK map and still generated wrong integration
code* — requires generating code against the .NET contract and calling the live APIs. At the time of
writing, no PayPal sandbox or NYT credential exists on this machine, so Phase 4's four candidate
misses (enum member spelling, argument order, `quantity` typing, `experienceContext` placement) are
**unevidenced**. They are named in `nyt-unlock-build-plan.md` §12 as predictions, not observations,
and must not be written up as findings until a real prompt-and-output pair is captured.
