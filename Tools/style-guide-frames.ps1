# Keep the verified files open through rendering so a subsequent capture cannot replace the pixels being exported.
function Use-StyleGuideFrames([string]$Source, [string[]]$Files, [scriptblock]$Render) {
    $reportPath = Join-Path $Source 'report.json'
    $report = if (Test-Path -LiteralPath $reportPath -PathType Leaf) {
        Get-Content -Raw -Encoding UTF8 -LiteralPath $reportPath | ConvertFrom-Json
    }
    if ($report.sampleLibrary -isnot [bool] -or !$report.sampleLibrary) {
        throw 'Style guide export requires a completed sample library run. Capture again with -StyleGuide in a sample workspace.'
    }
    $hashes = @{}
    foreach ($shot in $report.shots) {
        if ($shot.ok -and $shot.sha256) { $hashes[$shot.file] = $shot.sha256 }
    }
    $streams = @{}
    try {
        foreach ($name in ($Files | Sort-Object -Unique)) {
            $path = Join-Path $Source $name
            if (!$hashes.ContainsKey($name) -or !(Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "Missing frame from the sample capture: $name"
            }
            $stream = [IO.File]::OpenRead($path)
            $streams[$name] = $stream
            if ((Get-FileHash -InputStream $stream -Algorithm SHA256).Hash -ne $hashes[$name]) {
                throw "Frame changed since the sample capture: $name"
            }
            $stream.Position = 0
        }
        & $Render $streams
    } finally {
        foreach ($stream in $streams.Values) { $stream.Dispose() }
    }
}
