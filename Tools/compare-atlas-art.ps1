# Compares every paint-kit PNG with the pixels the atlas actually ships, one sprite rect at a time.
# The apply script reports what it pasted, not what landed: a sprite that was skipped in an earlier run
# keeps its old art and still looks "applied". This walks the kit, reads the rect and texture GUID out
# of each sprite asset, and measures the difference, so a stale cell shows up as a number.
param(
    [string]$KitRoot = 'E:\DeepseekHarness\OpenSekai\LocalPacks\paint-kit',
    [string]$SpriteFolder = 'Assets\Graphics\Sprites',
    [string]$Filter,
    [double]$Threshold = 8,
    [int]$Show = 40
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$guidMap = @{}
Get-ChildItem 'Assets\Graphics\Textures' -Filter '*.png.meta' | ForEach-Object {
    $guid = [regex]::Match((Get-Content $_.FullName -Raw), 'guid: ([0-9a-f]{32})').Groups[1].Value
    $guidMap[$guid] = $_.FullName -replace '\.meta$', ''
}

# Sprite assets do not always share the file name (B/C/S are named after a hash), so index by m_Name.
$spriteByName = @{}
$spriteNameByFile = @{}
Get-ChildItem $SpriteFolder -Filter '*.asset' | ForEach-Object {
    $head = (Get-Content $_.FullName -TotalCount 20) -join "`n"
    $m = [regex]::Match($head, 'm_Name: (.+)')
    if ($m.Success) {
        $spriteByName[$m.Groups[1].Value.Trim()] = $_.FullName
        $spriteNameByFile[$_.FullName] = $m.Groups[1].Value.Trim()
    }
    if (-not $spriteByName.ContainsKey($_.BaseName)) { $spriteByName[$_.BaseName] = $_.FullName }
}

# Atlases are big; keep each one open for the whole run.
$atlases = @{}
function Get-Atlas([string]$path) {
    if (-not $atlases.ContainsKey($path)) {
        $atlases[$path] = New-Object System.Drawing.Bitmap($path)
    }
    return $atlases[$path]
}

$kit = Get-ChildItem (Join-Path $KitRoot 'new_hud') -Filter '*.png'
if ($Filter) { $kit = $kit | Where-Object { $_.BaseName -like $Filter } }

# Resolve like the apply script does: two kit files can reach one sprite (a stand-in drawn as
# `label_autolive_0.png` next to the real `label_autolive.png`), and only one of them can land in the
# atlas. Comparing the loser would report a difference that no build can ever show.
$kitForSprite = @{}
$exactChosen = @{}
$noSprite = New-Object System.Collections.ArrayList
$shadowed = New-Object System.Collections.ArrayList
foreach ($file in ($kit | Sort-Object Name)) {
    $kitName = $file.BaseName
    if (-not $spriteByName.ContainsKey($kitName)) { $noSprite.Add($kitName) | Out-Null; continue }
    $spritePath = $spriteByName[$kitName]
    $isExact = ($kitName -eq $spriteNameByFile[$spritePath])
    if (-not $kitForSprite.ContainsKey($spritePath)) {
        $kitForSprite[$spritePath] = $file.FullName
        $exactChosen[$spritePath] = $isExact
    }
    elseif ($isExact -and -not $exactChosen[$spritePath]) {
        $shadowed.Add(("{0} replaces {1}" -f $file.Name, (Split-Path $kitForSprite[$spritePath] -Leaf))) | Out-Null
        $kitForSprite[$spritePath] = $file.FullName
        $exactChosen[$spritePath] = $true
    }
    else {
        $shadowed.Add(("{0} shadowed by {1}" -f $file.Name, (Split-Path $kitForSprite[$spritePath] -Leaf))) | Out-Null
    }
}

$match = 0
$differ = New-Object System.Collections.ArrayList
$noRect = New-Object System.Collections.ArrayList

foreach ($spritePath in $kitForSprite.Keys) {
    $kitPath = $kitForSprite[$spritePath]
    $name = [System.IO.Path]::GetFileNameWithoutExtension($kitPath)

    $yaml = Get-Content $spritePath -Raw
    $rx = [regex]::Match($yaml, 'm_Rect:\s*\r?\n\s*serializedVersion: 2\s*\r?\n\s*x: ([\d.]+)\s*\r?\n\s*y: ([\d.]+)\s*\r?\n\s*width: ([\d.]+)\s*\r?\n\s*height: ([\d.]+)').Groups
    if (-not $rx[0].Success) { $noRect.Add($name) | Out-Null; continue }

    $x = [int][math]::Round([double]$rx[1].Value); $y = [int][math]::Round([double]$rx[2].Value)
    $w = [int][math]::Round([double]$rx[3].Value); $h = [int][math]::Round([double]$rx[4].Value)
    $guid = [regex]::Match($yaml, 'texture: \{fileID: 2800000, guid: ([0-9a-f]{32})').Groups[1].Value
    $atlasPath = $guidMap[$guid]
    if (-not $atlasPath -or -not (Test-Path $atlasPath)) { $noRect.Add("$name (no atlas)") | Out-Null; continue }

    $atlas = Get-Atlas $atlasPath
    if ($w -le 0 -or $h -le 0 -or ($x + $w) -gt $atlas.Width -or ($y + $h) -gt $atlas.Height) {
        $noRect.Add("$name (rect outside atlas)") | Out-Null
        continue
    }

    # Unity rects are bottom-left based; System.Drawing rows start at the top.
    $top = $atlas.Height - ($y + $h)
    $crop = $atlas.Clone((New-Object System.Drawing.Rectangle($x, $top, $w, $h)), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $painted = New-Object System.Drawing.Bitmap($kitPath)

    # A sparse grid is enough to tell "same art" from "different art" and keeps 220+ sprites quick.
    $stepX = [math]::Max(1, [int]($w / 40))
    $stepY = [math]::Max(1, [int]($h / 40))
    $sum = 0.0
    $samples = 0
    for ($py = 0; $py -lt $h; $py += $stepY) {
        $ky = [math]::Min([int]($py * $painted.Height / $h), $painted.Height - 1)
        for ($px = 0; $px -lt $w; $px += $stepX) {
            $kx = [math]::Min([int]($px * $painted.Width / $w), $painted.Width - 1)
            $a = $crop.GetPixel($px, $py)
            $b = $painted.GetPixel($kx, $ky)
            $sum += ([math]::Abs($a.R - $b.R) + [math]::Abs($a.G - $b.G) + [math]::Abs($a.B - $b.B) + [math]::Abs($a.A - $b.A)) / 4.0
            $samples++
        }
    }
    $mean = if ($samples) { $sum / $samples } else { 0 }
    if ($mean -le $Threshold) {
        $match++
    } else {
        $differ.Add(("{0}  meanDiff={1}  {2}x{3}  kit {4}x{5}" -f $name, [math]::Round($mean, 1), $w, $h, $painted.Width, $painted.Height)) | Out-Null
    }

    $crop.Dispose()
    $painted.Dispose()
}

foreach ($bitmap in $atlases.Values) { $bitmap.Dispose() }

"kit={0} match={1} differ={2} noSprite={3} noRect={4} shadowed={5}" -f $kit.Count, $match, $differ.Count, $noSprite.Count, $noRect.Count, $shadowed.Count
if ($differ.Count) {
    '--- painted file and atlas disagree (the cell was never updated, or the kit is the whole texture) ---'
    $differ | Select-Object -First $Show
}
if ($shadowed.Count) {
    '--- two kit files target the same sprite, the atlas can only hold one ---'
    $shadowed | Select-Object -First $Show
}
if ($noSprite.Count -and $Show -gt 0) {
    '--- kit files with no sprite asset (whole-texture or renamed targets) --- ' + $noSprite.Count
}
if ($noRect.Count) {
    '--- sprite without a usable rect ---'
    $noRect | Select-Object -First $Show
}
