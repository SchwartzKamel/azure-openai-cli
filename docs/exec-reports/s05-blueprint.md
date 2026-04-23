# Season 5 -- Blueprint -- *Protocols & Plugins*

> *Pre-season treatment. Twenty-four candidate episodes, in slate
> form. Showrunner greenlight required before S05E01 ships.*

**Lead:** Kramer (protocol & code lead)
**Guest:** Newman (plugin trust model & sandboxing)
**Drafted:** 2026-04-23, the morning after Newman's v2 audit landed
**Supersedes:** the *Theme A* sketch in `s03-blueprint.md` (S03 went
multi-provider; S04 closed the loop on model intelligence; S05 is
the ecosystem season we deferred twice)

---

## Showrunner note

S04 closed the loop on internal model intelligence -- aliases, cache,
pattern library, cost ceilings. The binary now thinks for itself.
S05 turns the binary outward. We have spent four seasons building a
clean, fast, AOT-shaped tool that talks to exactly six built-in tools
and exactly the providers we ship in-tree. That ceiling was right
for v1 and v2. It is the wrong ceiling for v3. S05 is the season
where we open the doors -- both directions -- to the wider AI-tooling
ecosystem, and decide on camera what we will and will not let through
those doors. Every other concern is downstream of that.

## Theme statement

Four pillars. **MCP client** so any of the ~18,000 public Model
Context Protocol servers (filesystem, github, postgres, playwright,
linear, atlassian, ...) can plug into our agent loop. **MCP server**
so Claude Code, Codex CLI, gh copilot, and every other MCP host can
call `az-ai` as a sub-tool -- the single largest untapped distribution
vector identified in S02E19 *The Competition*. **Plugin tool registry**
beyond the six built-ins, manifest-driven, AOT-clean, language-agnostic
(per FR-012). **Trust model + sandboxing seams** so the first three
do not detonate the careful security posture Newman just blessed in
the v2 audit (`docs/security/v2-audit.md`, S02E13 *The Inspector*).

These four are inseparable. MCP without trust is a phishing vector --
a malicious server can return crafted tool descriptions that
prompt-inject the model. A plugin registry without sandboxing is a
forked-shell-script malware delivery system. A trust model without
capability negotiation is a yes/no toggle the user clicks through.
S05 ships them as one season because they are one design.

## Why this season, why now

Per S02E19 *The Competition* and `docs/competitive-analysis.md`
(April 2026), MCP went from "emerging Anthropic standard" to
table-stakes inside one calendar year:

- **MCP registry** (`registry.modelcontextprotocol.io`) indexes
  ~18,000 public servers as of March 2026, up from a few dozen at
  Nov-2024 launch.
- **C# SDK v1.0** shipped March 2026 (`modelcontextprotocol/csharp-sdk`,
  Microsoft + Anthropic co-maintained), Tier 1, AOT-aware, three NuGet
  packages (`.Core`, full, `.AspNetCore`).
- **2025-11-25 spec** is the current stable revision -- tools,
  resources, prompts, elicitation, sampling, experimental tasks.
- **Streamable HTTP** replaced HTTP+SSE in March 2025; SSE-only
  servers are deprecated and being phased out by mid-2026. OAuth 2.1 +
  PKCE is mandatory for public/remote servers as of June 2025.
- **Competitor adoption:** Claude Code, OpenAI Codex CLI, Gemini CLI,
  gh copilot CLI, aichat, crush, ollama-via-bridge, fabric -- all ship
  MCP client. Most ship server. We ship neither.

Two costs of waiting another season:

1. **Inbound gap.** Every team that standardized on an MCP tool
   catalog has to fork us to plug us into their stack.
2. **Outbound gap.** We are absent from every "az-ai inside Claude
   Code" demo we could be in. That is the funnel we leave on the
   floor every day FR-013 doesn't ship.

Sue Ellen Mischke flagged this in S02E19. Costanza agreed in
FR-013 §1. S04 was right to ship first; S05 cannot slip again.

## Landscape snapshot (2026)

