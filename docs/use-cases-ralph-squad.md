# Use Cases: Ralph Mode & Squad/Persona System

> **Binary:** `az-ai`
> **Required environment variables:** `AZUREOPENAIENDPOINT`, `AZUREOPENAIAPI`,
> `AZUREOPENAIMODEL` — see [`prerequisites.md`](prerequisites.md).

---

## Part 1 — Ralph Mode

Ralph mode (autonomous Wiggum loop) is an **autonomous self-correcting loop**. It runs an agentic task, optionally validates the result with
an external command, and if validation fails, feeds the error output back into
the next iteration — repeating until the task passes or the iteration budget is
exhausted.

### How the Loop Works

Ralph runs the agent, then (optionally) runs your `--validate` command. Exit 0 → done. Non-zero → feed stderr back as context, retry. Bounded by `--max-iterations`.

Each iteration is **stateless** — the full task prompt plus accumulated error
context is sent fresh every time, so the model never inherits a corrupted
conversation history.

---

### 1. Ralph Mode Activation (`--ralph`)

The `--ralph` flag enables the loop. Ralph automatically implies agentic mode
(`--agent`), so the model has full access to tools (shell, file I/O, web,
datetime).

**Basic usage — ask Ralph to complete a coding task:**

```bash
az-ai --ralph "Create a Python function that parses ISO 8601 dates with timezone support"
```

**What happens:**

1. Ralph starts iteration 1/10 (default max).
2. The agent writes the code, possibly creates a file.
3. With no `--validate` command, Ralph checks only the agent's exit code.
4. Exit 0 → success; exit ≠ 0 → Ralph re-attempts with error context.

```
🔁 Ralph mode — Wiggum loop active
   Max iterations: 10

━━━ Iteration 1/10 ━━━
📝 Agent response (847 chars)

✅ Ralph complete after 1 iteration(s)
```

---

### 2. Validation Command (`--validate`)

The `--validate` flag provides an external command that Ralph runs **after each
agent iteration** to decide pass/fail. If the command exits 0, Ralph declares
success. If it exits non-zero, Ralph feeds `stdout` + `stderr` back as context
for the next attempt.

**Run tests after each iteration:**

```bash
# .NET project
az-ai --ralph --validate "dotnet test" \
  "Fix the failing test in UserServiceTests.cs"

# Python project
az-ai --ralph --validate "python -m pytest tests/ -x" \
  "Add input validation to the parse_config function"

# Node.js project
az-ai --ralph --validate "npm test" \
  "Refactor the auth middleware to support JWT refresh tokens"

# Go project
az-ai --ralph --validate "go test ./..." \
  "Fix the race condition in the connection pool"

# Linting pass
az-ai --ralph --validate "eslint src/ --max-warnings 0" \
  "Fix all ESLint warnings in the src directory"

# Docker build
az-ai --ralph --validate "docker build -t myapp ." \
  "Fix the Dockerfile so it builds successfully"
```

**Validation flow (example with `dotnet test`):**

```
🔁 Ralph mode — Wiggum loop active
   Validate: dotnet test
   Max iterations: 10

━━━ Iteration 1/10 ━━━
📝 Agent response (1423 chars)
🔍 Validating: dotnet test... ❌ FAILED (exit 1)

━━━ Iteration 2/10 ━━━
📝 Agent response (2105 chars)
🔍 Validating: dotnet test... ✅ PASSED

✅ Ralph complete after 2 iteration(s)
```

The validation command is run via `/bin/sh -c "<command>"`, so standard shell
syntax (pipes, `&&`, etc.) works:

```bash
az-ai --ralph --validate "dotnet build && dotnet test --no-build" \
  "Fix the build errors in the Data project"
```

---

### 3. Task File Input (`--task-file`)

For complex task descriptions, write the requirements in a file and pass it
with `--task-file`. The file contents replace the inline prompt.

```bash
az-ai --ralph --validate "npm test" --task-file requirements.md
```

**Sample task file (`requirements.md`):**

```markdown
# Feature: Rate Limiting Middleware

## Requirements

1. Create an Express middleware in `src/middleware/rate-limit.ts`
2. Use a sliding-window algorithm (not fixed window)
3. Configuration:
   - `windowMs`: window duration in milliseconds (default: 60000)
   - `maxRequests`: max requests per window (default: 100)
   - `keyGenerator`: function to derive the client key (default: `req.ip`)
4. Return HTTP 429 with a `Retry-After` header when limit is exceeded
5. Store counters in memory (no external dependency)
6. Export the middleware as the default export

## Tests Required

- Should allow requests under the limit
- Should block requests over the limit with 429
- Should reset after the window expires
- Should track limits per client key independently
```

