# S02E23 -- Findings backlog (sub-agent staging)

> Staging file for findings surfaced by S02E23 *The Adversary*. The
> orchestrator integrates these into `s02-writers-room.md` ->
> "Findings backlog" subsection on the next writers' room update.
>
> Format follows [`findings-backlog`](../../.github/skills/findings-backlog.md)
> verbatim. Newman (guest) scored severity. FDR (lead) authored
> the diagnosis lines.

## Findings (21)

### ShellExec bypasses (8)

- **`e23-shell-ifs-tokenization`** [bug, queued-as-episode: TBD (Newman lead, candidate S02E26)]
  Surfaced by S02E23 *The Adversary*. The first-token blocklist
  check uses `command.TrimStart().Split(' ', 2)[0]`, but `${IFS}`
  expands to whitespace at shell time, not at gate time. Input
  `rm${IFS}-rf /tmp/x` reads as a single non-matching token at
  the gate and as `rm -rf /tmp/x` at `/bin/sh`. Routes ANY blocked
  command past the gate. **Highest priority -- CVE-shape.**
  File: `azureopenai-cli/Tools/ShellExecTool.cs:76`.
  Pinned by skipped test
  `Bypass_IfsExpansionTokenization_ShouldBeRejected`.

- **`e23-shell-tab-separator`** [bug, queued-as-episode: TBD]
  Surfaced by S02E23. Tokenizer splits on a single ASCII space
  (`' '`) only. `/bin/sh` treats tab as IFS. Input `rm\t-rf /tmp/x`
  evades the gate. CVE-shape. Same fix as the IFS finding.
  File: `azureopenai-cli/Tools/ShellExecTool.cs:76, 83`.
  Pinned by skipped test
  `Bypass_TabAsCommandSeparator_ShouldBeRejected`.

- **`e23-shell-newline-segment`** [bug, queued-as-episode: TBD]
  Surfaced by S02E23. The pipe-segment splitter uses `'|'`,
  `';'`, `'&'` but NOT `'\n'`. `/bin/sh` interprets newline as
  a statement terminator, so a newline-injected blocked command
  after a benign one executes; the blocklist never sees it as
  its own segment. CVE-shape.
  File: `azureopenai-cli/Tools/ShellExecTool.cs:81`.
  Pinned by skipped test
  `Bypass_NewlineCommandSeparator_ShouldBeRejected`.

- **`e23-shell-eval-after-andand`** [bug, queued-as-episode: TBD]
  Surfaced by S02E23. `command.Contains("; eval ")` is the
  only post-separator eval check. `&& eval`, `|| eval`, and
  newline-separated eval all evade. CVE-shape.
  File: `azureopenai-cli/Tools/ShellExecTool.cs:71-72`.
  Pinned by skipped test
  `Bypass_EvalAfter_AndAnd_ShouldBeRejected`.

- **`e23-shell-quoted-command-name`** [bug, queued-as-episode: TBD]
  Surfaced by S02E23. Tokenizer takes the literal first token
  including quote characters. The HashSet contains `"rm"`, not
  `"\"rm\""`. `/bin/sh` strips the quotes before resolving the
  command name. Input `"rm" /tmp/x` evades. CVE-shape.
  File: `azureopenai-cli/Tools/ShellExecTool.cs:76`.
  Pinned by skipped test
  `Bypass_QuotedCommandName_ShouldBeRejected`.

- **`e23-shell-backslash-command-name`** [bug, queued-as-episode: TBD]
  Surfaced by S02E23. `/bin/sh` allows a leading backslash as a
  no-op character escape: `\rm` resolves to `rm`. Tokenizer sees
  `\rm` and finds no HashSet match. CVE-shape.
  File: `azureopenai-cli/Tools/ShellExecTool.cs:76`.
  Pinned by skipped test
  `Bypass_BackslashEscapedCommandName_ShouldBeRejected`.

- **`e23-shell-env-var-indirection`** [bug, queued-as-episode: TBD]
  Surfaced by S02E23. `${RM:-rm}` expands to `rm` at shell time
  but reads as a non-matching token at gate time. The
  `SensitiveEnvVars` scrub list covers API keys only, not arbitrary
  variables an attacker could exploit for indirection. CVE-shape
  (slightly mitigated by needing the variable set, but
  default-shell substitution `${VAR:-default}` works without it).
  File: `azureopenai-cli/Tools/ShellExecTool.cs:76, 31-41`.
  Pinned by skipped test
  `Bypass_EnvVarCommandIndirection_ShouldBeRejected`.

