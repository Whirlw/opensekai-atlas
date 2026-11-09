# Imports the delivered audio pack into StreamingAssets and writes the manifest the game reads.
# Layout produced:
#   StreamingAssets/sound/<bundle>/<cue>.wav
#   StreamingAssets/sound/manifest.csv        (bundle,cue)
# Bundle names match the ones SoundManager asks for, so the file name is the interface.
param(
    [string]$Pack = 'E:\Projects\NewSkin\audio-pack',
    [string]$Target = 'Assets\StreamingAssets\sound',
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'

# folder in the pack -> bundle name the game loads
$bundles = [ordered]@{
    'custom01'        = 'live/tap_se/custom01'
    'custom02'        = 'live/tap_se/custom02'
    'default'         = 'live/sound/live_se/default'
    'live_se_common'  = 'live/sound/live_se/common'
}

function Read-WavHeader([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 44) { return $null }
    $riff = [System.Text.Encoding]::ASCII.GetString($bytes, 0, 4)
    $wave = [System.Text.Encoding]::ASCII.GetString($bytes, 8, 4)
    if ($riff -ne 'RIFF' -or $wave -ne 'WAVE') { return $null }

    $channels = 0; $rate = 0; $bits = 0; $format = 0
    $offset = 12
    while ($offset + 8 -le $bytes.Length) {
        $id = [System.Text.Encoding]::ASCII.GetString($bytes, $offset, 4)
        $size = [System.BitConverter]::ToInt32($bytes, $offset + 4)
        if ($id -eq 'fmt ') {
            $format = [System.BitConverter]::ToInt16($bytes, $offset + 8)
            $channels = [System.BitConverter]::ToInt16($bytes, $offset + 10)
            $rate = [System.BitConverter]::ToInt32($bytes, $offset + 12)
            $bits = [System.BitConverter]::ToInt16($bytes, $offset + 22)
            break
        }
        $offset += 8 + $size + ($size % 2)
    }

    [pscustomobject]@{ Channels = $channels; Rate = $rate; Bits = $bits; Format = $format }
}

$rows = @()
$problems = @()
$copied = 0
foreach ($folder in $bundles.Keys) {
    $sourceDir = Join-Path $Pack $folder
    if (-not (Test-Path $sourceDir)) { $problems += "missing folder: $folder"; continue }
    $bundle = $bundles[$folder]
    $targetDir = Join-Path $Target $bundle
    if ($Apply) { New-Item -ItemType Directory -Force -Path $targetDir | Out-Null }

    foreach ($file in Get-ChildItem $sourceDir -File -Filter '*.wav') {
        $header = Read-WavHeader $file.FullName
        if ($null -eq $header) { $problems += "$folder/$($file.Name): not a RIFF WAVE"; continue }
        if ($header.Rate -ne 44100) { $problems += "$folder/$($file.Name): $($header.Rate) Hz"; continue }
        if ($header.Bits -ne 16 -or $header.Format -ne 1) { $problems += "$folder/$($file.Name): not pcm_s16le"; continue }

        $rows += [pscustomobject]@{ Bundle = $bundle; Cue = $file.BaseName; Channels = $header.Channels; File = "$folder/$($file.Name)" }
        if ($Apply) {
            Copy-Item $file.FullName (Join-Path $targetDir $file.Name) -Force
            $copied++
        }
    }
}

if ($Apply) {
    $manifest = Join-Path $Target 'manifest.csv'
    New-Item -ItemType Directory -Force -Path $Target | Out-Null
    $lines = @('bundle,cue') + ($rows | ForEach-Object { "$($_.Bundle),$($_.Cue)" })
    Set-Content $manifest ($lines -join "`n") -Encoding UTF8 -NoNewline
}

"wavs=$($rows.Count) copied=$copied problems=$($problems.Count) bundles=$($bundles.Count)"
if ($problems.Count) { '--- problems ---'; $problems | Select-Object -First 10 }
$rows | Group-Object Bundle | ForEach-Object { "  {0,-32} {1}" -f $_.Name, $_.Count }
