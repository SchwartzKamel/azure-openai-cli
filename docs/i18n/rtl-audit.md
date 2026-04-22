# RTL output audit — honest state

*Sub-document of [docs/i18n.md](../i18n.md) §4. Maintained by Babu Bhatt.
Closes audit findings L-01 and M-04 for the RTL slice.*

This file answers one question: **what does `az-ai-v2` actually do with
right-to-left text?** Not what we wish it did. Not what a future
`--locale` flag might do. What it does today.

---

## 1. What the CLI does

The CLI is a **byte-transparent** pipe for RTL content. Specifically:

| Stage                 | Behaviour                                                                |
|-----------------------|--------------------------------------------------------------------------|
| stdin (prompt arg)    | Read as UTF-8. Bytes passed to the model unchanged.                      |
| HTTP request body     | JSON-encoded; Arabic/Hebrew/Persian survives `System.Text.Json` round-trip. |
| HTTP response parse   | UTF-8; model output preserved byte-for-byte.                             |
| stdout (`--raw`)      | Model text written to the stream with **no BiDi control injection**.     |
| stdout (default)      | Same as `--raw` for body text; only our own status prefixes are ASCII.   |
| `--json`              | Model text is a JSON string value; escaping follows RFC 8259.            |
| Log / telemetry lines | Our own prefixes (`[INFO]`, `[ERROR]`, etc.) are ASCII; see §3.          |

We do not:

- add `U+202A..U+202E` (BiDi isolates / overrides) to model output
- strip them if the model emits them (see §4 for the threat-model note)
- call any `--dir` / `rtl` / `bidi` terminal-control sequence
- emit mirrored ASCII (`(` ↔ `)`) — Unicode punctuation rendering is the
  terminal's job

In short: if a pipeline processes the bytes correctly on `en_US.UTF-8`,
it processes them correctly on `ar_EG.UTF-8` or `he_IL.UTF-8`. Bytes are
bytes.

---

## 2. What terminals do (not us)

The first place RTL becomes visually RTL is the terminal emulator.
Coverage (observed on the maintainer matrix):

| Terminal                            | BiDi rendering | Notes                                  |
|-------------------------------------|----------------|----------------------------------------|
| Windows Terminal (≥ 1.18)           | correct        | Recommended on Windows.                |
| iTerm2 (macOS)                      | correct        | BiDi enabled by default.               |
| GNOME Terminal / Konsole / Kitty    | correct        | Modern libvte / custom renderers.      |
| Alacritty                           | partial        | Glyphs correct, logical order — some mixed LTR/RTL lines read out of visual order. Acceptable. |
| WezTerm                             | correct        |                                        |
| Windows Console Host (`conhost.exe`)| broken         | Legacy. Use Windows Terminal instead.  |
| tmux / screen (pass-through)        | as-above       | Multiplexer inherits the host terminal's BiDi behaviour. |

This is **not a commitment matrix**. It is field notes. If your terminal
renders RTL badly, please don't file it as a CLI bug — the bytes we emit
are correct; upgrade the terminal or open a terminal-upstream issue.

---

## 3. ASCII-safety of our own prefixes

Status and diagnostic lines that originate in the CLI (not in model
output) are **ASCII-only** by convention:

- `[INFO] ...`, `[WARN] ...`, `[ERROR] ...`, `[DEBUG] ...` — ASCII
- `az-ai-v2:` banner prefixes — ASCII
- Help text from `--help` — ASCII (see [docs/i18n.md §9](../i18n.md))

Benefits: these lines are BiDi-neutral (they render left-to-right on
any terminal, including those with partial RTL support); they are safe
to `grep`, `awk`, or `sed` with POSIX locale; they do not mix directions
with RTL model output on the same line.

When mixed content does land on one line (e.g. `[INFO] fetched 3 tokens
for prompt: مرحبا`), the Unicode Bidirectional Algorithm handles the
visual transition; we do not inject a directional isolate around the
Arabic fragment. If a downstream consumer needs deterministic visual
order, use `--json` and read the `prompt` field structurally.

---

## 4. Threat-model note: BiDi-override attacks

RFC-9839-shaped attacks exist where model output (or an adversarial
prompt) contains `U+202E` (RIGHT-TO-LEFT OVERRIDE) to make source code
or paths appear to say one thing while meaning another — the "Trojan
Source" family.

Our posture:

- We **do not strip** BiDi overrides from model output. The bytes are
  preserved because truncation and stripping are themselves a security
  footgun (see FDR's evil-input catalog).
- We **do classify** them as untrusted content in `docs/v2-dogfood-plan.md:132`.
- Consumers writing the output to files they later parse as code should
  run their own Trojan-Source linter (e.g. `rg '[\u202A-\u202E\u2066-\u2069]'`).
- Terminals that render BiDi correctly will still *render* the override;
  that is a visual deception, not a CLI-injected one.

If a future release decides to sanitise BiDi controls, it will be behind
a `--strip-bidi-controls` flag (reserved; not implemented).

---

## 5. What would break the byte-transparent contract

Regression we explicitly watch for:

1. A `string.Normalize()` call added somewhere in the response path
   (NFC/NFD reshuffle — would move combining marks across grapheme
   boundaries).
2. A `TextWriter` created without explicit UTF-8 encoding (on Windows,
   this defaults to the console code page — mojibake for non-ASCII).
3. A `string.Trim()` over a response that happened to start or end
   with `U+200F` (RIGHT-TO-LEFT MARK) — would change rendered meaning.
4. Adding `CultureInfo.CurrentCulture` to comparison / normalisation
   logic (see [docs/i18n.md §1](../i18n.md) — invariant is the contract).

Any of these in a PR is a Babu-block.

---

## 6. Recommended reviewer checks

For any PR that touches prompt plumbing, response rendering, or I/O:

- [ ] `git diff | rg 'Normalize\('` — did we add a normalisation pass?
- [ ] `git diff | rg 'OutputEncoding|StreamWriter|TextWriter'` — is UTF-8 explicit?
- [ ] `git diff | rg 'Trim\(|TrimStart\(|TrimEnd\('` — are we trimming raw model output?
- [ ] Does the test corpus ([docs/i18n/test-corpus.md](test-corpus.md))
      still pass byte-identity round-trip?

If all four are green, the RTL contract is intact.
