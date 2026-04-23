# S02E21 -- *The Conscience*

> The Rabbi takes the lead. Newman holds the clipboard. An "ought"
> for every "must" -- and one honest gap left visible.

**Commit:** `<docs sha>` + `<exec-report sha>`
**Branch:** `main` (direct push)
**Runtime:** ~25 min wall-clock
**Director:** Larry David (showrunner)
**Cast:** Rabbi Kirschbaum (lead, AI Ethics & Responsible Use), Newman (guest, Security Inspector)

## The pitch

Document the project's responsible-use posture as an "ought / must"
matrix so contributors and users can see the principles and the
technical guardrails side by side. The Rabbi names each ethical
surface in the language of obligation; Newman maps each one to the
file path and line number where the obligation is enforced -- or
admits, honestly, that no enforcement exists.

The premise: an "ought" without a "must" is a wish, and a "must"
without an "ought" is bureaucracy. We owe contributors and users
both columns, including the rows where one column is empty.

## Scene-by-scene

### Act I -- Surface inventory

Walked the codebase looking for ethical surfaces. The Rabbi's
working list, before mapping:

1. Prompt injection / data exfiltration via tools.
2. Credential handling.
3. Telemetry and PII.
4. Sub-agent delegation depth.
5. Misuse facilitation (general-purpose CLI as force-multiplier).
6. Bias in model output.
7. Accessibility.
8. Transparency to the user about what the CLI is doing on their
   behalf.

Eight surfaces. Each got grep-tested for an existing technical
control before the Rabbi committed to a `Status` value.

### Act II -- Matrix construction

Newman did the mapping. Spot-checked:

- `azureopenai-cli/Tools/ReadFileTool.cs` -- blocklist + symlink
  resolution + 256 KB cap.
- `azureopenai-cli/Tools/WebFetchTool.cs` -- DNS resolution before
  connect, RFC-1918 / RFC-4193 / loopback / link-local block,
  post-redirect re-check (cap of 3 redirects).
- `azureopenai-cli/Tools/ShellExecTool.cs` -- command blocklist,
  `ArgumentList` for OS escaping.
- `azureopenai-cli/Tools/DelegateTaskTool.cs:16` -- `MaxDepth = 3`.
- `docs/telemetry.md` -- zero-default audit recipe.
- `AnsiPolicy` chokepoint for color decisions; `--raw` enforcement.

Eight rows landed: five `ENFORCED`, two `PARTIAL`, one `NAMED-ONLY`.
The `NAMED-ONLY` is row 6 (bias in model output). It is the honest
row -- there is no CLI-layer control we can add. Naming it is the
control.

### Act III -- Per-surface notes + Newman callouts

For each row, a paragraph in the Rabbi's voice (the "ought" in plain
English) and a paragraph naming the "must" in concrete code-pointer
terms. Then a single-sentence "Newman maps it" blockquote pointing
at the file path or audit recipe. Eight rows, eight callouts.

### Act IV -- Disclosure doc

A separate one-page user-facing statement: where prompts go, the
Microsoft data-handling link, the non-storage / non-training
posture, the keystore behavior, and a frank "the model can be
wrong" reminder. Written for users, not lawyers. Jackie owns
S02E15 for the legal text; this is not that.

### Act V -- Ship

Two new docs, one CHANGELOG bullet, this exec report. ASCII-only
validation passed (no smart quotes, no em/en dashes). No production
code touched.

## What shipped

**Created:**

- `docs/ethics/responsible-use.md` -- the matrix, the per-surface
  notes, the eight Newman callouts, the "what we do not do"
  section.
- `docs/ethics/disclosure.md` -- one-page plain-language user
  disclosure.
- `docs/exec-reports/s02e21-the-conscience.md` -- this file.

**Edited:**

- `CHANGELOG.md` -- one bullet under `[Unreleased] > Added`.

**Not shipped (intentional follow-ups):**

- Did NOT add a prompt-category refusal layer at the CLI. Hosted
  model owns content policy; doing it badly at the CLI tends to
  produce false positives.
- Did NOT add per-tool consent prompts beyond the existing first-run
  wizard. Trade-off documented in row 8.
- Did NOT add a `--explain` flag for tool-call transparency. Named
  as a future episode in row 8.
- Did NOT take a position on competitor or industry ethics. This is
  our posture, not a manifesto. Sue Ellen's S02E19 territory.
- Did NOT touch `LICENSE` or any legal text. Jackie's S02E15 territory.
- Did NOT introduce telemetry of any kind. Zero-default posture from
  S02E07 preserved.
- Did NOT touch glossary, user-stories, AGENTS.md, README.md,
  CONTRIBUTING.md, copilot-instructions, or any other episode-owned
  doc.
- Did NOT modify any production code. Documentation pass only.

## Lessons from this episode

1. **The hardest "ought" to map was bias in model output.** There is
   no CLI-layer control. We marked it `NAMED-ONLY` rather than invent
   a fake control. Honest gaps beat invisible ones. This row will
   likely remain `NAMED-ONLY` for the life of v1.
2. **The two `PARTIAL` rows (misuse, transparency) are real.** We
   could have rounded them up to `ENFORCED` by being generous with
   the meaning of the word. Newman insisted on `PARTIAL`; the Rabbi
   agreed. The cost of optimism here is exactly the kind of
   trust-erosion the page is trying to prevent.
3. **Most of the work was already done in earlier episodes.** S02E04
   gave us the keystore. S02E06 gave us a11y. S02E07 gave us the
   zero-telemetry posture. This episode's job was to name the
   ethical principle each one already implements, not to invent new
   controls. The Rabbi's contribution is naming, not building.
4. **The "Newman maps it" callouts are the load-bearing pattern.**
   A matrix without code pointers is decorative. The callouts are
   what make the page auditable -- a contributor can grep for any
   row's claim and verify it themselves.

## Metrics

- Diff size: 2 new docs (~16 KB combined), 1 CHANGELOG bullet, 1
  exec report.
- Matrix rows: 8 (5 ENFORCED, 2 PARTIAL, 1 NAMED-ONLY).
- Newman callouts: 8.
- Files touched in production code: 0.
- Preflight: not applicable (docs only).
- ASCII validation: passed (no smart quotes, no em/en dashes).
- CI status: docs-only commit; CI gates on code paths.

## Credits

- **Rabbi Kirschbaum** -- lead. Voiced the eight "ought" rows,
  wrote the per-surface paragraphs, owned the `NAMED-ONLY`
  decision, drafted the disclosure doc.
- **Newman** -- guest. Mapped each "ought" to a file path,
  pushed back on `ENFORCED` claims that should have been `PARTIAL`,
  authored the eight callouts.
- **Larry David** -- showrunner. Cast the episode, scoped it
  tightly, kept it from becoming a manifesto.
- **Copilot** -- co-author trailer on both commits.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
