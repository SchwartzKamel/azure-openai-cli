# S04E27 -- *The Strict Door*

> *The default was "everything", and the person who knew better had to say so, tool by tool.*

**Commit:** `(this commit)` -- the third of three stacked episodes (E25 to E27)
**Branch:** `fix/s04e25-27-the-gaps` (PR, off `main`)
**Runtime:** one session, seventh of the gap-fixing batch
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Newman (security: what a model is handed by default)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Newman (LEAD), Kramer (the filter and the server), Puddy (the malformed-input cases), Costanza (guest: why strict is a flag and not the default)

## The pitch

S04E22 gave `--mcp-allow` and `--mcp-deny`, and left the default alone: with neither flag the model is shown **every** tool a server
lists. `newman-2026-11-N-12` asked for the safe default (hide tools that do not declare `readOnlyHint`) plus a mode where a mistyped
pattern is an error. Two facts stood in the way. The client did not read MCP annotations at all, so `readOnlyHint` was invisible to it.
And hopper's *own* server declared none, so even hopper talking to hopper had nothing to go on.

## Scene-by-scene

### Act I -- A lever, not a flip

Hiding every tool without a hint, by default, would break every `--mcp-server` use that works today, and the ops toolkit declares no
hints at all, so it would hide everything useful. The lesson of E22 stands: ship the lever first. `--mcp-strict` is opt-in. The default
flip is a major-version decision and is recorded as `newman-2026-11-N-14`.

### Act II -- What strict means, in one sentence

With **no** `--mcp-allow`, strict exposes only tools the server itself declares `readOnlyHint: true` (and not also
`destructiveHint: true`). Four rules keep it predictable:

- **An allow list wins over hints.** If you named the tools, you named them; the hints are not consulted. A tool with no hint is
  exposed if you allow it, which is how the toolkit is used (`--mcp-allow janitor_report ...`).
- **Deny still wins**, over everything.
- **A tool that claims both read-only and destructive is hidden.** The safe reading of a contradiction.
- **A pattern that matches nothing is fatal** (exit 2, before any model call). In E22 it was a warning, because a deny list shared
  across servers may match nothing on one. Strict is the mode for people who want a typo to stop the run, because a mistyped deny
  otherwise leaves the tool exposed.

With strict and nothing to expose, the run says so and says why, instead of quietly handing the model no tools:

```text
[mcp] 'toolkit': strict mode exposes only tools that declare readOnlyHint: true, and this server declares none; name the tools you trust with --mcp-allow
```

### Act III -- Hints are untrusted input

The MCP spec says a client must treat annotations as untrusted unless the server is. Two consequences became tests.

First, a hint is the **server's own claim**: a server that lies about `readOnlyHint` defeats any policy built on it. Strict is for
servers you already trust to describe themselves; it is not a sandbox. That is written into the type's documentation and the help text.

Second, and caught during design rather than by a test: the first sketch gave `McpToolInfo` a typed `bool?` property. A sloppy server
that sends `"readOnlyHint": "true"` (a string) would then make deserialization **throw**, which would break *every* tool from a server that
worked fine before annotations were read, including for users who never asked for strict. Annotations are now read by a small
hand-written converter: only a JSON `true` or `false` counts, a string, number, null, array or object is "no hint", unknown fields and
an `annotations` value that is not an object are ignored, and "no hint" means not read-only. Seven mutation cases pin it.

### Act IV -- Make hopper a good citizen

Hopper's own server now declares `readOnlyHint: true` for its five read-only built-ins (`read_file`, `web_fetch`, `get_clipboard`,
`get_datetime`, `queue_status`), and nothing else (`queue_add` writes the queue; `delegate_task` runs a child agent). The list is the
same one `--read-only` exposes, now a single shared constant, and a test pins it from the other side: in `--read-only` mode every tool
the server offers must be declared read-only, so the two can never drift apart. A plugin that happens to share a built-in's name is not
assumed to be read-only. `hopper mcp tools` marks declared tools `[read-only]` and `--json` carries the annotation, so it is easy to
see what strict would expose.

### Act V -- Proof

Against hopper's own server (7 tools), by what reaches the model:

| Flags | Tools in the request |
|---|---|
| `--mcp-strict` | `get_clipboard get_datetime queue_status read_file web_fetch` |
| `--mcp-strict --mcp-allow queue_add` | `queue_add` |
| `--mcp-strict --mcp-deny web_fech` (typo) | none: exit 2, **no request sent** |

Against the live ops toolkit and a real model: `--mcp-strict --mcp-allow` the four reports gives the same, source-verified answer as
before (janitor degraded with exit 4 and an idle streak of 20; updater healthy); `--mcp-strict` alone gives the model no tools, tells
you why, and the model says it cannot answer.

## What shipped

- `McpToolAnnotations` and its lenient converter; `readOnlyHint` declared by the server; `McpToolFilter` strict mode; the client reading
  hints; `--mcp-strict` with help text and bash, zsh and fish completions; a `[read-only]` marker and `--json` annotations in
  `hopper mcp tools`.
- `McpStrictTests.cs`: 28 unit cases (the policy, the lenient parser, the server's declarations, real client against real server over
  pipes). Six integration cases, three of them reading the request body a fake model receives.
- **Mutation-checked:** 17 mutations, each caught, each edit verified applied: 11 at unit level (policy, parser, server, client) and 6
  in the wiring (preview, agent run, fatal typo, the server's list, the exit code, the marker).
- README, the field guide, CHANGELOG, the findings backlog and this report.

**Not shipped**

- The default flip, and any way to make strict the default for headless runs (an environment variable or config key). Both are
  `N-14`.
- Trusting hints: a lying server defeats strict. A per-server trust setting would be the next step.

## Lessons from this episode

1. **Ship the lever before the default.** Two episodes in a row, for the same reason.
2. **Untrusted input must be parsed leniently.** A typed property would have broken servers that work today.
3. **Say what the safe mode is not.** Hints are claims, so strict is a convenience for trusted servers, not a sandbox.
4. **Make the exposing side tell the truth.** Hopper's server now declares what it knows, and a test keeps its two lists together.
5. **When there is nothing to expose, say why.** Silence looks like a working empty set.
6. **A typo in a safety flag should stop the run.** Strict turns the fail-open case into a stop.

## Metrics

- **Test delta:** +28 unit, +6 integration. The combined tree with S04E24 (smith's *The Copilot*) and S04E25 and S04E26 is unit 2,411 and integration 218.
- **Mutations:** 17 of 17 caught.
- **Live:** strict with an allow list returns the verified answer; strict alone exposes 0 of 9 and says why.
- **Preflight:** format, 0 warnings, unit, integration, `ascii-check`, `markdownlint`, `exec-report-check`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Newman led because the subject is what a model is handed by default. Kramer wrote the filter and the server's declarations, Puddy's
lens is the malformed hint, and Costanza's is the default that must not move. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