| Ecosystem | Trust model | Sandbox model | Transport | Capability negotiation |
|-----------|-------------|---------------|-----------|------------------------|
| **MCP (2025-11-25)** | OAuth 2.1 + PKCE for remote; per-server allowlist for stdio | Caller-defined; spec is silent | stdio, Streamable HTTP (SSE deprecated) | `initialize` exchanges client + server `capabilities` map (tools / resources / prompts / sampling / elicitation) |
| **VS Code extensions** | Marketplace publisher verification + signed VSIX (Marketplace 2025); workspace-trust prompt for project-local | Extension Host process isolation; Web Worker for web-only; no syscall sandbox | RPC over IPC | `engines.vscode` semver + `activationEvents` declared in `package.json` |
| **Espanso hub** | Manifest-pinned packages, hub maintainer review, no signing | None -- packages are YAML expanded into the user's keystrokes | filesystem | Version pin in `package.yml`; `espanso package install <name>` |
| **AHK ecosystem** | None central; trust = "I downloaded this `.ahk` from a forum post" | None | n/a | n/a -- the cautionary tale |
| **Homebrew taps** | Maintainer-curated `homebrew-core`; SHA256 in formula; cask gate adds notarization check | Runs as user; no sandbox | n/a (formula = Ruby) | `depends_on macos: ">= :ventura"`, `bottle` arch matrix |
| **LSP** | n/a (trusted local binary launched by editor) | n/a | stdio / TCP | Bidirectional `initialize` capabilities object -- the canonical well-trodden pattern MCP imitates |
| **OAuth 2.x scopes** | Issuer-signed token | n/a (authorization, not isolation) | HTTP | `scope=read:files write:gist` -- string-typed least-privilege grants |
| **us, today** | Built-ins are in-tree; providers gated by ADR-007 six guardrails | `ShellExecTool` blocklist + `argv` only; SSRF guard on `WebFetchTool`; subagent depth cap of 3 | n/a (no plugin transport yet) | n/a (six tools, all known at compile time) |

The pattern across the table: every mature plugin ecosystem
eventually reinvents (a) a manifest, (b) a publisher signature, (c) a
capability declaration, (d) a workspace-trust prompt, (e) a sandbox
or a clear "we do not sandbox" disclaimer. AHK is the proof of what
happens when you skip steps (b)-(e). VS Code is the proof of what
happens when you do them in the right order. We will copy VS Code's
order.

## 24-episode candidate slate

Casting note: Kramer leads the protocol-shaped episodes (he wrote
FR-013); Newman leads the trust- and sandbox-shaped episodes (he
just wrote the v2 audit and owns ADR-007); Jerry leads CI-for-
plugin-builds; Elaine leads plugin-author docs; Costanza leads UX of
discovery; Lloyd Braun gets the "write your first plugin" learner
episode (every plugin ecosystem dies if onboarding is not great). At
least one supporting-player cameo per arc.

### Arc 1 -- MCP client (E01-E04)

- **S05E01 -- *The Protocol*.** MCP client M0 spike: prove
  `ModelContextProtocol` SDK survives `PublishAot=true`; fall back to
  in-tree codec if not. *Lead: Kramer.* (FR-013 §5, M0)
- **S05E02 -- *The Handshake*.** stdio transport + `initialize` +
  `notifications/initialized` + `tools/list` + `tools/call`; reuse
  `ToolRegistry`. *Lead: Kramer.* (FR-013 M2-M3)
- **S05E03 -- *The Streamable*.** HTTP+SSE / Streamable HTTP
  transport; OAuth 2.1 + PKCE; `mcp doctor` for remote servers.
  *Lead: Kramer; cameo: Newman (token storage in keyring).*
- **S05E04 -- *The Negotiation*.** Capability negotiation done right
  -- declare what we support (`tools` only in v1, `resources` and
  `prompts` deferred), surface what the server supports, fail loud
  on mismatch. *Lead: Costanza; cameo: The Maestro (LSP precedent).*

### Arc 2 -- MCP server (E05-E08)

- **S05E05 -- *The Reciprocation*.** `az-ai mcp serve --stdio`;
  expose six built-ins via the same hardening path. *Lead: Kramer.*
  (FR-013 M6)
- **S05E06 -- *The Gatekeeper*.** Server hardening: `--allow-shell`
  must be explicit; `--read-only` shorthand; `delegate_task`
  off-by-default to prevent recursive cost-blast (FR-013 §15 Q2).
  *Lead: Newman; cameo: Morty Seinfeld (cost ceiling).*
- **S05E07 -- *The Scope*.** OAuth-style scope grammar for our
  server: `tools:read`, `tools:exec`, `fs:read`, `fs:read:/scoped`,
  `net:fetch`. Reuses ADR-007 vocabulary. *Lead: Newman.*
