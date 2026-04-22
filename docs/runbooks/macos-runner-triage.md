# macOS Runner Triage

> When the macOS leg of the release matrix queues for an hour and nobody
> knows whether to wait, rerun, or rip the cord out. One screen. One
> decision tree. One link per escalation path.

**Audience:** release manager on-call during a `v*` tag push.
**Scope:** the `osx-arm64` (`macos-14`) leg of `build-binaries-v2` in
`.github/workflows/release.yml`. Generalizes to any future macOS leg
(e.g., if `macos-15` is added).
**Non-goals:** macOS *code* issues (AOT failures, codesign, notarization).
Those are bugs, not infra. This runbook is for when the leg never picks
up a runner in the first place, or picks one up and dies.

---

## 1. Current macOS surface (2026-04-22, post-v2.0.4)

| Leg           | Runner label | Workflow file            | Status |
|---------------|--------------|--------------------------|--------|
| `osx-arm64`   | `macos-14`   | `release.yml` line ~231  | ✅ shipped |
| ~~`osx-x64`~~ | ~~`macos-13`~~ | --                      | ❌ cut in v2.0.4 ([commit `afa95fd`](https://github.com/SchwartzKamel/azure-openai-cli/commit/afa95fd)) |

> **Source of truth:** `.github/workflows/release.yml` `build-binaries-v2.strategy.matrix`.
> If you are reading this more than ~6 months after 2026-04-22, re-check
> the matrix before trusting this table.

Local-dev cross-builds for `osx-x64` still work via `make
publish-osx-x64`; this runbook is strictly about **shipped** release-matrix
legs.

---

## 2. Detection -- is the macOS leg actually wedged?

```bash
# What is the current release run doing?
gh run list --event push --limit 3
gh run view <run-id>

# Drill into the job list; filter for macos legs that are still queued.
gh run view <run-id> --json jobs \
  --jq '.jobs[] | select(.name|contains("osx")) | {name,status,conclusion,startedAt}'
```

You have a wedged macOS leg when **all** of the following are true:

1. The linux-x64 leg of the same run **succeeded** (or is well past its
   "Set up job" step).
2. The osx-arm64 leg has been in `queued` for **> 30 min** with no
   `startedAt` populated.
3. [GitHub Actions status page](https://www.githubstatus.com/) shows
   either *Operational* or partial degradation on `Actions`.
4. Issue search for [`"macos-14 queued"`](https://github.com/orgs/community/discussions?discussions_q=macos-14+queued)
   or [`"macos-14 runner pool"`](https://github.com/orgs/community/discussions?discussions_q=macos-14+runner+pool)
   is quiet (no fresh complaints) -- **or** loud (others affected; it's
   not just us).

If a macOS leg *started* and then failed, that's a different problem;
see §3 leaf 3.

---

## 3. Decision tree

```
Is the release run failing or just slow?
│
├─ All legs still queued, no linux leg started ──▶ §3.2  GitHub Actions outage
│
├─ linux-x64 + win-x64 succeeded, only macOS queued ──▶ §3.1  Runner pool backlog
│
└─ macOS leg FAILED (not queued) ──▶ §3.3  Rerun once, investigate on second failure
```

### 3.1 -- Linux + Windows green, macOS queued > 30 min

**Diagnosis.** GitHub-hosted `macos-14` runner pool backlog. This is the
same class of failure that blocked v2.0.2 and v2.0.3 on `macos-13`
before the cut. It is **transient** and **not our code**.

**Playbook.**

1. **Wait up to 60 min from `createdAt`.** Most backlogs clear on their
   own. Do not cancel yet.
2. At 60 min, post a comment on the tag's release tracking issue (or
   `#release` channel): *"macos-14 queued 60+ min on run `<id>`, holding
   before rerun."* This is how Frank knows you're on it.
3. At 90 min, **rerun failed/cancelled legs only**:
   ```bash
   # DO NOT use `gh workflow run release.yml --ref <tag>` on an old tag --
   # workflow_dispatch needs the workflow_dispatch trigger to exist at
   # the *ref's* commit, not HEAD. On a tag cut before that trigger was
   # added you get HTTP 422. This is the "workflow_dispatch trap."
   gh run rerun <run-id> --failed --repo SchwartzKamel/azure-openai-cli
   ```
   `--failed` reschedules only cancelled/failed legs and preserves the
   green ones -- their artifacts are already uploaded and won't be
   rebuilt.
4. If the rerun also queues > 60 min, escalate (§4).

### 3.2 -- All legs queued, no job started

GitHub Actions is having a bad day. Check
[githubstatus.com](https://www.githubstatus.com/) and
[@githubstatus](https://twitter.com/githubstatus). Nothing to do but
wait. Don't rerun -- you'll just add to the queue.

### 3.3 -- macOS leg started and failed

Different failure class. The runner picked the job up; something in our
pipeline broke.

1. **Read the logs.** `gh run view <run-id> --log-failed` or download
   with `gh run download <run-id> --name <artifact>`.
2. **Rerun once.** `gh run rerun <run-id> --failed`. Flaky macOS runners
   are real (network drops during `dotnet restore`, disk pressure
   during AOT link).
3. **If it fails a second time with the same error, it's not flake.**
   Don't rerun a third time -- you're burning minutes. Open an issue,
   link the two runs, and either:
   - Fix-forward (bump csproj, tag next patch), or
   - If the platform is persistently unreliable, consider §5.

---

## 4. Escalation

In order of escalation:

1. **GitHub Actions status page** -- <https://www.githubstatus.com/>.
   If `Actions` is degraded or in incident, wait.
2. **GitHub Community discussion search** -- look for others hitting the
   same backlog; if it's a widespread event, a fix is usually hours away.
3. **GitHub Support ticket** -- <https://support.github.com/>. Include
   the run URL and the specific job that has been queued. Support can
   see runner-pool capacity you cannot. Do this after 2+ hours of
   unexplained backlog.
4. **`#release` / on-call rotation** -- loop in Frank (SRE) if the
   backlog is blocking a time-sensitive release (CVE fix, security
   advisory). Otherwise, hold and let the pool clear.

---

## 5. When to cut a RID from the matrix

The canonical example is **`osx-x64` / `macos-13`**. Timeline:

- **v2.0.1 (2026-04-21):** `macos-13` queued ~30 min during release,
  partial backlog, run limped through.
- **v2.0.2 (2026-04-21):** `macos-13` queued **~82 min**, ran into the
  `workflow_dispatch` 422 trap on recovery. Documented in
  `docs/launch/v2.0.2-publish-handoff.md`.
- **v2.0.3 (2026-04-22):** `macos-13` queued **~9 hours**. Same backlog.
- **v2.0.4 (2026-04-22):** **cut.** [`afa95fd`](https://github.com/SchwartzKamel/azure-openai-cli/commit/afa95fd)
  removed `osx-x64` from `build-binaries-v2.strategy.matrix`. Release
  went green on the first try. Intel-Mac users get Rosetta 2 from the
  `osx-arm64` artifact or `linux/amd64` from the GHCR image.

### Heuristic (the "two cycles" rule)

If a platform's runner pool backlogs **two consecutive release cycles**
(or one cycle > 6h) and the platform has a viable substitution path for
end users, **cut it**. Don't wait for the third incident. The PR diff
from v2.0.4 is the template:

```diff
 matrix:
   include:
     - rid: linux-x64
     - rid: linux-musl-x64
-    - rid: osx-x64
     - rid: osx-arm64
     - rid: win-x64
```

Companion edits when cutting a RID:
- `CHANGELOG.md` -- explicit `Removed` section with the substitution
  paths (Rosetta, Docker, source build). See [v2.0.4 entry](../../CHANGELOG.md).
- `docs/verifying-releases.md` -- remove the RID from multi-platform
  verify loops.
- `Makefile` -- leave the local-dev `publish-<rid>` target; contributors
  still want it. Add a note that it's local-dev only.
- `packaging/homebrew/Formula/az-ai.rb`, `packaging/nix/flake.nix`,
  `packaging/scoop/az-ai.json` -- drop the RID's hash block.
- This runbook -- update §1 table and move the cut RID to the
  strikethrough row.

---

## 6. Prior art (historical postmortems)

The consolidated knowledge below came from reading these in sequence.
Each is marked **RESOLVED** as of v2.0.4; read them for context, not
operational guidance.

- **[`docs/launch/v2.0.1-release-attempt-diagnostic.md`](../launch/v2.0.1-release-attempt-diagnostic.md)**
  -- v2.0.1 failed on two defects: `win-x64` zip packaging (fixed via
  PowerShell `Compress-Archive`) and `macos-13` queueing (unresolved --
  see v2.0.2 postmortem).
- **[`docs/launch/v2.0.2-release-attempt-diagnostic.md`](../launch/v2.0.2-release-attempt-diagnostic.md)**
  -- v2.0.2 cleared the Dockerfile.v2 `--no-restore` regression but sat
  on a `macos-13` leg queued 82 min. Documented the `workflow_dispatch`
  HTTP 422 trap (dispatching against a tag cut before the trigger was
  added returns 422).
- **[`docs/launch/v2.0.2-publish-handoff.md`](../launch/v2.0.2-publish-handoff.md)**
  -- the recovery recipe: `gh run rerun <id> --failed` is the lever;
  `gh workflow run release.yml --ref <tag>` is not. Recipe is still
  valid; the specific incident is closed.
- **[`docs/launch/v2-tag-rehearsal-report.md`](../launch/v2-tag-rehearsal-report.md)**
  -- pre-tag rehearsal for v2.0.0 (linux-x64 only); flagged metadata
  mismatches that were cleaned up before the real v2.0.0 cut. Good
  reference for what a *clean* rehearsal looks like.

---

## 7. Retrospective question (quarterly)

At the end of each quarter, look back at `gh run list --workflow
release.yml` and answer:

> *Did any single macOS leg queue > 30 min on more than one release
> this quarter?*

If **yes** on the same platform twice, escalate to a matrix-cut
discussion (see §5). If **yes** but on different platforms (transient),
note it and move on. If **no**, close the question for the quarter.

> "You ever notice how every red release started with someone waiting
> on a runner that was never coming back? Yeah. You noticed."
