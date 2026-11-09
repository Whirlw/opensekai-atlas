param(
    [string]$Layer = '',
    [string]$Package = 'io.github.opensekai',
    [string]$Activity = 'com.opensekai.CustomUnityPlayerActivity'
)

# Measures on-device frame pacing through SurfaceFlinger. The Unity surface reports
# (desiredPresentTime, actualPresentTime, frameReadyTime) in nanoseconds, newest first.
$adbCandidates = @(
    'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe',
    'adb'
)
$adb = $adbCandidates | Where-Object { if ($_ -eq 'adb') { $null -ne (Get-Command adb -ErrorAction SilentlyContinue) } else { Test-Path $_ } } | Select-Object -First 1
if (-not $adb) { throw 'adb not found' }

if (-not $Layer) {
    $list = & $adb shell dumpsys SurfaceFlinger --list 2>&1
    $match = $list | Select-String -Pattern "SurfaceView\[$([regex]::Escape($Package))/" | Select-Object -Last 1
    if (-not $match) { throw "SurfaceView layer for $Package not found; is the app in the foreground?" }
    $raw = $match.Line
    if ($raw -match '\{([^}]*SurfaceView\[[^\]]*\][^}]*)parentId') { $raw = $Matches[1] }
    $raw = $raw -replace '^\s*\d+\s+', ''
    $Layer = ($raw -replace '\}\s*$', '').Trim()
}

function Get-Stats([string]$label) {
    $dump = & $adb shell "dumpsys SurfaceFlinger --latency '$Layer'" 2>&1
    $rows = @($dump | Where-Object { $_ -match '^\d+\s+\d+\s+\d+' })
    if ($rows.Count -lt 2) { return "$label : no frames" }

    $times = foreach ($row in $rows) { [double](($row -split '\s+')[0]) }
    $times = @($times | Where-Object { $_ -gt 0 })
    if ($times.Count -lt 2) { return "$label : no frames" }

    # SurfaceFlinger prints newest first, so ascending order is the reverse.
    [array]::Reverse($times)
    $n = $times.Count
    $span = ($times[$n - 1] - $times[0]) / 1e9
    $fps = ($n - 1) / $span

    $deltas = for ($i = 1; $i -lt $n; $i++) { ($times[$i] - $times[$i - 1]) / 1e6 }
    $sorted = $deltas | Sort-Object
    $p50 = $sorted[[int][Math]::Floor($sorted.Count * 0.50)]
    $p95 = $sorted[[int][Math]::Floor($sorted.Count * 0.95)]
    $max = ($deltas | Measure-Object -Maximum).Maximum
    $janky = @($deltas | Where-Object { $_ -gt $p50 * 1.5 }).Count

    return ('{0}: frames={1} span={2:N2}s fps={3:N1} p50={4:N2}ms p95={5:N2}ms max={6:N2}ms janky={7}' -f `
        $label, $n, $span, $fps, $p50, $p95, $max, $janky)
}

"layer: $Layer"
Get-Stats 'sample-1'
Start-Sleep -Seconds 2
Get-Stats 'sample-2'
