# FR-002: Interactive Chat Mode with Conversation Memory

**Priority:** P0 -- Critical  
**Impact:** Turns single-shot queries into actual AI conversations  
**Effort:** Medium (2-3 days)  
**Category:** Core UX

---

## The Problem

Every invocation of this CLI is a one-night stand. There's no relationship. No memory. No continuity.

```bash
az-ai "Write a Python function to parse CSV files"
# Great output!
az-ai "Now add error handling to it"
# "Add error handling to WHAT?" -- the AI has no idea what "it" is.
```

The user has to re-paste the entire prior response, plus their new question, into a single prompt. This is the #1 reason developers abandon CLI AI tools and go back to ChatGPT's web interface -- because the web UI *remembers*.

Today, every invocation:

1. Spins up a new Docker container (~1-2s overhead)
2. Creates a brand new Azure OpenAI client
3. Sends exactly 2 messages: system prompt + user prompt
4. Exits and destroys all state

That's fine for one-off queries. It's terrible for the iterative, exploratory workflow that makes AI actually useful.

---

## The Proposal

### Phase 1: Interactive REPL Mode (`--chat`)

Add a `--chat` flag that launches an interactive session:

```bash
az-ai --chat
```

```text
Azure OpenAI CLI -- Interactive Mode (gpt-4o)
Type /help for commands, /exit to quit.

You: Write a Python function to parse CSV files
AI: Here's a function that reads CSV files using the csv module...

You: Now add error handling to it
AI: Sure, here's the updated version with try/except blocks...

You: Make it async
AI: Here's the async version using aiofiles...
```

**Implementation:**

- Simple `while(true)` loop around `Console.ReadLine()`
- Maintain a `List<ChatMessage>` that grows with each exchange
- Reuse the same `ChatClient` instance (no re-authentication per turn)
- Support slash commands: `/exit`, `/clear` (reset context), `/model <name>`, `/help`

### Phase 2: Session Persistence (`--continue`)

Save conversation history to disk so users can resume later:

```bash
az-ai --chat                    # starts a new session
az-ai --continue                # resumes the last session
az-ai --continue <session-id>   # resumes a specific session
az-ai --sessions                # lists saved sessions
```

**Storage:** `~/.azureopenai-cli/sessions/` directory, one JSON file per session:

```json
{
  "id": "2025-07-17-a3f2",
  "model": "gpt-4o",
  "created": "2025-07-17T10:30:00Z",
  "messages": [
    { "role": "system", "content": "You are a secure, concise CLI assistant." },
    { "role": "user", "content": "Write a Python CSV parser" },
    { "role": "assistant", "content": "..." },
    { "role": "user", "content": "Add error handling" },
    { "role": "assistant", "content": "..." }
  ]
}
```

### Phase 3: Context Window Management

As conversations grow, they'll exceed model context limits. Implement a sliding window:

1. Always keep the system prompt (message 0)
2. Always keep the last N exchanges (configurable, default 10)
3. For older messages, summarize them into a "conversation so far" block
4. Show the user a `/context` command that displays token usage

---

## Architectural Considerations

### Docker Constraint

The interactive mode must work inside Docker. The Makefile `run` target currently uses `docker run --rm`, which works for single commands but needs `-it` (interactive TTY) for chat mode:

```makefile
chat:
	@docker run --rm -it --env-file .env $(FULL_IMAGE) --chat
```

The container stays alive for the duration of the chat session. This is fine -- it's how interactive Docker tools work (`docker run -it python` does the same thing).

### Token Counting

To manage context windows properly, the tool will eventually need token counting. For Phase 1, a simple character-based estimate (4 chars ≈ 1 token) is sufficient. A proper tokenizer can come later.

### Ctrl+C Handling

The REPL must handle `SIGINT` (Ctrl+C) gracefully:

- During streaming: cancel the current response, stay in the loop
- At the prompt: exit cleanly (or require double Ctrl+C)

---

## Why This Is P0

This is what separates a "query tool" from a "thinking partner." The entire value proposition of AI is iterative refinement -- and this tool currently makes that impossible.

**The competitive gap is glaring:**

- `aichat` has full REPL mode with session management
- `sgpt` has `--repl` mode  
- `mods` supports conversation continuations
- ChatGPT's web UI is the gold standard here

Without this feature, we're asking users to do their exploratory work in ChatGPT's browser and only use our CLI for one-off commands. That's not a tool someone loves -- that's a tool someone tolerates until they forget it exists.

---

## Exit Criteria

### Phase 1

- [ ] `az-ai --chat` launches an interactive REPL
- [ ] Conversation context is maintained across turns
- [ ] `/exit`, `/clear`, `/help`, `/model` slash commands work
- [ ] Ctrl+C during streaming cancels response without exiting
- [ ] Streaming works identically to single-shot mode (token-by-token output)
- [ ] `Makefile` has a `chat` target with `-it` flags

### Phase 2

- [ ] Sessions are auto-saved to `~/.azureopenai-cli/sessions/`
- [ ] `--continue` resumes the most recent session
- [ ] `--sessions` lists all saved sessions with timestamps
- [ ] Sessions can be deleted with `--delete-session <id>`

### Phase 3

- [ ] `/context` shows current token usage and capacity
- [ ] Conversations exceeding context window are automatically trimmed
- [ ] System prompt is always preserved
