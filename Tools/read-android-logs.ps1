#Requires -Version 7.0
<#
.SYNOPSIS
Captures OpenSekai Android logs and writes an evidence report.
.EXAMPLE
./Tools/read-android-logs.ps1 -Duration 60 -Scenario 'Hold handoff' -IncludeTrace
.EXAMPLE
./Tools/read-android-logs.ps1 -Snapshot
#>
[CmdletBinding(DefaultParameterSetName = 'Record')]
param(
    [Parameter(ParameterSetName = 'Record')]
    [ValidateRange(1, 3600)]
    [int]$Duration = 60,
    [Parameter(Mandatory, ParameterSetName = 'Snapshot')]
    [switch]$Snapshot,
    [string]$Scenario,
    [switch]$IncludeTrace,
    [switch]$HoldTransitions,
    [ValidatePattern('^\d+(?:[.,]\d{1,3})?$')]
    [string]$BarTime,
    [string]$Device,
    [ValidatePattern('^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$')]
    [string]$Package = 'io.github.opensekai',
    [string]$Adb,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../TestResults/android-logs')
)

$ErrorActionPreference = 'Stop'

$filterByBarTime = $PSBoundParameters.ContainsKey('BarTime')
if ($filterByBarTime) {
    $HoldTransitions = $true
    $BarTime = [decimal]::Parse($BarTime.Replace(',', '.'), [Globalization.CultureInfo]::InvariantCulture)
}

if (-not $Adb) {
    $editorVersion = (Get-Content "$PSScriptRoot/../ProjectSettings/ProjectVersion.txt" -TotalCount 1) -replace '^m_EditorVersion:\s*'
    $sdkPaths = @(
        if ($env:ANDROID_HOME) { Join-Path $env:ANDROID_HOME 'platform-tools/adb.exe' }
        if ($env:ANDROID_SDK_ROOT) { Join-Path $env:ANDROID_SDK_ROOT 'platform-tools/adb.exe' }
        if ($env:ProgramFiles) {
            Join-Path $env:ProgramFiles "Unity/Hub/Editor/$editorVersion/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
        }
    )
    $Adb = $sdkPaths | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $Adb) { $Adb = (Get-Command adb -CommandType Application -ErrorAction SilentlyContinue).Source }
}
if (-not $Adb) { throw 'ADB not found. Specify -Adb with the path to adb.exe.' }
$Adb = (Resolve-Path -LiteralPath $Adb).ProviderPath

function Start-AdbProcess([string[]]$Arguments) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new($Adb)
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardErrorEncoding = [Text.Encoding]::UTF8
    $startInfo.StandardOutputEncoding = [Text.Encoding]::UTF8
    foreach ($argument in $Arguments) { $startInfo.ArgumentList.Add($argument) }
    [System.Diagnostics.Process]::Start($startInfo)
}

function Invoke-Adb([string[]]$Arguments, [int]$TimeoutSeconds = 15) {
    $process = Start-AdbProcess $Arguments
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) { $process.Kill(); $process.WaitForExit() }
        [pscustomobject]@{
            Ok = -not $timedOut -and $process.ExitCode -eq 0
            Output = $stdout.GetAwaiter().GetResult()
            ErrorText = if ($timedOut) { "ADB timed out after $TimeoutSeconds seconds." } else { $stderr.GetAwaiter().GetResult().Trim() }
        }
    }
    finally { $process.Dispose() }
}

$devices = Invoke-Adb @('devices', '-l')
if (-not $devices.Ok) { throw "Could not list Android devices. $($devices.ErrorText)" }
$ready = @($devices.Output -split '\r?\n' | ForEach-Object {
    if ($_ -match '^(\S+)\s+device(?:\s|$)') { $Matches[1] }
})
if ($Device) {
    if ($Device -cnotin $ready) { throw "Device '$Device' is unavailable. Check USB debugging and device authorization.`n$($devices.Output)" }
}
elseif ($ready.Count -eq 1) { $Device = $ready[0] }
else { throw "Expected one authorized Android device. Connect a device or select one with -Device.`n$($devices.Output)" }

