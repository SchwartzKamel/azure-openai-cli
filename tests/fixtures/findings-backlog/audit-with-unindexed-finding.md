# Test fixture audit (unindexed)

This audit ships a CRITICAL finding `T-1` that is intentionally NOT present
in the project's `docs/findings-backlog.md`. The findings-backlog gate
must fail when this fixture is treated as an audit.

## CRITICAL

### T-1 -- Synthetic critical finding for negative regression test

**Severity:** CRITICAL.

This row exists only to exercise the gate. It is never resolved.
