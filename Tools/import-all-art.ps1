# Imports the gameplay textures and atlas sprites from ArtSource into Assets.
param([string]$KitRoot = 'ArtSource')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
Write-Host 'Importing whole textures...'
& (Join-Path $PSScriptRoot 'apply-paint-kit.ps1') -KitRoot $KitRoot -Apply
Write-Host 'Importing atlas sprites...'
& (Join-Path $PSScriptRoot 'apply-atlas-art.ps1') -KitRoot $KitRoot -Apply
Write-Host 'Importing the countdown digits, three cells of one texture...'
& (Join-Path $PSScriptRoot 'apply-atlas-art.ps1') -KitRoot $KitRoot -SpriteFolder 'Assets\Graphics\Resources\effects\live_countdown\texture' -Apply
Write-Host 'Fitting lane and countdown sprite meshes...'
& (Join-Path $PSScriptRoot 'fit-lane-sprite-meshes.ps1')
& (Join-Path $PSScriptRoot 'fit-countdown-sprite-meshes.ps1')
Write-Host 'Import complete. Refresh assets in Unity and rebuild AssetBundles if needed.'
