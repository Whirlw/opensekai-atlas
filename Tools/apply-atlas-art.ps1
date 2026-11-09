# Pastes painted PNGs straight into the packed atlas textures, using the rect stored in each sprite asset.
# PaintKit's own apply reported success but left the regions untouched, so this does the paste itself and
# reports any sprite that has no matching kit file.
param(
    [string]$KitRoot = 'E:\DeepseekHarness\OpenSekai\LocalPacks\paint-kit',
    [string]$SpriteFolder = 'Assets\Graphics\Sprites',
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
$guidMap = @{}
Get-ChildItem 'Assets\Graphics\Textures' -Filter '*.png.meta' | ForEach-Object {
    $guid = [regex]::Match((Get-Content $_.FullName -Raw), 'guid: ([0-9a-f]{32})').Groups[1].Value
    $guidMap[$guid] = $_.FullName -replace '\.meta$', ''
}

$atlases = @{}
function Get-Atlas([string]$path) {
    if (-not $atlases.ContainsKey($path)) {
        $bytes = [System.IO.File]::ReadAllBytes($path)
        $ms = New-Object System.IO.MemoryStream(,$bytes)
        $atlases[$path] = New-Object System.Drawing.Bitmap($ms)
    }
    return $atlases[$path]
}

$kitDirs = @(Join-Path $KitRoot 'new_hud')
$kitFiles = @{}
foreach ($d in $kitDirs) {
    if (-not (Test-Path $d)) { continue }
    Get-ChildItem $d -File -Filter '*.png' | ForEach-Object { $kitFiles[$_.BaseName] = $_.FullName }
}

$painted = 0; $wholeCopied = 0; $whole = @(); $noSprite = @(); $sizeDiff = @(); $dirty = @{}

# sprite assets do not always share their file name (B/C/S live in files named after a hash), so index by m_Name
$spriteByName = @{}
Get-ChildItem $SpriteFolder -Filter '*.asset' | ForEach-Object {
    $head = (Get-Content $_.FullName -TotalCount 20) -join "`n"
    $m = [regex]::Match($head, 'm_Name: (.+)')
    if ($m.Success) { $spriteByName[$m.Groups[1].Value.Trim()] = $_.FullName }
        if (-not $spriteByName.ContainsKey($_.BaseName)) { $spriteByName[$_.BaseName] = $_.FullName }
}

foreach ($name in $kitFiles.Keys) {
    if (-not $spriteByName.ContainsKey($name)) { $noSprite += $name; continue }
    $asset = $spriteByName[$name]
    $yaml = Get-Content $asset -Raw
    $rx = [regex]::Match($yaml, 'm_Rect:\s*\r?\n\s*serializedVersion: 2\s*\r?\n\s*x: ([\d.]+)\s*\r?\n\s*y: ([\d.]+)\s*\r?\n\s*width: ([\d.]+)\s*\r?\n\s*height: ([\d.]+)').Groups
    if (-not $rx[0].Success) { continue }
    $x = [int][math]::Round([double]$rx[1].Value); $y = [int][math]::Round([double]$rx[2].Value)
    $w = [int][math]::Round([double]$rx[3].Value); $h = [int][math]::Round([double]$rx[4].Value)
    $guid = [regex]::Match($yaml, "texture: \{fileID: 2800000, guid: ([0-9a-f]{32})").Groups[1].Value
    $atlasPath = $guidMap[$guid]
    if (-not $atlasPath -or -not (Test-Path $atlasPath)) { continue }

    $kb = [System.Drawing.Bitmap]::FromFile($kitFiles[$name])
    $atlasDim = (Get-Size $atlasPath) -split 'x'
    $atlasW = [int]$atlasDim[0]; $atlasH = [int]$atlasDim[1]

    # A sprite whose rect is inset into its own texture (judgeLine trims ~13x15 px of padding) is drawn
    # from a slice, but the painted file is the whole texture. Pasting it into the rect squeezes it and
    # the line lands a few pixels off. When the painted file matches the texture, replace the texture.
    if ($kb.Width -eq $atlasW -and $kb.Height -eq $atlasH -and ($w -lt $atlasW -or $h -lt $atlasH)) {
        $whole += $name
        if ($Apply) { Copy-Item $kitFiles[$name] $atlasPath -Force; $wholeCopied++ }
        $kb.Dispose()
        continue
    }

    if ([math]::Abs($kb.Width - $w) -gt 2 -or [math]::Abs($kb.Height - $h) -gt 2) {
        $sizeDiff += "$name  kit $($kb.Width)x$($kb.Height) vs rect ${w}x${h}"
    }
    if ($Apply) {
        $atlas = Get-Atlas $atlasPath
        $top = $atlas.Height - ($y + $h)
        $g = [System.Drawing.Graphics]::FromImage($atlas)
        $g.CompositingMode = 'SourceCopy'
        $g.InterpolationMode = 'HighQualityBicubic'
        $dest = New-Object System.Drawing.Rectangle($x, $top, $w, $h)
        $g.SetClip($dest)
        $g.DrawImage($kb, $dest)
        $g.ResetClip()
        $g.Dispose()
        $dirty[$atlasPath] = $true
        $painted++
    }
    $kb.Dispose()
}

if ($Apply) {
    foreach ($path in $dirty.Keys) {
        $bmp = $atlases[$path]
        $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $atlases.Remove($path)
    }
}
"painted=$painted wholeTexture=$wholeCopied noSprite=$($noSprite.Count) sizeDiff=$($sizeDiff.Count) atlases=$($dirty.Count)"
if ($sizeDiff.Count) { '--- size difference (scaled to rect) ---'; $sizeDiff | Select-Object -First 10 }
if ($whole.Count) { '--- whole texture (rect is inset, file replaced) ---'; $whole | Select-Object -First 10 }
if ($noSprite.Count) { '--- cleared sprites without kit file (leave as is) --- ' + $noSprite.Count }