# Responsible Use

> Rabbi Kirschbaum, on the record. Newman in the margins, mapping
> every "ought" to a "must."

A CLI is a small thing. A CLI that pipes text to a language model
and then runs tools on the user's behalf is not a small thing -- it
sits at the intersection of the user's keyboard, their filesystem,
their network, and a hosted model that neither of us controls. So
it is worth taking the ethical questions seriously, even (especially)
the ones that do not have clean technical answers. This page is
where we name them, side by side with the technical controls that
back them up.

The principle is simple: an "ought" without a "must" is a wish,
and a "must" without an "ought" is bureaucracy. We try to write
both columns honestly. Where we have only the "ought" -- where we
have named a responsibility but cannot point at a guardrail -- we
mark it `NAMED-ONLY` and leave the gap visible. Honest gaps are
better than invisible ones.

## The ought / must matrix

| # | Surface | Ethical principle (ought) | Technical control (must) | Status |
|---|---------|---------------------------|--------------------------|--------|
| 1 | Prompt injection / data exfiltration via tools | Do not help users harm themselves or third parties through the model's ability to read files or fetch URLs on their behalf. | `ReadFileTool` blocklist (sensitive paths, symlink resolution, size cap), `WebFetchTool` SSRF protection (DNS resolution + private-range block, post-redirect re-check), `ShellExecTool` command blocklist + shell-substitution rejection. | ENFORCED |
| 2 | Credential handling | Minimize blast radius if a user's API key is compromised; never surface keys in logs, output, or telemetry. | Per-OS keystore via S02E04 LOLBin storage (DPAPI / libsecret / Keychain), masked input in the wizard, no key logging anywhere in the binary. | ENFORCED |
| 3 | Telemetry and PII | User trust ranks above product analytics. Prompt content, file paths, and credentials must never leave the machine without explicit consent. | Zero-by-default posture established in S02E07. No telemetry transport ships in v1 -- there is nothing to opt out of because there is nothing collecting. | ENFORCED |
| 4 | Sub-agent delegation depth | Keep the user in control. Recursive delegation can hide cost, behavior, and intent behind a single prompt. | `DelegateTaskTool.MaxDepth = 3` cap (`azureopenai-cli/Tools/DelegateTaskTool.cs:16`); `RALPH_DEPTH` environment variable propagation. | ENFORCED |
| 5 | Misuse facilitation | Do not be a force-multiplier for harm. A general-purpose CLI with shell, file, and web tools is exactly the kind of thing that needs guardrails. | Tool-level blocklists (rows 1 and 4) plus the architectural fact that this CLI is a thin shell over a hosted model that enforces its own content policy. | PARTIAL |
| 6 | Bias in model output | Name the responsibility honestly: we do not choose the model's training data, but we do ship defaults, and defaults are choices. | Document the model selection mechanism (`AZUREOPENAIMODEL`, multi-model fallback) and the fact that the user can override it. See `docs/ethics/disclosure.md`. | NAMED-ONLY |
| 7 | Accessibility | Excluding disabled users is an ethical issue, not just a UX one. | `NO_COLOR` / `FORCE_COLOR` env-var gates (S02E06), wizard accessibility review, `--raw` flag for assistive-tech-friendly output. | ENFORCED |
| 8 | Transparency to the user | The user should be able to tell what is being sent where, and what is being run on their behalf. | Tool calls are surfaced in agent-mode output; `disclosure.md` names the data path and the project's non-storage / non-training posture. | PARTIAL |

Eight rows. Five `ENFORCED`, two `PARTIAL`, one `NAMED-ONLY`. The
`NAMED-ONLY` row is the honest one -- bias in model output is real,
and we cannot fix it from the CLI layer. The two `PARTIAL` rows are
admissions that the technical control covers part of the ethical
surface but not all of it.

## Per-surface notes

### 1. Prompt injection and data exfiltration via tools

