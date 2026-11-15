param([string[]]$Only)
$ErrorActionPreference='Stop'
if($PSBoundParameters.ContainsKey('Only') -and (-not $Only -or $Only.Count -eq 0)){throw 'No background files selected; nothing rendered'}
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'AstralBackgrounds.cs') -ReferencedAssemblies System.Drawing
[AstralBackgrounds]::Run((Split-Path $PSScriptRoot -Parent),$Only)