- **`e23-shell-fullwidth-unicode-lookalike`** [smell, one-line-fix]
  Surfaced by S02E23. Fullwidth `\uFF52\uFF4D` ("rm" lookalike)
  passes the blocklist (HashSet uses OrdinalIgnoreCase, not
  Unicode-fold). `/bin/sh` resolves the literal codepoints and
  fails to find the command, so this is harmless TODAY.
  By-design-but-fragile: if a future change normalizes Unicode
  at execution time without doing the same at gate time, the
  attack surface grows. Worth a no-op-style note in the blocklist
  header.
  File: `azureopenai-cli/Tools/ShellExecTool.cs:14-20`.
  Pinned by skipped test
  `Bypass_FullwidthUnicodeLookalike_ShouldBeRejected`.

### ReadFile sensitive-path gaps (7)

> Cross-references the existing
> `e13-readfile-blocklist-home-dir-gap` finding (S02E13) and
> queued-as-episode S02E26 *The Locked Drawer*. Pinned per-path
> here so that episode can flip Skip -> green one path at a time
> with mechanical proof of coverage.

- **`e23-readfile-ssh-userdir-not-blocked`** [bug, queued-as-episode: S02E26]
  Surfaced by S02E23. `BlockedPathPrefixes` covers `/root/.ssh`
  but NOT `~/.ssh` (the invoking user's SSH dir). Most
  attacker-relevant SSH keys live under the invoking user's home.
  **CVE-shape -- highest impact among the home-dir gaps.**
  File: `azureopenai-cli/Tools/ReadFileTool.cs:14-30`.
  Pinned by skipped test
  `IsBlockedPath_UserSshDir_ShouldBeBlocked`.

- **`e23-readfile-kube-config-not-blocked`** [bug, queued-as-episode: S02E26]
  Surfaced by S02E23. `~/.kube/config` carries cluster
  credentials and OIDC tokens. Not in the blocklist. CVE-shape.
  File: `azureopenai-cli/Tools/ReadFileTool.cs:14-30`.
  Pinned by skipped test
  `IsBlockedPath_KubeConfig_ShouldBeBlocked`.

- **`e23-readfile-gnupg-not-blocked`** [bug, queued-as-episode: S02E26]
  Surfaced by S02E23. `~/.gnupg/private-keys-v1.d` holds GPG
  private key material. Not in the blocklist. CVE-shape.
  File: `azureopenai-cli/Tools/ReadFileTool.cs:14-30`.
  Pinned by skipped test
  `IsBlockedPath_GnuPGDir_ShouldBeBlocked`.

- **`e23-readfile-netrc-not-blocked`** [bug, queued-as-episode: S02E26]
  Surfaced by S02E23. `~/.netrc` holds host/login/password
  triples for FTP, SMTP, Heroku, GitHub HTTP. Not in the
  blocklist. CVE-shape.
  File: `azureopenai-cli/Tools/ReadFileTool.cs:14-30`.
  Pinned by skipped test
  `IsBlockedPath_Netrc_ShouldBeBlocked`.

- **`e23-readfile-docker-config-not-blocked`** [bug, queued-as-episode: S02E26]
  Surfaced by S02E23. `~/.docker/config.json` holds registry
  auth tokens (DockerHub, ECR, GCR, ACR). Not in the blocklist.
  CVE-shape.
  File: `azureopenai-cli/Tools/ReadFileTool.cs:14-30`.
  Pinned by skipped test
  `IsBlockedPath_DockerConfig_ShouldBeBlocked`.

- **`e23-readfile-git-credentials-not-blocked`** [bug, queued-as-episode: S02E26]
  Surfaced by S02E23. `~/.git-credentials` is the credential-helper
  store for git's HTTPS auth. Not in the blocklist. CVE-shape.
  File: `azureopenai-cli/Tools/ReadFileTool.cs:14-30`.
  Pinned by skipped test
  `IsBlockedPath_GitCredentials_ShouldBeBlocked`.

- **`e23-readfile-npmrc-pypirc-not-blocked`** [bug, queued-as-episode: S02E26]
  Surfaced by S02E23. `~/.npmrc` and `~/.pypirc` carry registry
  auth tokens for npm/yarn and PyPI. Not in the blocklist.
  CVE-shape (lower volume than ssh/git but easy add).
  File: `azureopenai-cli/Tools/ReadFileTool.cs:14-30`.
  Pinned by skipped test
  `IsBlockedPath_PackageRegistryAuth_ShouldBeBlocked`.

### WebFetch SSRF gaps (4)

- **`e23-webfetch-dns-rebinding-toctou`** [bug, b-plot]
  Surfaced by S02E23. The pre-flight
  `Dns.GetHostAddressesAsync` resolves the hostname before
  `HttpClient` does its own resolution. A hostile DNS server
  can answer the first lookup with a public IP and the second
  lookup (microseconds later) with `169.254.169.254` /
  `127.0.0.1` / RFC1918. The post-redirect re-validation only
  fires after a redirect, not on the initial request.
  Hardening-gap with a real attack path on hostile-DNS
  environments. Structural fix (resolve once, connect by IP
  with Host header preserved). **Top-3 fix priority per
  Newman.**
  File: `azureopenai-cli/Tools/WebFetchTool.cs:50-68`.
  Pinned by skipped test
  `DnsRebinding_TimeOfCheckTimeOfUse_ShouldBeMitigated`.

- **`e23-webfetch-multicast-broadcast-not-blocked`** [bug, b-plot]
  Surfaced by S02E23. `IsPrivateAddress` covers loopback,
  RFC1918, link-local, IPv6 ULA / link-local, IPv4-mapped IPv6.
  Multicast (`224.0.0.0/4`), SSDP (`239.255.255.250`), and
  broadcast (`255.255.255.255`) are not RFC1918 but are also not
  safe SSRF targets -- can hit on-LAN services (mDNS, SSDP,
  WS-Discovery) and reveal network topology. Hardening-gap.
  File: `azureopenai-cli/Tools/WebFetchTool.cs:144-185`.
  Pinned by skipped test
  `IsPrivateAddress_MulticastAndBroadcast_ShouldBeBlocked`.

- **`e23-webfetch-cgnat-100_64-not-blocked`** [bug, b-plot]
  Surfaced by S02E23. RFC6598 carrier-grade NAT space
  (`100.64.0.0/10`) is not RFC1918 but is also not routable on
  the public internet. SSRF target on customer networks behind
  CGNAT (most mobile carriers, many ISPs). Hardening-gap.
  File: `azureopenai-cli/Tools/WebFetchTool.cs:144-185`.
  Pinned by skipped test
  `IsPrivateAddress_Cgnat100_64_ShouldBeBlocked`.

- **`e23-webfetch-decimal-ip-encoding-untested`** [gap, one-line-fix]
  Surfaced by S02E23. `https://2130706433/` (= 127.0.0.1 as
  decimal) is parsed by `Uri.TryCreate`. Behavior under
  `Dns.GetHostAddressesAsync` depends on the resolver stack
  (glibc commonly resolves to 127.0.0.1; alternate stdlibs may
  short-circuit). No test pins the behavior either way.
  Untested-either-way; one passing or one Skipped test would
  close the gap.
  File: `azureopenai-cli/Tools/WebFetchTool.cs:50-68`.
  Pinned by skipped test
  `DecimalIpEncoding_LoopbackBlocked`.

### Stream / dispatch chaos (2)

- **`e23-tool-non-string-param-throws`** [smell, b-plot]
  Surfaced by S02E23. Each tool's `ExecuteAsync` calls
  `prop.GetString()` after `TryGetProperty` without checking
  `ValueKind`. Input `{"command": 123}` (number, not string)
  throws `InvalidOperationException`. Caught by the
  `ToolRegistry.ExecuteAsync` envelope today, so the agent loop
  does not crash -- but the tool itself violates its
  graceful-degradation contract. Hardening-gap (defense in
  depth: each layer should hold alone).
  Files: `azureopenai-cli/Tools/ShellExecTool.cs:61`,
  `azureopenai-cli/Tools/ReadFileTool.cs:50`,
  `azureopenai-cli/Tools/WebFetchTool.cs:43`.
  Pinned by skipped test
  `ShellExec_NonStringCommandParam_ReturnsError_NoThrow`.

- **`e23-delegate-negative-depth-bypass`** [bug, b-plot]
  Surfaced by S02E23. `RALPH_DEPTH=-1` parses as a valid int;
  `-1 < MaxDepth (3)` so delegation proceeds. A hostile parent
  setting `RALPH_DEPTH=-99` effectively raises the cap by 99
  (3 - (-99) = 102 levels of recursion). Defense should clamp
  parsed depth to `>= 0` before comparing to `MaxDepth`. Bug --
  one-line fix (`if (currentDepth < 0) currentDepth = 0;`).
  File: `azureopenai-cli/Tools/DelegateTaskTool.cs:42-47`.
  Pinned by skipped test
  `DelegateTask_NegativeDepth_ShouldBeRejected`.
