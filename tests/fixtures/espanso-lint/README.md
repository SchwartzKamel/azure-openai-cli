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