`--task-file` also works without `--ralph` for standard or agentic mode:

```bash
# Standard one-shot (no ralph loop)
az-ai --task-file design-doc-prompt.md

# Agentic mode with task file
az-ai --agent --task-file migration-steps.md
```

---

### 4. Max Iterations Control (`--max-iterations`)

Controls how many times Ralph will retry before giving up. Accepts values from
**1 to 50** (default: **10**).

```bash
# Quick fix — give Ralph only 3 tries
az-ai --ralph --max-iterations 3 --validate "cargo test" \
  "Fix the lifetime error in lib.rs"

# Complex migration — allow more iterations
az-ai --ralph --max-iterations 25 --validate "dotnet build" \
  --task-file migration-plan.md

# Single-shot with validation (try once, pass or fail)
az-ai --ralph --max-iterations 1 --validate "make check" \
  "Run the full check suite and report status"
```

**When iterations are exhausted:**

```
━━━ Iteration 5/5 ━━━
📝 Agent response (1893 chars)
🔍 Validating: dotnet test... ❌ FAILED (exit 1)

❌ Ralph loop exhausted 5 iterations without passing validation.
```

Ralph exits with code **1** and writes the full iteration log to `.ralph-log`.

---

### 5. Ralph Log (`.ralph-log`)

Every Ralph run writes a `.ralph-log` file in the current directory. It is a
Markdown-formatted record of every iteration: the prompt sent, the agent's exit
code, a truncated response, and (if `--validate` is set) the validation result.

**Review the log after a run:**

```bash
cat .ralph-log
```

**Sample `.ralph-log` content:**

```markdown
# Ralph Loop Log

## Iteration 1
**Prompt:** Fix the NullReferenceException in UserService.cs
**Agent exit:** 0
**Response:** I found the issue in `UserService.cs` at line 42. The `user` variable can be null when...

**Validation:** FAILED (exit 1)
```
Validation output (truncated at 2 000 chars):
```
  Failed UserServiceTests.GetUser_ReturnsNull_WhenNotFound
  NullReferenceException: Object reference not set to an instance...
```

```markdown
## Iteration 2
**Prompt:** Fix the NullReferenceException in UserService.cs

[Iteration 1 — validation FAILED]
[Validation command: dotnet test]
[Exit code: 1]
[Validation output]:
  Failed UserServiceTests.GetUser_ReturnsNull_WhenNotFound...

**Agent exit:** 0
**Response:** Added a null check on line 42 and updated the test to verify the fix...
```

The log is **overwritten** on each new Ralph run (not appended), so it always
reflects the most recent session. Add `.ralph-log` to `.gitignore` if you
don't want it checked in.

---

### 6. Real Ralph Scenarios

#### Fix a Failing Test

```bash
az-ai --ralph --validate "dotnet test" \
  "Fix the NullReferenceException in UserService.cs — the GetUser method
   returns null when the database query finds no match but the caller
   doesn't check for null."
```

Ralph reads the test output, locates the exception, adds null guards, and
re-runs `dotnet test` until it passes.

#### Build a Feature from a Spec

```bash
az-ai --ralph --validate "npm run build && npm test" \
  --task-file feature-spec.md
```

The task file describes the full feature. Ralph implements code, runs the
build + tests, and loops until both pass.

#### Fix a Docker Build

```bash
az-ai --ralph --validate "docker build -t myapp ." \
  "Fix the Dockerfile so it builds successfully — currently failing
   on the COPY step because the build context is wrong."
```

Ralph reads the Dockerfile, fixes the `COPY` path, and validates with
`docker build` until the image builds cleanly.

#### Self-Correcting Migration

```bash
az-ai --ralph --max-iterations 20 \
  --validate "python manage.py migrate --check && python manage.py test" \
  "Create a Django migration that adds a 'status' enum field to the
   Order model with values: pending, confirmed, shipped, delivered.
   Include a data migration that sets existing orders to 'confirmed'."
```

#### Lint-Clean Refactor

```bash
az-ai --ralph --validate "npx eslint src/ --max-warnings 0" \
  "Refactor src/utils/helpers.js — split it into separate modules
   under src/utils/ with one function per file. Keep all exports
   backward-compatible."
```

---

## Part 2 — Squad / Persona System

The Squad system gives `az-ai` **persistent, role-specific personas** — each
with its own system prompt, tool set, and accumulated memory. Think of it as a
team of specialists that live in your repo.

---

### 7. Squad Initialization (`--squad-init`)

Run this once in any project directory to scaffold the Squad:

