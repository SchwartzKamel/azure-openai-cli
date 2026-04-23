# Project Glossary

Plain-English definitions for the acronyms and shorthand the rest of the
docs assume you know. New to the project? Start here. Future episodes
append; this file is the single source of truth.

> Maintained by Lloyd Braun (the junior dev who actually asked) and
> seeded by Babu Bhatt during S02E08 "The Translation".

---

### AOT

Ahead-of-Time compilation. .NET 10 native AOT compiles the C# down to a
single self-contained native binary, no runtime install required. We use
it for fast cold start (~30 ms vs. ~250 ms for a JIT'd .NET app), which
matters because the CLI is invoked once per Espanso/AHK expansion.

### CJK

Chinese / Japanese / Korean. The shorthand for "wide" characters that
occupy two terminal columns instead of one. Anything that aligns text in
columns (help text, tables, the spinner clear-string) has to account for
this or layout breaks.

### DPAPI

Data Protection API. The Windows-only credential encryption service.
Bound to the current user account: data encrypted by user A on machine X
cannot be decrypted by user B or by user A on machine Y. We use it to
protect the API key on Windows. See `DpapiCredentialStore.cs`.

### i18n

Internationalization. The "18" is the count of letters between the `i`
and the `n` in "internationalization". It refers to the engineering
work of making software *ready* for translation: externalizing strings,
avoiding mid-sentence concatenation, using locale-aware number/date
formatting. i18n is preparation. (Lloyd: "But isn't translation
i18n?" -- no, that's l10n.)

### l10n

Localization. The "10" is the letter count between the `l` and the `n`
in "localization". This is the actual translation work plus
locale-specific rendering: `de-DE` translations of strings, comma vs.
dot as decimal separator, 24-hour vs. 12-hour time, currency symbols.
l10n is delivery; i18n is preparation. You can't do l10n competently if
the i18n hasn't been done first.

### libsecret

The Linux secret-storage daemon (more precisely: the FreeDesktop
Secret Service API, implemented by GNOME Keyring and bridged to
KWallet). We talk to it via the `secret-tool` CLI when present;
otherwise we fall back to a plaintext file with mode `0600`. See
`SecretToolCredentialStore.cs`.

### MCP

Model Context Protocol. Anthropic's specification for how language
models talk to external tools and context providers (filesystem, HTTP,
databases, etc.) over a uniform JSON-RPC channel. Not yet implemented
here; tracked as a future direction.

### RPM

Requests Per Minute. One of the two Azure OpenAI quota units (the other
is TPM). When you provision a model deployment, Azure assigns it an
RPM ceiling based on the SKU. Going over returns HTTP 429.

### RTL

Right-to-left. The text direction used by Arabic, Hebrew, Persian, and
Urdu. Affects two things in a CLI: (1) string concatenation
(`"prefix" + variable + "suffix"` reads garbled when the surrounding
language is RTL), and (2) terminal column ordering for any layout that
assumes columns flow left-to-right. The Unicode Bidi Algorithm handles
inline mixed-direction text in compliant terminals; layout assumptions
in *our* code are still our problem.

### SSRF

Server-Side Request Forgery. The class of attack where a user-supplied
URL tricks the server into fetching from an internal address it
shouldn't be able to reach (cloud metadata endpoints, internal
services, `localhost`). The `web_fetch` tool defends against this by
refusing private IP ranges and re-validating the final URL after
following redirects. Newman's territory.

### TPM

Tokens Per Minute. The other Azure OpenAI quota unit. Counts both
prompt and completion tokens. The constraining quota is whichever of
TPM or RPM you hit first. See `docs/cost/` for the per-model rates.

---

## Added in S02E15 "The Lawyer"

### MIT vs Apache 2.0

Both are permissive, attribution-only OSS licenses, and from a user's
perspective they let you do basically the same things: use the code,
ship it, modify it, sell it, all without paying anyone or releasing
your own source. Apache 2.0 adds two things MIT does not have: an
explicit patent grant (which terminates if you sue contributors over
patents in their contribution), and a `NOTICE` file convention --
downstream redistributors must preserve any upstream `NOTICE` text
alongside the license. MIT has neither, which is why MIT fits in one
paragraph and Apache 2.0 fits on four pages. Both are compatible with
this project; both require attribution in `THIRD_PARTY_NOTICES.md`.

### GPL contagion

Shorthand for the way GPL-family licenses (GPL, AGPL, LGPL with
caveats) require derivative works to be released under the same
license. Statically linking or otherwise baking GPL code into your
binary forces the binary as a whole to be offered under the GPL too,
which means publishing your source under terms that let downstream
users do the same. AGPL extends this trigger to network use, not just
distribution. For a permissive CLI like `azure-openai-cli`, allowing a
GPL-family package into the runtime closure would force a relicense or
a removal -- which is why `docs/legal/license-audit.md` tracks GPL
findings as release-blocking rather than as paperwork.

---

## Conventions for new entries

- One H3 per term, alphabetical order within each addition.
- Definition first sentence. Context second. Why-we-care third.
- Link to the relevant source file or doc when one exists.
- ASCII only. No smart quotes, em-dashes, or en-dashes (docs-lint).
- New episodes append; do not rewrite existing entries without a note
  in the relevant exec report.
