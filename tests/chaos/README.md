# tests/chaos — FDR adversarial drill against v2

Reproducible attack scripts used by the v2 chaos drill (`docs/chaos-drill-v2.md`).

```bash
# Build + snapshot the AOT binary, then run everything.
dotnet publish azureopenai-cli-v2 -c Release -r linux-x64 -p:PublishAot=true
cp azureopenai-cli-v2/bin/Release/net10.0/linux-x64/publish/az-ai-v2 tests/chaos/artifacts/
bash tests/chaos/run_all.sh
```

Every script writes to `tests/chaos/artifacts/`:
- `<id>.out` — first 1 KB of stdout per attack
- `<id>.err` — first 1 KB of stderr per attack
- `results.tsv` — id / label / rc / stdout / stderr, one row per attack

Do not commit `tests/chaos/artifacts/` — it contains test fixtures (including
100 MB files) and drill output.
