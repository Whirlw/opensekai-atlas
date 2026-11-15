param([string[]]$Only)
$ErrorActionPreference='Stop'
if($PSBoundParameters.ContainsKey('Only') -and (-not $Only -or $Only.Count -eq 0)){throw 'No matching HUD images selected; nothing rendered'}
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing,PresentationCore,WindowsBase
Add-Type -Path (Join-Path $PSScriptRoot 'AstralHud.cs') -ReferencedAssemblies System.Drawing,PresentationCore,WindowsBase,System.Core
[AstralHud]::Run($root,$Only)