$target = @('-s', $Device)
$runDirectory = Join-Path $OutputDirectory ("{0:yyyyMMdd-HHmmss}-{1}" -f [DateTime]::Now, [Guid]::NewGuid().ToString('N').Substring(0, 8))
$runDirectory = (New-Item -ItemType Directory -Path $runDirectory -Force).FullName
$notes = [System.Collections.Generic.List[string]]::new()
$traceDirectory = "/sdcard/Android/data/$Package/files/logs"

function Get-TraceInventory {
    $listing = Invoke-Adb ($target + @('shell', "if [ -d '$traceDirectory' ]; then for f in '$traceDirectory'/live-*.log; do if [ -f `"`$f`" ]; then stat -c '%s %n' `"`$f`" || exit 1; fi; done; fi"))
    if (-not $listing.Ok) {
        $notes.Add("Could not list LiveTrace files. $($listing.ErrorText)")
        return
    }
    $files = @{}
    foreach ($line in ($listing.Output -split '\r?\n')) {
        if ($line -match '^([0-9]+) .*/(live-[0-9]{8}-[0-9]{6}(?:-[0-9]+)?\.log)$') {
            $files[$Matches[2]] = [long]$Matches[1]
        }
    }
    return $files
}

$deviceModel = Invoke-Adb ($target + @('shell', 'getprop', 'ro.product.model'))
$version = Invoke-Adb ($target + @('shell', 'dumpsys', 'package', $Package))
$versionName = [regex]::Match($version.Output, '(?m)^\s*versionName=(\S+)').Groups[1].Value
$versionCode = [regex]::Match($version.Output, '(?m)^\s*versionCode=(\d+)').Groups[1].Value
if (-not $versionName) { $notes.Add("Could not determine the version of $Package.") }

$traceBefore = if ($IncludeTrace -and -not $Snapshot) { Get-TraceInventory }
$logArguments = $target + @('logcat', '-b', 'main', '-b', 'crash', '-v', 'threadtime')
if ($Snapshot) { $logArguments += '-d' }
else {
    $deviceTime = Invoke-Adb ($target + @('shell', 'date', '+%m-%d_%H:%M:%S'))
    $since = $deviceTime.Output.Trim().Replace('_', ' ')
    if (-not $deviceTime.Ok -or $since -notmatch '^\d{2}-\d{2} \d{2}:\d{2}:\d{2}$') {
        throw "Could not read the device clock. $($deviceTime.ErrorText)"
    }
    $logArguments += @('-T', "$since.000")
}
$logArguments += @('-s', 'Unity:V', 'AndroidRuntime:E', 'DEBUG:F', 'libc:F')
$logPath = Join-Path $runDirectory 'logcat.log'
$stream = [IO.File]::Create($logPath)
$process = $null
$status = 'complete'
$started = [DateTimeOffset]::UtcNow
try {
    $process = Start-AdbProcess $logArguments
    $copy = $process.StandardOutput.BaseStream.CopyToAsync($stream)
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $Snapshot) { Write-Host "Recording for $Duration seconds. Reproduce the issue now." }
    $waitSeconds = if ($Snapshot) { 30 } else { $Duration }
    $exited = $process.WaitForExit($waitSeconds * 1000)
    if (-not $exited) {
        $process.Kill()
        $process.WaitForExit()
    }
    $null = $copy.GetAwaiter().GetResult()
    $logError = $stderr.GetAwaiter().GetResult().Trim()
    if (($Snapshot -and (-not $exited -or $process.ExitCode -ne 0)) -or (-not $Snapshot -and $exited)) {
        $status = 'incomplete'
        $notes.Add("Logcat ended before capture completed (exit $($process.ExitCode)). $logError")
    }
    elseif ($logError) {
        $status = 'incomplete'
        $notes.Add("Logcat reported: $logError")
    }
}
finally {
    $stream.Dispose()
    if ($process) {
        if (-not $process.HasExited) { $process.Kill() }
        $process.Dispose()
    }
}
$ended = [DateTimeOffset]::UtcNow

