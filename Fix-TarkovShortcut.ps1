# After a Tarkov/BSG update: find ALL related shortcuts, delete them, recreate ONE
# user Start Menu shortcut -> publish\TarkovNvColor.exe --session
# Run: Fix-TarkovShortcut.bat  (right-click Run as administrator if common Start Menu won't delete)

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$exe = Join-Path $projectRoot "publish\TarkovNvColor.exe"
if (-not (Test-Path $exe)) {
    Write-Host "ERROR: missing $exe"
    Write-Host "Put TarkovNvColor.exe in publish\ then re-run."
    exit 1
}

$shell = New-Object -ComObject WScript.Shell

function Get-ShortcutInfo([string]$lnkPath) {
    try {
        $sc = $shell.CreateShortcut($lnkPath)
        return [PSCustomObject]@{
            Path              = $lnkPath
            Target            = [string]$sc.TargetPath
            Arguments         = [string]$sc.Arguments
            WorkingDirectory  = [string]$sc.WorkingDirectory
            IconLocation      = [string]$sc.IconLocation
        }
    }
    catch {
        return $null
    }
}

function Find-BsgRelatedShortcuts {
    $roots = @(
        (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu"),
        (Join-Path $env:ProgramData "Microsoft\Windows\Start Menu"),
        ([Environment]::GetFolderPath("Desktop")),
        (Join-Path $env:PUBLIC "Desktop")
    )

    $found = @()
    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        foreach ($lnk in (Get-ChildItem $root -Filter "*.lnk" -Recurse -ErrorAction SilentlyContinue)) {
            $info = Get-ShortcutInfo $lnk.FullName
            if ($null -eq $info) { continue }

            $isRelated =
                ($lnk.Name -match 'Battle|Bsg|Tarkov|Escape') -or
                ($info.Target -match 'BsgLauncher\.exe$|TarkovNvColor\.exe$|EscapeFromTarkov|Battlestate')

            if ($isRelated) {
                $found += $info
            }
        }
    }
    return $found
}

function Find-BsgLauncherExe([object[]]$shortcuts) {
    foreach ($s in @($shortcuts)) {
        $t = [string]$s.Target
        if ($t -like '*BsgLauncher.exe' -and (Test-Path -LiteralPath $t)) {
            return $t
        }
    }

    foreach ($c in @(
        "C:\Battlestate Games\BsgLauncher\BsgLauncher.exe",
        (Join-Path $env:LOCALAPPDATA "Battlestate Games\BsgLauncher\BsgLauncher.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Battlestate Games\BsgLauncher\BsgLauncher.exe"),
        (Join-Path $env:ProgramFiles "Battlestate Games\BsgLauncher\BsgLauncher.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Battlestate Games\BsgLauncher\BsgLauncher.exe")
    )) {
        if (Test-Path -LiteralPath $c) { return $c }
    }
    return $null
}

Write-Host "Scanning for Battlestate / Tarkov shortcuts..."
$existing = @(Find-BsgRelatedShortcuts)
Write-Host "Found $($existing.Count):"
foreach ($s in $existing) {
    Write-Host "  - $($s.Path)"
    Write-Host "      target=$($s.Target)"
    Write-Host "      args=$($s.Arguments)"
}

$bsgExe = Find-BsgLauncherExe $existing
if (-not $bsgExe) { $bsgExe = Find-BsgLauncherExe @() }
if ($bsgExe) {
    Write-Host "BSG launcher exe: $bsgExe"
}
else {
    Write-Host "WARN: BsgLauncher.exe not found - icon may be generic."
}

Write-Host ""
Write-Host "Removing old / duplicate shortcuts..."
$failed = @()
foreach ($s in $existing) {
    try {
        Remove-Item -LiteralPath $s.Path -Force -ErrorAction Stop
        Write-Host "  DEL $($s.Path)"
    }
    catch {
        Write-Host "  FAIL $($s.Path)"
        Write-Host "       $($_.Exception.Message)"
        $failed += $s.Path
    }
}

$userDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Battlestate Games"
$userLnk = Join-Path $userDir "Battlestate Games Launcher.lnk"
if (-not (Test-Path $userDir)) {
    New-Item -ItemType Directory -Path $userDir -Force | Out-Null
}

Write-Host ""
Write-Host "Creating fresh shortcut..."
$sc = $shell.CreateShortcut($userLnk)
$sc.TargetPath = $exe
$sc.Arguments = "--session"
$sc.WorkingDirectory = Split-Path $exe -Parent
$sc.WindowStyle = 1
$sc.Description = "TarkovNvColor session - BSG launcher + NVIDIA colors"
if ($bsgExe) {
    $sc.IconLocation = "$bsgExe,0"
}
else {
    $sc.IconLocation = "$exe,0"
}
$sc.Save()

Write-Host "OK  $userLnk"
Write-Host "    -> $exe --session"
if ($bsgExe) { Write-Host "    icon: $bsgExe" }

Write-Host ""
Write-Host "Verify scan after fix:"
$after = @(Find-BsgRelatedShortcuts)
foreach ($s in $after) {
    Write-Host "  - $($s.Path)"
    Write-Host "      target=$($s.Target)"
    Write-Host "      args=$($s.Arguments)"
}

$onlyOne = ($after.Count -eq 1)
$correct = $onlyOne -and
    ([string]$after[0].Target -eq $exe) -and
    ([string]$after[0].Arguments).Trim() -eq "--session"

if ($correct) {
    Write-Host ""
    Write-Host "SUCCESS: one shortcut -> TarkovNvColor --session"
    exit 0
}

Write-Host ""
Write-Host "NOT CLEAN YET. Remaining=$($after.Count) FailedDeletes=$($failed.Count)"
if ($failed.Count -gt 0) {
    Write-Host "Re-run Fix-TarkovShortcut.bat as Administrator to delete ProgramData shortcuts."
}
exit 1
