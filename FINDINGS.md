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

## Not yet obtainable

The highest-value class of finding — *the agent had the SDK map and still generated wrong integration
code* — requires generating code against the .NET contract and calling the live APIs. At the time of
writing, no PayPal sandbox or NYT credential exists on this machine, so Phase 4's four candidate
misses (enum member spelling, argument order, `quantity` typing, `experienceContext` placement) are
**unevidenced**. They are named in `nyt-unlock-build-plan.md` §12 as predictions, not observations,
and must not be written up as findings until a real prompt-and-output pair is captured.
