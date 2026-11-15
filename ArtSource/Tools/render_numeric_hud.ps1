$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$names=@(Import-Csv (Join-Path $PSScriptRoot 'manifest.csv') -Delimiter "`t" | ForEach-Object { [IO.Path]::GetFileName($_.file) } | Where-Object {
 $_ -match '^number_48px|^tex_live_countdown_|^skillEffect_text(?:_[0-9])?\.png$|^skillLv_40px_(?:shadow_)?[1-4](?:_wh)?\.png$'
})
if($names.Count -ne 39){throw "Expected 39 non-Combo numeric textures, found $($names.Count)"}
& (Join-Path $PSScriptRoot 'render_hud.ps1') -Only $names
