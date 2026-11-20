#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [string]$Path
)

$ErrorActionPreference = 'Stop'
$culture = [Globalization.CultureInfo]::InvariantCulture
$file = (Resolve-Path -LiteralPath $Path).ProviderPath
$header = Get-Content -LiteralPath $file -TotalCount 3
if ($header.Count -ne 3 -or $header[2] -notmatch '^# end=\S+ rows=(\d+) overflow=(\d+)(?: probe_overflow=(\d+))?$') {
    throw 'Missing capture metadata or row counts.'
}
$expectedRows = [int]$Matches[1]
$overflow = [int]$Matches[2]
$probeOverflow = if ($Matches[3]) { [int]$Matches[3] } else { 0 }
$rows = @(Get-Content -LiteralPath $file | Select-Object -Skip 3 | ConvertFrom-Csv -Delimiter "`t")
if ($rows.Count -ne $expectedRows) {
    throw "Incomplete capture. Expected $expectedRows rows, read $($rows.Count)."
}
if ($probeOverflow) {
    Write-Warning "The Android probe buffer discarded $probeOverflow rows. Pairing is incomplete."
}
if ($overflow) {
    Write-Warning "The recorder discarded $overflow rows after reaching capacity. Keep this run separate."
}

function Number($value) {
    [double]::Parse($value, $culture)
}

$metrics = @{}
function Add-Sample([string]$metric, [double]$milliseconds) {
    if (-not $metrics.ContainsKey($metric)) {
        $metrics[$metric] = [System.Collections.Generic.List[double]]::new()
    }
    $metrics[$metric].Add($milliseconds)
}

function Edge-Key($row) {
    "$($row.finger)/$($row.touch_id)/$($row.phase)/$($row.source_s)"
}

$edges = @{}
$actionRows = @{}
$seRequests = @{}
$unmatchedSePlays = 0
$firstSeRequest = $null
$hasSePlayTrace = $rows.Count -gt 0 -and $null -ne $rows[0].PSObject.Properties['request_seq']
$hasMixTrace = $header[0] -match '(?:^| )mix_probe=True(?: |$)'
$unmeasuredMixPlays = @{}
$mixNoSignal = 0
$mixUnpaired = 0
$mixPaired = 0
$unmatchedConsumed = 0
$ambiguousEdges = 0
$consumedAt = @{}
$bgmId = ''
foreach ($row in $rows) {
    if ($row.stage -in 'bgm_start', 'segment_start') { $bgmId = $row.value }

    switch ($row.stage) {
        'frame' {
            if ($row.detail -eq 'bgm') { Add-Sample 'frame (BGM)' ((Number $row.value) * 1000) }
        }
        'audio_sample' {
            if ($row.note_id -eq $bgmId) { Add-Sample 'BGM clock minus source position' (Number $row.value) }
        }
        'press_spent' {
            if ($row.phase -eq 'Began' -and $row.result -ne 'None') {
                Add-Sample 'press error (signed)' (Number $row.value)
                Add-Sample 'press error (absolute)' ([Math]::Abs((Number $row.value)))
            }
        }
        'note_se' {
            if ($hasSePlayTrace) {
                $seRequests[$row.seq] = $row
                if ($null -eq $firstSeRequest) { $firstSeRequest = $row.seq }
            }
        }
        'note_se_play' {
            $request = $seRequests[$row.request_seq]
            if ($request) {
                $elapsed = ((Number $row.unity_s) - (Number $request.unity_s)) * 1000
                Add-Sample 'SE request to first AudioSource.Play' $elapsed
                if ($request.seq -eq $firstSeRequest) {
                    Add-Sample 'SE request to first AudioSource.Play (first request in segment)' $elapsed
                }
                $seRequests.Remove($row.request_seq)
                if ($hasMixTrace) { $unmeasuredMixPlays[$row.request_seq] = $true }
            } else {
                $unmatchedSePlays++
            }
        }
        'note_se_mix' {
            if (-not $unmeasuredMixPlays.ContainsKey($row.request_seq)) {
                $mixUnpaired++
            } else {
                $unmeasuredMixPlays.Remove($row.request_seq)
                $delay = Number $row.value
                if ($row.detail -eq 'source_filter' -and [double]::IsFinite($delay) -and $delay -ge 0) {
                    Add-Sample 'AudioSource.Play to first non-silent source filter block' $delay
                    $mixPaired++
                } else {
                    $mixNoSignal++
                }
            }
        }
    }

    if ($row.stage -in 'judged', 'note_effect', 'note_se') {
        $key = "$($row.frame)/$($row.note_id)/$($row.stage)"
        if ($actionRows.ContainsKey($key)) { $actionRows[$key] = $null }
        else { $actionRows[$key] = $row }
    }

    if ($row.phase -notin 'Began', 'Ended', 'Canceled') { continue }
    if ($row.stage -eq 'received') {
        $key = Edge-Key $row
        if ($edges.ContainsKey($key)) {
            $ambiguousEdges++
            $edges[$key] = $null
        } else {
            $edges[$key] = $row
        }
        Add-Sample "timestamp to callback ($($row.phase))" (((Number $row.unity_s) - (Number $row.source_s)) * 1000)
    } elseif ($row.stage -eq 'consumed' -and $row.detail -ne 'editor_mouse') {
        $key = Edge-Key $row
        if ($edges.ContainsKey($key)) {
            $received = $edges[$key]
            if ($received) {
                Add-Sample "callback to consumption ($($row.phase))" (((Number $row.unity_s) - (Number $received.unity_s)) * 1000)
                $consumedAt[$received.seq] = Number $row.unity_s
            }
            $edges.Remove($key)
        } else {
            $unmatchedConsumed++
        }
    }
}

