# Copies the painted textures from the paint kit back into the project.
# Atlas sprites are handled by Tools/apply-atlas-art.ps1; this script only moves whole textures
# (effects, note skins, 2D backgrounds) and matches them by file name inside an explicit root.
param(
    [string]$KitRoot = 'E:\Projects\NewSkin\paint-kit',
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Get-Size([string]$path) {
    $b = [System.Drawing.Bitmap]::FromFile($path)
    $s = "$($b.Width)x$($b.Height)"
    $b.Dispose()
    return $s
}

# Note skins come in two styles, so each note folder gets its own target root - mixing them
# (which a single global index does) silently overwrites one style with the other.
$noteRoot = 'Assets\Game\Resources\live\note'
$groups = @(
    @{ Source = 'new_effects';     Roots = @('Assets\Game\AssetBundles\resources\tutorial\effect_asset\live') },
    @{ Source = 'new_notes';       Roots = @("$noteRoot\custom01") },
    @{ Source = 'new_notes_v2';    Roots = @("$noteRoot\custom02") },
    @{ Source = 'new_backgrounds'; Roots = @('Assets\Game\AssetBundles\resources\startapp', 'Assets\Graphics\Resources') },
    @{ Source = 'new_hud';         Roots = @('Assets\Graphics\Textures', 'Assets\Graphics\Resources\effects\transition\texture') }
)

$copied = 0; $same = 0; $missing = @(); $mismatch = @(); $absentGroups = @()
foreach ($group in $groups) {
    $srcDir = Join-Path $KitRoot $group.Source
    if (-not (Test-Path $srcDir)) { $absentGroups += $group.Source; continue }

    # index only this group's roots
    $targets = @{}
    foreach ($root in $group.Roots) {
        if (-not (Test-Path $root)) { continue }
        Get-ChildItem $root -Recurse -File -Filter '*.png' | ForEach-Object {
            if (-not $targets.ContainsKey($_.Name)) { $targets[$_.Name] = @() }
            $targets[$_.Name] += $_.FullName
        }
    }

    foreach ($file in Get-ChildItem $srcDir -Recurse -File -Filter '*.png') {
        # judgement-effect and result files carry a folder prefix: live_tap_effect_0_tex_note_common_all.png
        $leaf = $file.Name
        if ($leaf -match '^live_(tap_effect|result)_[a-z0-9_]*?_(?<rest>(tex|txt|bg|mask|ALL|FULL|LIVE).*)$') {
            $leaf = $Matches['rest']
        }
        if ($leaf -match '^live_result_default_(?<rest>.+)$') { $leaf = $Matches['rest'] }

        if (-not $targets.ContainsKey($leaf)) { $missing += "$($group.Source)/$($file.Name)"; continue }
        $srcSize = Get-Size $file.FullName
        foreach ($target in $targets[$leaf]) {
            $dstSize = Get-Size $target
            if ($dstSize -ne $srcSize) { $mismatch += "$($file.Name) $srcSize -> $dstSize ($target)"; continue }
            if ((Get-FileHash $file.FullName).Hash -eq (Get-FileHash $target).Hash) { $same++; continue }
            if ($Apply) { Copy-Item $file.FullName $target -Force }
            $copied++
        }
    }
}

"copied=$copied same=$same missed=$($missing.Count) mismatch=$($mismatch.Count) absentGroups=$($absentGroups -join ',')"
if ($missing.Count) { '--- not found in project ---'; $missing | Select-Object -First 20 }
if ($mismatch.Count) { '--- size mismatch (skipped) ---'; $mismatch | Select-Object -First 20 }
