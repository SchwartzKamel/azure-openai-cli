# S02E31 -- Findings backlog (sub-agent staging)

> Staging file for findings surfaced by S02E31 *The Audition*. The
> orchestrator integrates these into `s02-writers-room.md` ->
> "Findings backlog" subsection on the next writers' room update.
>
> Format follows [`findings-backlog`](../../.github/skills/findings-backlog.md)
> verbatim.

## Findings (7)

- **`e31-personas-no-stay-in-character-clause`** [smell, b-plot]
  Surfaced by S02E31 *The Audition*. None of the five generic
  persona system prompts contain a "decline off-topic asks" or
  "stay in role" clause; a user asking `coder` to write a poem or
  `security` to recommend a restaurant has nothing in the prompt
  pushing the model to refuse or redirect.
  File: `azureopenai-cli/Squad/SquadInitializer.cs:65-119` (the five
  `SystemPrompt` strings).
  Two skipped tests pin the gap:
  `Coder_SystemPromptShouldContainStayInCharacterGuidance_OutOfCharacterAdversarial`,
  `Security_SystemPromptShouldContainStayInCharacterGuidance_OutOfCharacterAdversarial`.

- **`e31-routing-substring-coder-overshadow`** [bug, b-plot]
  Surfaced by S02E31 *The Audition*. `SquadCoordinator.Route`
  matches keywords as raw substrings and breaks ties by rule
  declaration order. The natural phrasing "review the security of
  this code" hits a three-way tie at score 1 (coder via "code",
  reviewer via "review", security via "security") and routes to
  `coder` because coder's rule is declared first. Substring (not
  word-boundary) matching turns common bigrams into accidental
  overshadows for the first-declared rule.
  File: `azureopenai-cli/Squad/SquadCoordinator.cs:25-43`.
  Pinned by passing test
  `Route_ReviewTheSecurityOfThisCode_RoutesToCoder_DocumentedSurprise`.

- **`e31-write-not-in-writer-keywords`** [smell, one-line-fix]
  Surfaced by S02E31 *The Audition*. The verb "write" is the most
  natural English trigger for the writer persona but is absent from
  writer's routing pattern (`document,readme,docs,guide,tutorial,
  changelog`). Asking `--persona auto "write a security audit"`
  silently routes away from writer to reviewer.
  File: `azureopenai-cli/Squad/SquadInitializer.cs:144-148` (the
  writer routing rule pattern).
  Pinned by passing test
  `Route_WriteASecurityAudit_RoutesToReviewer_DocumentedSurprise`.

- **`e31-auto-routing-silent-fallback`** [smell, b-plot]
  Surfaced by S02E31 *The Audition*. `--persona auto` with a
  prompt that matches no routing keyword silently falls back to
  the first persona (`coder`) and surfaces no diagnostic. Compare
  the explicit-name path, which errors loudly with "Unknown
  persona '<x>'. Available: ..." (`Program.cs:716-719`). Auto
  should at least log "no routing rule matched; defaulting to
  <name>" on stderr.
  File: `azureopenai-cli/Squad/SquadCoordinator.cs:46`.
  Pinned by passing test
  `Route_AutoWithGibberishPrompt_FallsBackToFirstPersona_DocumentedSurprise`.

- **`e31-persona-name-no-kebab-snake-normalization`** [gap, b-plot]
  Surfaced by S02E31 *The Audition*. `SquadConfig.GetPersona` does
  case-insensitive `Equals` only -- no separator normalization.
  Multi-word persona names (forthcoming with the S02E30 cast --
  e.g. `mr-pitt`, `mr_pitt`, `MrPitt`) will only match one
  spelling. Pre-emptive logging so the gap is on the books before
  the cast lands.
  File: `azureopenai-cli/Squad/SquadConfig.cs:42-43`.
  Skipped test:
  `GetPersona_KebabAndSnakeCaseVariantsOfMultiWordName_ShouldNormalize`.

- **`e31-persona-empty-system-prompt-not-validated`** [gap, b-plot]
  Surfaced by S02E31 *The Audition*. `SquadConfig.Load` does not
  validate that `PersonaConfig.SystemPrompt` is non-empty. A
  persona with an empty prompt loads cleanly and degenerates to
  default-model behavior (no role anchoring) -- silent failure
  mode. Validation should happen at load time with a clear error.
  File: `azureopenai-cli/Squad/SquadConfig.cs:28-37` (load) +
  `:72-91` (PersonaConfig).
  Pinned by passing test
  `GetPersona_WithEmptySystemPrompt_StillReturnsPersona_NoValidation`.

- **`e31-persona-tool-availability-contradiction`** [smell, b-plot]
  Surfaced by S02E31 *The Audition*. The five generic personas
  declare tool lists (e.g. `architect` -> `["file","web",
  "datetime"]`) but their system prompts never mention whether
  tools are available. In standard mode (no `--agent`) Program.cs
  does not expose tools to the model, so the persona prompt and
  the runtime contradict each other silently. Either the prompt
  should mention tool availability (and have it injected at request
  time matching the actual mode) or the persona's declared tools
  should auto-enable agent mode.
  File: `azureopenai-cli/Squad/SquadInitializer.cs:69, 81, 93, 106,
  120` (Tools lists) vs the corresponding SystemPrompt strings.
  Pinned by passing test
  `Architect_DeclaresTools_SystemPromptDoesNotMentionToolAvailability_DocumentedContradiction`.

- **`e31-persona-ralph-composition-untested`** [gap, b-plot]
  Surfaced by S02E31 *The Audition*. The composition of a
  persona's `SystemPrompt` with the ralph-mode appendix happens
  inline at `azureopenai-cli/Program.cs:1717-1721` inside the
  Ralph loop. There is no pure-function seam to unit-test the
  composition without a network call. Cross-references S02E18
  finding `e18-ralph-mode-temperature-inheritance`: when a persona
  is active in Ralph mode the ralph-default temperature path is
  also untested. Refactor: extract the composition into a static
  helper (`RalphSystemPromptBuilder.Compose(personaPrompt, safety,
  history)`) so it can be unit-tested.
  File: `azureopenai-cli/Program.cs:1717-1721`.
  Skipped test:
  `PersonaCoder_PlusRalphMode_PreservesPersonaPrompt_AppendsRalphAppendix`.

- **`e31-persona-agent-tool-override-untested`** [gap, b-plot]
  Surfaced by S02E31 *The Audition*. `Program.cs:735-738` mutates
  `opts.EnabledTools` when a persona declares a tool list, with
  no diagnostic. Reviewer declares `["file","shell"]`; a user
  passing `--tools=web --persona reviewer` silently loses `web`
  access. Behavior may be intentional (persona is the source of
  truth) but it is undocumented and untested. Refactor: extract
  tool-override into a pure helper, surface a stderr line when
  the override drops user-requested tools.
  File: `azureopenai-cli/Program.cs:735-738`.
  Skipped test:
  `PersonaReviewer_PlusAgentMode_OverridesEnabledToolsToReviewerTools`.