```bash
az-ai --squad-init
```

**Output:**

```
✅ Squad initialized! Created .squad.json and .squad/ directory.
   Edit .squad.json to customize your personas.
   Use --persona <name> to select a persona.
```

**Files created:**

```
your-project/
├── .squad.json            # Persona definitions + routing rules
└── .squad/
    ├── history/           # Per-persona memory (auto-managed)
    ├── decisions.md       # Shared decision log
    └── README.md          # Explains the .squad/ directory
```

If you run `--squad-init` again in a directory that already has `.squad.json`,
it's a no-op:

```
Squad already initialized (.squad.json exists).
```

**Commit `.squad.json` and `.squad/` to version control.** Anyone who clones
gets the full team with all accumulated knowledge.

---

### 8. Persona Selection (`--persona <name>`)

Use `--persona` to invoke a specific persona. The persona's system prompt
replaces the default, and its tool set is applied. Persona mode automatically
enables agentic mode.

#### Coder — Software Engineer

```bash
az-ai --persona coder "Implement a binary search in Python with type hints"
```

```
🎭 Persona: coder (Software Engineer)
```

The coder writes clean, tested, production-ready code. It follows existing
project conventions and prefers small, focused changes.

#### Reviewer — Code Reviewer

```bash
az-ai --persona reviewer "Review this PR for security issues"
```

```
🎭 Persona: reviewer (Code Reviewer)
```

The reviewer focuses on bugs, logic errors, security vulnerabilities, and
performance issues. It cites specific line numbers and suggests fixes. It
ignores style/formatting unless it hides a bug.

#### Architect — System Architect

```bash
az-ai --persona architect "Design a microservice for user authentication"
```

```
🎭 Persona: architect (System Architect)
```

The architect thinks about separation of concerns, extensibility, scalability,
and operational complexity. It proposes designs with diagrams and documents
trade-offs. Important decisions are logged to `.squad/decisions.md`.

#### Writer — Technical Writer

```bash
az-ai --persona writer "Write docs for the /api/v2/users endpoint"
```

```
🎭 Persona: writer (Technical Writer)
```

The writer creates documentation that is accurate (verified against actual
code), scannable (headers, tables, code blocks), and complete (happy path +
edge cases).

#### Security — Security Auditor

```bash
az-ai --persona security "Audit this codebase for vulnerabilities"
```

```
🎭 Persona: security (Security Auditor)
```

The security auditor systematically checks for injection vulnerabilities,
auth bypasses, data exposure, dependency vulnerabilities, and container
security. Findings are classified by severity
(Critical / High / Medium / Low) with remediation steps.

---

### 9. Auto-Routing (`--persona auto`)

When you don't know which persona to use, let the Squad Coordinator decide.
It scores each routing rule's keywords against your prompt and picks the
best match.

```bash
az-ai --persona auto "Fix the authentication bug in the login handler"
```

```
🎭 Auto-routed to: coder (Software Engineer)
```

**How keyword matching works:**

The routing rules in `.squad.json` contain comma-separated keyword patterns.
The coordinator lowercases both the prompt and the patterns, then counts how
many keywords from each rule appear in the prompt. The rule with the highest
match count wins.

**Default routing rules and their keywords:**

| Persona      | Keywords                                                 |
|--------------|----------------------------------------------------------|
| `coder`      | code, implement, build, fix, refactor, feature, bug      |
| `reviewer`   | review, audit, check, inspect, quality                   |
| `architect`  | design, architecture, system, scale, pattern, migration   |
| `writer`     | document, readme, docs, guide, tutorial, changelog        |
| `security`   | security, vulnerability, cve, owasp, harden, credential, secret |

**Examples of auto-routing in action:**

```bash
# Matches "fix" + "bug" → coder (2 keyword hits)
az-ai --persona auto "Fix the bug in the payment processor"

# Matches "review" + "check" → reviewer (2 keyword hits)
az-ai --persona auto "Review and check the new middleware for issues"

# Matches "design" + "architecture" + "scale" → architect (3 keyword hits)
az-ai --persona auto "Design the architecture for a system that needs to scale"

# Matches "document" + "docs" → writer (2 keyword hits)
az-ai --persona auto "Document the new API — update the docs folder"

# Matches "security" + "vulnerability" → security (2 keyword hits)
az-ai --persona auto "Check for security vulnerabilities in our dependencies"

# No keyword matches → falls back to first persona (coder)
az-ai --persona auto "Tell me a joke"
```

If no routing rule matches (zero keyword hits in all rules), the coordinator
falls back to the **first persona** in the list (by default, `coder`).

---

### 10. List Personas (`--personas`)

