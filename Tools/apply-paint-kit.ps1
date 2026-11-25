# Copies the painted textures from the paint kit back into the project.
# Atlas sprites are handled by Tools/apply-atlas-art.ps1; this script only moves whole textures
# (effects, note skins, 2D backgrounds) and matches them by file name inside an explicit root.
param(
    [string]$KitRoot = 'ArtSource',   # relative to the project, or a full path
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
if (-not [IO.Path]::IsPathRooted($KitRoot)) { $KitRoot = Join-Path $ProjectRoot $KitRoot }
if (-not (Test-Path -LiteralPath $KitRoot)) { throw "No art folder at $KitRoot" }
Set-Location $ProjectRoot
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
    @{ Source = 'Effects';     Roots = @('Assets\Game\AssetBundles\resources\tutorial\effect_asset\live') },
    @{ Source = 'Notes';       Roots = @("$noteRoot\custom01") },
    @{ Source = 'NotesV2';    Roots = @("$noteRoot\custom02") },
    @{ Source = 'Backgrounds'; Roots = @('Assets\Game\AssetBundles\resources\startapp', 'Assets\Graphics\Resources') },
    @{ Source = 'HUD';         Roots = @('Assets\Graphics\Textures', 'Assets\Graphics\Resources\effects\transition\texture', 'Assets\Graphics\AtlasSprites') }
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
    if ($group.Source -eq 'Backgrounds') {
        $rankPlate = 'Assets\Graphics\Textures\ui_scorerank_plate.png'
        if (Test-Path -LiteralPath $rankPlate) {
            $targets['ui_scorerank_plate.png'] = @((Get-Item -LiteralPath $rankPlate).FullName)
        }
    }

    foreach ($file in Get-ChildItem $srcDir -File -Filter '*.png') {
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
            # A sprite under Assets/Graphics/AtlasSprites is packed by a Sprite Atlas, so new art may
            # change its size or shape; everywhere else a size change still means the wrong file.
            $packed = $target -like '*\Graphics\AtlasSprites\*'
            if ($dstSize -ne $srcSize -and -not $packed) { $mismatch += "$($file.Name) $srcSize -> $dstSize ($target)"; continue }
            if ((Get-FileHash $file.FullName).Hash -eq (Get-FileHash $target).Hash) { $same++; continue }
            if ($Apply) {
                Copy-Item $file.FullName $target -Force
                # A copy keeps the painting's date, and AssetBundleFreshnessCheck goes by dates: art painted before
                # the last bundle build but imported after it would ship as the old art.
                (Get-Item -LiteralPath $target).LastWriteTime = Get-Date
            }
            $copied++
        }
    }
}

"copied=$copied same=$same missed=$($missing.Count) mismatch=$($mismatch.Count) absentGroups=$($absentGroups -join ',')"
if ($missing.Count) { '--- not found in project ---'; $missing | Select-Object -First 20 }
if ($mismatch.Count) { '--- size mismatch (skipped) ---'; $mismatch | Select-Object -First 20 }
