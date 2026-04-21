#Requires -Version 5.1
<#
.SYNOPSIS
    az-ai setup-secrets (Windows) — interactive walkthrough for storing
    Azure OpenAI credentials so espanso/AHK on Windows-native can see them.

.DESCRIPTION
    Two storage tiers:

      Tier 1 — User-scope environment variables
        Writes AZUREOPENAIENDPOINT / AZUREOPENAIAPI / AZUREOPENAIMODEL
        to the current user's environment in the registry. Persist across
        reboots. Any process the user launches after setup inherits them.
        Not encrypted at rest (registry is per-user but plaintext).

      Tier 2 — DPAPI-encrypted file + PowerShell profile hook
        Writes %APPDATA%\az-ai\env.dat, DPAPI-protected (only decryptable
        by this user on this machine). Adds a hook to $PROFILE that
        decrypts and loads the vars on every PowerShell session start.
        Espanso invoking `shell: powershell` picks these up automatically
        because Espanso loads the user's profile.

    Idempotent — safe to re-run to rotate keys or switch tiers.

    This is a bootstrap. The 2.1 roadmap ships `az-ai setup` as a
    first-class subcommand (see docs/proposals/FR-022-native-setup-wizard.md).

.PARAMETER VerifyOnly
    Run verification probes without prompting. Exits 0 green / nonzero red.

.EXAMPLE
    .\scripts\setup-secrets.ps1
    # Interactive walkthrough.

.EXAMPLE
    .\scripts\setup-secrets.ps1 -VerifyOnly
    # Re-check without re-prompting.
#>