List all configured personas without running a task:

```bash
az-ai --personas
```

**Output:**

```
Squad: Default Squad
  AI team for your project. Customize personas in .squad.json.

  coder        Software Engineer    Writes clean, tested, production-ready code.
  reviewer     Code Reviewer        Reviews code for bugs, security issues, and best practices.
  architect    System Architect     Designs systems, evaluates trade-offs, makes structural decisions.
  writer       Technical Writer     Creates clear documentation, guides, and content.
  security     Security Auditor     Identifies vulnerabilities, hardens defenses, reviews for compliance.
```

This requires `.squad.json` to exist. If it doesn't:

```
No .squad.json found. Run --squad-init first.
```

---

### 11. Persona Memory (`.squad/history/`)

Each persona accumulates knowledge across sessions. Memory is stored as
Markdown files in `.squad/history/` — one file per persona.

**How it works:**

1. When you invoke `--persona coder`, the CLI reads `.squad/history/coder.md`.
2. The history is appended to the persona's system prompt under a
   `## Your Memory (from previous sessions)` header.
3. After the session, key learnings are appended to the history file.

**Browsing persona memory:**

```bash
# See what the coder has learned about your project
cat .squad/history/coder.md

# See the reviewer's notes
cat .squad/history/reviewer.md
```

**Sample `.squad/history/coder.md`:**

```markdown
## Session — 2025-01-15 09:23 UTC
**Task:** Implement binary search in Python with type hints
**Result:** Created `src/search.py` with generic `binary_search[T]` function.
Used `typing.Protocol` for the `Comparable` constraint.

## Session — 2025-01-16 14:07 UTC
**Task:** Add pagination to the /api/users endpoint
**Result:** Added `offset` and `limit` query params. Default limit is 50, max
is 200. Updated OpenAPI spec.
```

**Memory is capped at 32 KB per persona.** When the history file exceeds this
limit, the oldest entries are truncated (the tail — most recent learnings — is
kept).

**Shared decisions log:**

Architectural and cross-cutting decisions are logged to `.squad/decisions.md`,
accessible to all personas:

```bash
cat .squad/decisions.md
```

```markdown
# Squad Decisions

Shared decision log across all personas.

### 2025-01-15 10:45 UTC — architect
Decided to use PostgreSQL over MongoDB for the user service. Rationale:
relational model fits the user-role-permission hierarchy better; JOIN
performance matters more than document flexibility for our access patterns.
```

---

### 12. Custom Personas

Edit `.squad.json` to add, remove, or customize personas. Here's the full
structure of a persona entry:

```json
{
  "team": {
    "name": "My Project Squad",
    "description": "Custom AI team for the Acme project."
  },
  "personas": [
    {
      "name": "devops",
      "role": "DevOps Engineer",
      "description": "Manages CI/CD pipelines, infrastructure, and deployments.",
      "system_prompt": "You are a DevOps engineer. Focus on: (1) CI/CD pipeline reliability, (2) infrastructure as code, (3) monitoring and alerting, (4) container orchestration. Use Terraform/Bicep for infra. Prefer declarative over imperative. Always consider rollback strategies.",
      "tools": ["shell", "file", "web"],
      "model": null
    },
    {
      "name": "data-scientist",
      "role": "Data Scientist",
      "description": "Analyzes data, builds models, creates visualizations.",
      "system_prompt": "You are a data scientist. Focus on: (1) data cleaning and validation, (2) statistical rigor, (3) reproducible analysis, (4) clear visualizations. Use pandas, numpy, and scikit-learn. Always state assumptions. Show your work — include the reasoning behind model choices.",
      "tools": ["shell", "file"],
      "model": null
    },
    {
      "name": "dba",
      "role": "Database Administrator",
      "description": "Designs schemas, optimizes queries, manages migrations.",
      "system_prompt": "You are a database administrator. Focus on: (1) schema normalization, (2) index strategy, (3) query performance (EXPLAIN ANALYZE everything), (4) migration safety. Never suggest migrations that lock tables for more than a few seconds in production. Always consider backward compatibility.",
      "tools": ["shell", "file"],
      "model": null
    }
  ],
  "routing": [
    {
      "pattern": "deploy,pipeline,ci,cd,terraform,docker,kubernetes,infra",
      "persona": "devops",
      "description": "Infrastructure and deployment tasks"
    },
    {
      "pattern": "data,analysis,model,predict,visualize,pandas,statistics",
      "persona": "data-scientist",
      "description": "Data analysis and ML tasks"
    },
    {
      "pattern": "database,schema,query,index,migration,sql,postgres",
      "persona": "dba",
      "description": "Database tasks"
    }
  ]
}
```

