# Builds a contact sheet: paint-kit file | atlas crop (what the build ships), per HUD sprite.
param(
    [string]$KitRoot = 'E:\DeepseekHarness\OpenSekai\LocalPacks\paint-kit',
    [string]$Out = 'Temp\hud-verify.png'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$names = @('btn_stop','bar_lifeGauge_gn','bg_scoreGauge','judge_perfect','number_combo_154px_1_pr','number_48px_4_wh')
$rows = @()
foreach ($n in $names) {
    $asset = "Assets\Graphics\Sprites\$n.asset"
    if (-not (Test-Path $asset)) { continue }
    $yaml = Get-Content $asset -Raw
    $rx = [regex]::Match($yaml, 'm_Rect:\s*\r?\n\s*serializedVersion: 2\s*\r?\n\s*x: ([\d.]+)\s*\r?\n\s*y: ([\d.]+)\s*\r?\n\s*width: ([\d.]+)\s*\r?\n\s*height: ([\d.]+)').Groups
    $x = [int][math]::Round([double]$rx[1].Value); $y = [int][math]::Round([double]$rx[2].Value)
    $w = [int][math]::Round([double]$rx[3].Value); $h = [int][math]::Round([double]$rx[4].Value)
    $tex = [regex]::Match($yaml, "texture: \{fileID: 2800000, guid: ([0-9a-f]{32})").Groups[1].Value
    $kit = Join-Path $KitRoot "new_hud\$n.png"
    $rows += [pscustomobject]@{ Name = $n; X = $x; Y = $y; W = $w; H = $h; Guid = $tex; Kit = $kit }
}

$cellW = 260; $cellH = 200; $pad = 10; $labelH = 22
$bmp = New-Object System.Drawing.Bitmap(($cellW * 2 + $pad * 3), (($cellH + $labelH) * $rows.Count + $pad))
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 24, 26, 34))
$g.InterpolationMode = 'NearestNeighbor'
$font = New-Object System.Drawing.Font('Consolas', 9)

# map guid -> atlas png
$guidMap = @{}
Get-ChildItem 'Assets\Graphics\Textures' -Filter '*.png.meta' | ForEach-Object {
    $guid = [regex]::Match((Get-Content $_.FullName -Raw), 'guid: ([0-9a-f]{32})').Groups[1].Value
    $guidMap[$guid] = $_.FullName -replace '\.meta$', ''
}

$row = 0
foreach ($r in $rows) {
    $top = $pad + $row * ($cellH + $labelH)
    # kit column
    $k = New-Object System.Drawing.Rectangle($pad, $top, $cellW, $cellH)
    $g.DrawString("kit: $($r.Name)", $font, [System.Drawing.Brushes]::Gainsboro, $pad, ($top + $cellH + 2))
    if (Test-Path $r.Kit) {
        $kb = [System.Drawing.Bitmap]::FromFile($r.Kit)
        $sw = [math]::Min($cellW / $kb.Width, $cellH / $kb.Height)
        $dw = [int]($kb.Width * $sw); $dh = [int]($kb.Height * $sw)
        $g.DrawImage($kb, (New-Object System.Drawing.Rectangle(($pad + ($cellW - $dw) / 2), ($top + ($cellH - $dh) / 2), $dw, $dh)))
        $kb.Dispose()
    } else { $g.DrawString('missing', $font, [System.Drawing.Brushes]::OrangeRed, $pad, $top) }

    # atlas column
    $left = $pad * 2 + $cellW
    $g.DrawString("atlas: $($r.Name)", $font, [System.Drawing.Brushes]::Gainsboro, $left, ($top + $cellH + 2))
    $texPath = $guidMap[$r.Guid]
    if ($texPath -and (Test-Path $texPath)) {
        $ab = [System.Drawing.Bitmap]::FromFile($texPath)
        $topPx = $ab.Height - ($r.Y + $r.H)
        $crop = $ab.Clone((New-Object System.Drawing.Rectangle($r.X, $topPx, $r.W, $r.H)), $ab.PixelFormat)
        $sw = [math]::Min($cellW / $crop.Width, $cellH / $crop.Height)
        $dw = [int]($crop.Width * $sw); $dh = [int]($crop.Height * $sw)
        $g.DrawImage($crop, (New-Object System.Drawing.Rectangle(($left + ($cellW - $dw) / 2), ($top + ($cellH - $dh) / 2), $dw, $dh)))
        $crop.Dispose(); $ab.Dispose()
    }
    $row++
}

$g.Dispose()
$dir = Split-Path $Out -Parent
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$bmp.Save((Join-Path (Get-Location) $Out), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"saved $Out rows=$($rows.Count)"
