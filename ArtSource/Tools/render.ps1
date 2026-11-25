# Draws the V2 art into ArtSource/<group>. Run in Windows PowerShell (5.1):
#   powershell -NoProfile -ExecutionPolicy Bypass -File ArtSource/Tools/render.ps1
#   ... -Group Notes,HUD                        only these folders
#   ... -Only judgeLine.png,judge_*.png         only these files (* matches anything)
# Files that are drawn overwrite the ones there; nothing outside ArtSource is touched.
param(
    [string[]]$Group = @('Notes', 'NotesV2', 'HUD', 'Effects', 'Backgrounds'),
    [string[]]$Only
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fonts = Join-Path (Split-Path $root -Parent) 'Assets\Game\Resources\Fonts'
# "-Only a,b" arrives as one string under `powershell -File`.
$Group = @($Group | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Only = @($Only | ForEach-Object { if ($_) { $_ -split ',' } } | Where-Object { $_ })
if ($PSBoundParameters.ContainsKey('Only') -and $Only.Count -eq 0) { throw 'No file names after -Only; nothing drawn' }

. (Join-Path $PSScriptRoot 'load-renderer.ps1')
$count = [ArtV2.Render]::Run($root, $fonts, [string[]]$Group, [string[]]$Only)
if ($count -eq 0) { throw 'No file matches; nothing drawn' }
"Drew $count files."
