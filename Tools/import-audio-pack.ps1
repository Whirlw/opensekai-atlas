# Import project audio sources into StreamingAssets. Validate the whole pack before writing.
param(
    [string]$Pack = (Join-Path $PSScriptRoot '..\AudioSource'),
    [string]$Target = (Join-Path $PSScriptRoot '..\Assets\StreamingAssets\sound'),
    [string]$Ffmpeg = 'ffmpeg',
    [switch]$Apply,
    [switch]$AllowRemove
)
$ErrorActionPreference = 'Stop'
$bundles = [ordered]@{
    'live_se_key' = 'live/tap_se/live_se_key'
    'live_se_common' = 'live/sound/live_se/common'
}

# Pad the meter input only: a short hit still needs one complete 400 ms R128 block.
function Measure-HitSound([string]$path) {
    $meter = 'apad=whole_dur=0.4,ebur128=metadata=1:peak=true,ametadata=print:file=-'
    $readings = & $Ffmpeg -nostdin -v error -i $path -af $meter -f null NUL
    if ($LASTEXITCODE -ne 0) { throw "Could not measure $path" }
    $loudness = $null
    $peak = 0.0
    foreach ($line in $readings) {
        if ($line -match '^lavfi\.r128\.I=([-\d.]+)$') { $loudness = [double]$Matches[1] }
        if ($line -match '^lavfi\.r128\.true_peak=([\d.]+)$') { $peak = [Math]::Max($peak, [double]$Matches[1]) }
    }
    if ($null -eq $loudness -or $loudness -le -70) { throw "No measurable hit sound in $path" }
    return [pscustomobject]@{ Lufs = $loudness; Peak = $peak }
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
# Integrated LUFS measured from the corresponding LocalPacks/upstream-audio/custom01 hits.
$targets = @{
    se_live_connect           = -21.950
    se_live_connect_critical  = -20.830
    se_live_critical          = -19.015
    se_live_flick             = -18.033
    se_live_flick_critical    = -16.774
    se_live_good              = -19.710
    se_live_great             = -18.410
    se_live_long              = -20.722
    se_live_long_critical     = -18.785
    se_live_perfect           = -21.199
    se_live_tap               = -30.200
    se_live_trace             = -19.250
    se_live_trace_critical    = -16.782
}
foreach ($folder in $bundles.Keys) {
    $sourceDir = Join-Path $Pack $folder
    if (-not (Test-Path -LiteralPath $sourceDir -PathType Container)) { $problems += "missing folder: $sourceDir"; continue }
    [string[]]$paths = [IO.Directory]::GetFiles((Resolve-Path -LiteralPath $sourceDir).Path, '*.wav')
    if ($paths.Length -eq 0) { $problems += "no WAV files: $sourceDir"; continue }
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    foreach ($path in $paths) {
        $file = [IO.FileInfo]$path
        $header = Read-WavHeader $file.FullName
        $pcm16 = $null -ne $header -and $header.Format -eq 1 -and $header.Bits -eq 16
        $hitFloat = $folder -eq 'live_se_key' -and $null -ne $header -and $header.Format -eq 3 -and $header.Bits -eq 32
        if ((-not $pcm16 -and -not $hitFloat) -or $header.Rate -notin @(44100, 48000) -or $header.Channels -notin @(1, 2)) {
            $problems += "$($file.FullName): expected 44100 or 48000 Hz mono/stereo PCM16 WAV (live_se_key also accepts float32)"
            continue
        }
        if ($folder -eq 'live_se_key' -and -not $targets.ContainsKey($file.BaseName)) { $problems += "missing loudness target: $($file.Name)"; continue }
        if ($file.BaseName -match '[,\r\n]') { $problems += "invalid cue name: $($file.Name)"; continue }
        $rows += [pscustomobject]@{ Bundle = $bundles[$folder]; Cue = $file.BaseName; Source = $file.FullName; Name = $file.Name }
    }
}
foreach ($cue in $targets.Keys) {
    if (-not (Test-Path -LiteralPath (Join-Path $Pack "live_se_key/$cue.wav") -PathType Leaf)) { $problems += "missing hit sound: $cue" }
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
$staging = New-Item -ItemType Directory -Path (Join-Path ([IO.Path]::GetTempPath()) ('opensekai-audio-' + [Guid]::NewGuid()))
try {
    foreach ($row in $rows) {
        if ($row.Bundle -ne $bundles['live_se_key']) { continue }
        $targetLufs = $targets[$row.Cue]
        $inputLevel = Measure-HitSound $row.Source
        $gain = ($targetLufs - $inputLevel.Lufs).ToString('F4', [Globalization.CultureInfo]::InvariantCulture)
        $render = Join-Path $staging.FullName $row.Name
        & $Ffmpeg -nostdin -v error -i $row.Source -map_metadata -1 -af "volume=${gain}dB" -c:a pcm_s16le -fflags +bitexact $render
        if ($LASTEXITCODE -ne 0) { throw "Could not render $($row.Name)" }
        $level = Measure-HitSound $render
        if ($level.Peak -ge 1 -or [Math]::Abs($level.Lufs - $targetLufs) -gt 0.1) {
            throw "$($row.Name): expected $targetLufs LUFS without clipping, got $($level.Lufs) LUFS, true peak $($level.Peak)"
        }
        $row.Source = $render
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
} finally {
    foreach ($file in (Get-ChildItem -LiteralPath $staging.FullName -File)) { Remove-Item -LiteralPath $file.FullName }
    Remove-Item -LiteralPath $staging.FullName
}
Write-Host "Validated $($rows.Count) WAV files in $($bundles.Count) groups. Applied: $([bool]$Apply)"
$rows | Group-Object Bundle | ForEach-Object { Write-Host "  $($_.Name): $($_.Count)" }
