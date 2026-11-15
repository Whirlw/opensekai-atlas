$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'PaletteVariant.cs') -ReferencedAssemblies System.Drawing
$root=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'NotesV2'
$source=Join-Path (Split-Path $root -Parent) 'Notes'
$before=@(Get-ChildItem -LiteralPath $source -Recurse -File -Filter *.png | Get-FileHash)
[PaletteVariant]::Build($source,(Join-Path $root '.'))
foreach($file in $before){if((Get-FileHash -LiteralPath $file.Path).Hash -ne $file.Hash){throw 'Source new_notes was modified'}}
Write-Output 'PASS: original new_notes preserved unchanged.'