- **S05E08 -- *The Round-Trip*.** Dogfood: `az-ai --mcp-server self`
  against `az-ai mcp serve` round-trips `read_file` end-to-end. The
  forever smoke test. *Lead: Puddy; cameo: Kramer.*

### Arc 3 -- Plugin registry (E09-E12)

- **S05E09 -- *The Manifest*.** FR-012 Option A: manifest-declared
  shell-out plugins, JSON, registered in `AppJsonContext`,
  argv-substituted (no shell). *Lead: Kramer.* (FR-012 §A)
- **S05E10 -- *The Discovery*.** XDG-conformant discovery path
  (`$XDG_CONFIG_HOME/azureopenai-cli/tools/*.json` →
  `~/.config/...` → `./.azureopenai-cli/tools/*.json`); `--list-tools`
  surfaces source; `--no-plugins` kill switch. *Lead: Costanza;
  cameo: Mickey Abbott (a11y of `--list-tools` output).*
- **S05E11 -- *The Versioning*.** Manifest schema versioning (`schema_version: 1`),
  semver `min_az_ai_version`, deprecation warnings. *Lead: Mr. Lippman.*
- **S05E12 -- *The Signature*.** Optional Ed25519 manifest signing;
  `az-ai plugins verify`; signature is opt-in for v1, required for
  the eventual public index. *Lead: Newman; cameo: Jackie Chiles
  (key-management policy).*

### Arc 4 -- Trust model (E13-E16)

