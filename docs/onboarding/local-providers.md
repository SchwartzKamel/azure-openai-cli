# Local providers -- the first hour

> Hi, I'm Lloyd. You just installed `az-ai` and you would like the very
> next thing it does to be a model running on your own laptop -- not a
> cloud you have to swipe a credit card at. This page walks you from
> "binary on disk" to "Hi, what are you?" without skipping the
> embarrassing little steps that the README assumes you already know.
>
> Maintained by Lloyd Braun (junior dev / onboarding lens). If a step
> here surprises you, that is the bug; please file an issue.

This is the companion to [`docs/onboarding.md`](../onboarding.md), which
covers the first sixty minutes for *contributors*. This page is the
first hour for a *user* who wants a local model.

A note on episode numbers: this project tracks itself like a TV season.
"S03E14" means "season 3, episode 14". Some of the pieces this tutorial
relies on already shipped (S03E09 *The Compat*, S03E11 *The Wizard,
Reprise*); some are slated to ship soon and are clearly marked
**(coming soon: S03ENN)** with a workaround you can use today.

---

## 1. What is a local provider, and why would I want one?

A "local provider" is a model running on your own machine instead of
calling out to OpenAI, Azure, Groq, or another cloud. You hand it a
prompt over `http://localhost:...`, it answers, the bytes never leave
the box.

You probably want one if:

- **Privacy.** Sensitive snippets, internal docs, regulated content --
  nothing leaves your laptop, no terms of service to read.
- **Offline.** Trains, planes, hotels with hostile Wi-Fi, air-gapped
  workstations.
- **Cost.** Once the model is on disk, queries are free. Useful for
  bulk classification, dev-loop iteration, anything you would feel
  guilty metering.
- **Latency.** No round-trip to a data center. First-token latency on a
  GPU can beat a flaky cloud connection.

What you give up: the very biggest models. A 7-billion-parameter local
model is good; it is not GPT-4-tier good. Pick the right tool for the
job. For a triage assistant in a script, local wins. For a one-shot
deep reasoning task, a frontier cloud model still wins.

---

## 2. Prerequisites I might not have thought of

Before you install anything, do a quick sanity check on the box.

### Operating system

| Platform | Local provider story |
|---|---|
| Linux (native, x86_64 or arm64) | Best supported. |
| macOS (Apple Silicon, M-series) | Best supported. Metal acceleration is automatic. |
| macOS (Intel) | Works, slower; CPU only on most builds. |
| Windows 10 / 11 (native) | Works via the Windows installer. |
| WSL2 (Ubuntu / Debian inside Windows) | Works -- install the Linux build inside the WSL distro, not the Windows side. See "Things that will trip you up". |

### Disk

Plan for the model files, not the runtime. The runtime is a small
download. The models are not.

- A 1B-parameter model is about 1-2 GB.
- A 7-8B model is 4-5 GB.
- A 13B model is 7-9 GB.
- A 70B model is 40+ GB.

If you only have 20 GB free on your home partition, do not pull a 70B
model on impulse. Start small (see step 2).

### RAM

A model has to fit in RAM (or VRAM, if you have a GPU) to run. Rough
rule of thumb for quantized models, the kind a local runtime ships by
default:

- 8 GB total RAM: 1B-3B models fit comfortably; 7B is tight.
- 16 GB: 7B-8B models are the sweet spot.
- 32 GB+: 13B and up start to be reasonable.
- 64 GB+: required for 70B.

If your laptop has 8 GB of RAM and is also running a browser, a chat
app, an IDE, and Docker -- start with the 1B model. Trust me.

### GPU (optional, but a big speedup)

- **NVIDIA.** Run `nvidia-smi`. If it prints a table with your GPU and
  a CUDA version, you are set; the runtime will pick it up.
- **Apple Silicon.** It just works. No flags. The runtime uses the
  Metal API automatically.
- **AMD.** Possible but bumpier. ROCm support varies by runtime and
  card. Check the runtime's docs before assuming acceleration. CPU
  fallback always works.
