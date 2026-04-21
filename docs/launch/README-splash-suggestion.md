# README splash — v2.0.0 announcement

**Where to paste:** Insert the block below in [`README.md`](../../README.md) **immediately before** the existing `### New in v2.0.0` heading (currently around line 47). The goal is a visible banner above the flag table that announces the release and links out to the long-form pieces, without displacing the v1.9.x feature prose that still governs today's install.

**After the cutover** (once `az-ai` *is* v2), drop the "during the dual-tree window" parenthetical from the second bullet.

```markdown
> ### 🚀 v2.0.0 is here
>
> - **Microsoft Agent Framework underneath.** ~2,200 lines of hand-rolled orchestration swapped for first-party primitives; every v1 flag, env var, and exit code unchanged.
> - **Ships as `az-ai-v2`** during the dual-tree window — install side by side, migrate when you're ready.
> - **AOT binary: 12.91 MB (1.456× v1), startup p95 inside the ≤25% budget, RSS ≤ v1.** Hot-path Espanso / AHK users are safe.
> - New: `--estimate`, opt-in `--telemetry`, `--prewarm`, persona routing fully wired.
>
> 📣 [Launch announcement](docs/launch/v2.0.0-announcement.md) · 📝 [Release notes](docs/release-notes-v2.0.0.md) · 🔀 [Migration guide](docs/migration-v1-to-v2.md)
```