**Key points:**

- **`name`** — what you pass to `--persona`. Case-insensitive.
- **`role`** — displayed in the `🎭 Persona:` banner.
- **`system_prompt`** — replaces the default system prompt entirely.
- **`tools`** — restricts which tools the persona can use (options: `shell`,
  `file`, `web`, `datetime`).
- **`model`** — optionally override the deployment model for this persona
  (set to `null` to use the default `AZUREOPENAIMODEL`).
- **`routing.pattern`** — comma-separated keywords for auto-routing.

After editing, use the new persona immediately:

```bash
az-ai --persona devops "Set up a GitHub Actions workflow for this repo"
az-ai --persona data-scientist "Analyze the CSV in data/sales.csv and find trends"
az-ai --persona dba "Optimize the slow query in reports/monthly.sql"
```

---

### 13. Combined Scenarios

#### Team Code Review Workflow

Use multiple personas in sequence to simulate a full team review:

```bash
# Step 1: Architect reviews the design
az-ai --persona architect \
  "Review the proposed microservice split in docs/architecture.md.
   Log your decision about the service boundaries."

# Step 2: Coder implements the changes
az-ai --persona coder \
  "Implement the UserService as a standalone service based on the
   architecture decision in .squad/decisions.md"

# Step 3: Reviewer checks the implementation
az-ai --persona reviewer \
  "Review the new UserService implementation in src/services/user/
   for bugs, error handling, and edge cases"

# Step 4: Security audits the result
az-ai --persona security \
  "Audit src/services/user/ for authentication bypasses,
   injection vulnerabilities, and secret exposure"
```

Each persona's memory accumulates across these sessions, so the reviewer
knows what the coder built, and the security auditor knows the architecture
context.

#### Ralph + Persona: Self-Correcting Specialist

Combine Ralph mode with a persona for a self-correcting loop run by a
specialist:

```bash
# Security auditor that auto-fixes what it finds
az-ai --ralph --persona security \
  --validate "npm audit --audit-level=high" \
  "Fix all high-severity vulnerabilities in this project"

# Coder persona with Ralph validation
az-ai --ralph --persona coder \
  --validate "dotnet test" \
  "Implement the repository pattern for the Order entity"

# DevOps persona fixing a CI pipeline (requires custom persona from §12)
az-ai --ralph --persona devops \
  --validate "act -j build" \
  "Fix the GitHub Actions workflow — the build job is failing"
```

#### Auto-Routed Complex Request

For prompts that span multiple concerns, auto-routing picks the dominant
persona:

```bash
# "design" + "architecture" + "scale" → architect (3 hits)
az-ai --persona auto \
  "Design the architecture for a notification system that needs
   to scale to 10M users. Include the database schema and API design."

# "implement" + "feature" → coder (2 hits)
az-ai --persona auto \
  "Implement the feature described in docs/notifications.md"

# "review" + "security" → tie between reviewer(1) and security(1),
# highest-scoring rule wins; if tied, first match wins → reviewer
az-ai --persona auto \
  "Review this code for security problems"
```

#### Full Project Lifecycle with Auto-Routing

```bash
# Planning phase — routes to architect
az-ai --persona auto "Design the system architecture for a URL shortener"

# Implementation — routes to coder
az-ai --persona auto "Implement the URL shortener based on the design"

# Validation — coder + ralph for self-correction
az-ai --ralph --persona coder --validate "go test ./..." \
  "Implement and test the URL shortener redirect logic"

# Documentation — routes to writer
az-ai --persona auto "Document the URL shortener API endpoints"

# Security review — routes to security
az-ai --persona auto "Check the URL shortener for security vulnerabilities"

# Final review — routes to reviewer
az-ai --persona auto "Review the complete URL shortener implementation for quality"
```

---

## Quick Reference

| Flag                      | Description                                      | Requires         |
|---------------------------|--------------------------------------------------|------------------|
| `--ralph`                 | Enable autonomous self-correcting loop            | —                |
| `--validate <cmd>`        | External validation command (exit 0 = pass)       | `--ralph`        |
| `--task-file <path>`      | Read task prompt from a file                      | —                |
| `--max-iterations <1-50>` | Max Ralph loop iterations (default: 10)           | `--ralph`        |
| `--squad-init`            | Initialize `.squad.json` and `.squad/` directory  | —                |
| `--persona <name>`        | Use a named persona (enables agentic mode)        | `.squad.json`    |
| `--persona auto`          | Auto-route to best persona by keyword match       | `.squad.json`    |
| `--personas`              | List all available personas                       | `.squad.json`    |