- **No GPU at all.** That is fine. CPU inference is slower but real.
  The 1B model in step 2 is comfortable on CPU; a 7B model on CPU is
  usable for short prompts and patience-testing for long ones.

### Network

You need internet exactly **once**: for the first download of the
runtime and the model. After that, a local provider is fully offline.

---

## 3. Step 1: Install Ollama

Ollama is the lowest-friction local runtime as of this writing -- one
binary, sensible defaults, OpenAI-compatible HTTP API on port 11434.
This tutorial uses Ollama. Other backends (llama.cpp, vLLM, NIM) work
too; see "What about other backends?" near the end.

### Linux

The official one-liner is:

```bash
curl -fsSL https://ollama.com/install.sh | sh
```

**A word on `curl | sh`.** Piping a script straight from the internet
into a shell is convenient and risky -- you are trusting whatever
bytes the server returns at that moment. If you would rather not, you
have two safer options:

1. Download first, read, then run:

   ```bash
   curl -fsSL https://ollama.com/install.sh -o ollama-install.sh
   less ollama-install.sh        # eyeball it
   sh ollama-install.sh
   ```

2. Use a packaged install if your distro has one (Homebrew on Linux,
   Nix, AUR, etc.). These give you a path back to a maintainer.

When the project ships its outbound-allowlist documentation in
**(coming soon: S03E16 *The Allowlist*)** we will link it here for the
fuller threat-model explanation.

### macOS

```bash
brew install ollama
```

Or download the `.dmg` from the Ollama site and drag it to
`Applications/`. The `.dmg` includes a menu-bar app that starts the
server for you; the brew install gives you a CLI you can launch with
`ollama serve` or via `brew services`.

### Windows (native)

Download the installer from the Ollama site and run it. It installs a
service that starts on login and listens on `127.0.0.1:11434`.

### WSL2 (Ubuntu / Debian inside Windows)

Install **inside** the WSL distro (the Linux one-liner above), not on
the Windows host. If you install the Windows version *and* the WSL
version, you will end up with two services fighting over port 11434
and you will not enjoy that afternoon.

If you only installed it on the Windows side and you want to talk to
it from inside WSL, that works too -- WSL2 can reach the Windows host
on `localhost`, but it is one more thing that can go wrong. Start with
"install in WSL", graduate later if you have a reason.

### Verify

After install:

```bash
ollama --version
```

Expected output is a version string. If you get "command not found",
your shell has not picked up the new `PATH` yet -- open a new terminal
or `source ~/.bashrc` / `source ~/.zshrc`.

---

## 4. Step 2: Pull a small model first

Resist the urge to pull a 40 GB model as your first move. Pull a tiny
one and verify the whole pipeline -- runtime, port, az-ai wiring --
before you commit to a long download.

```bash
ollama pull llama3.2:1b
```

Expected output (abridged):

```text
pulling manifest
pulling abc123...   100%   1.3 GB
pulling def456...   100%   12 KB
verifying sha256 digest
writing manifest
success
```

On a 100 Mbit home connection, expect roughly two minutes for the 1B
download. On a coffee-shop Wi-Fi, expect to read a chapter of a book.
On a fiber line, less than thirty seconds.

Once it lands, list what is on disk:

```bash
ollama list
```

You should see `llama3.2:1b` with its size and a recent timestamp. If
you do not, the pull silently failed -- re-run with the same command.

---

## 5. Step 3: Confirm Ollama is serving

Ollama exposes an OpenAI-compatible endpoint on port `11434` by
default. Confirm the server is up:

```bash
curl http://localhost:11434/v1/models
```

Expected output (abridged):

```json
{
  "object": "list",
  "data": [
    {
      "id": "llama3.2:1b",
      "object": "model",
      "created": 1730000000,
      "owned_by": "library"
    }
  ]
}
```

If you get **"Connection refused"**, the server is not running. See
"Things that will trip you up" below.

If you get JSON but `data` is empty, the pull from step 2 did not
land.

The path that matters for `az-ai` is the **base URL**:
`http://localhost:11434/v1`. The `/v1` suffix is the OpenAI-wire
convention; do not omit it.

