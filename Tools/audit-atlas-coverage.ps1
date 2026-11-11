# Reports, per atlas, how many sprites the paint kit covers and how many are not covered yet.
# A sprite counts as covered when a file of the same name exists in the kit and has the right size.
param(
    [string]$KitRoot = 'LocalPacks\paint-kit',
    [string]$SpriteFolder = 'Assets\Graphics\Sprites',
    [string]$Root = 'Assets'
)
$ErrorActionPreference = 'Stop'

$kitNames = @{}
foreach ($dir in @('new_hud', 'new_effects', 'new_notes', 'new_notes_v2', 'new_backgrounds')) {
    $path = Join-Path $KitRoot $dir
    if (-not (Test-Path $path)) { continue }
    Get-ChildItem $path -Recurse -File -Filter '*.png' | ForEach-Object { $kitNames[$_.BaseName] = $true }
}

$spritesByAtlas = @{}
foreach ($asset in Get-ChildItem $SpriteFolder -Filter '*.asset' -File) {
    $yaml = Get-Content $asset.FullName -Raw
    $tex = [regex]::Match($yaml, "texture: \{fileID: 2800000, guid: ([0-9a-f]{32})").Groups[1].Value
    $name = [regex]::Match($yaml, 'm_Name: (.+)').Groups[1].Value.Trim()
    if (-not $tex) { continue }
    if (-not $spritesByAtlas.ContainsKey($tex)) { $spritesByAtlas[$tex] = New-Object System.Collections.ArrayList }
    [void]$spritesByAtlas[$tex].Add($name)
}

$consumers = Get-ChildItem $Root -Recurse -File -Include *.prefab,*.unity,*.mat,*.asset |
    Where-Object { $_.FullName -notlike "*$SpriteFolder*" }
$blob = ($consumers | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

$atlasNames = @{}
foreach ($meta in Get-ChildItem 'Assets\Graphics\Textures' -Filter 'sactx-*.png.meta' -File) {
    $guid = [regex]::Match((Get-Content $meta.FullName -Raw), 'guid: ([0-9a-f]{32})').Groups[1].Value
    $atlasNames[$guid] = (Get-Item ($meta.FullName -replace '\.meta$', '')).BaseName
}

"| 图集 | 精灵 | 画包覆盖 | 未覆盖 | 其中未被引用 |"
"| --- | --- | --- | --- | --- |"
$totalSprites = 0; $totalCovered = 0; $totalOriginal = 0; $totalUnusedOriginal = 0
foreach ($atlasGuid in $spritesByAtlas.Keys) {
    $names = $spritesByAtlas[$atlasGuid]
    $covered = 0; $unusedOriginal = 0
    foreach ($n in $names) {
        $asset = Join-Path $SpriteFolder "$n.asset"
        $guid = if (Test-Path ($asset + '.meta')) { [regex]::Match((Get-Content ($asset + '.meta') -Raw), 'guid: ([0-9a-f]{32})').Groups[1].Value } else { '' }
        $isCovered = $kitNames.ContainsKey($n)
        if ($isCovered) { $covered++ }
        elseif ($guid -and -not $blob.Contains($guid)) { $unusedOriginal++ }
    }
    $name = $atlasNames[$atlasGuid]
    if (-not $name) { continue }
    $original = $names.Count - $covered
    $totalSprites += $names.Count; $totalCovered += $covered; $totalOriginal += $original; $totalUnusedOriginal += $unusedOriginal
    "| $name | $($names.Count) | $covered | $original | $unusedOriginal |"
}
"| **合计** | **$totalSprites** | **$totalCovered** | **$totalOriginal** | **$totalUnusedOriginal** |"
