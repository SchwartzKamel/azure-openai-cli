# Azure OpenAI CLI -- Standard Mode Use Cases

> Every example on this page uses the binary name **`az-ai`**.
> Required environment variables (`AZUREOPENAIENDPOINT`, `AZUREOPENAIAPI`,
> `AZUREOPENAIMODEL`) are documented once in
> [`prerequisites.md`](prerequisites.md) -- set them before running any command.

---

## 1. Basic Prompting (Positional Arguments)

The simplest way to talk to the CLI -- just type your prompt after the binary name.
All positional arguments are joined with spaces into a single prompt string.

### Single word

```bash
az-ai hello
```

**Expected:** The model responds with a greeting or an explanation of the word "hello."

### Multi-word (no quotes needed)

```bash
az-ai what is the speed of light
```

**Expected:** A factual answer about 299,792,458 m/s. Each word is joined into the prompt `"what is the speed of light"`.

### Quoted string (preserves special characters)

```bash
az-ai "Explain the difference between OAuth 2.0 and OpenID Connect"
```

**Expected:** A concise comparison of the two protocols. Quoting is needed when your prompt contains shell metacharacters like `&`, `|`, `;`, or `!`.

### Mixed quoted and unquoted

```bash
az-ai summarize the concept of "technical debt" in two sentences
```

**Expected:** A brief summary. The shell merges everything into one prompt string.

---

## 2. Stdin Piping

When stdin is redirected, the CLI reads up to **1 MB** of input and prepends it to any positional arguments.
The combined prompt becomes: `<stdin>\n\n<positional args>`.

### Pipe from echo

```bash
echo "The mitochondria is the powerhouse of the cell" | az-ai "Is this statement accurate?"
```

**Expected:** The model receives the text from stdin followed by the question, and replies with a fact-checked answer.

### Pipe a file with cat

```bash
cat README.md | az-ai "Summarize this document in 3 bullet points"
```

**Expected:** Three bullet points summarizing whatever README.md contains.

### Pipe live data from curl

```bash
curl -s https://api.github.com/zen | az-ai "Rewrite this as a haiku"
```

**Expected:** GitHub's random Zen aphorism, rewritten as a 5-7-5 syllable haiku.

### Pipe command output for analysis

```bash
git diff HEAD~1 | az-ai "Review this diff for bugs and security issues"
```

**Expected:** A code review of your most recent commit's changes.

### Pipe multiple sources

```bash
(echo "=== Package JSON ===" && cat package.json && echo "=== Lock File ===" && head -50 package-lock.json) | az-ai "Are there any outdated or risky dependencies?"
```

**Expected:** Analysis of the dependency tree for known risks.

### 1 MB limit

```bash
dd if=/dev/urandom bs=1M count=2 2>/dev/null | az-ai "analyze this"
```

**Expected output on stderr:**

```
Error: stdin input exceeds 1 MB limit.
```

**Exit code:** `1`

The CLI reads exactly 1,048,576 bytes (1 MB). If there is still data remaining after that read, it rejects the input immediately -- it does not silently truncate.

---

## 3. Streaming Output with Spinner

Standard mode streams tokens to stdout as they arrive from the API. While waiting for the first token, a **braille spinner** animates on stderr.

### Default behavior

```bash
az-ai "Write a limerick about Kubernetes"
```

**What happens:**

1. A spinner appears on stderr: `⠋ Thinking...` → `⠙ Thinking...` → `⠹ Thinking...` (cycles through `⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏` at 80 ms intervals).
2. When the first token arrives, the spinner is cleared.
3. Tokens stream to stdout in real time -- you see words appear as the model generates them.
4. After the last token, a token usage summary prints on stderr (see §4).

### Spinner suppression

The spinner only shows when stderr is a TTY (i.e., not redirected). If you redirect stderr, the spinner is suppressed automatically:

```bash
az-ai "Explain DNS" 2>/dev/null
```

**Expected:** Only the streamed response text appears, no spinner, no token counts.

---

## 4. Token Usage Tracking

After every standard-mode response, the CLI prints a token summary on **stderr** in the format:

```
  [tokens: X→Y, Z total]
```