---

## 6. Step 4: Tell az-ai about it

There are two paths. Both end at the same `~/.config/az-ai/env` file.

### Path A: the wizard (what S03E11 *The Wizard, Reprise* shipped)

`az-ai --setup` walks you through an interactive prompt. The wizard
currently knows about five named provider presets: `azure`, `openai`,
`groq`, `together`, `cloudflare`. As of writing **it does not yet have
a dedicated `ollama` choice** -- that ships with **(coming soon:
S03E14 *The Daemon*)**. For now, use Path B.

When E14 lands, the flow will look like:

```text
$ az-ai --setup
Welcome to az-ai setup!

Default provider:
    1) azure
    2) openai
    3) groq
    4) together
    5) cloudflare
  * 6) ollama        <-- new in S03E14
Pick [openai]: 6

Ollama base URL [http://localhost:11434/v1]:
Ollama model name(s), comma-separated [llama3.2:1b]: llama3.2:1b
```

### Path B: edit the env file by hand (works today)

Open `~/.config/az-ai/env` in your editor (create it if it does not
exist). Add a default-section export so `az-ai` will route the model
name `llama3.2:1b` through the OpenAI-compat adapter pointed at your
local server:

```text
# ~/.config/az-ai/env
# Local Ollama, S03E19 workaround until S03E14 ships an ollama preset.

export OPENAI_API_KEY="ollama"
export AZ_AI_COMPAT_MODELS="openai:llama3.2:1b"
```

A couple of things to know about that file:

- `AZ_AI_COMPAT_MODELS` is the allowlist for the compat dispatcher
  (S03E09 *The Compat*). Format: `preset:model[,preset:model...]`.
  Each entry must be one of the built-in preset names.
- `OPENAI_API_KEY="ollama"` is a placeholder. Ollama does not check
  it, but the OpenAI-compat adapter requires *something* in the env
  var named on the preset. The literal string `ollama` is conventional.
- The file should be `chmod 600` on Linux / macOS. The wizard does
  this for you; if you wrote the file by hand, run
  `chmod 600 ~/.config/az-ai/env`.

**Important caveat for the workaround:** the built-in `openai` preset
points at `https://api.openai.com/v1`, not at your localhost. Until
**(coming soon: S03E14 *The Daemon*)** ships an `ollama` preset whose
URL is `http://localhost:11434/v1`, you cannot route a model named
`llama3.2:1b` through the `openai` preset and have it land on Ollama
-- the URL is baked into the preset record. So this Path B is honest
about what works **today**:

- If you already run a real OpenAI-compatible cloud (Groq, Together)
  on a real model, the compat path works today.
- If you want Ollama on `localhost`, you have two choices: (a) wait
  for E14, or (b) build from a branch where someone has added the
  `ollama` preset locally. Option (b) is for contributors only.

This page is here in advance of E14 so the moment it ships, the
walkthrough is already written. Lloyd does not like to be caught flat
on his first day.

---

## 7. Step 5: Opt in to local providers

For safety, the dispatcher will refuse to call a localhost-style URL
unless you have explicitly opted in. The opt-in is a single env var:

```bash
export AZ_AI_LOCAL_PROVIDERS=1
```

Why this exists, in plain English: a typo in `AZ_AI_COMPAT_MODELS`,
or an env var leaking in from the wrong shell, should never silently
cause `az-ai` to start poking at services on your laptop. The default
is "no, do not connect to localhost"; the opt-in says "yes, I meant
to". This belongs to **(coming soon: S03E16 *The Allowlist*)** -- it
formalizes the threat model and the precise IP ranges treated as
local.

If you forget the opt-in, you will see an error like:

```text
[ERROR] Refusing to dispatch to local URL http://localhost:11434/v1
        without AZ_AI_LOCAL_PROVIDERS=1 set. See
        docs/onboarding/local-providers.md step 5.
```

Add the export to `~/.config/az-ai/env` next to the other lines if you
want it to stick across shells.

---

## 8. Step 6: Run it

This is the step you came for.

```bash
az-ai --model llama3.2:1b "Hi, what are you?"
```

