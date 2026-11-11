# Draws a plain stand-in for every remaining legacy UI primitive and drops it into the paint kit.
# The shape is redrawn from scratch, but the colour is sampled from the old atlas pixel so masks, plates
# and gauge bases keep the tint they had: a translucent mask stays translucent, a coloured base keeps its
# hue instead of turning white.
param(
    [string]$Kit = 'LocalPacks\paint-kit\new_hud',
    [string]$Sprites = 'Assets\Graphics\Sprites',
    [string]$Table = 'docs\reference\remaining-uncovered-sprites.csv',
    [string]$OldAtlas = 'Temp\liveatlas-old.png',
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if ($Apply -and -not (Test-Path $Kit)) { New-Item -ItemType Directory -Force -Path $Kit | Out-Null }
$oldByAtlas = @{}
foreach ($pair in @(
    @('LiveAtlas',    $OldAtlas),
    @('CommonAtlas',  'Temp\common1-old.png'),
    @('CommonAtlas0', 'Temp\common0-old.png'),
    @('CommonAtlas1', 'Temp\common1-old.png'),
    @('JudgeAtlas',   'Temp\judge-old.png'))) {
    if (Test-Path $pair[1]) { $oldByAtlas[$pair[0]] = [System.Drawing.Bitmap]::FromFile((Resolve-Path $pair[1]).Path) }
}
$old = if ($oldByAtlas.ContainsKey('LiveAtlas')) { $oldByAtlas['LiveAtlas'] } else { $null }

function Get-OldColour($rect, [string]$atlasName) {
    $fallback = [System.Drawing.Color]::FromArgb(255, 235, 235, 235)
    $pick = if ($atlasName -match 'CommonAtlas') { if ($atlasName -match '^sactx-1') { 'CommonAtlas1' } else { 'CommonAtlas0' } } elseif ($atlasName -match 'JudgeAtlas') { 'JudgeAtlas' } else { 'LiveAtlas' }
    if (-not $oldByAtlas.ContainsKey($pick)) { return $fallback }
    $atlas = $oldByAtlas[$pick]
    $top = $atlas.Height - ($rect.Y + $rect.Height)
    if ($rect.X -lt 0 -or $top -lt 0 -or ($rect.X + $rect.Width) -gt $atlas.Width -or ($top + $rect.Height) -gt $atlas.Height) { return $fallback }
    $crop = $atlas.Clone((New-Object System.Drawing.Rectangle($rect.X, $top, $rect.Width, $rect.Height)), $old.PixelFormat)
    $mid = $crop.GetPixel([int]($rect.Width / 2), [int]($rect.Height / 2))
    $crop.Dispose()
    if ($mid.A -lt 8) { return $fallback }
    return $mid
}

function Get-Rect([string]$name) {
    $asset = Join-Path $Sprites "$name.asset"
    if (-not (Test-Path $asset)) { return $null }
    $m = [regex]::Match((Get-Content $asset -Raw), 'm_Rect:\s*\r?\n\s*serializedVersion: 2\s*\r?\n\s*x: ([\d.]+)\s*\r?\n\s*y: ([\d.]+)\s*\r?\n\s*width: ([\d.]+)\s*\r?\n\s*height: ([\d.]+)')
    if (-not $m.Success) { return $null }
    [pscustomobject]@{
        X      = [int][math]::Round([double]$m.Groups[1].Value)
        Y      = [int][math]::Round([double]$m.Groups[2].Value)
        Width  = [int][math]::Round([double]$m.Groups[3].Value)
        Height = [int][math]::Round([double]$m.Groups[4].Value)
    }
}

function New-RoundedPath([int]$w, [int]$h, [int]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [math]::Max(1, $r * 2)
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($w - $d - 1, 0, $d, $d, 270, 90)
    $path.AddArc($w - $d - 1, $h - $d - 1, $d, $d, 0, 90)
    $path.AddArc(0, $h - $d - 1, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-ShapeImage([string]$name, [int]$w, [int]$h, [System.Drawing.Color]$fill) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.SmoothingMode = 'HighQuality'
    $brush = New-Object System.Drawing.SolidBrush($fill)

    $radiusMatch = [regex]::Match($name, '_r(\d+)')
    $radius = if ($radiusMatch.Success) { [int]$radiusMatch.Groups[1].Value } else { [int]([math]::Min($w, $h) * 0.22) }

    if ($name -match 'circle|btn_round|eff_btn') {
        $d = [math]::Min($w, $h)
        $circle = New-Object System.Drawing.RectangleF((($w - $d) / 2), (($h - $d) / 2), $d, $d)
        if ($name -match '^eff_btn') {
            $pen = New-Object System.Drawing.Pen($fill, [math]::Max(2, $d * 0.06))
            $g.DrawEllipse($pen, $circle)
            $pen.Dispose()
        }
        else {
            $g.FillEllipse($brush, $circle)
        }
    }
    elseif ($name -match 'gauge|bar_') {
        $path = New-RoundedPath $w $h ([int][math]::Max(2, [math]::Min($h / 2, 8)))
        $g.FillPath($brush, $path)
        $path.Dispose()
    }
    elseif ($name -match 'balloon_hint') {
        $points = @(
            (New-Object System.Drawing.PointF(($w / 2), 0)),
            (New-Object System.Drawing.PointF($w, $h)),
            (New-Object System.Drawing.PointF(0, $h))
        )
        $g.FillPolygon($brush, $points)
    }
    else {
        $path = New-RoundedPath $w $h ([int][math]::Min($radius, [math]::Min($w, $h) / 2))
        $g.FillPath($brush, $path)
        $path.Dispose()
    }

    $brush.Dispose(); $g.Dispose()
    return $bmp
}

function New-LetterImage([string]$letter, [int]$w, [int]$h, [System.Drawing.Color]$fill) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $size = [math]::Max(8, [math]::Min($h * 0.92, $w * 0.92))
    $font = New-Object System.Drawing.Font('Arial', $size, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = 'Center'
    $format.LineAlignment = 'Center'
    $rect = New-Object System.Drawing.RectangleF(0, 0, $w, $h)
    $brush = New-Object System.Drawing.SolidBrush($fill)
    $g.DrawString($letter, $font, $brush, $rect, $format)
    $brush.Dispose(); $font.Dispose(); $format.Dispose(); $g.Dispose()
    return $bmp
}

$rows = Import-Csv $Table
$made = 0; $skipped = @(); $notes = @()
foreach ($row in $rows) {
    $name = $row.sprite
    $rect = Get-Rect $name
    if (-not $rect) { $skipped += "$name (no sprite asset)"; continue }

    $fill = Get-OldColour $rect $row.atlas
    $notes += ("{0} A={1} RGB={2},{3},{4}" -f $name, $fill.A, $fill.R, $fill.G, $fill.B)

    if ($name -match '^[A-Z]_?\d*$') {
        $bmp = New-LetterImage $name.Substring(0, 1) $rect.Width $rect.Height $fill
    }
    else {
        $bmp = New-ShapeImage $name $rect.Width $rect.Height $fill
    }

    if ($Apply) { $bmp.Save((Join-Path $Kit "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    $made++
}

foreach ($b in $oldByAtlas.Values) { $b.Dispose() }
"drawn=$made skipped=$($skipped.Count) apply=$Apply"
$notes | Where-Object { $_ -match 'A=(\d+)' -and [int]$Matches[1] -lt 250 } | Select-Object -First 10
