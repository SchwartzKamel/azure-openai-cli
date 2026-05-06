# Fixtures for scripts/lint-espanso-yml.sh.
#
# - bash-injection-broken.yml -- reproduces the v2.1 audit F-1/F-2 pattern
#   (form placeholder substituted directly into a bash double-quoted
#   string and into a single-quoted --system arg). The lint MUST flag
#   this.
#
# - bash-injection-fixed.yml -- the unified S03E01 stdin/heredoc pattern.
#   The lint MUST pass this.
#
# Both fixtures exercise only the bash code path; the PowerShell-side
# checks (S02E37 trigger drift, BACKSPACE counts) live alongside their
# own scenarios in the existing ai-windows-to-wsl.yml.
#
# Cross-file trigger collision (added 2026-05, S03 :aidata rename):
#
# - collision-broken.yml + collision-other.yml -- both define :aidata.
#   Run `bash scripts/lint-espanso-yml.sh tests/fixtures/espanso-lint/collision-broken.yml \
#       tests/fixtures/espanso-lint/collision-other.yml` -- the lint
#   MUST exit non-zero with a "trigger collides across kits" error.
#
# - collision-fixed.yml + collision-other.yml -- the broken file's
#   :aidata has been renamed to :aidataworkflow. The same lint
#   invocation against the -fixed and -other pair MUST exit 0.
#
# These fixtures pin the cross-file collision detector against
# regression. They are deliberately MINIMAL (just `replace:` strings,
# no shell var) so they do not exercise the bash/PS structural checks
# -- those are covered by bash-injection-{broken,fixed}.yml above.
#
# Linux/macOS variant catch-up (S03E04 follow-up):
#
# After the S03E04 patch landed the unified-S03 stdin/heredoc fix on
# ai-windows-to-wsl.yml, the Linux (ai.yml) and macOS (ai-macos.yml)
# variants were discovered to still ship the same F-1/F-2 class on
# :aiimg, :aiweb, and :aitone. The follow-up episode ported the same
# fix to both files. The bash-injection-broken.yml fixture catches
# this class equally regardless of which platform variant introduces
# it -- the lint is the cross-platform source of truth, and a future
# regression on any variant will fail this fixture.
#
# F-15 / F-16 lint follow-ups (2026-05, S03E07 re-audit):
#
# - heredoc-indented.yml -- a `shell: bash` cmd block that uses the
#   indented heredoc form `<<-'__AZ_AI_EOF__'` (leading tabs stripped
#   from body lines and the closing tag may be tab-indented). The form
#   value reaches az-ai only inside the quoted heredoc body. The lint
#   MUST pass this. Pre-fix this fixture failed because the heredoc
#   regex was anchored on `<<\s*'TAG'` and missed the `<<-` variant
#   (F-15).
#
# - heredoc-comment.yml -- a `shell: bash` cmd block whose body
#   contains a comment line `# ... {{question.query}} ...`. Bash never
#   parses comment lines, so the placeholder is inert and must NOT
#   trip the form-substitution guard. The lint MUST pass this.
#   Pre-fix this fixture failed because comment lines were fed through
#   the placeholder regex unchanged (F-16).
#
# - shell-sh.yml -- a `shell: sh` cmd block with `{{form.field}}`
#   interpolated outside any heredoc. POSIX `sh` interpolates the same
#   way bash does for this surface, so it shares the bash-class guard.
#   The lint MUST fail with the bash-class guard message. Pre-fix this
#   fixture also failed, but only because the PowerShell-only
#   structural assertions tripped on a non-PS body -- the right-reason
#   failure now reports "bash-class guard" (F-15).
#
# - shell-cmd.yml -- a `shell: cmd` (Windows cmd.exe) cmd block with
#   `{{form.field}}` interpolation. No current trigger uses cmd.exe
#   for form-input handling, so the lint emits a STDERR WARNING and
#   exits 0 (rc=0). If a real `shell: cmd` use case ever lands, this
#   convention can be escalated to a hard failure. Convention:
#   warnings are written to stderr, do not increment the failure
#   count, and do not affect exit status (F-15).
