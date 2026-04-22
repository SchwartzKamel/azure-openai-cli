# Low-bandwidth and high-latency SSH

> *Every escape code is a byte. On a 300-baud link, every byte is an
> eternity.* -- M.A.

This appendix documents how `az-ai-v2` behaves on slow, flaky, or
bandwidth-constrained links -- the sysadmin jumping through three
bastion hosts, the satellite-internet consultant, the rural dev on a
copper DSL line, the disaster-recovery operator over a fallback LTE
modem. "Low bandwidth" is a live audience, not a relic.

Pair with [`tty-detection.md`](tty-detection.md) (which output class
gets emitted) and [`keyboard-workflows.md`](keyboard-workflows.md)
(how to drive it without a mouse).

---

## 1. Rule of thumb

**Use `--raw`.** On any SSH session slower than a LAN, `--raw` is the
right default. It strips:

- All ANSI color escapes (≈5 bytes each, one per color change).
- Cursor-hide / cursor-show codes (`ESC[?25l`, `ESC[?25h`).
- Spinner frames (`⠋⠙⠹⠸`… -- 3 UTF-8 bytes per frame, 10+ frames/s).
- The token-usage stderr footer (two extra round-trips of text).
- `[ERROR]` / `[INFO]` line prefixes when combined with `--json`.

On a 56 kbps link, a 3-second spinner at 10 Hz is ≈900 bytes of pure
noise. `--raw` drops it to zero and leaves the model content intact.

```sh
# The recommended invocation over any slow link
az-ai-v2 --raw "your prompt here"
```

---

## 2. Multiplex the SSH session

Repeated `ssh` handshakes are the second-biggest bandwidth tax after
ANSI chrome. Use OpenSSH's control-master to share one TCP + TLS
session across many `az-ai-v2` invocations:

```sh
# In ~/.ssh/config
Host jumpbox
    ControlMaster auto
    ControlPath ~/.ssh/cm-%r@%h:%p
    ControlPersist 10m
    Compression yes
    ServerAliveInterval 60
    ServerAliveCountMax 5
```

First `ssh jumpbox az-ai-v2 --raw "…"` pays the handshake; subsequent
calls reuse the socket. `Compression yes` is a net win on low-BW
links because model output is highly compressible text.

On a flaky link, `mosh` is often a better choice than raw `ssh` -- it
handles packet loss and roaming without dropping your session. `mosh`
strips some ANSI by default but **does not** strip spinner frames;
`--raw` is still recommended.

---

## 3. Stream or buffer?

Streaming (the default) sends tokens as they arrive from the model.
On a slow link, that can feel *better* -- you see progress -- but it
also means every token round-trips through SSH individually. For
bandwidth, buffering is cheaper:

```sh
# Streaming (default) -- bytes trickle in as generated
az-ai-v2 --raw "long prompt"

# Buffered -- one payload, emitted at end
az-ai-v2 --raw "long prompt" | cat
```

The `| cat` trick forces stdout to line-buffered or block-buffered
mode (implementation-dependent) so SSH sees a single large write
rather than token-by-token small writes. On a 32 kbps GSM link this
can be 2-3× faster wall-clock for the same response.

For scripts, prefer:

```sh
response=$(az-ai-v2 --raw "prompt")   # capture, then use
```

`$( … )` captures to memory in one shot -- SSH sees one flush at end
of command, not N flushes during streaming.

---

## 4. Reduce the response size

Bandwidth wasted on a response you did not need is bandwidth spent
twice -- once to generate, once to deliver. Cut both:

```sh
# Cap the response length
az-ai-v2 --raw --max-tokens 200 "summarize in two sentences"

# Estimate before you commit -- no API call, no network
az-ai-v2 --estimate "the prompt you're about to send"

# Use a smaller (and faster) model alias
az-ai-v2 --raw --model fast "quick yes/no: is this PR safe?"
```

`--estimate` is free -- it runs entirely locally, tokenizes offline,
and prints the estimated USD cost. Use it before sending large
prompts over a slow link.

---

## 5. Cache the response

The opt-in prompt cache (`--cache`, FR-008) serves byte-identical
responses from local disk for repeat queries. On a slow link, this is
the single biggest win for interactive workflows:

```sh
az-ai-v2 --raw --cache --cache-ttl 24 "what does EACCES mean?"
# First run: hits the network.
# Second run (within 24h): served from ~/.azureopenai-cli/cache/,
# zero bytes over the wire.
```

Caching is skipped automatically for agent / ralph / persona / json /
schema / estimate invocations -- those are inherently non-deterministic
or already-local. For plain Q&A, it is safe and fast.

---

## 6. Minimize screen redraw

Some terminals over SSH redraw the whole line on every Unicode width
change. `--raw` avoids this -- no color, no bold, no italic means no
SGR state changes, means no full-line redraws. Plain LF-terminated
output is the cheapest thing SSH can ship.

Avoid:

- `watch az-ai-v2 …` -- full-screen redraw every interval, brutal on
  low-BW.
- `tmux` with heavy themes / powerline status bars -- the status bar
  alone can saturate a dial-up modem.
- Terminal emulators with GPU-accelerated animations -- ironic on slow
  links; the terminal waits for redraw confirmations that never come.

Prefer:

- `screen` or bare `tmux` with minimal status.
- `ssh -C` (compression) for the entire session.
- `TERM=dumb` if your terminal supports it -- rule 2 in the color
  contract (see [`tty-detection.md`](tty-detection.md)) will kick in
  and strip color without any flag.

---

## 7. Byte budget -- a worked example

Comparing the same prompt and the same response on a simulated 56 kbps
link (≈7 KB/s):

| Invocation                          | Bytes sent to stdout | Time to last byte |
|-------------------------------------|---------------------:|------------------:|
| `az-ai-v2 "Q"` (TTY, CHROME)        | ~1800                | ~260 ms           |
| `az-ai-v2 "Q" \| cat` (CLEAN, stream) | ~1100                | ~160 ms           |
| `az-ai-v2 --raw "Q"` (CLEAN, stream)  | ~950                 | ~140 ms           |
| `az-ai-v2 --raw "Q" \| cat`           | ~950                 | ~140 ms           |
| `--raw --cache` on 2nd run            | ~950                 | **~5 ms** (local) |

Numbers are illustrative (indicative of the direction, not a
benchmark); the CHROME / CLEAN split is real and reproducible with
`cat -A`.

---

## 8. Emergency mode -- when the link is really bad

If you are on a link so bad that even `--raw` feels slow:

```sh
# Pre-pay the round trips: submit, disconnect, pick up later.
ssh jumpbox 'az-ai-v2 --raw "big prompt" > /tmp/r 2>/tmp/e; echo $? > /tmp/rc'

# Later, come back and grab it:
ssh jumpbox 'cat /tmp/r; echo "exit: $(cat /tmp/rc)"'
```

Works for any length of response, tolerates arbitrary disconnects,
and uses zero bytes of your bandwidth during generation. The sysadmin
pattern, decades old, still correct.

---

## 9. Cross-links

- [`docs/accessibility.md`](../accessibility.md) -- the canonical
  `--raw` and color contracts.
- [`docs/accessibility/tty-detection.md`](tty-detection.md) --
  behavior matrix.
- [`docs/accessibility/keyboard-workflows.md`](keyboard-workflows.md)
  -- pipe / stdin / `$EDITOR` patterns.
- [`docs/cost-optimization.md`](../cost-optimization.md) -- if
  present, the FR-008 cache and FR-015 estimator reference.

---

*Small, loud, principled. Ship the smallest payload that still speaks
the meaning.* -- M.A.
