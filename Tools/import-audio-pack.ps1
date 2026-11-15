# Import project audio sources into StreamingAssets. Validate the whole pack before writing.
param(
    [string]$Pack = (Join-Path $PSScriptRoot '..\AudioSource'),
    [string]$Target = (Join-Path $PSScriptRoot '..\Assets\StreamingAssets\sound'),
    [switch]$Apply,
    [switch]$AllowRemove
)
$ErrorActionPreference = 'Stop'
$bundles = [ordered]@{
    'custom01' = 'live/tap_se/custom01'
    'custom02' = 'live/tap_se/custom02'
    'default' = 'live/sound/live_se/default'
    'live_se_common' = 'live/sound/live_se/common'
}

function Read-WavHeader([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 12 -or [Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne 'RIFF' -or [Text.Encoding]::ASCII.GetString($bytes, 8, 4) -ne 'WAVE') { return $null }
    $offset = 12
    while ($offset + 8 -le $bytes.Length) {
        $id = [Text.Encoding]::ASCII.GetString($bytes, $offset, 4)
        $size = [BitConverter]::ToUInt32($bytes, $offset + 4)
        if (($offset + 8 + [long]$size) -gt $bytes.Length) { return $null }
        if ($id -eq 'fmt ') {
            if ($size -lt 16) { return $null }
            return [pscustomobject]@{ Format = [BitConverter]::ToUInt16($bytes, $offset + 8); Channels = [BitConverter]::ToUInt16($bytes, $offset + 10); Rate = [BitConverter]::ToUInt32($bytes, $offset + 12); Bits = [BitConverter]::ToUInt16($bytes, $offset + 22) }
        }
        $offset += 8 + [long]$size + ($size % 2)
    }
    return $null
}

$rows = @()
$problems = @()
foreach ($folder in $bundles.Keys) {
    $sourceDir = Join-Path $Pack $folder
    if (-not (Test-Path -LiteralPath $sourceDir -PathType Container)) { $problems += "missing folder: $sourceDir"; continue }
    $files = @(Get-ChildItem -LiteralPath $sourceDir -File -Filter '*.wav' | Sort-Object Name)
    if ($files.Count -eq 0) { $problems += "no WAV files: $sourceDir"; continue }
    foreach ($file in $files) {
        $header = Read-WavHeader $file.FullName
        if ($null -eq $header -or $header.Format -ne 1 -or $header.Rate -notin @(44100, 48000) -or $header.Bits -ne 16 -or $header.Channels -notin @(1, 2)) {
            $problems += "$($file.FullName): expected 44100 or 48000 Hz, 16-bit PCM mono or stereo WAV"
            continue
        }
        if ($file.BaseName -match '[,\r\n]') { $problems += "invalid cue name: $($file.Name)"; continue }
        $rows += [pscustomobject]@{ Bundle = $bundles[$folder]; Cue = $file.BaseName; Source = $file.FullName; Name = $file.Name }
    }
}
$manifest = Join-Path $Target 'manifest.csv'
if ((Test-Path -LiteralPath $manifest) -and -not $AllowRemove) {
    $keys = @{}
    foreach ($row in $rows) { $keys[($row.Bundle + '|' + $row.Cue)] = $true }
    foreach ($old in @(Import-Csv -LiteralPath $manifest)) {
        if (-not $keys.ContainsKey($old.bundle + '|' + $old.cue)) { $problems += "existing cue missing from pack: $($old.bundle)/$($old.cue) (use -AllowRemove to remove intentionally)" }
    }
}
if ($problems.Count) {
    $problems | ForEach-Object { Write-Host "ERROR: $_" }
    Write-Host 'Validation failed; no files were changed.'
    exit 1
}
if ($Apply) {
    foreach ($row in $rows) {
        $targetDir = Join-Path $Target $row.Bundle
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
        Copy-Item -LiteralPath $row.Source -Destination (Join-Path $targetDir $row.Name) -Force
    }
    New-Item -ItemType Directory -Force -Path $Target | Out-Null
    $lines = @('bundle,cue') + @($rows | ForEach-Object { "$($_.Bundle),$($_.Cue)" })
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($manifest), (($lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
}
Write-Host "Validated $($rows.Count) WAV files in $($bundles.Count) groups. Applied: $([bool]$Apply)"
$rows | Group-Object Bundle | ForEach-Object { Write-Host "  $($_.Name): $($_.Count)" }
