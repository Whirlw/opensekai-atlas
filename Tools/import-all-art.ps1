$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
Write-Host 'Importing whole textures...'
& (Join-Path $PSScriptRoot 'apply-paint-kit.ps1') -Apply
Write-Host 'Importing atlas sprites...'
& (Join-Path $PSScriptRoot 'apply-atlas-art.ps1') -Apply
Write-Host 'Fitting lane sprite meshes...'
& (Join-Path $PSScriptRoot 'fit-lane-sprite-meshes.ps1')
Write-Host 'Import complete. Refresh assets in Unity and rebuild AssetBundles if needed.'
