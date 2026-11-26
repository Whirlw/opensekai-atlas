# Regression for two screenshot runs sharing an output folder. Only the scratch copy is changed.
$ErrorActionPreference = 'Stop'
$export = Join-Path $PSScriptRoot 'export-style-guide-images.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('style-guide-source-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $temp 'shots'
$destination = Join-Path $temp 'docs'
$report = Join-Path $source 'report.json'
$frame = Join-Path $source 'result-fc.png'
$sampleFrame = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGPg0nADAAC4AHl3QfnaAAAAAElFTkSuQmCC')

$personalFrame = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGO40y4CAAO6AXi1+iEyAAAAAElFTkSuQmCC')

function Write-Report([bool]$sample, [string]$hash) {
    @{ sampleLibrary = $sample; shots = @(@{ ok = $true; kind = 'result'; file = 'result-fc.png'; sha256 = $hash }) } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $report -Encoding UTF8
}

try {
    New-Item -ItemType Directory -Path (Join-Path $source 'sample') -Force | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::Open((Join-Path $source 'sample/sample-pack.zip'), 'Create').Dispose()
    [IO.File]::WriteAllBytes($frame, $sampleFrame)
    $sampleHash = (Get-FileHash -LiteralPath $frame -Algorithm SHA256).Hash
    Write-Report $true $sampleHash

    # A regular editor captures the same result preset into the same output directory.
    [IO.File]::WriteAllBytes($frame, $personalFrame)
    Write-Report $false $null
    try {
        & $export -Source $source -Destination $destination
        throw 'Export accepted a non-sample run.'
    } catch {
        if ($_.Exception.Message -notlike '*completed sample library run*') {
            throw "Expected rejection of the current non-sample report before rendering. Actual: $_"
        }
    }
    if (Test-Path -LiteralPath $destination) { throw 'Rejected capture changed the destination.' }
    Write-Host 'PASS: an older sample pack cannot authorize a later ordinary capture.'

    . (Join-Path $PSScriptRoot 'style-guide-frames.ps1')
    # The ordinary capture has overwritten the PNG but its run has not published a new report yet.
    Write-Report $true $sampleHash
    try {
        Use-StyleGuideFrames $source @('result-fc.png') { throw 'Unexpected render of a replaced frame.' }
        throw 'The replaced PNG passed validation.'
    } catch {
        if ($_.Exception.Message -notlike 'Frame changed since the sample capture:*') { throw }
    }
    Write-Host 'PASS: the old sample report cannot authorize a replaced PNG.'

    [IO.File]::WriteAllBytes($frame, $sampleFrame)
    $verifiedHash = Use-StyleGuideFrames $source @('result-fc.png') {
        param($streams)
        (Get-FileHash -InputStream $streams['result-fc.png'] -Algorithm SHA256).Hash
    }
    if ($verifiedHash -ne $sampleHash) { throw 'The verified sample bytes were unavailable to the renderer.' }
    try {
        Use-StyleGuideFrames $source @('result-fc.png','info_001.500s.png') { throw 'Unexpected partial render.' }
        throw 'Export continued with a frame absent from the report.'
    } catch {
        if ($_.Exception.Message -notlike 'Missing frame from the sample capture:*') { throw }
    }
    Write-Host 'PASS: verified sample bytes reach rendering and omitted frames block the entire export.'
} finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
