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
