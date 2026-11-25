# Refits the meshes of the lane-family sprites (lane_base, lane_line, lane_mask, judgeLine) in
# Assets/Graphics/Sprites to the lane geometry the notes use.
#
# These sprites are standalone .asset files whose meshes were once traced tightly around the old
# artwork's alpha. When the art follows the lane geometry (ArtSource/Tools/Hud.cs, HalfWidth)
# the old meshes clip its edges, so this script replaces each mesh with a small convex polygon around the
# perspective trapezoid plus a margin, clamped to the sprite rect.
#
# Geometry (LiveConfig): 12 lanes x 1.19 units judged at y -2.96, notes travelling from (0, 5.46), so a
# lane edge at world y has half width 7.14 * (5.46 - y) / 8.42. Keep in sync with Hud.cs.
#
# Only m_SubMeshes counts, m_IndexBuffer and the vertex positions change. UVs stay zero like every sprite
# in the project: Unity derives them from uvTransform (pixels per unit and pivot).
param([switch]$Check)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$SpriteDir = Join-Path $ProjectRoot 'Assets/Graphics/Sprites'

function LaneHalf([double]$worldY) { 7.14 * (5.46 - $worldY) / 8.42 }

# Per sprite: pixels per unit, pivot in pixels (x, row from the top), world y of the pivot, the rows the art
# spans, the margin around the art and the sprite rect (x0, x1, row0, row1) the mesh may not leave.
$sprites = @(
    @{ Name = 'judgeLine'; Ppu = 108.0;     PivotX = 804.0;  PivotRow = 57.0;  OriginY = -2.92; Top = 15.5; Bottom = 98.5;   Margin = 2.0; Rect = @(0.0, 1608.0, 15.07612, 98.92388) },
    @{ Name = 'lane_base'; Ppu = 101.27473; PivotX = 1024.0; PivotRow = 588.0; OriginY = -0.8;  Top = 8.0;  Bottom = 1171.0; Margin = 2.0; Rect = @(9.07612, 2038.924, 0.0, 1170.987) },
    @{ Name = 'lane_line'; Ppu = 101.27473; PivotX = 1024.0; PivotRow = 588.0; OriginY = -0.8;  Top = 0.0;  Bottom = 1176.0; Margin = 4.0; Rect = @(0.21179456, 2047.788, 0.0, 1176.0) },
    @{ Name = 'lane_mask'; Ppu = 101.27473; PivotX = 1024.0; PivotRow = 588.0; OriginY = -0.8;  Top = 0.0;  Bottom = 1176.0; Margin = 2.0; Rect = @(0.0, 2048.0, 0.0, 1176.0) }
)

function Get-Outline($s) {
    $top = [Math]::Max($s.Top - $s.Margin, $s.Rect[2]); $bottom = [Math]::Min($s.Bottom + $s.Margin, $s.Rect[3])
    $half = { param($row) (LaneHalf ($s.OriginY + ($s.PivotRow - $row) / $s.Ppu)) * $s.Ppu + $s.Margin }
    $xMax = $s.Rect[1] - $s.PivotX; $xMin = $s.PivotX - $s.Rect[0]   # room on each side of the pivot
    # Right side from top to bottom, clamped to the rect; the half width grows linearly with the row.
    $h0 = [double](& $half $top); $h1 = [double](& $half $bottom)
    $px = New-Object 'System.Collections.Generic.List[double[]]'   # (x from pivot, row), clockwise
    $px.Add([double[]]@([Math]::Min($h0, $xMax), $top))
    if ($h1 -gt $xMax -and $h0 -lt $xMax) { $px.Add([double[]]@($xMax, ($top + ($xMax - $h0) / ($h1 - $h0) * ($bottom - $top)))) }
    $px.Add([double[]]@([Math]::Min($h1, $xMax), $bottom))
    $px.Add([double[]]@(-[Math]::Min($h1, $xMin), $bottom))
    if ($h1 -gt $xMin -and $h0 -lt $xMin) { $px.Add([double[]]@(-$xMin, ($top + ($xMin - $h0) / ($h1 - $h0) * ($bottom - $top)))) }
    $px.Add([double[]]@(-[Math]::Min($h0, $xMin), $top))
    # Returned as sprite-local units (y up).
    $local = New-Object 'System.Collections.Generic.List[double[]]'
    foreach ($p in $px) { $local.Add([double[]]@(($p[0] / $s.Ppu), (($s.PivotRow - $p[1]) / $s.Ppu))) }
    return , $local
}

function To-Hex([byte[]]$bytes) { ($bytes | ForEach-Object { $_.ToString('x2') }) -join '' }

foreach ($s in $sprites) {
    $file = Join-Path $SpriteDir "$($s.Name).asset"
    $text = [IO.File]::ReadAllText($file)
    $points = Get-Outline $s
    $count = $points.Count
    $indices = New-Object System.Collections.Generic.List[uint16]
    for ($i = 1; $i -lt $count - 1; $i++) { $indices.Add(0); $indices.Add($i); $indices.Add($i + 1) }

    $ib = New-Object System.IO.MemoryStream; $w = New-Object System.IO.BinaryWriter($ib)
    foreach ($i in $indices) { $w.Write([uint16]$i) }; $w.Flush()
    $vb = New-Object System.IO.MemoryStream; $v = New-Object System.IO.BinaryWriter($vb)
    foreach ($p in $points) { $v.Write([single]$p[0]); $v.Write([single]$p[1]); $v.Write([single]0) }
    while ($vb.Length % 16 -ne 0) { $v.Write([byte]0) }                       # stream 1 starts 16-aligned
    foreach ($p in $points) { $v.Write([single]0); $v.Write([single]0) }      # UVs: derived by Unity
    $v.Flush()

    $first = { param($pattern, $replacement) $script:text = ([regex]$pattern).Replace($script:text, $replacement, 1) }
    $script:text = $text
    & $first '(?m)^(\s+indexCount: )\d+' "`${1}$($indices.Count)"
    & $first '(?m)^(\s+vertexCount: )\d+' "`${1}$count"
    & $first '(?m)^(\s+m_IndexBuffer: )[0-9a-f]*' "`${1}$(To-Hex $ib.ToArray())"
    & $first '(?m)^(\s+m_VertexCount: )\d+' "`${1}$count"
    & $first '(?m)^(\s+m_DataSize: )\d+' "`${1}$($vb.Length)"
    & $first '(?m)^(\s+_typelessdata: )[0-9a-f]*' "`${1}$(To-Hex $vb.ToArray())"

    $summary = ($points | ForEach-Object { '({0:0.###}, {1:0.###})' -f $_[0], $_[1] }) -join ' '
    if ($Check) { Write-Host "$($s.Name): $count vertices $summary" }
    else { [IO.File]::WriteAllText($file, $script:text); Write-Host "Fitted $($s.Name): $count vertices $summary" }
}