The "ought": a model with file-read and web-fetch tools is a model
that can be coaxed -- by a hostile document, a malicious URL, or a
clever prompt -- into reading a private key and posting it to an
attacker. The user did not ask for that. The user asked for help
with their code. The ethical obligation is to make that class of
attack hard, not easy.

The "must": three tool-level blocklists, each in code. `ReadFileTool`
refuses sensitive paths (`/etc/shadow`, `~/.ssh`, etc.), resolves
symlinks before checking, and caps reads at 256 KB. `WebFetchTool`
resolves the hostname before connecting, refuses RFC-1918 / RFC-4193
/ loopback / link-local addresses, and re-checks after every redirect
(up to a cap of three). `ShellExecTool` rejects `$()`, backticks,
`<()`, `>()`, `eval`, `exec`, and a list of dangerous commands; it
uses `ArgumentList` rather than `Arguments` so the OS does the
escaping.

> **Newman maps it:** rows enforced in
> `azureopenai-cli/Tools/ReadFileTool.cs`,
> `azureopenai-cli/Tools/WebFetchTool.cs`, and
> `azureopenai-cli/Tools/ShellExecTool.cs`; every blocked pattern has
> a matching test in `tests/AzureOpenAI_CLI.Tests/ToolHardeningTests`.

### 2. Credential handling

The "ought": the user hands us their Azure OpenAI key. That is an
act of trust. We owe them the smallest possible blast radius if
that key ever leaks -- which means storing it where the OS can
defend it, and never writing it to a log, a stack trace, or a
crash report.

The "must": the S02E04 "Locksmith" episode added per-OS keystore
support -- DPAPI on Windows, libsecret on Linux, Keychain on macOS.
The first-run wizard masks input. No code path in the binary writes
the API key to stdout, stderr, or any log file. There is no
telemetry to leak it through (see row 3).

> **Newman maps it:** keystore in `azureopenai-cli/Security/`
> (per-OS providers); wizard masking in the first-run flow; an audit
> grep for `AZUREOPENAIAPI` across the source tree returns only
> read-paths, never log-paths.

### 3. Telemetry and PII

The "ought": if telemetry exists, it must not leak prompt content,
file paths, or credentials. The trust the user places in a CLI that
sees their prompts is greater than the trust they place in a web
app, because the CLI runs unsupervised. Product-analytics curiosity
does not outweigh that trust.

The "must": there is no telemetry transport in this binary. S02E07
"The Observability" landed the zero-by-default posture and
`docs/telemetry.md` documents the grep commands a contributor can
run to verify it themselves. There is nothing to opt out of because
there is nothing collecting.

> **Newman maps it:** `grep -rn "HttpClient\|telemetry\|insights"
> azureopenai-cli/` returns nothing user-data-bearing; see
> `docs/telemetry.md` for the full audit recipe.

### 4. Sub-agent delegation depth

The "ought": when a user runs the CLI in agent mode, they are
making one decision -- "answer this prompt." Recursive sub-agent
delegation lets the model multiply that one decision into many,
each of which costs money and runs tools. Past some depth the user
is no longer in control of what is happening on their behalf.

The "must": `DelegateTaskTool.MaxDepth = 3`. The `RALPH_DEPTH`
environment variable is propagated and checked on every delegation.
Beyond depth 3, the tool refuses and returns an error to the model.

> **Newman maps it:**
> `azureopenai-cli/Tools/DelegateTaskTool.cs:16` is the cap;
> `azureopenai-cli/Tools/DelegateTaskTool.cs:46-47` is the
> enforcement point.

### 5. Misuse facilitation

The "ought": a general-purpose CLI with shell, file, and web tools
can be used to do things we would not endorse. We accept that we
cannot enumerate every misuse. We can, however, refuse to be a
force-multiplier for the obvious ones.

The "must" -- and this is where we mark `PARTIAL` honestly: we
enforce the tool-level blocklists from row 1, and we lean on the
fact that this CLI is a thin shell over a hosted model that enforces
its own content policy at the upstream layer. We do not add a
prompt-category refusal layer in the CLI itself, both because that
is the model provider's job and because doing it badly at the CLI
layer (regex over user prompts) tends to produce false positives
that frustrate legitimate users without stopping motivated bad
actors.

