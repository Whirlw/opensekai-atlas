# Downloads a native NextRUSH or NextRUSH+ package for the game's existing .oskp/.zip import.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][uri]$LevelUrl,
    [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
if ($LevelUrl.Scheme -notin @('https', 'sonolus')) { throw 'Use an HTTPS share link or a sonolus:// level link.' }
if ($LevelUrl.Query -or $LevelUrl.Fragment) { throw 'Use the direct /levels/{name} link without query or fragment.' }
$match = [regex]::Match($LevelUrl.AbsolutePath, '^(.*)/levels/([^/]+)/?$')
if (-not $match.Success) { throw 'Expected a direct /levels/{name} link.' }
$server = 'https://' + $LevelUrl.Authority + $match.Groups[1].Value
$name = [uri]::UnescapeDataString($match.Groups[2].Value)
$work = Join-Path ([IO.Path]::GetTempPath()) ('OpenSekai-NextRush-' + [guid]::NewGuid().ToString('N'))
$probe = & (Join-Path $PSScriptRoot 'probe-sonolus.ps1') -Server $server -LevelName $name -IncludeMedia -OutputDirectory $work
$details = Get-Content -LiteralPath (Join-Path $work 'details.json') -Raw | ConvertFrom-Json
$item = $details.item
if ($item.engine.name -cnotin @('NextRUSH_P', 'next-rush')) { throw "Unsupported engine: $($item.engine.name). Expected NextRUSH or NextRUSH+." }
$package = Join-Path $work 'package'
$null = New-Item -ItemType Directory -Path $package

function Get-MediaExtension([string]$Path, [bool]$Audio) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 12) { throw 'Resource is too short to identify.' }
    $prefix = [Text.Encoding]::ASCII.GetString($bytes, 0, 4)
    if ($Audio) {
        if ($prefix.StartsWith('ID3') -or ($bytes[0] -eq 255 -and ($bytes[1] -band 224) -eq 224)) { return '.mp3' }
        if ($prefix -eq 'OggS') { return '.ogg' }
        if ($prefix -eq 'RIFF' -and [Text.Encoding]::ASCII.GetString($bytes, 8, 4) -eq 'WAVE') { return '.wav' }
    } else {
        if ([BitConverter]::ToString($bytes, 0, 8) -eq '89-50-4E-47-0D-0A-1A-0A') { return '.png' }
        if ($bytes[0] -eq 255 -and $bytes[1] -eq 216 -and $bytes[2] -eq 255) { return '.jpg' }
    }
    throw 'Unsupported media format.'
}
$audio = 'audio' + (Get-MediaExtension (Join-Path $work 'bgm.bin') $true)
$cover = 'jacket' + (Get-MediaExtension (Join-Path $work 'cover.bin') $false)
Copy-Item -LiteralPath (Join-Path $work 'level-data.json.gz') -Destination (Join-Path $package 'level-data.json.gz')
Copy-Item -LiteralPath (Join-Path $work 'bgm.bin') -Destination (Join-Path $package $audio)
Copy-Item -LiteralPath (Join-Path $work 'cover.bin') -Destination (Join-Path $package $cover)
$difficulty = $item.tags | ForEach-Object { $_.title } | Where-Object { $_ -in @('easy', 'normal', 'hard', 'expert', 'master', 'append') } | Select-Object -First 1
if ($difficulty) { $difficulty = $difficulty.ToLowerInvariant() } else { $difficulty = 'master' }
$manifest = [ordered]@{
    formatVersion = 1; id = [guid]::NewGuid().ToString('N'); chartFormat = 'next-rush'
    title = $item.title; scoreTitle = $item.title; composer = $item.artists; userName = $item.author
    description = $details.description; playLevel = $item.rating; musicDifficultyType = $difficulty
    audioFileName = $audio; jacketFileName = $cover; scoreFileName = 'level-data.json.gz'
    fillerSec = 0; secForMusicScoreMaker = 0
}
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $package 'manifest.json') -Encoding utf8
$details | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $package 'sonolus-source.json') -Encoding utf8
if (-not $OutputPath) { $OutputPath = Join-Path $work 'chart.oskp' }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if ([IO.File]::Exists($OutputPath)) { throw "Output already exists: $OutputPath" }
if (-not [IO.Directory]::Exists([IO.Path]::GetDirectoryName($OutputPath))) { throw 'Output directory does not exist.' }
[IO.Compression.ZipFile]::CreateFromDirectory($package, $OutputPath)
[pscustomobject]@{ Title = $item.title; Engine = $item.engine.name; Package = $OutputPath; Report = $probe.Report }
Write-Host 'Import this package in the library. Playback will check the supported native chart features.'
