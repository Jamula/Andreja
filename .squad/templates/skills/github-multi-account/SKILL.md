---
name: github-multi-account
description: Detect and set up account-locked gh aliases for multi-account GitHub. The AI reads this skill, detects accounts, asks the user which is personal/work, and runs the setup automatically.
confidence: high
source: https://github.com/tamirdresher/squad-skills/tree/main/plugins/github-multi-account
author: tamirdresher
---

# GitHub Multi-Account — AI-Driven Setup

## When to Activate
When the user has multiple GitHub accounts (check with `gh auth status`). If you see 2+ accounts listed, this skill applies.

## What to Do (as the AI agent)

### Step 1: Detect accounts
Run: `gh auth status`
Look for multiple accounts. Note only the usernames listed. Never request, query, log, or display account email addresses.

### Step 2: Ask the user
Ask: "I see you have multiple GitHub accounts: {list them}. Which one is your personal account and which is your work/EMU account?"

### Step 3: Run the setup automatically
Once the user confirms, do ALL of this for them:

```powershell
# 1. Use only the account logins the user explicitly confirmed
$personalAccount = "USER_CONFIRMED_PERSONAL_LOGIN"
$workAccount = "USER_CONFIRMED_WORK_LOGIN"

foreach ($account in @($personalAccount, $workAccount)) {
    if ($account -notmatch '^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$') {
        throw "Invalid GitHub account login."
    }
}

# 2. Add to PowerShell profile
$profilePath = $PROFILE.CurrentUserAllHosts
if (!(Test-Path $profilePath)) { New-Item -Path $profilePath -Force | Out-Null }
$existing = Get-Content $profilePath -Raw -ErrorAction SilentlyContinue
if ($existing -notmatch "gh-personal") {
    $block = @"

# === GitHub Multi-Account Aliases ===
function gh-personal { gh auth switch --user '$personalAccount' 2>`$null | Out-Null; gh @args }
function gh-work { gh auth switch --user '$workAccount' 2>`$null | Out-Null; gh @args }
Set-Alias ghp gh-personal
Set-Alias ghw gh-work
"@
    Add-Content -Path $profilePath -Value $block
}

# 3. Create CMD wrappers
$binDir = Join-Path $env:USERPROFILE ".squad\bin"
if (!(Test-Path $binDir)) { New-Item -ItemType Directory -Path $binDir -Force | Out-Null }
"@echo off`ngh auth switch --user `"$personalAccount`" >nul 2>&1`ngh %*" | Out-File "$binDir\ghp.cmd" -Encoding ascii
"@echo off`ngh auth switch --user `"$workAccount`" >nul 2>&1`ngh %*" | Out-File "$binDir\ghw.cmd" -Encoding ascii

# 4. Add to PATH
$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ($userPath -notmatch [regex]::Escape($binDir)) {
    [Environment]::SetEnvironmentVariable("PATH", "$binDir;$userPath", "User")
    $env:PATH = "$binDir;$env:PATH"
}

# 5. Load in current session
function gh-personal { gh auth switch --user $personalAccount 2>$null | Out-Null; gh @args }
function gh-work { gh auth switch --user $workAccount 2>$null | Out-Null; gh @args }
Set-Alias ghp gh-personal
Set-Alias ghw gh-work
```

### Step 4: Verify for the user
Run these and show the results:
```powershell
ghp api user --jq '.login'   # should show personal username
ghw api user --jq '.login'   # should show work username
```

### Step 5: Tell the user
"All set! From now on use `ghp` for personal repos and `ghw` for work repos. I'll use them too."

## After Setup — Usage Rules

1. **NEVER** use bare `gh` for repo operations — always `ghp` or `ghw`
2. **NEVER** manually `gh auth switch` — the aliases handle it
3. Derive the current repository owner from the Git remote. Treat the remote as data and accept only recognized GitHub URL forms:

```powershell
$remoteUrl = (git remote get-url origin).Trim()
if ($LASTEXITCODE -ne 0) { throw "Unable to read the origin remote." }

$ownerMatch = [regex]::Match(
    $remoteUrl,
    '^(?:https://[^/]+/|ssh://git@[^/]+/|git@[^:]+:)(?<owner>[A-Za-z0-9_.-]+)/[^/]+(?:\.git)?$'
)
if (!$ownerMatch.Success) { throw "Unsupported GitHub origin URL." }
$repoOwner = $ownerMatch.Groups['owner'].Value
```

4. Ask the user whether `$repoOwner` must use the confirmed personal or work account. Do not infer the account for an organization owner. Store the answer using the already confirmed account variables:

```powershell
$confirmedRepoAccount = Read-Host "Use personal or work account for '$repoOwner'?"
$accountByOwner = @{}
switch ($confirmedRepoAccount.ToLowerInvariant()) {
    "personal" { $accountByOwner[$repoOwner] = $personalAccount }
    "work" { $accountByOwner[$repoOwner] = $workAccount }
    default { throw "Choose personal or work." }
}
```

Use `ghp` only when the mapped value equals `$personalAccount`; use `ghw` only when it equals `$workAccount`. Repeat the confirmation for an unmapped owner.

## For Squad Agents
At the top of any script touching GitHub, use the user-confirmed account variables; never embed account names or derive identity from email:

```powershell
if ([string]::IsNullOrWhiteSpace($personalAccount) -or [string]::IsNullOrWhiteSpace($workAccount)) {
    throw "User-confirmed GitHub account variables are required."
}
function gh-personal { gh auth switch --user $personalAccount 2>$null | Out-Null; gh @args }
function gh-work { gh auth switch --user $workAccount 2>$null | Out-Null; gh @args }
```
