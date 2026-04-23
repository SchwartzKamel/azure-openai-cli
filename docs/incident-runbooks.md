# Incident runbooks

**Owner:** Frank Costanza (SRE / observability / incident response)
**Audience:** End users hitting a failure mid-pipeline, and contributors
triaging an issue.

> SERENITY NOW! Most "the CLI is broken" tickets are one of three
> things: the key, the quota, or the network. Walk the three runbooks
> below before you reach for a debugger.

---

## What this page is

Three short runbooks for the user-facing failures we see most often.
Each one is structured the same way: **symptoms** (what the user sees),
**likely causes** (ranked by how often we see them), and **recovery
steps** (in order, stop when one works).

> **Lloyd asks:** *What's an SLO?*
>
> A Service Level Objective is a target you commit to for some
> user-visible signal -- "99% of chat calls complete in under 4
> seconds," for example. It's the line you draw in the sand and agree
> to defend. We don't have formal SLOs yet because we don't have a
> shared production deployment to measure against; what's below are
> *aspirational* targets and recovery patterns, not contractual
> guarantees. Real SLOs need real users and real telemetry consent.

For the canonical glossary entry, see `docs/glossary.md` (S02E08).

---

## Runbook 1: 401 Auth failure

**Symptoms**

- `[ERROR] 401 Unauthorized` on stderr, exit code 1.
- Azure response body mentions `invalid_api_key`,
  `Access denied due to invalid subscription key`, or
  `PermissionDenied`.
- Worked yesterday, fails today, no code change in between.

**Likely causes (ranked)**

1. **Wrong env var name.** The CLI reads `AZUREOPENAIAPI`, not
   `AZUREOPENAIKEY` and not `AZURE_OPENAI_API_KEY`. A typo here
   silently produces an empty key and a 401 from Azure. This is the
   single most common cause.
2. **Key rotated or expired.** Keys rotated in the Azure portal
   invalidate immediately. CI secrets and local `.env` files drift.
3. **Deployment access revoked or moved.** The key is valid but the
   identity no longer has `Cognitive Services User` (or equivalent) on
   the deployment, or the deployment was deleted and recreated under a
   different name.
4. **Endpoint / key mismatch.** `AZUREOPENAIENDPOINT` points at
   resource A; `AZUREOPENAIAPI` is a key for resource B.

**Recovery steps**

1. Verify the env var name and value:

   ```bash
   env | grep -E '^AZUREOPENAI(API|ENDPOINT|MODEL)='
   ```

   Confirm `AZUREOPENAIAPI` is set and non-empty. If you see
   `AZUREOPENAIKEY` instead, rename it.
2. Re-pull the key from the Azure portal (Resource > Keys and
   Endpoint > KEY 1) and re-export. Compare the first 4 and last 4
   characters; mid-string drift from a copy/paste is common.
3. Confirm the endpoint matches the resource that owns the key.
4. If the key is fresh and the endpoint matches, check
   `Access control (IAM)` on the resource for your identity.

---

## Runbook 2: 429 Rate limit

**Symptoms**

- `[ERROR] 429 Too Many Requests` on stderr.
- Azure response includes `Retry-After` (seconds) and a body mentioning
  `tokens per minute` (TPM) or `requests per minute` (RPM).
- Bursty workloads (Ralph mode, batch Espanso expansions) trip it
  faster than interactive use.

**Likely causes (ranked)**

1. **TPM exhaustion.** Long prompts plus large `max_tokens` plus
   parallel calls saturate the deployment's per-minute token budget.
2. **RPM exhaustion.** Many small calls in a short window (Ralph
   iterations, tight agent loops) exceed the request quota even when
   token volume is small.
3. **Shared-deployment contention.** A teammate or another process is
   draining the same deployment's quota.
4. **Burst on cold model.** Some preview models have lower initial
   quotas than the GA tier on the same account.

**Recovery steps**

1. **Back off.** Honour the `Retry-After` header. For interactive use,
   wait the suggested number of seconds and retry once.
2. **Fall back to a different deployment.** If you have multiple
   deployments configured, pass `--model <fallback-deployment>` to
   route the next call to a deployment with headroom. Comma-separated
   `AZUREOPENAIMODEL` is read by the CLI for multi-model routing.
3. **Reduce the burst.** In Ralph mode, lower `--max-iterations`. For
   batch jobs, add a sleep between calls or serialize them.
4. **Request a quota increase** from the Azure portal
   (Resource > Quotas > request increase) if the load is steady-state
   rather than bursty. Quota changes typically apply within a business
   day.

---

## Runbook 3: DNS / TLS failure

**Symptoms**

- `[ERROR]` mentions `Name or service not known`,
  `No such host is known`, `SSL connection could not be established`,
  `The remote certificate is invalid`, or `unable to get local issuer
  certificate`.
- Exit code 1, no Azure-side response body (the request never reached
  the service).
- `curl https://<your-endpoint>` from the same shell reproduces the
  failure independently of the CLI.

**Likely causes (ranked)**

1. **Network down / VPN dropped.** Captive portals (hotel WiFi,
   conference networks) intercept HTTPS and serve their own cert,
   which fails validation.
2. **Corporate proxy / TLS intercept.** A man-in-the-middle proxy
   (Zscaler, Netskope, BlueCoat) presents a corporate root cert that
   isn't in the container or runtime trust store. The Alpine-based
   Docker image is especially affected because its CA bundle is
   minimal.
3. **Endpoint typo.** `AZUREOPENAIENDPOINT` is missing the
   `https://` scheme, has a trailing slash issue, or points at a
   region the resource doesn't live in.
4. **Expired or stale cert chain in the container image.** Long-lived
   images miss CA updates.

**Recovery steps**

1. Sanity-check the network from the same shell:

   ```bash
   curl -sS -o /dev/null -w '%{http_code}\n' "$AZUREOPENAIENDPOINT"
   ```

   Anything other than `401`/`404` (an HTTP response) is a network
   issue, not a CLI issue.
2. Verify the endpoint syntax: it must start with `https://` and end
   with `.openai.azure.com` (or the gov-cloud equivalent). No trailing
   path; the SDK appends the route.
3. If you're behind a corporate proxy, set
   `HTTPS_PROXY` and add the corporate root CA to the trust store
   (`/etc/ssl/certs/` on the host, or `update-ca-certificates` in a
   custom image layer).
4. Pull a fresh CLI image (`docker pull ...`) to refresh the CA
   bundle if the container is older than ~6 months.

---

## See also

- [`docs/telemetry.md`](telemetry.md) -- why we don't auto-collect any
  of this and how to verify.
- [`docs/observability.md`](observability.md) -- if you want to wire
  the v2 binary into your own OTLP collector for a longer-term view.
- [`SECURITY.md`](../SECURITY.md) -- how to report a vulnerability
  rather than a runtime incident.