- **S05E13 -- *The Workspace*.** VS Code-style workspace-trust
  prompt: project-local plugins OFF by default; per-directory
  decision persisted in `~/.config/azureopenai-cli/trusted-dirs.json`;
  TOFU prompt with diff on plugin change. *Lead: Newman; cameo:
  Costanza (UX).* (FR-012 §Security #2)
- **S05E14 -- *The Publisher*.** Publisher identity for the hosted
  index: GitHub-account-bound publisher records (Marketplace model);
  unverified publishers visible but flagged in `--list-tools`. *Lead:
  Newman; cameo: Bob Sacamano (registry plumbing).*
- **S05E15 -- *The Capability*.** Capability scopes per plugin
  manifest (`requires: ["fs:read", "net:fetch"]`), prompted at
  install time, displayed at run time, enforced where the OS lets us
  enforce them. *Lead: Newman; cameo: Rabbi Kirschbaum (informed
  consent).*
- **S05E16 -- *The Rollback*.** Post-install signals + rollback:
  `az-ai plugins downgrade`, `az-ai plugins quarantine <name>`, audit
  log of every plugin invocation per FR-013 §7. *Lead: Frank Costanza;
  cameo: Newman.*

### Arc 5 -- Sandboxing (E17-E20)

- **S05E17 -- *The Bubblewrap*.** Linux sandbox seam:
  `bubblewrap`-based isolation when present (`--share-net=none`,
  bound rw on `cwd` only, ro on `$HOME` opt-in); seccomp-bpf filter
  for the syscall-allowlist case. Graceful fallback when not
  installed. *Lead: Newman; cameo: Kramer.*
- **S05E18 -- *The Hermit*.** macOS `sandbox-exec` profile; explore
  the App Sandbox container model for a future signed `.app`
  distribution. *Lead: Newman.*
- **S05E19 -- *The Container*.** Windows `AppContainer` + Job
  Objects for resource caps; document the gap on Windows Server SKUs.
  *Lead: Newman; cameo: Jerry (CI for Windows runners).*
- **S05E20 -- *The Wasm*.** WASI / Wasmtime cross-platform fallback
  for the in-process plugin path (FR-012 Option C). Capability-scoped,
  language-agnostic, +10-20 MB binary cost. Decide: ship in S05 or
  defer to S06? *Lead: Kramer; cameo: Morty Seinfeld (binary-size
  budget).*

### Arc 6 -- Plugin developer kit (E21-E23)

- **S05E21 -- *The Scaffold*.** `az-ai plugins new <name>` -- writes
  manifest, sample script, README, signing key stub. *Lead: Elaine;
  cameo: Lloyd Braun.*
- **S05E22 -- *The Harness*.** Plugin test harness: feed the manifest
  to a local stub model, assert the LLM picks the right tool with the
  right args; CI recipe for plugin authors. *Lead: Puddy; cameo:
  Jerry (GitHub Actions starter workflow).*
- **S05E23 -- *The Publish*.** End-to-end publish flow: sign, push to
  the index, verify install on a fresh machine. Bob Sacamano writes
  the Homebrew + Scoop + Nix bridges so plugins come along for the
  ride. *Lead: Bob Sacamano; cameo: Mr. Lippman (release cadence).*

### Arc 7 -- Finale (E24)

- **S05E24 -- *The Three Plugins***. Ship three reference plugins
  on launch day, one per surface, each demoing a different facet of
  what we just built:
  1. **`git_log`** -- a manifest-declared shell-out tool (FR-012 §A).
     Lloyd's "write your first plugin" walkthrough is built around it.
  2. **`az-ai-mcp-azure`** -- an MCP server we author, exposing
     `az group list`, `az resource show`, `az login --status` to any
     MCP host. The "az-ai inside Claude Code" Peterman launch quote.
  3. **`gemma-adapter`** -- a model-adapter plugin demonstrating
     that the provider seam (S03 *Local & Multi-Provider*) and the
     plugin seam (S05) are the same seam by E24. Validates Costanza's
     architectural bet from FR-012 §1. *Lead: all hands; finale
     director: Larry David.*

### One-line cast tally

Kramer 7 (E01, E02, E03, E05, E08-cameo, E09, E20). Newman 8 (E03-cameo,
E06, E07, E12, E13, E14, E15, E17, E18, E19). Jerry 1 lead + cameos
(E19, E22). Elaine 1 lead + ongoing (E21). Costanza 2 (E04, E10).
Lloyd Braun 1 (E21, finale walkthrough). Puddy 2 (E08, E22).
Mr. Lippman 1 (E11). Bob Sacamano 1 (E23). Frank Costanza 1 (E16).
The Maestro / Mickey Abbott / Morty Seinfeld / Rabbi Kirschbaum /
Jackie Chiles -- one cameo each. Larry David -- finale director on
E24.

## Cross-references to FR-NNN proposals

- **FR-012 -- Plugin Tool Registry.** Drives Arcs 3-6 (E09-E23).
  Option A is S05's recommended path; Option C (WASI) is the E20
  decision point.
- **FR-013 -- MCP Client + Server.** Drives Arcs 1-2 (E01-E08).
  Phase 1 (client) = E01-E04; Phase 2 (server) = E05-E08.
- **FR-014 -- Local Preferences (TOML).** S05 writes into
  `~/.config/az-ai/preferences.toml` for both `[mcp.servers.*]`
  (FR-013 §6.1) and `[plugins.*]` -- shares the canonical config
  surface FR-014 established.
- **SECURITY-AUDIT-001.** The 10 findings from v1.1 audit predate
  S05's surface; S05 must not regress any open finding. Newman's E12,
  E13, E14, E15 reuse the same threat-model vocabulary.
- **`docs/security/v2-audit.md` (S02E13).** The five-PASS / one-NEEDS-
  FOLLOW-UP baseline. S05 inherits the bar Newman set there. Each
  new tool surface (MCP client, MCP server, manifest plugin, sandbox
  seam) gets its own row in the v3 audit when this season wraps.
- **ADR-007 -- Third-Party HTTP Provider Security.** The six
  guardrails (digest-pinned containers, auth posture, SSRF block,
  credential discipline, ToU acknowledgment, audit log) are reused
  verbatim for MCP HTTP transport (E03) and the publisher index
  (E14).

## Risks and known unknowns

1. **MCP spec churn during S05 production.** The 2025-11-25 revision
   is current; a 2026-mid revision is widely expected. Tools / resources
   are stable; sampling and experimental tasks are the moving targets.
   Mitigation: ship `tools` only in v1 per FR-013 §2; gate everything
   else behind feature flags.
2. **Sandboxing without root on shared CI runners.** `bubblewrap`
   requires user namespaces; some hosted runners disable them.
   `sandbox-exec` is deprecated-ish on macOS. AppContainer needs
   Windows-side investigation. Mitigation: every sandbox seam is
   advisory + best-effort; the plugin system does not *require* a
   sandbox to load (it requires *a documented trust decision*).
3. **WASI maturity for system-call needs.** Plugins that want to
   shell out, read files outside cwd, or open sockets are exactly the
   cases WASI is least mature for in 2026. Mitigation: WASI is the
   E20 decision point; if it slips, ship Option A in S05 and revisit
   Option C in S06.
4. **Plugin signing UX.** Forcing every plugin author to manage an
   Ed25519 key is the fastest way to kill the ecosystem (see: PGP).
   Mitigation: signing is opt-in for the file-system plugin path,
   required only for the hosted index; index can manage keys on
   behalf of GitHub-authenticated publishers (Marketplace model).
5. **Capability scope creep.** Easy to design 40 scopes ("read:fs:home:user:non-hidden:non-symlink"). Easy to ship a handful nobody can reason about. Mitigation: copy MCP's coarse-grained scope set + ADR-007's vocabulary; resist new scopes without a referenced finding.
6. **Plugin marketplace moderation cost.** A hosted index becomes
   GitHub Issues for plugin spam, takedowns, reported abuse, DMCA.
   That is a recurring labor cost we have not staffed for. Mitigation:
   S05 ships an *index* (a git-tracked JSON file, like Homebrew
   taps), not a *marketplace*. No hosting, no review queue, no
   moderation surface. Boundary explicit in §"What S05 does NOT cover".
7. **Trust collapse if a malicious plugin ships through us.** One
   credentials-stealing plugin with our index badge on it sets the
   project back two years. Mitigation: stack defense -- workspace
   trust (E13) + capability scopes (E15) + audit log (E16) + sandbox
   when available (E17-E20) + clear unverified-publisher labeling
   (E14). No single layer is load-bearing.
8. **MCP description prompt-injection.** A remote MCP server can
   return tool descriptions designed to social-engineer the model
   into calling another tool with attacker-controlled args. Already
   called out in FR-013 §7. Mitigation: tool-result fencing,
   description sanitization, namespacing -- all client-side, all
   non-negotiable.

## What S05 does NOT cover (boundary)

- **NOT** new providers -- that was S03 (*Local & Multi-Provider*).
  Provider abstraction is reused, not redesigned.
- **NOT** model routing -- that was S04 (*Model Intelligence*).
  Plugins do not influence model selection.
- **NOT** enterprise compliance / SSO / RBAC -- S06+ candidate.
  OAuth 2.1 + PKCE for MCP HTTP is a *protocol* requirement, not an
  enterprise IAM story.
- **NOT** a hosted plugin marketplace. S05 ships an **index**
  (git-tracked JSON, Homebrew-taps shape) and a **registry-aware
  client**. We do not host plugin code, do not run a moderation
  queue, do not operate a CDN. That is a S07+ decision once we know
  what plugin volume actually looks like.
- **NOT** sandboxing built-in tools. The six existing built-ins are
  trusted in-tree code that already passed Newman's v2 audit.
  Sandboxing applies only to plugins and MCP servers.
- **NOT** Resources, Prompts, Sampling, Elicitation, or experimental
  Tasks from the MCP spec. Tools-only in v1, per FR-013 §2.

## Open questions for showrunner greenlight

1. **24 episodes is a full season; FR-013 estimates ~6 weeks Phase 1 +
   Phase 2 alone.** Is the season-length expectation calendar-aligned
   (every other Tuesday) or shipping-cadence-aligned (one episode per
   merged PR cluster)? If calendar, we cut to ~16 episodes and combine
   Arcs 5-6.
2. **Hosted index in S05 or S06?** Arc 3 + Arc 4 produce a working
   plugin system without a hosted index -- users just point at git
   URLs. The index is a multiplier, not a prerequisite. Cutting it
   buys ~4 episodes back.
3. **WASI in-scope or deferred?** E20 is currently a decision episode.
   If the answer is "defer," Arc 5 collapses from four episodes to
   three and we ship the season ~2 weeks earlier.
4. **Do we coordinate the S05E08 dogfood smoke test with a `v2.x →
   v3.0` major-version cut?** MCP client + server is plausibly the
   v3.0 headline feature; Mr. Lippman has opinions.
5. **Newman is on-camera for 8 of 24 episodes.** Is that load
   sustainable, or does FDR (chaos / red team) deputize a few of the
   sandbox episodes (E17-E19) so Newman can focus on trust (E12-E16)?

---

*-- Kramer (protocol & code lead), with security guardrails from
Newman (still saying "Hello, Kramer." in the hallway every morning),
program-management notes from Mr. Pitt, and a quiet "are you sure
you want a marketplace, Jerry?" from Morty Seinfeld at the back of
the writers' room.*

---

## Adjacent blueprints

- Previous blueprint: [`s04-blueprint.md`](s04-blueprint.md) -- *Model Intelligence*.
- Next blueprint: [`s06-blueprint.md`](s06-blueprint.md) -- *Dogfooding*.
- Long-horizon slate: [`seasons-roadmap.md`](seasons-roadmap.md).
