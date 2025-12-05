# Interactive REPL Mode for azure-openai-cli

## Title
Interactive REPL Mode (chat-like session) for the CLI

## Overview
Add an interactive Read–Eval–Print Loop (REPL) mode to the azure-openai-cli that lets users hold multi-turn conversations with an OpenAI model from the terminal. Instead of invoking the CLI for individual one-off requests, users can enter a persistent session that preserves conversation context, supports streaming responses, quick configuration changes (model, temperature, system prompt), and exporting/importing session histories. This improves developer productivity for experimentation, prompt engineering, and iterative testing.

## User Story
As a developer or operator using azure-openai-cli,  
I want to open an interactive session in my terminal where I can ask follow-up questions, tweak parameters, and see streaming responses,  
so that I can iterate rapidly on prompts, debug behaviors, and prototype conversational interactions without repeatedly invoking separate CLI commands.

## Acceptance Criteria
- A new CLI subcommand (e.g., `azopenai chat` or `azopenai repl`) launches an interactive session with a single command.
- The session displays a prompt (e.g., `User: `) and supports multi-line input (via an explicit delimiter or multi-line editing).
- User messages and model replies are stored in session memory and used as context for subsequent messages.
- By default, the session uses configured CLI defaults (API key, endpoint, deployment/model); flags override defaults (e.g., `--model`, `--temperature`).
- Streaming responses are supported when the underlying API/deployment supports it; streaming shows tokens as they arrive.
- Users can set or update the system prompt during the session using a slash command (e.g., `/system Set you are a helpful assistant`).
- Users can view recent message history (e.g., `/history` shows last N turns) and clear session memory (`/clear`).
- Users can export the full session (messages + metadata: model, temperature, timestamps) to JSON or markdown (`/export session.json`) and import a session (`/import session.json`).
- A help command (`/help`) lists available slash commands and shortcuts.
- The session exits cleanly with `Ctrl+D`, `exit`, or `/quit`. Non-zero exit codes are used only for errors.
- Input validation and user-friendly error messages are provided for network/API failures, authentication issues, and model errors.
- Tests (unit or integration) cover command parsing for slash commands and basic session lifecycle behaviors.

## Implementation Notes
- Add a new top-level command in the CLI command registry (suggested names: `chat`, `repl`, or `interactive`).
- Use a minimal, well-supported line-editing/prompt library that supports multi-line input and history (e.g., readline or prompt-toolkit). Prefer reusing existing dependencies used elsewhere in the repo to avoid adding new runtime deps.
- Session state model: maintain an in-memory ordered list of messages with structure {role: system|user|assistant, content, timestamp}. Consider an optional cap (by tokens or turns) and warn when near limits.
- Streaming: reuse existing request/response code paths where possible; implement a streaming handler that prints tokens as received and appends the final assistant message to session history.
- Slash commands: parse lines starting with `/` locally and do not send them to the model. Implement a small command dispatcher with clear error messages for unknown commands.
- Export format: JSON with top-level keys: metadata (model, deployment, config), messages (list), created_at. Provide an alternate markdown export for human-readable sharing.
- Import semantics: default to replacing current session; provide a `/merge` flag for merging imported session messages into current session.
- Security: ensure exported sessions never include raw API keys. Provide an explicit warning in the UI and documentation about sensitive content in exported files.
- Truncation strategy: document and implement a predictable truncation policy (e.g., drop oldest turns) when context limits are reached; allow users to configure or clear history.

## Dependencies or Risks
- Cost: Long multi-turn sessions increase token usage and costs. Warn users and provide session size indicators.
- Security: Session exports may contain sensitive user content. Explicitly warn users and provide guidance to avoid storing secrets.
- Model context limits: Sessions that exceed model context length will require truncation; this can change model behavior. Document truncation policy and provide warnings.
- Platform differences: Streaming and multi-line input behavior may vary between Windows and Unix terminals. Test across major OSes.
- Backwards compatibility: Ensure the new command and flags do not conflict with existing subcommands or configuration options.
- Dependency footprint: Adding a terminal/prompt library increases maintenance; prefer existing repo libraries or keep the feature optional.
- Testing: Live integration tests should mock OpenAI/Azure endpoints to avoid cost and flakiness.
