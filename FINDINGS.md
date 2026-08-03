# FINDINGS — Context Plugins evaluation

What the plugins got right or wrong while building `nyt-unlock`. One entry per finding, in the
order they were hit. Per the build plan (§14), this file is never cut for time.

Template:

```
## Finding N - <one-line title>
**Asked for:** <the prompt, verbatim>
**Agent produced:** <the wrong code, minimal snippet>
**Actually correct:** <the right code, and where in the SDK it is documented>
**Should have been prevented by:** <skill name> / <what was missing>
**Reproducible:** yes/no - <how>
```

---

## Finding 0 - SDK map ships for .NET only

**Asked for:** which languages carry a usable operation contract, checked before choosing one.

**Observed:** the plugins ship identity and pattern skills for .NET, TypeScript, Java, Python,
PHP, Ruby and Go, but the SDK map — the folder enumerating every operation signature, model and
typed error — exists only for .NET. Verified across all 22 plugins in the marketplace.

**Consequence:** in every non-.NET language the agent has no operation contract and falls back to
guessing or to scraping the SDK's own `doc/` folder.

**Reproducible:** yes — list the skill directories of any plugin in the marketplace and compare
the per-language folders.

**Status:** recorded before implementation began. This is the finding that drives the language
choice for the rest of this build.

---

## Finding 1 - did installing the second plugin disturb the first?

**Asked for:** build plan Phase 0, step 8.

**Status:** NOT YET ANSWERED — no plugin is installed in this repo at time of writing
(`~/.claude/plugins` is empty, no marketplace registered). To be filled immediately after
installing `paypal` then `nytimes` and running `doctor`, capturing the state of the first
plugin's skills both before and after the second install.
