# Demo scripts

Reproducible end-to-end demos of `az-ai`. Mock-only -- no real provider
calls, no credentials required.

## What lives here

| File | Purpose |
|------|---------|
| `season3-finale.sh` | The Season 3 curtain-call demo (S03E27 *The Demo*). Five acts: Setup, Switch, Rules, Fallback, Curtain Call. 22 asserted invariants. |

## Prerequisites

- `bash` (uses `set -euo pipefail`; not strict POSIX, but only standard
  bash features).
- `grep`, `sed`, `mktemp` (every distro ships these).
- `az-ai` on `PATH`. The Season 3 demo requires a Season-3 build that
  includes `--doctor`, `--rotate-creds`, `--provider`, `--fallback`,
  `--offline`, and `AZ_AI_TELEMETRY=1` support. If your binary is older
  the script prints a clear "build az-ai first" message and exits 0
  (so CI does not flap on stale artefacts).
- `jq` is **optional**. When present, NDJSON telemetry from Act V is
  parse-asserted; otherwise the assertion is logged as skipped.

## Running the demo

```bash
# Build a Season-3 binary if you do not have one
DOTNET_ROOT=/usr/lib/dotnet make publish-aot
make install   # copies dist/aot/az-ai to ~/.local/bin/az-ai

# Run end-to-end
bash scripts/demo/season3-finale.sh
```

Exit codes:

| rc | Meaning |
|----|---------|
| 0  | All asserted invariants passed (or binary too old / missing -- gated) |
| 1  | At least one asserted invariant failed |
| 2  | Internal error (missing prerequisite tool) |

The script is **idempotent**: it stages its throwaway state under
`mktemp -d` and removes it on exit (`trap` on `EXIT INT TERM`). It does
not write to the user's real `~/.config/az-ai/preferences.json`.

## Recording with asciinema

```bash
asciinema rec \
  --idle-time-limit 1.5 \
  --title "az-ai season 3 -- The Demo" \
  --command "bash scripts/demo/season3-finale.sh" \
  docs/season-recaps/season-3-demo.cast
```

Tips:

- Run once cold to prime the .NET runtime cache; record the second run.
- Set `TERM=xterm-256color` for predictable ASCII rendering on playback.
- The script emits no smart quotes / em-dash / box-drawing characters --
  the cast file stays portable across renderers.
- For static asset capture, replay with `asciinema cat` and pipe to
  `asciinema-clean` if you want to strip ANSI for paste-into-doc usage.

## Replay

```bash
asciinema play docs/season-recaps/season-3-demo.cast
# or
agg --theme monokai docs/season-recaps/season-3-demo.cast \
    docs/season-recaps/season-3-demo.gif
```

## Why this exists

S03E27 is the Season 3 finale -- the curtain call. The demo is the
mechanical proof that the season's arc shipped: provider abstraction
(`--provider`), profile pinning (`AZ_PROFILE` + `preferences.json`),
SSRF + capability rules, the opt-in fallback chain, and the
air-gapped offline gate. If the script ever stops returning rc=0
against a fresh `make publish-aot && make install`, a regression in
one of those surfaces has slipped in -- the demo is the canary.

Sub-agents are forbidden from editing this directory without an
explicit brief from the showrunner. See
[`.github/skills/shared-file-protocol.md`](../../.github/skills/shared-file-protocol.md).

## See also

- [`docs/exec-reports/s03e27-the-demo.md`](../../docs/exec-reports/s03e27-the-demo.md)
  -- the finale exec report (Larry voice).
- [`docs/season-recaps/season-3-recap.md`](../../docs/season-recaps/season-3-recap.md)
  -- marketing-grade season retrospective.