foreach ($row in $rows) {
    if ($row.stage -ne 'press_spent' -or $row.phase -ne 'Began' -or $row.result -eq 'None') { continue }
    $judged = $actionRows["$($row.frame)/$($row.note_id)/judged"]
    if (-not $judged -or $judged.result -ne $row.result) { continue }
    foreach ($stage in 'note_effect', 'note_se') {
        $feedback = $actionRows["$($row.frame)/$($row.note_id)/$stage"]
        if ($feedback -and (Number $feedback.unity_s) -ge (Number $judged.unity_s)) {
            Add-Sample "judgment to $stage (same frame)" (((Number $feedback.unity_s) - (Number $judged.unity_s)) * 1000)
        }
    }
}

$unmatchedReceived = @($edges.Values | Where-Object { $null -ne $_ }).Count
Write-Host ($header -join [Environment]::NewLine)

# Android Activity side channel: map CLOCK_MONOTONIC to Unity realtime, pair edges, split the delay.
$androidEdges = @($rows | Where-Object stage -eq 'android_edge')
$anchors = @($rows | Where-Object stage -eq 'clock_anchor')
foreach ($probe in @($rows | Where-Object stage -eq 'android_probe')) { Write-Host "Android probe: $($probe.detail)" }
$androidReport = $null
if ($anchors.Count -ge 2) {
    # Least squares android = a + b * unity over anchors, centred for precision.
    $au = @($anchors | ForEach-Object { Number $_.unity_s })
    $aa = @($anchors | ForEach-Object { Number $_.android_receive_s })
    $aw = [System.Collections.Generic.List[double]]::new()
    foreach ($anchor in $anchors) { $aw.Add((Number $anchor.value)) }
    $mu = ($au | Measure-Object -Average).Average
    $ma = ($aa | Measure-Object -Average).Average
    $sxy = 0.0; $sxx = 0.0
    for ($i = 0; $i -lt $au.Count; $i++) {
        $sxy += ($au[$i] - $mu) * ($aa[$i] - $ma)
        $sxx += ($au[$i] - $mu) * ($au[$i] - $mu)
    }
    $slope = if ($sxx -gt 0) { $sxy / $sxx } else { 1.0 }
    $residuals = [System.Collections.Generic.List[double]]::new()
    for ($i = 0; $i -lt $au.Count; $i++) {
        $residuals.Add([Math]::Abs((($aa[$i] - $ma) - $slope * ($au[$i] - $mu)) * 1000))
    }
    $residuals.Sort(); $aw.Sort()
    $p95 = { param($v) $v[[int][Math]::Ceiling($v.Count * 0.95) - 1] }
    Write-Host ([string]::Format($culture,
        'Clock map: anchors={0} span={1:F1}s offset(android-unity)={2:F6}s drift={3:F2}ppm anchor_width p50={4:F4} p95={5:F4} ms fit_residual p95={6:F4} max={7:F4} ms',
        $anchors.Count, ($au | Measure-Object -Maximum).Maximum - ($au | Measure-Object -Minimum).Minimum, $ma - $mu,
        ($slope - 1) * 1e6, $aw[[int][Math]::Ceiling($aw.Count * 0.5) - 1], (& $p95 $aw), (& $p95 $residuals), $residuals[-1]))
    $toUnity = { param([double]$android) $mu + ($android - $ma) / $slope }

    # Pair each EnhancedTouch edge callback with the Activity edge of the same kind nearest in mapped event time.
    $tolerance = 0.004
    $pairs = [System.Collections.Generic.List[object]]::new()
    $unpairedUnity = [System.Collections.Generic.List[object]]::new()
    $unpairedAndroid = 0
    foreach ($phase in 'Began', 'Ended', 'Canceled') {
        $unityList = @($rows | Where-Object { $_.stage -eq 'received' -and $_.phase -eq $phase } |
            ForEach-Object { [pscustomobject]@{ Row = $_; T = Number $_.source_s } } | Sort-Object T)
        $androidList = @($androidEdges | Where-Object phase -eq $phase |
            ForEach-Object { [pscustomobject]@{ Row = $_; T = (& $toUnity (Number $_.android_event_s)); Used = $false } } | Sort-Object T)
        $j = 0
        foreach ($u in $unityList) {
            while ($j -lt $androidList.Count -and $androidList[$j].T -lt $u.T - $tolerance) { $j++ }
            $best = $null
            for ($k = $j; $k -lt $androidList.Count -and $androidList[$k].T -le $u.T + $tolerance; $k++) {
                $candidate = $androidList[$k]
                if (-not $candidate.Used -and ($null -eq $best -or [Math]::Abs($candidate.T - $u.T) -lt [Math]::Abs($best.T - $u.T))) { $best = $candidate }
            }
            if ($best) {
                $best.Used = $true
                $pairs.Add([pscustomobject]@{ Phase = $phase; Unity = $u.Row; Android = $best.Row; MappedEvent = $best.T })
            } else {
                $unpairedUnity.Add($u.Row)
            }
        }
        $unpairedAndroid += @($androidList | Where-Object { -not $_.Used }).Count
    }
    $unityEdgeCount = @($rows | Where-Object { $_.stage -eq 'received' -and $_.phase -in 'Began', 'Ended', 'Canceled' }).Count
    Write-Host ("Android pairing (+/-{0} ms): pairs={1} unity_edges={2} android_edges={3} unpaired_unity={4} unpaired_android={5} rate={6:P2}" -f
        ($tolerance * 1000), $pairs.Count, $unityEdgeCount, $androidEdges.Count, $unpairedUnity.Count, $unpairedAndroid,
        $(if ($unityEdgeCount) { $pairs.Count / $unityEdgeCount } else { 0 }))

    $fingerMatches = 0
    $tail = [System.Collections.Generic.List[object]]::new()
    $xs = [System.Collections.Generic.List[double[]]]::new()
    foreach ($pair in $pairs) {
        $u = $pair.Unity; $a = $pair.Android
        if ($u.finger -eq $a.touch_id) { $fingerMatches++ }
        $eventTime = Number $a.android_event_s
        $activity = Number $a.android_receive_s
        $callback = Number $u.unity_s
        $toActivity = ($activity - $eventTime) * 1000
        $toCallback = ($callback - (& $toUnity $activity)) * 1000
        Add-Sample "android: event to Activity ($($pair.Phase))" $toActivity
        Add-Sample "android: Activity to EnhancedTouch ($($pair.Phase))" $toCallback
        Add-Sample "android: mapped event minus Unity timestamp ($($pair.Phase), signed)" (($pair.MappedEvent - (Number $u.source_s)) * 1000)
        $consumed = $consumedAt[$u.seq]
        $toConsume = if ($null -ne $consumed) { ($consumed - $callback) * 1000 } else { [double]::NaN }
        $total = ($callback - (Number $u.source_s)) * 1000
        if ($total -ge 20) {
            $tail.Add([pscustomobject]@{
                Frame = $u.frame; Touch = $u.touch_id; Phase = $pair.Phase; 'ts->cb ms' = [Math]::Round($total, 2)
                'event->Activity' = [Math]::Round($toActivity, 2); 'Activity->ETouch' = [Math]::Round($toCallback, 2)
                'ETouch->consume' = [Math]::Round($toConsume, 2); Pointers = $a.finger
            })
        }
        if ($u.x -ne 'NaN' -and $a.x -ne 'NaN') { $xs.Add(@((Number $a.x), (Number $u.x), (Number $a.y), (Number $u.y))) }
    }
    if ($pairs.Count) { Write-Host ("Pointer id equals Unity finger index: {0}/{1}" -f $fingerMatches, $pairs.Count) }
    if ($xs.Count -ge 2) {
        # Unity screen space is the Activity view scaled to the render size, with y flipped.
        foreach ($axis in 0, 2) {
            $n = $xs.Count; $mx = 0.0; $my = 0.0
            foreach ($p in $xs) { $mx += $p[$axis]; $my += $p[$axis + 1] }
            $mx /= $n; $my /= $n; $cxy = 0.0; $cxx = 0.0
            foreach ($p in $xs) { $cxy += ($p[$axis] - $mx) * ($p[$axis + 1] - $my); $cxx += ($p[$axis] - $mx) * ($p[$axis] - $mx) }
            $b = if ($cxx -gt 0) { $cxy / $cxx } else { 0 }
            $err = [System.Collections.Generic.List[double]]::new()
            foreach ($p in $xs) { $err.Add([Math]::Abs($p[$axis + 1] - ($my + $b * ($p[$axis] - $mx)))) }
            $err.Sort()
            Write-Host ([string]::Format($culture, 'Coordinate fit {0}: unity = {1:F4} * android + {2:F1}; residual p95={3:F2} max={4:F2} px',
                $(if ($axis -eq 0) { 'x' } else { 'y' }), $b, $my - $b * $mx, (& $p95 $err), $err[-1]))
        }
    }
    if ($unpairedUnity.Count) {
        Write-Host 'Unpaired EnhancedTouch edges:'
        $unpairedUnity | Select-Object -First 20 seq, frame, touch_id, finger, phase, source_s, unity_s | Format-Table -AutoSize | Out-Host
    }
    if ($tail.Count) {
        Write-Host 'Paired edges with timestamp-to-callback >= 20 ms:'
        $tail | Format-Table -AutoSize | Out-Host
    }
} elseif ($androidEdges.Count) {
    Write-Warning 'Android edges present without clock anchors; segments were not computed.'
}
Write-Host "Edge pairs not matched: callback=$unmatchedReceived consumption=$unmatchedConsumed ambiguous=$ambiguousEdges"
Write-Host 'Unmatched edges are investigation candidates, including capture boundaries and snapshot phase changes.'
if ($hasSePlayTrace) {
    Write-Host "Unmatched SE rows: request=$($seRequests.Count) playback=$unmatchedSePlays"
}
if ($hasMixTrace) {
    Write-Host "SE mix probe: matched=$mixPaired no_signal_or_invalid=$mixNoSignal plays_without_probe_row=$($unmeasuredMixPlays.Count) orphan_probe_rows=$mixUnpaired"
}
$rows | Group-Object stage | Sort-Object Name | Format-Table Name, Count | Out-Host

function Percentile($values, [double]$p) {
    $values[[int][Math]::Ceiling($values.Count * $p) - 1]
}

foreach ($metric in ($metrics.Keys | Sort-Object)) {
    $values = $metrics[$metric]
    $values.Sort()
    [pscustomobject]@{
        Metric = $metric
        N = $values.Count
        'p50 ms' = [Math]::Round((Percentile $values 0.50), 3)
        'p95 ms' = [Math]::Round((Percentile $values 0.95), 3)
        'p99 ms' = if ($values.Count -ge 100) { [Math]::Round((Percentile $values 0.99), 3) }
    }
}
