# S04E25 -- *The Wrong Door*

> *You asked it to open a URL. It tried to run a program called `https://...`.*

**Commit:** `(this commit)` -- one commit in a stacked PR of three episodes (E25 to E27)
**Branch:** `fix/s04e25-27-the-gaps` (PR, off `main`)
**Runtime:** one session, fifth of the gap-fixing batch
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Mickey Abbott (CLI ergonomics: the message is the interface)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Mickey (LEAD), Newman (guest: credentials that ride inside URLs), Lloyd Braun (guest: the obvious question a newcomer asks)

## The pitch

Hopper's MCP client launches a command and speaks to it over stdio; it has no HTTP transport. Many MCP servers are HTTP. Hand
`--mcp-server` a URL and it said:

```text
An error occurred trying to start process 'http://127.0.0.1:9/mcp' with working directory '...'. No such file or directory
```

True, and useless: it says nothing about why, or what to do instead. S04SP12 built the way out (`examples/mcp/http-bridge.py`) and
nothing in the product pointed at it. The obvious question from a newcomer is "why is it trying to *run* my URL?".

## Scene-by-scene

### Act I -- Check the raw text, not the parsed one

The first idea was to check the parsed command. That is wrong, for a reason found by reading `ParseServerSpec`: it splits a
`name=command` spec at the first `=`. A URL with a query string (`https://host/mcp?token=abc`) has an `=`, so it parses into a "name"
of `https://host/mcp?token` and a "command" of `abc`. The check therefore runs on the raw spec, before parsing.

### Act II -- Only the first word is the program

The bridge's own recipe puts a URL in its arguments: `python3 examples/mcp/http-bridge.py https://host/mcp`. That is the intended
use and must keep working. Only the first word (after an optional `name=`) counts; and only `http`, `https`, `ws` and `wss`. A
`file://` path, a Windows `C:\` path and a command with `--url=https://...` as an argument are all left alone.

### Act III -- Credentials ride in URLs

A URL can carry a secret: `https://user:pw@host/mcp`, `?token=...`, `?access_token=...`. An error message is exactly where it would
end up in a log. The message shows only scheme, host, port and path; userinfo, query and fragment never appear. The tests use
distinct marker strings (`SECRETPW`, `SECRETTOK`) and require them to be absent from the helper's output, the exception and the
real binary's output.

### Act IV -- The test found a bug

The matrix included an IPv6 literal. It failed: `Uri.Host` already carries the brackets for `[::1]`, so the helper printed
`[[::1]]`. A one-line fix, and the kind of thing a happy-path demo never shows.

## What shipped

- `McpStdioClient.DescribeUrlSpec` and a check at the top of `ConnectSubprocessAsync`, so `--mcp-server` and `hopper mcp tools|call`
  both get it. The error names the cause ("is a URL", "no HTTP transport"), gives a ready-to-paste bridge command with the URL filled
  in, and points at `MCP_BRIDGE_TOKEN_FILE` and the field guide.
- `McpUrlSpecTests.cs`: 25 unit cases and three integration cases.
- **Mutation-checked:** nine mutations (eight unit, one integration), each caught, each edit verified applied. They covered: no check,
  flagging a URL argument, echoing the raw spec, not stripping `name=`, dropping `https`, dropping `ws`/`wss`, keeping the query and keeping
  userinfo.
- CHANGELOG, the findings backlog and this report.

**Not shipped**

- A real HTTP transport. The bridge works, and a transport means TLS, auth and session handling that hopper does not have. This closes
  the *usability* gap and leaves the capability gap as a deliberate choice.
- A friendlier message for an ordinary mistyped command (`No such file or directory`). That is a separate, smaller thing.

## Lessons from this episode

1. **Check the raw input before a parser reshapes it.** The parser would have split a URL at its query.
2. **An error should name the cause and the exit.** "Could not start process" named neither.
3. **Do not flag the intended use.** The bridge takes a URL as an argument, and a test pins that.
4. **Assume a URL has a secret in it.** Print scheme, host, port and path only.
5. **Put the awkward case in the matrix.** IPv6 failed on the first run.
6. **A test that needs credentials must supply its own.** The agent-path case passed locally only because the machine had an env file
   (set up the same day), and failed in CI, where hopper stopped at "AZUREOPENAIENDPOINT not set" before reaching the URL. Run the suite
   with an empty `HOME` to see what CI sees.

## Metrics

- **Before and after:** one unhelpful message became one that names the cause, the fix and the docs.
- **Test delta:** +25 unit, +3 integration.
- **Preflight:** format, 0 warnings, unit, integration, `ascii-check`, `markdownlint`, `exec-report-check`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Mickey led because the message is the interface. Newman's lens is the secret in the URL and Lloyd's is the newcomer's question.
Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
