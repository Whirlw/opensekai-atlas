# Draws the song info page sprites into ArtSource/MusicInfo and copies them into
# Assets/Graphics/Textures/MusicInfo. Import settings and the prefab layout are applied by the
# editor menu OpenSekai/Art/Rebuild Music Info Page (MusicInfoPageBuilder.cs).
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'MusicInfoArt.cs') -ReferencedAssemblies System.Drawing
[MusicInfoArt]::Run($root)
$project=Split-Path $root -Parent
$dest=Join-Path $project 'Assets/Graphics/Textures/MusicInfo'
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $root 'MusicInfo/*.png') $dest -Force
