# Puts the machine-generated assets back to their committed state.
#
# Unity and TextMeshPro rewrite the font assets while the editor or a play session runs: TMP bakes
# kerning, ligatures and glyphs on demand and writes that cache back into the asset file. Those files
# are tracked because prefabs and materials reference them by GUID, so every session leaves them dirty
# and an innocent `git add -A` would ship the cache.
#
# The list of such files lives in Tools/generated-assets.txt; Tools/verify-structure.ps1 fails when one
# of them drifts. This script is the "I just ran the game" half of that pair: it restores only the
# entries that are actually dirty, so a clean tree stays untouched.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/restore-generated-assets.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/restore-generated-assets.ps1 -WhatIf

[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Low')]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifest = Join-Path $root 'Tools/generated-assets.txt'

if (!(Test-Path -LiteralPath $manifest)) {
    Write-Host 'Missing generated asset manifest: Tools/generated-assets.txt'
    exit 1
}

$paths = [System.Collections.Generic.List[string]]::new()
foreach ($line in Get-Content -LiteralPath $manifest -Encoding UTF8) {
    if ($line -match '^\s*#' -or $line -notmatch '\S') { continue }
    if ($line -notmatch '^(?<hash>[0-9a-f]{64})\s+(?<size>\d+)\s+(?<path>.+?)\s*$') {
        Write-Host "Unreadable generated asset entry: $line"
        exit 1
    }
    $paths.Add($Matches['path'])
}
if ($paths.Count -eq 0) {
    Write-Host 'The manifest lists no generated assets.'
    exit 0
}

$dirty = [System.Collections.Generic.List[string]]::new()
foreach ($path in $paths) {
    $status = & git -C $root status --porcelain -- $path
    if ($LASTEXITCODE -ne 0) {
        Write-Host "git status failed for $path (exit $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
    if ($status) { $dirty.Add($path) }
}

if ($dirty.Count -eq 0) {
    Write-Host "Nothing to restore: all $($paths.Count) generated assets match the commit."
    exit 0
}

foreach ($path in $dirty) {
    Write-Host "restore $path"
}
if ($PSCmdlet.ShouldProcess("$($dirty.Count) generated asset(s)", 'git restore')) {
    & git -C $root restore -- @dirty
    if ($LASTEXITCODE -ne 0) {
        Write-Host "git restore failed (exit $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
    Write-Host "Restored $($dirty.Count) of $($paths.Count) generated assets."
} else {
    Write-Host "Would restore $($dirty.Count) of $($paths.Count) generated assets."
}