[CmdletBinding()]
param(
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$ScriptVersion = '1.0.0'
$ConfigDir     = Join-Path $env:APPDATA 'az-ai'
$DpapiFile     = Join-Path $ConfigDir   'env.dat'
$HookBegin     = '# >>> az-ai creds hook (managed by setup-secrets.ps1) >>>'
$HookEnd       = '# <<< az-ai creds hook <<<'

# ─── OS guard ─────────────────────────────────────────────────────────────
# This script is for Windows-native flows only. WSL / Linux / macOS users
# should run scripts/setup-secrets.sh which handles bash/zsh shells and
# Linux-native storage backends (chmod 600 / GPG / libsecret).
#
# $env:OS = 'Windows_NT' on every Windows build (5.1, 7+). Avoids StrictMode
# problems with $IsWindows being undefined on 5.1.
if ($env:OS -ne 'Windows_NT') {
    Write-Error @"
This script is for Windows. Detected non-Windows environment.

If you're on Linux / macOS / WSL, run instead:
    bash scripts/setup-secrets.sh
"@
    exit 2
}

# ─── colors (respect NO_COLOR per project color-contract) ─────────────────
$UseColor = (-not $env:NO_COLOR) -and ([Console]::IsOutputRedirected -eq $false)
function Write-Info($msg) { if ($UseColor) { Write-Host "[info]  $msg" -ForegroundColor Cyan   } else { Write-Host "[info]  $msg" } }
function Write-Ok($msg)   { if ($UseColor) { Write-Host "[ok]    $msg" -ForegroundColor Green  } else { Write-Host "[ok]    $msg" } }
function Write-Warn($msg) { if ($UseColor) { Write-Host "[warn]  $msg" -ForegroundColor Yellow } else { Write-Host "[warn]  $msg" } }
function Write-Err($msg)  { if ($UseColor) { Write-Host "[ERROR] $msg" -ForegroundColor Red    } else { Write-Host "[ERROR] $msg" } }

function Show-Banner {
    $bar = '━' * 62
    if ($UseColor) { Write-Host $bar -ForegroundColor Cyan } else { Write-Host $bar }
    Write-Host "  az-ai setup-secrets (Windows) v$ScriptVersion"
    Write-Host '  Interactive walkthrough for Azure OpenAI CLI credentials.'
    Write-Host '  Supports Windows-native PowerShell + espanso Path B host side.'
    if ($UseColor) { Write-Host $bar -ForegroundColor Cyan } else { Write-Host $bar }
    Write-Host ''
}

function Read-Prompt {
    param(
        [Parameter(Mandatory)][string]$Message,
        [switch]$Secret,
        [string]$Default
    )
    $display = $Message
    if ($Default) { $display = "$Message [$Default]" }
    if ($Secret) {
        $secure = Read-Host -Prompt $display -AsSecureString
        $bstr   = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        try {
            return [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        } finally {
            [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    } else {
        $answer = Read-Host -Prompt $display
        if (-not $answer -and $Default) { return $Default }
        return $answer
    }
}

# ─── Tier 1: user-scope env vars ──────────────────────────────────────────
function Write-UserEnv {
    param([string]$Endpoint, [string]$ApiKey, [string]$Model)
    # User scope = writes to HKCU\Environment. Per-user, survives reboot,
    # inherited by every process the user launches after this.
    [Environment]::SetEnvironmentVariable('AZUREOPENAIENDPOINT', $Endpoint, 'User')
    [Environment]::SetEnvironmentVariable('AZUREOPENAIAPI',      $ApiKey,   'User')
    [Environment]::SetEnvironmentVariable('AZUREOPENAIMODEL',    $Model,    'User')
    # Also populate the current process so VerifyOnly probes work immediately
    # without requiring a new shell.
    $env:AZUREOPENAIENDPOINT = $Endpoint
    $env:AZUREOPENAIAPI      = $ApiKey
    $env:AZUREOPENAIMODEL    = $Model
    # Remove any stale DPAPI file — tiers are mutually exclusive.
    if (Test-Path $DpapiFile) { Remove-Item -Force $DpapiFile }
    Write-Ok "wrote user-scope env vars (registry: HKCU\Environment)"
    Write-Info "new PowerShell / espanso sessions will inherit them automatically"
}

# ─── Tier 2: DPAPI-encrypted file ─────────────────────────────────────────
function ConvertTo-DpapiString {
    param([string]$Plain)
    # ConvertFrom-SecureString on Windows uses DPAPI by default — the output
    # is a base64 string encrypted to the current user + current machine.
    # Decryptable only by the same user on the same machine.
    $sec = ConvertTo-SecureString -String $Plain -AsPlainText -Force
    return ConvertFrom-SecureString -SecureString $sec
}

function Write-DpapiFile {
    param([string]$Endpoint, [string]$ApiKey, [string]$Model)
    if (-not (Test-Path $ConfigDir)) {
        New-Item -ItemType Directory -Path $ConfigDir -Force | Out-Null
    }
    $encEndpoint = ConvertTo-DpapiString $Endpoint
    $encApiKey   = ConvertTo-DpapiString $ApiKey
    $encModel    = ConvertTo-DpapiString $Model
    # One line per var: NAME=<dpapi-base64>. Newlines as separators.
    $body = @(
        "AZUREOPENAIENDPOINT=$encEndpoint"
        "AZUREOPENAIAPI=$encApiKey"
        "AZUREOPENAIMODEL=$encModel"
    ) -join "`n"
    Set-Content -Path $DpapiFile -Value $body -NoNewline -Encoding UTF8
    # ACL: owner (current user) only. No Everyone, no Users.
    $acl = Get-Acl $DpapiFile
    $acl.SetAccessRuleProtection($true, $false)   # disable inheritance
    $acl.Access | ForEach-Object { [void]$acl.RemoveAccessRule($_) }
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        [System.Security.Principal.WindowsIdentity]::GetCurrent().User,
        'FullControl', 'Allow')
    $acl.AddAccessRule($rule)
    Set-Acl -Path $DpapiFile -AclObject $acl

    # Also clear user-scope env vars so they don't shadow the DPAPI load.
    [Environment]::SetEnvironmentVariable('AZUREOPENAIENDPOINT', $null, 'User')
    [Environment]::SetEnvironmentVariable('AZUREOPENAIAPI',      $null, 'User')
    [Environment]::SetEnvironmentVariable('AZUREOPENAIMODEL',    $null, 'User')
    Write-Ok "wrote $DpapiFile (DPAPI-encrypted, owner-only ACL)"
    # Populate current session so VerifyOnly works without a new shell.
    $env:AZUREOPENAIENDPOINT = $Endpoint
    $env:AZUREOPENAIAPI      = $ApiKey
    $env:AZUREOPENAIMODEL    = $Model
}

# ─── $PROFILE hook (for Tier 2) ───────────────────────────────────────────
function Get-HookBlock {
    # Load, decrypt, and set creds into the current PowerShell session.
    # DPAPI decrypt is only possible by the same user on the same machine,
    # so the file is useless to anyone who exfiltrates it.
    @"
# This block auto-decrypts Azure OpenAI CLI credentials into every
# PowerShell session, using Windows DPAPI (user + machine scoped).
# Safe to remove — just delete from HOOK_MARK_BEGIN to HOOK_MARK_END.
`$_azAiDat = Join-Path `$env:APPDATA 'az-ai\env.dat'
if (Test-Path `$_azAiDat) {
    try {
        foreach (`$line in (Get-Content `$_azAiDat)) {
            if (-not `$line) { continue }
            `$idx = `$line.IndexOf('=')
            if (`$idx -lt 1) { continue }
            `$name = `$line.Substring(0, `$idx)
            `$enc  = `$line.Substring(`$idx + 1)
            `$sec  = ConvertTo-SecureString -String `$enc -ErrorAction Stop
            `$bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR(`$sec)
            try {
                `$val = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR(`$bstr)
                [Environment]::SetEnvironmentVariable(`$name, `$val, 'Process')
            } finally {
                [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR(`$bstr)
            }
        }
    } catch {
        # Silently swallow — az-ai will emit its own 'endpoint not set' error.
    }
}
Remove-Variable _azAiDat -ErrorAction SilentlyContinue
"@
}

function Install-ProfileHook {
    # `$PROFILE` is the CurrentUserCurrentHost profile by default — what
    # espanso's `shell: powershell` invocations load. We want this one.
    $target = $PROFILE
    $dir    = Split-Path -Parent $target
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    if (-not (Test-Path $target)) { Set-Content -Path $target -Value '' -Encoding UTF8 }

    $existing = Get-Content -Path $target -Raw -ErrorAction SilentlyContinue
    if ($null -eq $existing) { $existing = '' }

    if ($existing.Contains($HookBegin)) {
        Write-Info "hook already present in $target — updating in place"
        # Strip existing block via regex match between markers.
        $pattern = '(?s)' + [regex]::Escape($HookBegin) + '.*?' + [regex]::Escape($HookEnd) + "(`r?`n)?"
        $existing = [regex]::Replace($existing, $pattern, '')
    }
    $block = @"

$HookBegin
$(Get-HookBlock)
$HookEnd
"@
    Set-Content -Path $target -Value ($existing.TrimEnd() + $block) -Encoding UTF8
    Write-Ok "hook installed → $target"
}

function Remove-ProfileHook {
    if (-not (Test-Path $PROFILE)) { return }
    $existing = Get-Content -Path $PROFILE -Raw
    if (-not $existing.Contains($HookBegin)) { return }
    $pattern = '(?s)' + [regex]::Escape($HookBegin) + '.*?' + [regex]::Escape($HookEnd) + "(`r?`n)?"
    $cleaned = [regex]::Replace($existing, $pattern, '')
    Set-Content -Path $PROFILE -Value $cleaned -Encoding UTF8
    Write-Ok "hook removed from $PROFILE"
}

# ─── verification probes ──────────────────────────────────────────────────
function Invoke-Verify {
    Write-Info 'running verification probes...'
    $fail = 0

    # Probe 1: current session has the vars (works after Tier 1 or Tier 2
    # because both write the current session alongside the persistent store).
    if ($env:AZUREOPENAIENDPOINT) {
        Write-Ok 'current session sees AZUREOPENAIENDPOINT'
    } else {
        Write-Err 'current session does NOT see AZUREOPENAIENDPOINT'
        $fail = 1
    }
    if ($env:AZUREOPENAIAPI) {
        Write-Ok 'current session sees AZUREOPENAIAPI'
    } else {
        Write-Err 'current session does NOT see AZUREOPENAIAPI'
        $fail = 1
    }

    # Probe 2: a FRESH PowerShell sees them (this is what espanso fires).
    #   - Tier 1: new shell inherits user-scope env vars automatically.
    #   - Tier 2: new shell runs $PROFILE → hook decrypts DPAPI file → sets vars.
    # Two separate invocations keeps quoting simple and avoids -Command parser quirks.
    $freshEndpoint = & powershell.exe -NoLogo -Command '$env:AZUREOPENAIENDPOINT' 2>$null
    $freshKeyFlag  = & powershell.exe -NoLogo -Command 'if ([string]::IsNullOrEmpty($env:AZUREOPENAIAPI)) { "0" } else { "1" }' 2>$null
    if ($freshEndpoint) {
        Write-Ok 'new powershell.exe session sees AZUREOPENAIENDPOINT'
    } else {
        Write-Err 'new powershell.exe session does NOT see AZUREOPENAIENDPOINT (this is what espanso uses!)'
        $fail = 1
    }
    if ($freshKeyFlag -eq '1') {
        Write-Ok 'new powershell.exe session sees AZUREOPENAIAPI'
    } else {
        Write-Err 'new powershell.exe session does NOT see AZUREOPENAIAPI'
        $fail = 1
    }

    # Probe 3: az-ai binary resolvable
    $azAi = Get-Command az-ai -ErrorAction SilentlyContinue
    if (-not $azAi) { $azAi = Get-Command az-ai.exe -ErrorAction SilentlyContinue }
    if ($azAi) {
        Write-Ok "az-ai binary resolves → $($azAi.Source)"
    } else {
        Write-Warn 'az-ai binary is NOT on PATH'
        Write-Warn '  install (AOT): copy dist\aot\AzureOpenAI_CLI.exe to a folder on PATH, rename to az-ai.exe'
        Write-Warn '  or add its folder to User PATH via System Properties → Environment Variables'
    }

    if ($fail -ne 0) {
        Write-Err 'one or more probes failed — open a new PowerShell and re-run with -VerifyOnly to recheck'
        return 1
    }
    Write-Ok 'all probes passed — espanso (shell: powershell) should work'
    return 0
}

# ─── main ─────────────────────────────────────────────────────────────────
function Main {
    Show-Banner
    Write-Info "detected OS: Windows ($([System.Environment]::OSVersion.VersionString))"
    Write-Info "detected PowerShell: $($PSVersionTable.PSVersion)"

    if ($VerifyOnly) {
        exit (Invoke-Verify)
    }

    $defaultEndpoint = $env:AZUREOPENAIENDPOINT
    $defaultModel    = if ($env:AZUREOPENAIMODEL) { $env:AZUREOPENAIMODEL } else { 'gpt-4o-mini' }

    Write-Host ''
    $endpoint = Read-Prompt -Message 'Azure OpenAI endpoint URL (e.g. https://my-res.openai.azure.com/)' -Default $defaultEndpoint
    if (-not $endpoint) { Write-Err 'endpoint is required'; exit 1 }
    if ($endpoint -notmatch '^https://.*\.openai\.azure\.com/?$') {
        Write-Warn "endpoint doesn't look like a standard Azure OpenAI URL — continuing anyway"
    }

    $apiKey = Read-Prompt -Message 'Azure OpenAI API key (input hidden)' -Secret
    if (-not $apiKey) { Write-Err 'api key is required'; exit 1 }
    if ($apiKey.Length -lt 20) { Write-Warn 'api key is shorter than 20 chars — are you sure?' }

    $model = Read-Prompt -Message 'Default model deployment name' -Default $defaultModel
    if (-not $model) { Write-Err 'model is required'; exit 1 }

    Write-Host ''
    if ($UseColor) { Write-Host 'Choose storage tier:' -ForegroundColor Cyan } else { Write-Host 'Choose storage tier:' }
    Write-Host '  1) User-scope env vars           (fast, plaintext in registry per-user)'
    Write-Host '  2) DPAPI-encrypted file + hook   (encrypted at rest, user+machine bound; recommended)'

    while ($true) {
        $tier = Read-Prompt -Message 'Tier [1/2]' -Default '1'
        switch ($tier) {
            '1' { Write-UserEnv -Endpoint $endpoint -ApiKey $apiKey -Model $model; Remove-ProfileHook; break }
            '2' { Write-DpapiFile -Endpoint $endpoint -ApiKey $apiKey -Model $model; Install-ProfileHook; break }
            default { Write-Warn 'pick 1 or 2'; continue }
        }
        break
    }

    Write-Host ''
    $rc = Invoke-Verify
    Write-Host ''
    if ($UseColor) { Write-Host '━━━ done ━━━' -ForegroundColor Green } else { Write-Host '━━━ done ━━━' }
    Write-Host 'Next steps:'
    Write-Host '  1. Open a NEW PowerShell window (or restart espanso so it picks up the change).'
    Write-Host "  2. Test:    az-ai --raw --system 'Say hi.' <<< 'test'     # PS 7+ only, use -i on PS 5.1"
    Write-Host "     PS 5.1:  'test' | az-ai --raw --system 'Say hi.'"
    Write-Host '  3. Test espanso sim:'
    Write-Host "     powershell -NoLogo -Command 'Get-Clipboard | az-ai --raw --system ''Fix grammar.'''"
    Write-Host '  4. Rotate creds? Just re-run this script — it is idempotent.'
    Write-Host '  5. Recheck without re-prompting:'
    Write-Host '     .\scripts\setup-secrets.ps1 -VerifyOnly'
    Write-Host ''
    exit $rc
}

Main
