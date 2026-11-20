# Research probe: downloads one public level's resources, never installs it into the game.
[CmdletBinding()]
param(
    [uri]$Server = 'https://untitledcharts.com',
    [string]$LevelName,
    [string]$Keywords = 'hallway',
    [string]$OutputDirectory = (Join-Path $env:TEMP ('OpenSekai-Sonolus-' + [guid]::NewGuid().ToString('N'))),
    [switch]$IncludeMedia
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$base = $Server.AbsoluteUri.TrimEnd('/')
if ($Server.Scheme -ne 'https') { throw 'Use an HTTPS server address.' }
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

function Get-Api([string]$Path) {
    $response = Invoke-WebRequest -Uri ($base + $Path) -TimeoutSec 30
    if ($response.StatusCode -ne 200) { throw "Unexpected status for ${Path}: $($response.StatusCode)" }
    $script:sonolusVersion = $response.Headers['Sonolus-Version'] -join ','
    return ($response.Content | ConvertFrom-Json)
}
function Save-Json($Value, [string]$Name) {
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $OutputDirectory $Name) -Encoding utf8
}
function Get-Resource($Srl, [string]$Name) {
    if (-not $Srl.url) { throw "Missing resource URL: $Name" }
    # Sonolus leading-slash paths are relative to the server address, including its prefix.
    $url = if ($Srl.url.StartsWith('/')) { $base + $Srl.url } else { $Srl.url }
    if (([uri]$url).Scheme -ne 'https') { throw "Non-HTTPS resource: $Name" }
    $path = Join-Path $OutputDirectory $Name
    Invoke-WebRequest -Uri $url -OutFile $path -TimeoutSec 60
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA1).Hash.ToLowerInvariant()
    if ($Srl.hash -and $actual -ne $Srl.hash.ToLowerInvariant()) { throw "SHA-1 mismatch: $Name" }
    return [ordered]@{ bytes = (Get-Item -LiteralPath $path).Length; sha1 = $actual; verified = [bool]$Srl.hash }
}

$info = Get-Api '/sonolus/info'
$levelsInfo = Get-Api '/sonolus/levels/info'
$list = Get-Api '/sonolus/levels/list?page=0'
$page1 = Get-Api '/sonolus/levels/list?page=1'
$search = Get-Api ('/sonolus/levels/list?type=quick&keywords=' + [uri]::EscapeDataString($Keywords) + '&page=0')
if (-not $LevelName) {
    if (-not $list.items.Count) { throw 'Public level list is empty; provide -LevelName.' }
    $LevelName = $list.items[0].name
}
$details = Get-Api ('/sonolus/levels/' + [uri]::EscapeDataString($LevelName))
$item = $details.item
if (-not $item) { throw 'Level details did not contain item.' }
Save-Json $details 'details.json'
Save-Json $levelsInfo 'levels-info.json'
$resources = [ordered]@{}
$resources.data = Get-Resource $item.data 'level-data.json.gz'
$inputStream = [IO.File]::OpenRead((Join-Path $OutputDirectory 'level-data.json.gz'))
try {
    $gzip = [IO.Compression.GZipStream]::new($inputStream, [IO.Compression.CompressionMode]::Decompress)
    try {
        $reader = [IO.StreamReader]::new($gzip)
        try { $data = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    } finally { $gzip.Dispose() }
} finally { $inputStream.Dispose() }
Save-Json $data 'level-data.json'
if ($IncludeMedia) {
    $resources.bgm = Get-Resource $item.bgm 'bgm.bin'
    $resources.cover = Get-Resource $item.cover 'cover.bin'
}
$archetypes = @($data.entities | Group-Object archetype | Sort-Object Name | ForEach-Object {
    [ordered]@{ name = $_.Name; count = $_.Count; example = $_.Group[0] }
})
$report = [ordered]@{
    checkedAtUtc = [DateTime]::UtcNow.ToString('o')
    server = $base; title = $info.title; sonolusVersion = $script:sonolusVersion
    anonymous = $true; pageCount = $list.pageCount; page0Items = @($list.items).Count
    page1First = $page1.items[0].name; searchKeywords = $Keywords; searchItems = @($search.items).Count
    level = $item.name; levelTitle = $item.title; rating = $item.rating
    engine = $item.engine.name; engineVersion = $item.engine.version
    engineDescription = $item.engine.description
    bgmOffset = $data.bgmOffset; entityCount = @($data.entities).Count
    resources = $resources; archetypes = $archetypes
}
Save-Json $report 'report.json'
[pscustomobject]@{ Server = $base; Level = $item.name; Engine = $item.engine.name; Entities = @($data.entities).Count; Report = (Join-Path $OutputDirectory 'report.json') }