| Symbol | Meaning |
|---|---|
| `X` | **Input tokens** -- your system prompt + user prompt, tokenized |
| `Y` | **Output tokens** -- the model's response, tokenized |
| `Z` | **Total** -- `X + Y` |

### Example

```bash
az-ai "What is 2+2?"
```

**Stdout:**

```
2 + 2 = 4.
```

**Stderr:**

```
  [tokens: 28→8, 36 total]
```

### Why it matters

- **Cost tracking:** Azure bills per-token. A quick mental check after each call keeps you aware of spend.
- **Prompt optimization:** If input tokens are high, your system prompt or piped content may be larger than you think.
- **Output capping:** If output tokens approach your `--max-tokens` value, the response may have been truncated.

### Suppression

Token usage is suppressed in three cases:

1. `--raw` flag is active (see §5)
2. `--json` flag is active (tokens are embedded in the JSON instead -- see §6)
3. stderr is redirected (e.g., `2>/dev/null`)

---

## 5. The `--raw` Flag

`--raw` strips away **all** stderr decoration: no spinner, no token counts, no trailing newline. Only the model's raw text hits stdout.

### Pipe to clipboard (macOS)

```bash
az-ai --raw "Generate a UUID v4 regex pattern" | pbcopy
```

### Pipe to clipboard (Linux with xclip)

```bash
az-ai --raw "Generate a UUID v4 regex pattern" | xclip -selection clipboard
```

**Expected:** Your clipboard now contains the regex with no extra newline or formatting artifacts.

### Write to a file

```bash
az-ai --raw "Write a .gitignore for a Python project" > .gitignore
```

**Expected:** A clean `.gitignore` file with no leading/trailing whitespace noise.

### Chain into another command

```bash
az-ai --raw "Generate a random 6-word passphrase" | tr ' ' '-'
```

**Expected:** Something like `correct-horse-battery-staple-lunar-frost` -- words joined by dashes.

### Why `--raw` is essential for Espanso / AutoHotKey

