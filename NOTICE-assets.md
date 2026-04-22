# Bundled-Assets Attribution

This file records the provenance, license, and rationale for every
non-code asset bundled with `az-ai` / the Azure OpenAI CLI. It exists
to satisfy the attribution hygiene called for in
[`docs/audits/docs-audit-2026-04-22-jackie.md`](docs/audits/docs-audit-2026-04-22-jackie.md)
finding **F-01**: every bundled asset must carry a source-or-license line
so the distributed artifact is defensible on inspection.

Scope: anything under `img/`, `docs/demos/recordings/`, or other
binary/media assets shipped in the repo or any packaging channel
(Homebrew formula, Scoop manifest, Nix flake, tarball, GHCR image,
GitHub Release). Code-licensing attribution lives in
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) and
[`NOTICE`](NOTICE); this file is for everything else.

---

## Manifest

### `img/its_alive_too.gif`

- **Type:** Animated GIF (GIF89a, 1543 × 821).
- **Origin:** First-party. Recorded via `asciinema rec` against the
  `az-ai` binary and rendered to GIF via
  [`agg`](https://github.com/asciinema/agg). The exact regeneration
  procedure is documented in
  [`docs/demos/hero-gif.md`](docs/demos/hero-gif.md).
- **Source script:** [`docs/demos/scripts/01-standard-prompt.sh`](docs/demos/scripts/01-standard-prompt.sh).
- **Copyright:** © 2025-2026 SchwartzKamel.
- **License:** MIT (same as the project; see [`LICENSE`](LICENSE)).
- **Filename note.** The filename `its_alive_too` is a nominative
  reference to the 1931 *Frankenstein* "It's alive!" line. The asset
  itself contains **no footage, stills, audio, makeup design, or other
  protectable element** from that film or any other third-party work
  — it is a terminal recording of this project's own binary. The name
  is a joke; the bytes are first-party.

---

## Policy for new assets

Whenever a new asset is added to `img/`, `docs/demos/recordings/`, or
any other bundled location:

1. **Record the source in this file** before the PR merges. If the
   asset is first-party, declare the recording workflow (script +
   renderer) so anyone can reproduce it. If the asset is third-party,
   declare the upstream URL, creator, license (SPDX identifier where
   possible), and the specific grant that permits redistribution.
2. **Prefer first-party, CC0, or public-domain assets.** Anything else
   must be cleared with a licensing reviewer before commit.
3. **No bare fair-use claims.** Fair use is a defense, not a license,
   and pleading it after the fact is expensive. If fair use is the
   only available theory, escalate to the release manager
   (Mr. Lippman) before the asset is committed.
4. **Regenerate → update.** Re-recording an existing asset (e.g., a
   new theme for `its_alive_too.gif`) does not change the attribution
   obligation. Update this file if the provenance details shift (new
   script, new binary version, new renderer).
5. **Remove on doubt.** If an asset's provenance cannot be
   established with confidence, delete it. A missing hero GIF is
   cheaper than a DMCA takedown.

---

## DCO / inbound-license policy (advisory)

The docs-audit 2026-04-22 (F-04) flagged that `CONTRIBUTING.md` does not
state an inbound license or DCO posture. Pending an owner-of-record edit
to `CONTRIBUTING.md` (Elaine / Mr. Wilhelm), the operative policy is
**inbound = outbound**: by submitting a contribution through the project's
GitHub repository, the contributor agrees that the contribution is
licensed to the project under the [MIT License](LICENSE), matching
GitHub ToS §D.6. A formal DCO sign-off (`git commit -s`) is **not yet
required** but is recommended for contributions originating outside the
GitHub web UI (email patches, mirrors). This advisory paragraph does not
substitute for a proper `CONTRIBUTING.md` clause and should be removed
once that clause lands.

---

## Cross-references

- [`NOTICE`](NOTICE) — Apache-2.0 §4(d) compliance + trademark posture
  for code dependencies.
- [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) — authoritative
  per-package manifest for the v2.x dependency graph.
- [`docs/licensing-audit.md`](docs/licensing-audit.md) — reproducible
  audit of the resolved dependency graph with verification cadence.
- [`docs/legal/trademark-policy.md`](docs/legal/trademark-policy.md) —
  project trademark posture and nominative-use rules.
- [`docs/demos/hero-gif.md`](docs/demos/hero-gif.md) — regeneration
  workflow for the hero GIF.
