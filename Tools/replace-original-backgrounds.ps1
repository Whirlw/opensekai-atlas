# Replaces the six long-loading backgrounds and the common background with generated art.
# The originals are Project Sekai unit illustrations (Leoneed / MoreMoreJump / ...), so they have to go
# before the repository is published. Sizes and file names stay identical, so the .asset references and
# GUIDs keep working.
param(
    [string]$Target = 'Assets\Graphics\Resources',
    [string]$Backup = 'LocalPacks\original-skin\backgrounds',
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# name -> accent colour (RGB). Each file keeps a different tint so the loading screens stay distinguishable.
$palette = [ordered]@{
    'longLoading_bg_leoneed'         = @(96, 186, 214)
    'longLoading_bg_moremorejump'    = @(126, 214, 156)
    'longLoading_bg_nightcode'       = @(150, 128, 226)
    'longLoading_bg_vertualsinger'   = @(214, 140, 200)
    'longLoading_bg_vividbadsquad'   = @(232, 150, 96)
    'longLoading_bg_wonderlands'     = @(240, 206, 120)
    'bg_common_color'                = @(120, 132, 200)
}

function New-Background([int]$width, [int]$height, [int[]]$accent, [string]$path) {
    $bmp = New-Object System.Drawing.Bitmap($width, $height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'HighQuality'
    $g.InterpolationMode = 'HighQualityBicubic'

    # base: deep space gradient
    $rect = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect,
        [System.Drawing.Color]::FromArgb(255, 10, 12, 24),
        [System.Drawing.Color]::FromArgb(255, 26, 30, 54), 90.0)
    $g.FillRectangle($brush, $rect)
    $brush.Dispose()

    # accent glow low on the sheet, like the live playfield's horizon
    $glowW = [int]($width * 1.2); $glowH = [int]($height * 0.9)
    $glowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $glowPath.AddEllipse(-($glowW - $width) / 2, $height - $glowH * 0.55, $glowW, $glowH)
    $pathBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($glowPath)
    $pathBrush.CenterColor = [System.Drawing.Color]::FromArgb(150, $accent[0], $accent[1], $accent[2])
    $pathBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, $accent[0], $accent[1], $accent[2]))
    $g.FillPath($pathBrush, $glowPath)
    $pathBrush.Dispose(); $glowPath.Dispose()

    # thin diagonal streaks
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(26, $accent[0], $accent[1], $accent[2]), 2)
    for ($i = 0; $i -lt 7; $i++) {
        $x = [int]($width * (0.08 + 0.13 * $i))
        $g.DrawLine($pen, $x, 0, $x - [int]($height * 0.35), $height)
    }
    $pen.Dispose()

    # sparse stars, deterministic grid with a fixed offset pattern (no RNG so reruns are identical)
    $star = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150, 220, 230, 255))
    for ($row = 0; $row -lt 26; $row++) {
        for ($col = 0; $col -lt 34; $col++) {
            if ((($row * 7 + $col * 13) % 11) -ne 0) { continue }
            $x = [int](($col + (($row * 37) % 10) / 10.0) * ($width / 34.0))
            $y = [int](($row + (($col * 17) % 10) / 10.0) * ($height / 26.0))
            $size = 1 + (($row + $col) % 3)
            $g.FillEllipse($star, $x, $y, $size, $size)
        }
    }
    $star.Dispose()

    # vignette
    $vignette = New-Object System.Drawing.Drawing2D.GraphicsPath
    $vignette.AddRectangle($rect)
    $vBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($vignette)
    $vBrush.CenterColor = [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
    $vBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(170, 0, 0, 0))
    $g.FillPath($vBrush, $vignette)
    $vBrush.Dispose(); $vignette.Dispose()

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

if ($Apply -and -not (Test-Path $Backup)) { New-Item -ItemType Directory -Force -Path $Backup | Out-Null }

$done = 0
foreach ($name in $palette.Keys) {
    $file = Get-ChildItem $Target -Recurse -File -Filter "$name.png" | Select-Object -First 1
    if (-not $file) { Write-Warning "missing $name.png"; continue }
    $bmp = [System.Drawing.Bitmap]::FromFile($file.FullName)
    $w = $bmp.Width; $h = $bmp.Height
    $bmp.Dispose()

    if ($Apply) {
        Copy-Item $file.FullName (Join-Path $Backup $file.Name) -Force
        New-Background -width $w -height $h -accent $palette[$name] -path $file.FullName
    }
    "{0,-34} {1}x{2} accent=({3})" -f $file.Name, $w, $h, ($palette[$name] -join ',')
    $done++
}
"painted=$done apply=$Apply backup=$Backup"