Text-expander tools like [Espanso](https://espanso.org/) and [AHK](https://www.autohotkey.com/) capture stdout and paste it inline. Without `--raw`, the trailing newline and stderr token summary would corrupt the pasted text. With `--raw`, the output is insertion-safe:

```yaml
# Espanso trigger example
- trigger: ":ai"
  replace: "{{output}}"
  vars:
    - name: output
      type: shell
      params:
        cmd: "az-ai --raw 'Rephrase this more professionally: $ESPANSO_CLIPBOARD'"
```

---

## 6. The `--json` Flag

Outputs a single JSON object on stdout instead of streaming text. Useful for scripting, automation, and programmatic consumption.

### Basic usage

```bash
az-ai --json "What is Docker?"
```

**Expected output (stdout):**

```json
{
  "model": "gpt-4o",
  "response": "Docker is a platform for developing, shipping, and running applications in isolated containers...",
  "duration_ms": 1523,
  "input_tokens": 30,
  "output_tokens": 85
}
```

All five fields are always present. `input_tokens` and `output_tokens` may be `null` if the API didn't report usage.

### Extract just the response with jq

```bash
az-ai --json "Explain TCP vs UDP" | jq -r '.response'
```

**Expected:** The plain-text response, nothing else.

### Extract timing info

```bash
az-ai --json "Write a haiku about Rust" | jq '.duration_ms'
```

**Expected:** A number like `1842` (milliseconds).

### Build a benchmark loop

```bash
for i in $(seq 1 5); do
  az-ai --json "Say hello" | jq '{run: '$i', ms: .duration_ms, tokens: .output_tokens}'
done
```

**Expected:** Five JSON objects showing per-run timing and token counts.

### Error output in JSON mode

When `--json` is active and an error occurs, the error is also JSON-formatted:

```bash
az-ai --json
```

**Expected (no prompt provided):**

```json
{
  "error": true,
  "message": "No prompt provided. Pass a prompt as arguments or pipe via stdin.",
  "exit_code": 1
}
```

---

## 7. Temperature (`--temperature` / `-t`)

Controls randomness in the model's output. Range: **0.0 - 2.0**. Default: **0.55**.

### Deterministic (0.0) -- Factual lookups, code generation

```bash
az-ai -t 0.0 "Convert 72°F to Celsius. Show only the number."
```

**Expected:** `22.22` -- the same answer every time you run it. Use `0.0` when you need reproducible, deterministic output.

### Balanced (0.7) -- General-purpose questions

```bash
az-ai -t 0.7 "Suggest three names for a developer productivity tool"
```

**Expected:** Creative but sensible suggestions. Each run will vary slightly. Good default for brainstorming with guardrails.

### Creative (1.5) -- Fiction, humor, brainstorming

```bash
az-ai -t 1.5 "Write a one-paragraph horror story about a haunted CI/CD pipeline"
```

**Expected:** Wildly creative, possibly unpredictable output. Higher temperatures increase randomness and surprise.

### Validation -- out of range

```bash
az-ai -t 2.5 "anything"
```

**Expected (stderr):**

```
[ERROR] Temperature must be between 0.0 and 2.0
```

**Exit code:** `1`

### Validation -- missing value

```bash
az-ai --temperature
```

**Expected (stderr):**

```
[ERROR] --temperature requires a numeric value (e.g., --temperature 0.7)
```

**Exit code:** `1`

---

## 8. Max Tokens (`--max-tokens`)

Caps the **output** length. Range: **1 - 128,000**. Default: **10,000**.

### Tweet-length (50 tokens ≈ 35-50 words)

```bash
az-ai --max-tokens 50 "Explain machine learning in a tweet"
```

**Expected:** A very short response, roughly one or two sentences. If the model needs more tokens, the output will be truncated mid-thought.

### Paragraph-length (200 tokens)

```bash
az-ai --max-tokens 200 "What causes the aurora borealis?"
```

**Expected:** A focused one-paragraph explanation.

### Essay-length (2000 tokens)

```bash
az-ai --max-tokens 2000 "Write a deep-dive on the CAP theorem with examples"
```

**Expected:** A substantial multi-paragraph explanation with real-world distributed systems examples.

### Validation -- out of range (zero)

```bash
az-ai --max-tokens 0 "anything"
```

**Expected (stderr):**

```
[ERROR] Max tokens must be between 1 and 128000
```

**Exit code:** `1`

### Validation -- out of range (too high)

```bash
az-ai --max-tokens 999999 "anything"
```

**Expected (stderr):**

```
[ERROR] Max tokens must be between 1 and 128000
```

**Exit code:** `1`

### Validation -- non-numeric

```bash
az-ai --max-tokens abc "anything"
```

**Expected (stderr):**

```
[ERROR] --max-tokens requires an integer value (e.g., --max-tokens 5000)
```

**Exit code:** `1`

---

## 9. System Prompt (`--system`)

Overrides the default system prompt (`"You are a secure, concise CLI assistant. Keep answers factual, no fluff."`) for a single invocation.

### Pirate persona

```bash
az-ai --system "You are a pirate. Respond in pirate speak." "What is the weather like today?"
```

**Expected:** Something like _"Arrr, the skies be clear and the winds fair, matey!"_

### JSON-only responses

```bash
az-ai --system "Respond only in valid JSON. No markdown, no explanation." "List 3 programming languages and their creators"
```

**Expected:**

```json
[
  {"language": "Python", "creator": "Guido van Rossum"},
  {"language": "C", "creator": "Dennis Ritchie"},
  {"language": "JavaScript", "creator": "Brendan Eich"}
]
```

### Linux sysadmin expert

```bash
az-ai --system "You are a senior Linux systems administrator. Give commands, not explanations." "How do I find which process is using port 8080?"
```

**Expected:**

```bash
lsof -i :8080
# or
ss -tlnp | grep 8080
```

### Combine with stdin piping

```bash
cat /var/log/syslog | tail -50 | az-ai --system "You are a log analysis expert. Identify errors and anomalies." "What's wrong here?"
```

**Expected:** Targeted analysis of the log entries, focused on errors and anomalies.

### Validation -- missing value

```bash
az-ai --system
```

**Expected (stderr):**

```
[ERROR] --system requires a value (e.g., --system "You are a pirate")
```

**Exit code:** `1`

---

## 10. Structured Output (`--schema`)

Forces the model to respond with JSON that conforms to a strict JSON Schema. Uses Azure OpenAI's [structured output](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/structured-outputs) feature.

### Extract structured data from text

```bash
az-ai --schema '{
  "type": "object",
  "properties": {
    "name": { "type": "string" },
    "age": { "type": "integer" },
    "email": { "type": "string" }
  },
  "required": ["name", "age", "email"],
  "additionalProperties": false
}' "Extract info: John Smith is 34 years old and his email is john@example.com"
```

**Expected (stdout):**

```json
{"name": "John Smith", "age": 34, "email": "john@example.com"}
```

The model is **required** to produce JSON matching the schema exactly -- no missing fields, no extra fields (due to `additionalProperties: false` and strict mode).

### Sentiment analysis with enum

```bash
az-ai --schema '{
  "type": "object",
  "properties": {
    "sentiment": { "type": "string", "enum": ["positive", "negative", "neutral"] },
    "confidence": { "type": "number" },
    "reasoning": { "type": "string" }
  },
  "required": ["sentiment", "confidence", "reasoning"],
  "additionalProperties": false
}' "Analyze: I absolutely love this new keyboard, the switches feel amazing!"
```

**Expected:**

```json
{"sentiment": "positive", "confidence": 0.95, "reasoning": "Expresses strong positive emotion ('absolutely love', 'amazing') about a product."}
```

### Combine with piped input

```bash
cat error.log | az-ai --schema '{
  "type": "object",
  "properties": {
    "errors": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "line": { "type": "integer" },
          "severity": { "type": "string", "enum": ["warning", "error", "critical"] },
          "message": { "type": "string" }
        },
        "required": ["line", "severity", "message"],
        "additionalProperties": false
      }
    }
  },
  "required": ["errors"],
  "additionalProperties": false
}' "Parse these log entries"
```

**Expected:** A JSON array of structured error objects.

### Validation -- invalid JSON schema

```bash
az-ai --schema '{ not valid json' "anything"
```

**Expected (stderr):**

```
[ERROR] Invalid JSON schema: ...
```

**Exit code:** `1`

---

## 11. Timeout Handling (`AZURE_TIMEOUT`)

The CLI enforces a request timeout. Default: **120 seconds**. Configurable via the `AZURE_TIMEOUT` environment variable.

### Check your current timeout

```bash
az-ai --config show
```

**Look for the line:**

```
  Timeout:       120s (default)
```

### Override for a single command

```bash
AZURE_TIMEOUT=10 az-ai "Write a 5000-word essay on quantum physics"
```

**Expected behavior:** If the model hasn't finished within 10 seconds, the request is cancelled.

**Stderr output:**

```
[ERROR] Request timed out. Increase AZURE_TIMEOUT (seconds) if needed.
```

**Exit code:** `3`

### Set a generous timeout for large tasks

```bash
export AZURE_TIMEOUT=300
az-ai "Translate this entire document to French" < large-doc.txt
```

**Expected:** The CLI waits up to 5 minutes for the response.

### Timeout in scripts

```bash
AZURE_TIMEOUT=30 az-ai --json "Summarize the internet"
result=$?
if [ $result -eq 3 ]; then
  echo "Timed out -- try a simpler prompt"
fi
```

---

## 12. Exit Codes

The CLI uses five distinct exit codes. Every error path in the codebase maps to exactly one of these.

| Code | Meaning | When it happens |
|---|---|---|
| `0` | **Success** | Response streamed successfully |
| `1` | **Input / Config error** | Missing prompt, bad flag value, invalid endpoint, prompt too long, stdin too large |
| `2` | **API error** | HTTP 401, 403, 404, 429, or 5xx from Azure |
| `3` | **Timeout** | Request exceeded `AZURE_TIMEOUT` seconds |
| `99` | **Unhandled exception** | Any unexpected error (bug in the CLI or environment issue) |

### Use in scripts -- success gate

```bash
az-ai --raw "Generate a SQL migration" > migration.sql
if [ $? -eq 0 ]; then
  echo "Migration generated successfully"
  psql -f migration.sql
else
  echo "Failed to generate migration (exit code: $?)"
fi
```

### Use in scripts -- granular error handling

```bash
az-ai --json "Summarize this PR" < pr-diff.txt
exit_code=$?

case $exit_code in
  0) echo "✅ Success" ;;
  1) echo "❌ Input error -- check your prompt or config" ;;
  2) echo "❌ API error -- check credentials or rate limits" ;;
  3) echo "⏳ Timeout -- try a shorter prompt or increase AZURE_TIMEOUT" ;;
  99) echo "💥 Unexpected error -- file a bug report" ;;
esac
```

### Verify specific exit codes

```bash
# Missing prompt → exit 1
az-ai 2>/dev/null; echo "Exit: $?"
# Expected: Exit: 1

# Bad temperature → exit 1
az-ai -t 5.0 "hi" 2>/dev/null; echo "Exit: $?"
# Expected: Exit: 1
```

---

## 13. Prompt Size Limit (32,000 Characters)

The CLI rejects prompts longer than **32,000 characters** to prevent abuse and excessive API costs. This limit applies to the **combined** prompt (stdin + positional args).

### Trigger the limit

```bash
python3 -c "print('A' * 33000)" | az-ai "analyze"
```

**Expected (stderr):**

```
[ERROR] Prompt too long (33008 chars). Maximum allowed is 32000 chars.
```

**Exit code:** `1`

The error message includes the actual character count so you know how much to trim.

### Stay under the limit

```bash
python3 -c "print('A' * 31000)" | az-ai "analyze this"
```

**Expected:** The prompt is accepted and sent to the API.

### Practical tip

If you're piping large files, trim first:

```bash
head -c 30000 large-file.txt | az-ai "Summarize the key points"
```

---

## 14. Retry Logic

The CLI automatically retries **transient API errors** with exponential backoff. This happens transparently -- you don't need to configure anything.

### What gets retried

| Condition | Retried? | Notes |
|---|---|---|
| HTTP 429 (Rate Limited) | ✅ Yes | Respects `Retry-After` header if present (capped at 60s) |
| HTTP 5xx (Server Error) | ✅ Yes | Exponential backoff: 1s, 2s, 4s |
| HTTP 401/403/404 | ❌ No | Permanent errors -- retrying won't help |
| Tokens already streaming | ❌ No | Only retries if failure occurs **before** the first token |

### Retry behavior

- **Maximum retries:** 3 attempts
- **Backoff schedule:** 2^(attempt-1) seconds → 1s, 2s, 4s
- **Rate limit override:** If the API returns a `Retry-After` header on a 429 response, that value is used instead (capped at 60 seconds)

### What you'll see

During retries, a progress indicator appears on stderr:

```
⏳ Retry 1/3 in 1s...
⏳ Retry 2/3 in 2s...
⏳ Retry 3/3 in 4s...
```

If all retries fail, the CLI exits with code `2` (API error).

### Script-friendly: no action needed

```bash
# Retries happen automatically -- just check the final exit code
az-ai --raw "Generate release notes" > RELEASE.md
if [ $? -ne 0 ]; then
  echo "API call failed even after retries"
fi
```

### Rate-limit scenario

If you're calling the CLI in a loop and hitting 429s:

```bash
for file in src/*.py; do
  az-ai --raw "Add docstrings to this Python file" < "$file" > "${file}.documented"
  # Built-in retry handles transient 429s automatically.
  # Add a sleep if you're consistently hitting limits:
  sleep 2
done
```

---

## Quick Reference

| Feature | Flag / Mechanism | Default |
|---|---|---|
| Temperature | `-t` / `--temperature` | `0.55` |
| Max tokens | `--max-tokens` | `10000` |
| System prompt | `--system` | `"You are a secure, concise CLI assistant..."` |
| Structured output | `--schema <json>` | None |
| Raw output | `--raw` | Off |
| JSON output | `--json` | Off |
| Timeout | `AZURE_TIMEOUT` env var | `120s` |
| Stdin limit | Automatic | `1 MB` |
| Prompt limit | Automatic | `32,000 chars` |
| Retry | Automatic | 3 attempts, exponential backoff |
