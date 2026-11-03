# S04E22 -- *The Allowlist*

> *A client that shows the model every tool a server lists has decided, on the user's behalf, that the model may do anything the server can.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `fix/s04e22-the-allowlist` (PR, off `main`)
**Runtime:** one session, third of the gap-fixing batch
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Newman (security: what a model may be handed)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Newman (LEAD), Kramer (the filter), Puddy (the cases where a typo must be loud), Costanza (guest: why the default did not change)

## The pitch

S04SP11 put hopper in front of the `agent-toolkit` ops server and it answered correctly. It also listed `janitor_run`
(cleans disk) and `updater_run` (upgrades packages) next to the reports, with no `readOnlyHint`, and hopper's MCP client
wrapped **every** tool and handed all nine to the model. `--read-only` governs hopper's own *server*; nothing governed the
client. The stopgap was a proxy script. `newman-2026-11-N-11` asked for the real thing.

## Scene-by-scene

### Act I -- Where the line is

The model can only call a tool it was told about: the agent dispatches to the functions registered with it, and a name it
invents resolves to nothing. So hiding a tool from the list is enforcement, not a request. The place to do it is the one
where the client turns the server's list into the model's list.

### Act II -- A glob, not a regex

Patterns are globs over the tool name (`*`, `?`), matched case-sensitively by a small iterative matcher. A regex was rejected:
a user-supplied pattern is exactly the input that causes catastrophic backtracking, and the matcher is linear. A test throws
the classic `*a*a*a*a*a*a*a*a*b` at 20,000 characters and requires it to finish.

Semantics, chosen to be hard to get wrong:

- **Deny wins**, even over an allow that matches the same tool.
- With any `--mcp-allow`, **only** matches are kept; an allow list that matches nothing exposes **nothing**, not everything.
- With neither flag, everything is exposed (see Act IV).
- Blank patterns are ignored. A blank allow must not become an active filter that allows nothing, and a blank deny must not
  hide everything.

### Act III -- The typo that fails open

A mistyped allow pattern fails safe: the tool you wanted is missing, and the agent says so. A mistyped **deny** fails open:
`--mcp-deny janitor_rn` hides nothing, and the run looks identical. So every pattern is checked against the names the
servers actually offered, and one that matches nothing prints a warning that, for a deny pattern, says the tool you meant to
hide is still exposed. It is a warning rather than an error because a deny list reused across several servers will
legitimately match nothing on some. A run with a filter also says how many tools it hid
(`exposing 4 of 9 tool(s) to the model`).

`hopper mcp tools <server> --mcp-allow ...` applies the same filter, so the result can be previewed before a model is involved.

### Act IV -- Why the default did not change

`newman-2026-11-N-11` also suggested hiding any tool without a `readOnlyHint` unless allowed. That would break every
`--mcp-server` use that works today, and the toolkit declares no hints, so it would hide everything useful. It is the right
*eventual* default and the wrong thing to spring on a user in a minor release. It stays open as `N-12`.

### Act V -- Proof where it counts

The check that matters is not "the flag parsed" but "the tool is not in the request the model receives". The end-to-end tests
point hopper at a fake model server that records each request, run it against hopper's own MCP server (seven tools), and read
the `tools` array of the request:

| Flags | Tools in the request |
|---|---|
| none | all 7 |
| `--mcp-allow 'get_*' --mcp-allow read_file` | `get_clipboard get_datetime read_file` |
| `--mcp-deny 'queue_*' --mcp-deny web_fetch --mcp-deny delegate_task` | `get_clipboard get_datetime read_file` |
| `--mcp-deny web_fech` (typo) | all 7, plus the warning |

Against the live model, asked to list its own tools, it named exactly the filtered set. And Recipe 3 was re-run with the
native flags instead of the proxy, 4 of 9 tools, 6 s, with every fact in the answer checked again against the tools' own output.

### Act VI -- A bug in my own test harness

The first run of the new integration block stopped after five cases, with no error. The SDK pretty-prints a request that
carries tools (159 lines), the parser read the log line by line, `json.loads` threw, and `set -e` quietly ended the suite. Two
fixes: the fake server logs each body as one compact line, and the parse step can report an unparseable log instead of killing
the run.

## What shipped

- `Mcp/McpToolFilter.cs`; `McpStdioClient.CreateMafToolsAsync(filter)`; the flags, the connection loop, the preview in
  `hopper mcp tools`, help text and completions for bash, zsh and fish.
- `McpToolFilterTests.cs`: 31 unit cases (the glob table, the policy, blank and duplicate patterns, the typo report, the flags,
  and the real client against the real server over pipes).
- Nine integration cases (five on `hopper mcp tools`, four on what reaches the model).
- **Mutation-checked:** twelve mutations, eight at unit level and four in the agent path, each caught, each edit verified applied.
  The "never warns on a typo" mutant in the agent path is caught only by the case added for it.
- README, the field guide (Recipe 3 now uses the native flags), CHANGELOG, the findings backlog and this report.

**Not shipped**

- A secure default (`N-12`): hide tools that do not declare `readOnlyHint` unless allowed, and a strict mode that turns an
  unmatched pattern into an error.
- `hopper mcp call` is deliberately unfiltered: it is an explicit, named action by the user, not a model's choice.

## Lessons from this episode

1. **Enforce where the list is made.** A tool the model is never told about cannot be called.
2. **Fail-open typos need to be loud.** A mistyped deny hides nothing and looks exactly like success.
3. **Do not change a default in the same release as the control.** Ship the lever, then argue about the default.
4. **Test what reaches the model, not what was parsed.** The request body is the real contract.
5. **A harness that dies silently is a bug in the harness.** `set -e` plus an unguarded parse ended a suite without a word.
6. **Prefer a linear matcher for user-supplied patterns.**

## Metrics

- **Surface:** 2 new flags; 7 tools in the test subject; 4 of 9 tools exposed in the live recipe.
- **Live result:** Recipe 3 with native flags: 6 s (19 s through the proxy), all facts verified.
- **Test delta:** unit 2,258 to 2,289 (+31); integration 189 to 198 (+9).
- **Preflight:** format, 0 warnings, unit, integration, `ascii-check`, `markdownlint`, `exec-report-check`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Newman led because the subject is what a model may be handed. Kramer wrote the filter, Puddy's lens is the typo that must not
be quiet, and Costanza's is the default that must not move. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