Expected output (abridged, the actual text varies):

```text
I'm a local language model running on your machine via Ollama. I can
help with...
```

A few things to expect, especially on the first run:

- **The first response is slow.** The model has to load into RAM
  (or VRAM) before it can generate the first token. On a CPU with a
  1B model, this is typically a few seconds; with a 7B model on CPU,
  it can be 10-30 seconds. Subsequent prompts in the same session are
  much faster because the model stays resident.
- **Tokens stream.** You will see text appear word-by-word, not all
  at once.
- **Quality is not GPT-4.** A 1B model is small. Use it for quick
  responses, classification, format conversion. Step up to 7B-13B
  when you want more from it.

If you set `--raw`, you get clean stdout suitable for piping to
Espanso / AHK / any script.

```bash
az-ai --raw --model llama3.2:1b "translate to French: hello"
```

---

## 9. Step 7: Verify it is actually local

Trust, but verify. S03E15 *The Probe* shipped the `az-ai --doctor`
subcommand, which probes every configured provider for endpoint
reachability, credential presence, and model allowlist. Run it:

```bash
az-ai --doctor
```

Expected output (abridged, ASCII table by default):

```text
provider   endpoint                          dns       reachable   auth     models
ollama     http://localhost:11434/v1         ok        ok          n/a      llama3.2:1b
```

The `dns` and `endpoint` columns are the ones you want -- the URL
must say `localhost` (or `127.0.0.1`), and DNS must resolve
local. If you see a public IP there, something is misconfigured.

`--doctor --json` and `--doctor --plain` are also available; exit
code is `0` when everything is healthy, `1` when at least one
provider is unhealthy. Useful in CI, useful in shell scripts, useful
when you want a one-liner to attach to an issue.

Until S03E15 *The Probe* shipped, the manual sanity-check was
`curl http://localhost:11434/v1/models` plus `ss -lnt | grep 11434`.
That still works as a belt-and-braces check.

---

## 10. Glossary

The doc above mentions a handful of terms that are obvious to people
who already know them. Here is the page where you do not have to
already know them.

- **Compat preset.** A named bundle of (base URL, auth scheme, env-var
  name) that lets one OpenAI-compat adapter talk to many endpoints.
  Like a saved Wi-Fi network for HTTP APIs. Built-in presets ship in
  `OpenAiCompatAdapter.cs`.
- **Allowlist.** A list of allowed destinations. Anything not on the
  list is blocked. The opposite of a blocklist (a list of forbidden
  destinations -- everything else allowed).
- **Localhost / 127.0.0.1.** Your own computer's loopback address.
  Connecting to localhost does not put bytes on any network. The IPv6
  equivalent is `::1`.
- **Token.** The chunk of text a model processes. Roughly 4 characters
  or 0.75 words in English. Pricing, context windows, and rate limits
  are all measured in tokens, not words.
- **Quantization.** Rewriting a model's numerical weights at lower
  precision so it takes less memory and runs faster. The model gets
  slightly less accurate. The suffix on a model name like
  `llama3.2:1b-q4_K_M` says the quantization scheme; lower numbers
  (q4, q3) are more compressed than higher (q8). Defaults are usually
  fine.
- **Context window.** How much text the model can consider at once,
  measured in tokens. A small model often has a small window (4K-8K
  tokens); larger models 32K-200K. If your prompt exceeds the window,
  the runtime truncates -- usually badly.
- **Backend vs. provider.** The "backend" is the software actually
  running the model (Ollama, llama.cpp, vLLM). The "provider" is how
  `az-ai` routes to it (a compat preset, in our case). One backend can
  serve many providers; one provider talks to exactly one backend.

For the project-wide glossary (AOT, MAF, DPAPI, libsecret, MCP, and
friends), see [`../glossary.md`](../glossary.md).

---

## 11. Things that will trip you up

The errors below are the ones I (Lloyd) actually hit. If you hit one
that is not listed here, please open an issue and add it.

### "model not found"