> **Newman maps it:** tool blocklists per row 1; upstream content
> policy enforced by Azure OpenAI itself, not this binary.

### 6. Bias in model output

The "ought": models reflect their training data. The CLI does not
choose that data, but it does ship a default model selection, and
defaults are choices. We owe the user honesty about what we control
and what we do not.

The "must" -- this is the `NAMED-ONLY` row. There is no technical
control we can add at the CLI layer that audits model bias. What we
can do is document the model selection mechanism (`AZUREOPENAIMODEL`,
comma-separated multi-model fallback), document that the user can
override it, and link to Microsoft's responsible-AI documentation
for the hosted models. See `docs/ethics/disclosure.md`. This row
will likely remain `NAMED-ONLY` for the life of v1.

> **Newman maps it:** there is nothing to map. The honest answer is
> that bias in model output is upstream of every line of code in
> this repository. Naming it is the control.

### 7. Accessibility

The "ought": a CLI that is unusable by screen-reader users, by
colorblind users, or by users who navigate by keyboard alone is a
CLI that has chosen to exclude them. That is an ethical choice, not
just a UX one.

The "must": S02E06 "The Screen Reader" landed `NO_COLOR` and
`FORCE_COLOR` honoring via the `AnsiPolicy` helper, with `NO_COLOR`
taking precedence per the spec. The wizard was reviewed for
keyboard-only operation. The `--raw` flag exists for assistive-tech
pipelines that need unformatted output.

> **Newman maps it:** `AzureOpenAI_CLI.ConsoleIO.AnsiPolicy` is the
> single chokepoint for color decisions in v1; `--raw` is enforced
> by the `isRaw` guard at every output path.

### 8. Transparency to the user

The "ought": when the CLI runs tools on the user's behalf, the user
should be able to tell what was run, what was sent where, and what
came back. Hidden side effects erode trust faster than any single
bug.

The "must" -- `PARTIAL`, honestly. Tool calls are surfaced in
agent-mode output, but the rendering is terse. The disclosure doc
(`docs/ethics/disclosure.md`) names the data path (prompt -> Azure
OpenAI -> response) and the project's non-storage / non-training
posture. A future episode could add a `--explain` flag that shows
the full tool-call tree in human-readable form; we have not done
that.

> **Newman maps it:** agent-mode output renders tool calls inline;
> `docs/ethics/disclosure.md` is the user-facing data-path statement.

## What we do not do

This page would not be honest if it pretended we had answered every
question. We did not.

- **We do not audit the model's training data.** We cannot. That is
  an upstream property of the hosted model.
- **We do not refuse prompt categories at the CLI layer.** The
  hosted model enforces its own content policy. Adding a second,
  weaker layer in the CLI tends to produce false positives without
  stopping motivated misuse.
- **We do not add user-consent flows beyond what already exists.**
  The first-run wizard is the consent surface; we did not add
  per-tool prompts. That trade-off is documented in row 8.
- **We do not take a position on competitor or industry ethics.**
  This page is our posture, not a manifesto. Sue Ellen Mischke
  handles competitive analysis in S02E19; this is not that.
- **We do not introduce telemetry of any kind.** The zero-default
  posture from S02E07 is preserved. There is no opt-in flag to add
  because there is no transport to gate.
- **We do not modify any production code in this episode.** This is
  a documentation pass over controls that already exist in the
  binary.

The honest gap is row 6. Bias in model output is real, we cannot
fix it from the CLI layer, and naming it is the only control we
have. We mark it `NAMED-ONLY` rather than pretending otherwise.

## See also

- `docs/ethics/disclosure.md` -- the short, plain-language statement
  for users.
- `docs/telemetry.md` -- the zero-default-telemetry audit recipe.
- `docs/accessibility.md` -- the a11y posture in detail.
- `SECURITY.md` -- vulnerability disclosure policy.
