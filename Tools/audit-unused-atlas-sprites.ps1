# Reports how much of each packed atlas is actually reached by a prefab, scene or material.
# Sprites that nothing references are dead weight in the build and still carry the original art,
# so the number this prints is the share of each atlas that can simply be deleted.
param(
    [string]$SpriteFolder = 'Assets\Graphics\Sprites',
    [string]$Root = 'Assets'
)
$ErrorActionPreference = 'Stop'

# 1) sprite -> atlas guid
$spritesByAtlas = @{}
$spriteGuidToAtlas = @{}
foreach ($asset in Get-ChildItem $SpriteFolder -Filter '*.asset' -File) {
    $guid = [regex]::Match((Get-Content ($asset.FullName + '.meta') -Raw), 'guid: ([0-9a-f]{32})').Groups[1].Value
    $head = (Get-Content $asset.FullName -Raw)
    $tex = [regex]::Match($head, "texture: \{fileID: 2800000, guid: ([0-9a-f]{32})").Groups[1].Value
    $name = [regex]::Match($head, 'm_Name: (.+)').Groups[1].Value.Trim()
    if (-not $tex) { continue }
    if (-not $spritesByAtlas.ContainsKey($tex)) { $spritesByAtlas[$tex] = New-Object System.Collections.ArrayList }
    [void]$spritesByAtlas[$tex].Add([pscustomobject]@{ Guid = $guid; Name = $name })
    $spriteGuidToAtlas[$guid] = $tex
}

# 2) atlas guid -> texture name + size
$atlasInfo = @{}
foreach ($meta in Get-ChildItem 'Assets\Graphics\Textures' -Filter 'sactx-*.png.meta' -File) {
    $guid = [regex]::Match((Get-Content $meta.FullName -Raw), 'guid: ([0-9a-f]{32})').Groups[1].Value
    $png = Get-Item ($meta.FullName -replace '\.meta$', '')
    $atlasInfo[$guid] = [pscustomobject]@{ Name = $png.BaseName; MB = [math]::Round($png.Length / 1MB, 2) }
}

# 3) which sprite guids appear anywhere outside the sprite folder
$consumers = Get-ChildItem $Root -Recurse -File -Include *.prefab,*.unity,*.mat,*.asset |
    Where-Object { $_.FullName -notlike "*$SpriteFolder*" }
$blob = ($consumers | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

"| 图集 | 精灵 | 被引用 | 未被引用 | 体积 MB |"
"| --- | --- | --- | --- | --- |"
foreach ($atlasGuid in $spritesByAtlas.Keys) {
    $list = $spritesByAtlas[$atlasGuid]
    $used = 0
    foreach ($s in $list) { if ($blob.Contains($s.Guid)) { $used++ } }
    $info = $atlasInfo[$atlasGuid]
    $name = if ($info) { $info.Name } else { $atlasGuid.Substring(0, 8) }
    $mb = if ($info) { $info.MB } else { 0 }
    "| $name | $($list.Count) | $used | $($list.Count - $used) | $mb |"
}
