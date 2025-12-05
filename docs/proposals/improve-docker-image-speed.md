# Improve Docker image build speed and runtime size

1. Title
- Improve Docker image build speed and runtime size

2. Overview
- What: Reduce Docker image build time (especially cold builds in CI) and decrease final image size to speed deployment and lower resource usage.
- Why it matters: Faster builds shorten feedback loops for contributors and CI; smaller images reduce network transfer times, startup latency, and hosting costs (pulls on cloud runners/Azure). This proposal gives a scoped, testable plan for attackers (coding agents) to implement and validate improvements.

3. User Story
- As a developer or CI operator, I want Docker images for this project to build faster and be smaller so CI jobs complete more quickly, developers spend less time waiting, and deployments are faster and cheaper.

4. Acceptance Criteria
- Baseline recorded: A documented baseline build time (cold build without cache) and final image size for the current mainline Dockerfile.
- Build time improvement: Cold build time reduced by at least 25% compared to baseline, measured on the same runner/configuration (or convincingly explained if lower/higher target is adopted).
- Image size reduction: Final image size reduced by at least 30% compared to baseline OR brought under a specific target (e.g., under 350MB) — choose one target after baseline measurement.
- Functional parity: All existing automated tests that run against the container still pass. Key runtime behaviors (CLI commands, API endpoints, or other smoke checks) are validated inside the new image.
- Reproducible builds: Builds are deterministic enough that CI cache hits/rebuilds behave predictably; documented caching strategy in CI.
- CI integration: CI updated to use the improved Dockerfile and caching instructions (e.g., BuildKit cache or registry cache). CI run demonstrates the improved build times.
- Documentation: Add a short "How we optimized images" section in docs (or repo README) describing changes and how to reproduce measurements.

5. Implementation Notes (scoped, actionable hints)
- Phase A — Audit & Baseline
  - Record baseline: run and record docker build --no-cache (or equivalent CI job), measure wall-clock time and final `docker image ls` size. Save measurements in docs/proposals/docker-image-speed/benchmarks.md (or within PR).
  - Create a checklist of large layers and expensive steps by examining Dockerfile and image history (docker history or dive).
  - Add a .dockerignore if missing and ensure node_modules, .git, logs, etc. are excluded.

- Phase B — Low-risk, high-impact changes (apply first)
  - Use multi-stage builds to separate build-time dependencies from runtime artifacts. Keep only runtime artifacts in the final stage.
  - Choose a smaller base image for the final stage (e.g., distroless, slim, or busybox-based) if compatible — evaluate compatibility with required runtime glibc/libssl.
  - Reorder Dockerfile so that infrequently changing layers (OS deps, language runtime) are before frequently changing layers (application code) to maximize layer cache reuse.
  - Combine RUN commands where appropriate to reduce layers and clean package caches (apt-get clean, rm -rf /var/lib/apt/lists/*) in same RUN.
  - Use package manager flags to avoid installing recommended packages (e.g., apt-get --no-install-recommends).
  - Remove build-only dependencies from final image (e.g., build tools, compilers).
  - For pip/node/npm: install dependencies into a separate layer and copy only lockfiles first to leverage cache, then copy source.
  - Use compressed assets or prebuilt wheels/binaries where possible.

- Phase C — Build & CI improvements
  - Enable BuildKit in CI and use its advanced cache features (docker buildx cache, --cache-from/--cache-to).
  - Push intermediate caches/artifacts to a registry or CI cache between runs for faster cold builds on CI workers.
  - Consider using a shared base image maintained by the team (prebuilt image with OS and language/runtime) to avoid reinstalling OS-level packages in every build.
  - Optionally explore experimental alternatives: distroless, scratch, or language-specific minimal runtime images — only after validating compatibility.

- Phase D — Measurement & Validation
  - Implement automated benchmarks in CI that measure build time and image size and fail if regressions exceed thresholds.
  - Run full test suite inside the new image (or run smoke checks) to ensure runtime behavior unchanged.
  - Produce a PR with the changes and attach before/after metrics and a brief explanation.

6. Suggested Work Items (for a coding agent PR)
- Task 1: Create a branch and add benchmarking script(s) and baseline metrics file.
- Task 2: Add/Update .dockerignore.
- Task 3: Refactor Dockerfile into a multi-stage build (with comments).
- Task 4: Replace final base with a smaller image where compatible; remove build deps in final stage.
- Task 5: Update CI workflow to enable BuildKit and cache-from/cache-to (document secrets/config needed).
- Task 6: Add CI job to measure and publish image size & build time artifacts.
- Task 7: Run full test suite and add smoke tests in CI using the built image.
- Task 8: Update docs/proposals/README.md and add a short migration note in repo docs.

7. Dependencies or Risks
- Compatibility risk: Using smaller or distroless images can break binaries dependent on specific system libraries (glibc, certs). Validate all runtime requirements before switching base images.
- Security: Removing packages may inadvertently remove security-related utilities or certificates; ensure ca-certificates and necessary security libs remain.
- CI environment differences: Measured improvements may vary between local dev and CI runners. Define measurement environment and keep it consistent.
- Caching complexity: Introducing registry-based build caches requires credentials/config in CI and can add maintenance overhead.
- Reproducibility vs. performance tradeoffs: Aggressive caching or relying on prebuilt base images can make builds less transparent. Document the approach.
- Windows containers / cross-platform: If project needs Windows images, solutions here are Linux-centric; a separate plan is required.

8. Measurement & Reporting (how to prove success)
- Required artifacts in PR:
  - baseline.md: baseline build time and image size (commands used and environment).
  - results.md: post-change build time and image size with the same measurement method.
  - CI run links demonstrating improved build times and passing tests.
  - Short summary in PR description with % improvements and top 3 changes that yielded most benefit.

Notes
- This proposal intentionally avoids assuming a specific language or package manager; implementers must adjust package-manager-specific flags and steps (apt, apk, pip, npm, go modules, etc.) based on repo contents.
- Start with the "low-risk, high-impact" changes first (multi-stage, .dockerignore, caching) to get immediate benefits before experimenting with more invasive base-image changes.
