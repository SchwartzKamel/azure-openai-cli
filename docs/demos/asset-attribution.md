# Demo Asset Attribution Audit

> *Outrageous what can hide in a demos folder — a stray screenshot, a
> borrowed icon, a font clipped from a slide deck, and suddenly you've got
> a DMCA in your inbox.* This document inventories every non-code asset
> under `docs/demos/` and `img/`, records its provenance, and establishes
> the clearance rule for anything added next.

## Scope

This audit covers non-code assets that are **committed to the repository
and redistributed** through any packaging channel (Homebrew formula,
Scoop manifest, Nix flake, tarball staging, GHCR image, GitHub Release).
"Non-code" means:

- Images (`.png`, `.jpg`, `.jpeg`, `.gif`, `.svg`, `.webp`, `.avif`).
- Recordings (`.cast`, `.mp4`, `.webm`, `.mov`).
- Fonts (`.ttf`, `.otf`, `.woff`, `.woff2`).
- Sample / fixture data that is not generated at build time.

Shell scripts under `docs/demos/scripts/` are first-party source code
licensed under the project's MIT [`LICENSE`](../../LICENSE); they are out
of scope for this audit and covered by the umbrella project license.

## Inventory as of 2026-04-22

A full walk of `docs/demos/` and `img/` was performed on 2026-04-22
(commit base: main @ `d14e753`):

```
docs/demos/hero-gif.md                              (Markdown — first-party)
docs/demos/README.md                                 (Markdown — first-party)
docs/demos/scripts/01-standard-prompt.sh             (shell — first-party, MIT)
docs/demos/scripts/02-raw-espanso.sh                 (shell — first-party, MIT)
docs/demos/scripts/03-agent-tool-calling.sh          (shell — first-party, MIT)
img/its_alive_too.gif                                (GIF — first-party; see NOTICE-assets.md)
```

### Findings

| # | Asset | Type | Provenance | Ledgered? | Clearance |
|---|---|---|---|---|---|
| 1 | `img/its_alive_too.gif` | GIF | First-party terminal recording via `asciinema rec` + `agg`, script `docs/demos/scripts/01-standard-prompt.sh`. | ✅ [`NOTICE-assets.md`](../../NOTICE-assets.md#imgits_alive_toogif) | MIT (project license). |

**Net new assets requiring attribution: 0.**

No images, recordings, fonts, or binary fixtures live under `docs/demos/`
today beyond the shell scripts. No third-party media is bundled.
`img/` contains exactly one asset and it is already ledgered.

### What was *not* found

For future-audit evidence, the following were searched for and are
**absent** from `docs/demos/` and `img/` as of this audit:

- No third-party screenshots (no vendor UI captures, no conference-talk
  slide clips).
- No stock-photo or stock-illustration assets (Getty / Shutterstock /
  Unsplash / Pexels — no embedded metadata strings matching these CDNs).
- No fonts embedded as files (CSS / Markdown renderers pull system
  fonts only).
- No copyrighted recordings (audio clips, broadcast GIFs, film stills).

## Clearance policy for new demo assets

Any PR that adds an asset under `docs/demos/`, `img/`, or a new
`docs/demos/recordings/` or `docs/demos/images/` subdirectory **must**,
before merge:

1. **Prefer first-party.** Re-record, re-screenshot, or
   re-illustrate. `docs/demos/scripts/` + `asciinema` + `agg` is the
   standard pipeline; document the source script and renderer in
   [`NOTICE-assets.md`](../../NOTICE-assets.md).
2. **If first-party is not possible**, use a clearly-labeled
   public-domain (CC0, US federal-works, pre-1929) or permissively-licensed
   (CC-BY, CC-BY-SA with attribution, MIT-licensed art) asset **and**
   add an entry to [`NOTICE-assets.md`](../../NOTICE-assets.md) capturing:
   - Source URL (stable, preferably archive.org-mirrored).
   - Creator / rights-holder.
   - SPDX license identifier (or a verbatim license grant if SPDX does
     not cover it).
   - A one-line description of what the asset is used for.
3. **Never plead bare fair use.** Fair use is a defense, not a license;
   it is expensive to litigate even when correct. If fair use is the
   only theory available, escalate to the release manager
   (Mr. Lippman) and licensing reviewer (Jackie) before the asset is
   committed.
4. **Copyleft contamination is a hard stop.** No `CC-BY-NC`, no `CC-BY-SA`
   applied to anything we redistribute commercially-adjacent, no
   "free-for-personal-use-only" stock sources. The project's MIT
   posture is not compatible with non-commercial or share-alike asset
   obligations.
5. **Trademark caution.** Do not bundle a third-party logo (Azure,
   OpenAI, Microsoft, GitHub, NuGet, Docker, etc.) in a demo asset
   without a specific nominative-use rationale. See
   [`docs/legal/trademark-policy.md`](../legal/trademark-policy.md).

## Reaffirmation cadence

Re-walk `docs/demos/` and `img/` on:

- Every PR that adds a new file under either directory (clearance is
  a merge gate, not a follow-up).
- Every minor release (`x.y.0`), as a Mr. Lippman pre-gate.
- Quarterly, as a baseline drift check — even absent PR activity,
  transitive docs imports and Markdown-embedded remote assets can
  change posture.

## Cross-references

- [`NOTICE-assets.md`](../../NOTICE-assets.md) — authoritative ledger
  of bundled non-code assets.
- [`NOTICE`](../../NOTICE) — Apache-2.0 §4(d) + trademark posture for
  code dependencies.
- [`THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md) — per-package
  manifest for code dependencies.
- [`docs/legal/trademark-policy.md`](../legal/trademark-policy.md) —
  project trademark posture.
- [`docs/audits/docs-audit-2026-04-22-jackie.md`](../audits/docs-audit-2026-04-22-jackie.md) —
  the licensing docs audit that motivated this file (finding F-01 and
  the surrounding attribution-hygiene recommendations).

---

*Not legal advice. Outrage supplied at no extra cost.*