$traceFiles = @()
$traceCounts = @{}
if ($IncludeTrace) {
    $traceAfter = Get-TraceInventory
    $traceNames = if ($traceAfter -and $Snapshot) {
        $traceAfter.Keys | Sort-Object { $_.Substring(0, 20) }, { [long]([regex]::Match($_, '^.{20}-([0-9]+)\.log$').Groups[1].Value) } -Descending | Select-Object -First 1
    }
    elseif ($traceAfter -and $null -ne $traceBefore) {
        $traceAfter.Keys | Where-Object { -not $traceBefore.ContainsKey($_) -or $traceAfter[$_] -ne $traceBefore[$_] } | Sort-Object
    }
    elseif ($traceAfter) { $notes.Add('LiveTrace baseline unavailable. Could not identify files changed during capture.') }

    foreach ($name in $traceNames) {
        $destination = Join-Path $runDirectory $name
        $pull = Invoke-Adb ($target + @('pull', "$traceDirectory/$name", $destination)) -TimeoutSeconds 60
        if (-not $pull.Ok) {
            $notes.Add("Could not pull ${name}. $($pull.ErrorText)")
            continue
        }
        $offset = if (-not $Snapshot -and $traceBefore.ContainsKey($name) -and $traceBefore[$name] -lt $traceAfter[$name]) { $traceBefore[$name] } else { 0 }
        if (-not $Snapshot -and $traceBefore.ContainsKey($name) -and $traceBefore[$name] -gt $traceAfter[$name]) {
            $notes.Add("$name shrank during capture. Counts cover the replacement file.")
        }
        $traceFiles += [pscustomobject]@{ file = $name; captureStartByte = $offset; sampledEndByte = $traceAfter[$name] }
        # LiveTrace writes UTF-8 without a BOM and uses LF on Android.
        $bytePosition = 0L
        $traceLine = 0
        $firstLine = $null
        $lastLine = $null
        foreach ($line in [IO.File]::ReadLines($destination)) {
            $lineStart = $bytePosition
            $bytePosition += [Text.Encoding]::UTF8.GetByteCount($line) + 1
            $traceLine++
            if ($bytePosition -gt $traceAfter[$name]) { break }
            if ($lineStart -lt $offset) { continue }
            if ($null -eq $firstLine) { $firstLine = $traceLine }
            $lastLine = $traceLine
            if ($line -match '^\[TL\]\[([^\]]+)\]') { $traceCounts[$Matches[1]]++ }
        }
        $traceFiles[-1] | Add-Member -NotePropertyMembers @{ firstLine = $firstLine; lastLine = $lastLine }
    }
    if (-not $traceFiles.Count) { $notes.Add('No LiveTrace files collected. Check the in-game trace switch and selected categories.') }
}