You forgot to `ollama pull` the model, or you used a different tag
than the one in `AZ_AI_COMPAT_MODELS`. Tag drift is real:
`llama3.2:1b` and `llama3.2:1b-q4_K_M` are different model entries on
disk. `ollama list` is the source of truth.

### "Connection refused" on the first run

Ollama is not running. Confirm by platform:

- **Linux (systemd):** `systemctl status ollama`. If inactive,
  `systemctl --user start ollama` or `sudo systemctl start ollama`
  depending on how it was installed.
- **macOS:** open the menu-bar app, or `brew services start ollama`,
  or `ollama serve` in a terminal.
- **Windows:** open Task Manager and look for `ollama.exe`. If it is
  not there, run the installer's start-menu entry or `ollama serve`
  in PowerShell.
- **WSL:** `ollama serve &` inside the distro, or set up a systemd
  service if your WSL has systemd enabled.

### Slow first response (then fast after)

Normal. The model is loading into RAM the first time. Ollama keeps
the model resident for a few minutes after the last request, then
unloads. If you are scripting bursts of prompts, you can keep it warm
with `ollama run <model> ""` or just by calling it more often than
the unload timeout.

### Out of memory / OOM kill

The model is too big for your RAM. Pick a smaller quantization (the
`-q4` family is a safe default) or a smaller model. Closing your
browser also helps more than you would think.

### Same env file, different machine

Your `~/.config/az-ai/env` says `AZ_AI_COMPAT_MODELS=ollama:llama3.2:1b`
but you copied the file to a fresh laptop where you never ran
`ollama pull`. The error is "model not found" again. Always `pull` on
each machine.

### `AZ_AI_LOCAL_PROVIDERS` not set

See step 5. `az-ai` refuses to connect to localhost without the opt-in.

### Two Ollamas (Windows + WSL)

Port 11434 is in use, both services are racing, requests land at the
wrong one. Pick one. Uninstall the other.

---

## 12. What about other backends?

Ollama is not the only option, just the easiest first one. Two more
adapter episodes are slated:

- **(coming soon: S03E17 *The Server*)** -- `llama-server` from the
  llama.cpp project. Same OpenAI-compat path, but you run the binary
  directly with flags like `--host 127.0.0.1 --port 8080 --api-key
  ...`. No service manager, no menu bar; tighter control. Good for
  air-gapped boxes where you want a single, audited, digest-pinned
  binary and nothing else.
- **(coming soon: S03E18 *The Capability Gate*)** -- not a new
  backend, but a related correctness fix: not every local model
  supports tool calling (the `--agent` execution mode). When you ask
  for tools on a model that cannot do them, today you get a confusing
  error; after E18 the adapter will probe `/v1/models`, cache the
  capability per model, and refuse `--agent` cleanly with a hint.

vLLM and NVIDIA NIM are also OpenAI-compat. They follow the same
recipe: stand up the server, pick the base URL, register a compat
preset, set the model name, and dispatch through `AZ_AI_COMPAT_MODELS`.

---

## 13. Where to ask for help

When something is broken and this doc did not save you:

1. Run `az-ai --doctor` (S03E15 *The Probe*) and copy its output. It
   will tell you a lot in one screenful, never emits credential
   values, and exits 1 when something is unhealthy.
2. Search [the issue tracker](https://github.com/SchwartzKamel/azure-openai-cli/issues).
   Onboarding-shaped pain is common; you are likely not the first.
3. If the issue is new, file one. Include:
   - OS + version (`uname -a` / `sw_vers` / `winver`).
   - `az-ai --version` output.
   - The exact command you ran.
   - The `[ERROR]` line you saw.
   - `ollama --version` and `ollama list` output if relevant.
4. If you are comfortable opting in, set `AZ_AI_TELEMETRY=1` (S03E13
   *The Telemetry*) before reproducing the bug. It emits a single
   NDJSON line per dispatch to stderr -- prompts and completions are
   never included; the schema is fixed at eight non-PII fields.
   Pasting that line into the issue helps a lot.

If something on this page is wrong or unclear, **that is the bug,
not your reading of it**. Open the issue. Lloyd will fix the doc.

Hello!
