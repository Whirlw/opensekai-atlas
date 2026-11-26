# Gives the three countdown sprites (tex_live_countdown_1..3) meshes that cover their whole rectangle.
#
# Their meshes were once traced around an older, heavier digit, so other digits lost whatever fell in a
# notch or outside the outline. Only m_SubMeshes counts, m_IndexBuffer and the vertices change, as in
# fit-lane-sprite-meshes.ps1; UVs stay zero, Unity derives them from uvTransform.
$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$SpriteDir = Join-Path $ProjectRoot 'Assets/Graphics/Resources/effects/live_countdown/texture'
$culture = [Globalization.CultureInfo]::InvariantCulture

function To-Hex([byte[]]$bytes) { ($bytes | ForEach-Object { $_.ToString('x2') }) -join '' }

foreach ($digit in 1..3) {
    $file = Join-Path $SpriteDir "tex_live_countdown_$digit.asset"
    $script:text = [IO.File]::ReadAllText($file)
    $number = '([-\d.e]+)'
    $rect = [regex]::Match($text, "m_Rect:\s+serializedVersion: 2\s+x: $number\s+y: $number\s+width: $number\s+height: $number").Groups
    $pivot = [regex]::Match($text, "m_Pivot: \{x: $number, y: $number\}").Groups
    $ppu = [double]::Parse([regex]::Match($text, "m_PixelsToUnits: $number").Groups[1].Value, $culture)
    $w = [double]::Parse($rect[3].Value, $culture); $h = [double]::Parse($rect[4].Value, $culture)
    $px = [double]::Parse($pivot[1].Value, $culture) * $w; $py = [double]::Parse($pivot[2].Value, $culture) * $h
    $left = -$px / $ppu; $right = ($w - $px) / $ppu; $bottom = -$py / $ppu; $top = ($h - $py) / $ppu

    $ib = New-Object System.IO.MemoryStream; $i = New-Object System.IO.BinaryWriter($ib)
    foreach ($n in 0, 1, 2, 0, 2, 3) { $i.Write([uint16]$n) }; $i.Flush()
    $vb = New-Object System.IO.MemoryStream; $v = New-Object System.IO.BinaryWriter($vb)
    foreach ($p in @($left, $top), @($right, $top), @($right, $bottom), @($left, $bottom)) {
        $v.Write([single]$p[0]); $v.Write([single]$p[1]); $v.Write([single]0)
    }
    foreach ($n in 1..8) { $v.Write([single]0) }                              # UVs: derived by Unity
    $v.Flush()

    $first = { param($pattern, $replacement) $script:text = ([regex]$pattern).Replace($script:text, $replacement, 1) }
    & $first '(?m)^(\s+indexCount: )\d+' '${1}6'
    & $first '(?m)^(\s+vertexCount: )\d+' '${1}4'
    & $first '(?m)^(\s+m_IndexBuffer: )[0-9a-f]*' "`${1}$(To-Hex $ib.ToArray())"
    & $first '(?m)^(\s+m_VertexCount: )\d+' '${1}4'
    & $first '(?m)^(\s+m_DataSize: )\d+' "`${1}$($vb.Length)"
    & $first '(?m)^(\s+_typelessdata: )[0-9a-f]*' "`${1}$(To-Hex $vb.ToArray())"
    [IO.File]::WriteAllText($file, $script:text)
    Write-Host ('Fitted tex_live_countdown_{0}: ({1:0.###}, {2:0.###}) to ({3:0.###}, {4:0.###})' -f $digit, $left, $bottom, $right, $top)
}
