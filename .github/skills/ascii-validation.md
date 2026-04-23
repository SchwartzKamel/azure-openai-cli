# Skill: ascii-validation

**Run before every commit that touches a markdown file outside the upstream exclusion list.** This is the local mirror of the smart-quote rule in `.github/workflows/docs-lint.yml`. The workflow hard-fails the merge; this skill keeps you from finding that out the hard way.

## The one-liner

```bash
grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' <files>
```

Exit 0 = clean. Exit 1 = at least one hit; fix before commit. Swap `<files>` for the paths you just touched, or use `git diff --name-only --diff-filter=AM HEAD | grep '\.md$' | xargs -r grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]'`.

## What it catches

The six characters `docs-lint` bans. Replace each with its ASCII equivalent:

| Codepoint | Char | Name              | ASCII replacement |
|-----------|------|-------------------|-------------------|
| U+2018    | `'`  | left single quote | `'`               |
| U+2019    | `'`  | right single quote / apostrophe | `'`     |
| U+201C    | `"`  | left double quote | `"`               |
| U+201D    | `"`  | right double quote | `"`              |
| U+2013    | `-`  | en dash           | `-`               |
| U+2014    | `--` | em dash           | `--` (two hyphens, no surrounding spaces required) |

## Files excluded upstream

`docs-lint.yml` skips these. You may still author them in ASCII for consistency, but the workflow will not fail if a smart quote slips in.

**Excluded filenames (any directory):**

- `README.md`
- `CHANGELOG.md`

**Excluded directories (recursively):**

- `node_modules/`
- `.git/`
- `.smith/`
- `bin/`, `obj/`
- `archive/`
- `perf/`, `benchmarks/`, `demos/`
- `launch/`, `announce/`, `talks/`
- `audits/`

> Note: `THIRD_PARTY_NOTICES.md` is **not** excluded by the workflow. If episode briefs claim otherwise, the workflow is right and the brief drifted.

## The rule

> Always run the grep before commit on any new or modified `.md` file outside the exclusion list above.

If you're touching a `README.md` or `CHANGELOG.md`, you can skip -- but it costs nothing to run, and historic entries with em-dashes are easier to read in ASCII.

## Fix-it cheatsheet

`sed -i` is the fastest path. Always pass the file list explicitly; do not let a regex sweep loose on the tree.

```bash
sed -i \
  -e "s/\xE2\x80\x98/'/g" \
  -e "s/\xE2\x80\x99/'/g" \
  -e "s/\xE2\x80\x9C/\"/g" \
  -e "s/\xE2\x80\x9D/\"/g" \
  -e "s/\xE2\x80\x93/-/g" \
  -e "s/\xE2\x80\x94/--/g" \
  path/to/file.md
```

Then re-run the grep to confirm zero hits.

## Why this exists

`docs-lint` hard-fails the merge with `::error file=...,line=...::Smart quote or en/em dash`. Episodes that skip this skill ship red CI, get a reshoot from the Soup Nazi, and waste a fix-forward commit. Run the grep. The line does not move.

## Cross-refs

- [`preflight.md`](preflight.md) -- the code-side gate; this is the docs-side gate
- [`docs-only-commit.md`](docs-only-commit.md) -- when this is the only validation you owe
- [`changelog-append.md`](changelog-append.md) -- CHANGELOG is excluded upstream, but stay ASCII for your new lines
