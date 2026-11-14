param([switch]$MainTitlesOnly,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,PresentationCore,WindowsBase
Add-Type -Path (Join-Path $PSScriptRoot 'ResultEffects.cs') -ReferencedAssemblies System.Drawing,PresentationCore,WindowsBase
[ResultEffects]::Run((Split-Path $PSScriptRoot -Parent),$OutputDirectory,$MainTitlesOnly.IsPresent)
