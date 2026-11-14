param([switch]$BandsOnly,[switch]$FramesOnly)
$ErrorActionPreference='Stop'
if($BandsOnly -and $FramesOnly){throw 'Select one effect group at a time'}
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'NoteEffects.cs') -ReferencedAssemblies System.Drawing,System.Core
if($FramesOnly){[NoteEffects]::UpdateFrames((Split-Path $PSScriptRoot -Parent))}
elseif($BandsOnly){[NoteEffects]::UpdateBandBeams((Split-Path $PSScriptRoot -Parent))}
else{[NoteEffects]::Run((Split-Path $PSScriptRoot -Parent))}