$errorsByMessage = [ordered]@{}
$performance = [System.Collections.Generic.List[object]]::new()
$audio = [System.Collections.Generic.List[object]]::new()
$lineNumber = 0
$parsedEntries = 0
$currentError = $null
$currentTag = $null
$format = '^\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d+\s+(?<pid>\d+)\s+\d+\s+(?<level>[VDIWEFAS])\s+(?<tag>[^:]+?)\s*:\s?(?<message>.*)$'
$packagePattern = '^(?:Process:\s*|>>>\s*)' + [regex]::Escape($Package) + '(?=[:,\s]|$)'
foreach ($line in [IO.File]::ReadLines($logPath)) {
    $lineNumber++
    if ($line -notmatch $format) { continue }
    $parsedEntries++
    $tag = $Matches.tag.Trim()
    $level = $Matches.level
    $message = $Matches.message
    $processId = $Matches.pid
    if ($tag -eq 'Unity' -and $message -match '^\[PerfHud\]\s+fps=') {
        $sample = [ordered]@{ line = $lineNumber; processId = $processId }
        foreach ($field in [regex]::Matches($message, '(fps|frames|avgMs|p50|p95|p99|worst|res)=([^\s]+)')) {
            $name = $field.Groups[1].Value
            $value = $field.Groups[2].Value
            if ($name -eq 'res') { $sample[$name] = $value }
            else {
                $number = 0.0
                if ([double]::TryParse($value.Replace(',', '.'), [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$number)) {
                    $sample[$name] = $number
                }
            }
        }
        $performance.Add([pscustomobject]$sample)
    }
    if ($tag -eq 'Unity' -and $message -match '^\[(?:PerfHud\] (?:device=|\[AudioOut\])|AudioOut\])') {
        $audio.Add([pscustomobject]@{ line = $lineNumber; message = $message })
    }

    if ($level -notin @('E', 'F', 'A') -and $message -notmatch '^\[TL\]\[INVARIANT\]') {
        $currentError = $null
        continue
    }
    $continuation = $message -match '^\s*(?:at\s|Caused by:|Suppressed:|\.{3}\s+\d+|Rethrow as:|\(Filename:|[\w.`+<>/]+:[\w.<>`]+\(|#\d+\s|backtrace:|Process:|>>>|$)'
    if ($currentError -and $currentTag -eq $tag -and $currentError.processId -eq $processId -and
        ($continuation -or ($tag -eq 'AndroidRuntime' -and $message -notmatch '^FATAL EXCEPTION') -or
        ($tag -eq 'DEBUG' -and $message -notmatch '^\*{3}'))) {
        $currentError.endLine = $lineNumber
        if ($message -match $packagePattern -or $message -match ('>>>\s*' + [regex]::Escape($Package) + '(?=[:\s<])')) {
            $currentError.packageMatched = $true
        }
        continue
    }
    $key = "$tag|${level}|$message"
    if (-not $errorsByMessage.Contains($key)) {
        $errorsByMessage[$key] = [pscustomobject]@{
            tag = $tag; level = $level; message = $message; occurrences = [System.Collections.Generic.List[object]]::new()
        }
    }
    $currentError = [pscustomobject]@{
        line = $lineNumber; endLine = $lineNumber; processId = $processId; packageMatched = ($message -match $packagePattern)
    }
    $currentTag = $tag
    $errorsByMessage[$key].occurrences.Add($currentError)
}
$errorGroups = @($errorsByMessage.Values | Sort-Object { $_.occurrences.Count } -Descending)
if (-not $parsedEntries) { $notes.Add('No matching logcat entries found.') }
if (-not $performance.Count) { $notes.Add('No PerfHud samples found. Enable the in-game Performance HUD and play during recording.') }

function Find-HoldTransitions([string]$File, [int]$FirstLine, [int]$LastLine) {
    $lineNumber = 0
    $pattern = '\[TL\]\[Hold\] barExcute type=(?<type>\S+) cat=(?<category>\S+) barT=(?<bar>\S+) now=(?<time>\S+) prev=(?<from>\w+)/(?<before>\w+) next=(?<to>\w+)/(?<after>\w+)'
    foreach ($line in [IO.File]::ReadLines((Join-Path $runDirectory $File))) {
        $lineNumber++
        if ($lineNumber -gt $LastLine) { break }
        if ($lineNumber -lt $FirstLine -or $line -notmatch $pattern) { continue }
        if ($Matches.from -eq $Matches.to) { continue }
        if ($filterByBarTime) {
            $parsed = [decimal]0
            if (-not [decimal]::TryParse($Matches.bar.Replace(',', '.'), [Globalization.NumberStyles]::Number, [Globalization.CultureInfo]::InvariantCulture, [ref]$parsed) -or
                $parsed -ne $BarTime) { continue }
        }
        [pscustomobject]@{
            file = $File; line = $lineNumber; barTime = $Matches.bar; musicTime = $Matches.time
            noteType = $Matches.type; category = $Matches.category
            fromState = $Matches.from; toState = $Matches.to
            fromResult = $Matches.before; toResult = $Matches.after
        }
    }
}

$holdTransitionFile = $null
if ($HoldTransitions) {
    $transitions = @(
        Find-HoldTransitions 'logcat.log' 1 $lineNumber
        foreach ($file in $traceFiles) {
            if ($file.firstLine) { Find-HoldTransitions $file.file $file.firstLine $file.lastLine }
        }
    )
    $holdTransitionFile = 'hold-transitions.json'
    ConvertTo-Json -InputObject $transitions -Depth 3 |
        Set-Content -LiteralPath (Join-Path $runDirectory $holdTransitionFile) -Encoding utf8
    if (-not $transitions.Count) { $notes.Add('No matching long-note state changes found. Enable the Hold trace category before playing and check the selected bar time.') }
}

$report = [ordered]@{
    schemaVersion = 1
    mode = if ($Snapshot) { 'snapshot' } else { 'record' }
    status = $status
    scenario = $Scenario
    startedUtc = $started.ToString('o')
    endedUtc = $ended.ToString('o')
    logcatSinceDeviceTime = if (-not $Snapshot) { "$since.000" }
    device = [ordered]@{ serial = $Device; model = $deviceModel.Output.Trim() }
    package = $Package
    versionName = $versionName
    versionCode = $versionCode
    logcatFile = 'logcat.log'
    logcatEntries = $parsedEntries
    errors = $errorGroups
    performanceWindows = $performance
    deviceAndAudioLines = $audio
    traceFiles = $traceFiles
    traceCategoryCounts = $traceCounts
    holdTransitionFile = $holdTransitionFile
    notes = $notes
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runDirectory 'report.json') -Encoding utf8

$summary = @(
    '# OpenSekai Android log report'
    ''
    "- Capture: $($report.mode), $status, $($report.startedUtc) to $($report.endedUtc) (UTC)"
    "- Scenario: $Scenario"
    "- Device: $($report.device.model) ($Device)"
    "- App: $Package $versionName ($versionCode)"
    "- Logcat entries: $parsedEntries"
    if ($Snapshot) { '- Snapshot entries may come from earlier runs and other apps.' }
    else { '- Logcat starts at the device clock rounded down to the second. Unity and crash tags may include other apps.' }
    ''
    "## Errors and invariant violations ($($errorGroups.Count) groups)"
    ''
    foreach ($group in ($errorGroups | Select-Object -First 15)) {
        $entry = $group.occurrences[0]
        $location = if ($entry.line -eq $entry.endLine) { "$($entry.line)" } else { "$($entry.line)–$($entry.endLine)" }
        $message = $group.message
        if ($message.Length -gt 180) { $message = $message.Substring(0, 180) + '…' }
        $matched = @($group.occurrences | Where-Object packageMatched).Count
        $attribution = if ($matched) { " ($matched with matching app package)" } else { '' }
        "- $($group.occurrences.Count)x [$($group.tag)/$($group.level)] $message$attribution — logcat.log lines $location"
    }
    if ($errorGroups.Count -gt 15) { '- See report.json for all groups and occurrences.' }
    ''
    "## Performance ($($performance.Count) windows)"
    ''
    if ($performance.Count) {
        $fps = $performance | Measure-Object -Property fps -Minimum -Maximum
        $worst = $performance | Where-Object { $null -ne $_.worst } | Sort-Object worst -Descending | Select-Object -First 1
        $resolutions = ($performance.res | Where-Object { $_ } | Sort-Object -Unique) -join ', '
        if ($null -ne $fps.Minimum) { "- Window FPS range: $($fps.Minimum)–$($fps.Maximum)" }
        if ($worst) { "- Slowest frame: $($worst.worst) ms — logcat.log line $($worst.line)" }
        if ($resolutions) { "- Resolution: $resolutions" }
        '- See report.json for per-window P95 and other frame timing data.'
    }
    foreach ($item in ($audio | Select-Object -Last 5)) { "- $($item.message) — logcat.log line $($item.line)" }
    ''
    "## LiveTrace ($($traceFiles.Count) files)"
    ''
    foreach ($file in $traceFiles) {
        if ($file.firstLine) { "- $($file.file) — lines $($file.firstLine)–$($file.lastLine)" }
        else { "- $($file.file) — no complete lines in the sampled byte range" }
    }
    foreach ($category in ($traceCounts.Keys | Sort-Object)) { "- ${category}: $($traceCounts[$category]) lines" }
    if ($holdTransitionFile) {
        ''
        '## Long-note state transitions'
        ''
        "- See $holdTransitionFile for the full timeline with source lines. Logcat and LiveTrace can contain the same events."
        foreach ($source in ($transitions | Group-Object file)) {
            "- $($source.Name): $($source.Count) state changes"
        }
        foreach ($entry in ($transitions | Select-Object -First 12)) {
            "- t=$($entry.musicTime)s, barT=$($entry.barTime)s: $($entry.fromState) -> $($entry.toState) — $($entry.file) line $($entry.line)"
        }
    }
    if ($notes.Count) {
        ''
        '## Collection notes'
        ''
        foreach ($note in $notes) { "- $note" }
    }
)
$summaryPath = Join-Path $runDirectory 'summary.md'
$summary | Set-Content -LiteralPath $summaryPath -Encoding utf8
Write-Output $summaryPath
if ($status -eq 'incomplete') { exit 1 }
