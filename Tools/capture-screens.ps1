# Full usage (Chinese): docs/operations/screenshot-tools.md
#
# Renders game screens to PNG for quick art checks: a chart frame under autoplay, the live info page,
# the library, the settings page and the result page. No phone needed.
#
# How it runs
#   - If a Unity Editor already has this project open, the script drives it through the Unity CLI
#     (`unity command script eval`): the Editor enters Play Mode, takes the shots, and leaves Play Mode.
#     Don't touch the Editor while it runs (it switches the Game view resolution and back).
#   - Otherwise it launches Unity on the project with -executeMethod, takes the shots and quits.
#     This needs a GPU and a desktop session (it is not -batchmode/-nographics: the Game view renders).
#   Code: Assets/Game/Editor/OpenSekai/Screenshots/ (also on the menu OpenSekai/Screenshots/...).
#
# Frames are reproducible: the music is muted and stopped, and the live clock is stepped at a fixed frame
# time, so the same chart/time gives the same picture. A live frame lands on the requested millisecond.
#
# Output: TestResults/screenshots/<stamp>/*.png plus report.json (actual times, sizes, notes).
#
# Examples
#   # Everything with sample data, phone resolution
#   powershell -File Tools/capture-screens.ps1 -All -Package DEINOS
#
#   # Chart frames at music time 45 s and 92.5 s, expert, 1750x795
#   powershell -File Tools/capture-screens.ps1 -Live 45,92.5 -Package DEINOS -Difficulty expert -Width 1750 -Height 795
#
#   # Info page at 0.3 s, 0.8 s, 1.5 s and 2.7 s into its animation
#   powershell -File Tools/capture-screens.ps1 -Info 0.3,0.8,1.5,2.7 -Package Test3
#
#   # End of the live: 1.6 s and 3 s after the last note (the FULL COMBO / ALL PERFECT animation starts at +1 s)
#   powershell -File Tools/capture-screens.ps1 -Finish 1.6,3 -FinishAnimation fullcombo -Package Test3
#
#   # Library, settings (and its trace sub-page) and the result page with a full-combo sample
#   powershell -File Tools/capture-screens.ps1 -Library -Settings -SettingsTrace -Result fc
#
#   # The home page, and the splash at 0.95 s (note falling), 1.12 s (the hit) and 2.6 s (wordmark)
#   powershell -File Tools/capture-screens.ps1 -HomePage -Intro 0.95,1.12,2.6
#
#   # Anything else: write a request JSON yourself (format: Assets/Game/Editor/OpenSekai/Screenshots/ShotRequest.cs)
#   powershell -File Tools/capture-screens.ps1 -Request my-shots.json
#
# Parameters
#   -Package      Package to play: id, folder name (or part of it), title, or a folder path.
#                 Installed packages are searched first, then Tools/fixtures.
#   -Difficulty   easy|normal|hard|expert|master|append; overrides the package's own (badge colours etc.).
#   -Live         Music times in seconds (the audio file's clock, filler included).
#   -Info         Seconds since the info page starts; it fades out at about 2.9 s
#                 (0.1 + 0.01 + JacketFadeOutDelaySec 2.35 + 0.45).
#   -Finish       Seconds after the last note is done; the end-of-live animation starts at +1 s.
#   -FinishAnimation  allperfect (default) | fullcombo | clear | finish | none.
#   -Result       Result sample preset: default | fc | ap | auto | failed | retired | loaderror.
#   -HomePage     The home page at rest (the splash's last frame).
#   -Intro        Seconds into the splash; it hands over to the home page at about 4.4 s. Pass
#                 -Intro default for a set of eight frames across the whole sequence.
#   -PlainAuto    Live frames show plain autoplay (AUTO, no combo) instead of PERFECT + combo.
#   -Width/-Height  Default 2800x1272 (the phone). 1750x795 is the other common size.
#   -OutDir       Default TestResults/screenshots/<yyyyMMdd-HHmmss>.
#   -UnityExe     Editor used when none is open; default Unity 6000.6.7f1 from the Hub folder.
#   -TimeoutSec   How long to wait for the run (default 900).

[CmdletBinding()]
param(
    [string]$Package,
    [string]$Difficulty,
    [string[]]$Live,
    [string[]]$Info,
    [string[]]$Finish,
    [string]$FinishAnimation = 'allperfect',
    [switch]$Library,
    [switch]$Settings,
    [switch]$SettingsTrace,
    [string]$Result,
    [switch]$HomePage,
    [string[]]$Intro,
    [switch]$All,
    [switch]$PlainAuto,
    [int]$Width = 2800,
    [int]$Height = 1272,
    [string]$OutDir,
    [string]$Request,
    [string]$ProjectPath,
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe',
    [int]$TimeoutSec = 900
)

$ErrorActionPreference = 'Stop'
if (-not $ProjectPath) { $ProjectPath = Split-Path -Parent $PSScriptRoot }

# "-Live 45,92.5" arrives as one string under `powershell -File`; accept both forms.
function ConvertTo-Seconds([string[]]$values) {
    if (-not $values) { return $null }
    return @($values | ForEach-Object { $_ -split '[,\s]+' } | Where-Object { $_ } |
        ForEach-Object { [double]::Parse($_, [Globalization.CultureInfo]::InvariantCulture) })
}
# Runs the Unity CLI and returns stdout only. Windows PowerShell turns a native command's stderr into
# an error under 'Stop', and the CLI writes hints there, so stderr is dropped here.
function Invoke-UnityCli([string[]]$arguments) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { return (& unity @arguments 2>$null | Out-String) } finally { $ErrorActionPreference = $previous }
}
$env:UNITY_NO_AGENT_HINTS = '1'

$liveTimes = ConvertTo-Seconds $Live
$infoTimes = ConvertTo-Seconds $Info
$finishTimes = ConvertTo-Seconds $Finish
$introDefault = $Intro -and (($Intro -join ',') -eq 'default')
$introTimes = if ($introDefault) { $null } else { ConvertTo-Seconds $Intro }
$ProjectPath = (Resolve-Path $ProjectPath).Path
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if (-not $OutDir) { $OutDir = "TestResults/screenshots/$stamp" }

# Not Temp/: a Unity launched on the project wipes Temp/ at startup, before -executeMethod reads the request.
$requestDir = Join-Path $ProjectPath 'Library/OpenSekaiShots'
New-Item -ItemType Directory -Force -Path $requestDir | Out-Null

if ($Request) {
    $requestPath = Join-Path $requestDir "request-$stamp.json"
    Copy-Item -LiteralPath (Resolve-Path $Request).Path -Destination $requestPath
} else {
    if ($All) {
        $Library = $true; $Settings = $true
        if (-not $Result) { $Result = 'default' }
        if (-not $infoTimes) { $infoTimes = @(0.3, 0.8, 1.5, 2.7) }
        if (-not $liveTimes) { $liveTimes = @(30.0, 60.0) }
    }
    $shots = New-Object System.Collections.ArrayList
    if ($HomePage) { [void]$shots.Add(@{ kind = 'home' }) }
    if ($introDefault) { [void]$shots.Add(@{ kind = 'intro' }) }
    elseif ($introTimes) { [void]$shots.Add(@{ kind = 'intro'; times = @($introTimes) }) }
    if ($Library) { [void]$shots.Add(@{ kind = 'library' }) }
    if ($Settings) { [void]$shots.Add(@{ kind = 'settings' }) }
    if ($SettingsTrace) { [void]$shots.Add(@{ kind = 'settings-trace' }) }
    if ($Result) {
        $sample = @{ kind = 'Completed' }
        $presets = @{
            fc        = @{ score = 1112806; maxCombo = 1250; perfect = 1206; great = 44; good = 0; bad = 0; miss = 0 }
            ap        = @{ score = 1135420; maxCombo = 1250; perfect = 1250; great = 0; good = 0; bad = 0; miss = 0 }
            auto      = @{ auto = $true; score = 1135420; maxCombo = 1250 }
            failed    = @{ kind = 'Failed'; score = 412803; rank = 'C'; maxCombo = 96; perfect = 402; great = 87; good = 31; bad = 22; miss = 58; life = 0 }
            retired   = @{ kind = 'Retired'; score = 233118; rank = 'D'; maxCombo = 207; perfect = 220; great = 12; good = 1; bad = 0; miss = 1 }
            loaderror = @{ kind = 'LoadError' }
        }
        if ($Result -ne 'default') {
            if (-not $presets.ContainsKey($Result)) { throw "Unknown -Result preset '$Result'." }
            foreach ($k in $presets[$Result].Keys) { $sample[$k] = $presets[$Result][$k] }
        }
        [void]$shots.Add(@{ kind = 'result'; name = "result-$Result"; result = $sample })
    }
    if ($infoTimes -or $liveTimes -or $finishTimes) {
        if (-not $Package) { throw '-Live, -Info and -Finish need -Package.' }
    }
    if ($infoTimes) {
        $shot = @{ kind = 'info'; package = $Package; times = @($infoTimes) }
        if ($Difficulty) { $shot.difficulty = $Difficulty }
        [void]$shots.Add($shot)
    }
    if ($liveTimes) {
        $shot = @{ kind = 'live'; package = $Package; times = @($liveTimes); fakePerfect = (-not $PlainAuto) }
        if ($Difficulty) { $shot.difficulty = $Difficulty }
        [void]$shots.Add($shot)
    }
    if ($finishTimes) {
        $shot = @{ kind = 'finish'; package = $Package; times = @($finishTimes); finishAnimation = $FinishAnimation }
        if ($Difficulty) { $shot.difficulty = $Difficulty }
        [void]$shots.Add($shot)
    }
    if ($shots.Count -eq 0) { throw 'Nothing to capture: pass -All, -HomePage, -Intro, -Live, -Info, -Finish, -Library, -Settings, -SettingsTrace, -Result or -Request.' }

    $requestPath = Join-Path $requestDir "request-$stamp.json"
    $body = @{ outDir = $OutDir; width = $Width; height = $Height; shots = @($shots) }
    $body | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $requestPath -Encoding UTF8
}

$statusPath = [IO.Path]::ChangeExtension($requestPath, $null).TrimEnd('.') + '.status.json'
if (Test-Path $statusPath) { Remove-Item $statusPath -Force }
Write-Host "[shots] request: $requestPath"

# Is an editor open on this project?
$editorOpen = $false
if (Get-Command unity -ErrorAction SilentlyContinue) {
    $statusJson = Invoke-UnityCli @('status', '--project-path', $ProjectPath, '--format', 'json')
    try { $editorOpen = @((ConvertFrom-Json $statusJson).data.items | Where-Object { $_.state -eq 'ready' }).Count -gt 0 } catch { }
}
if (-not $editorOpen) {
    $running = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
        Where-Object { $_.CommandLine -match [regex]::Escape($ProjectPath) -and $_.CommandLine -notmatch 'AssetImportWorker' }
    if ($running) { throw 'Unity has this project open but the Unity CLI cannot reach it (unity status). Close it, or wait until it is ready.' }
}

$started = Get-Date
$process = $null
if ($editorOpen) {
    Write-Host '[shots] driving the open editor'
    # The call goes in a file: quotes inside a native command's arguments do not survive Windows PowerShell.
    $escaped = $requestPath.Replace('"', '""')
    $codePath = Join-Path $requestDir "start-$stamp.cs"
    Set-Content -LiteralPath $codePath -Encoding UTF8 -Value "return OpenSekai.EditorTools.Screenshots.ScreenshotSession.Start(@`"$escaped`");"
    $out = Invoke-UnityCli @('command', 'script', 'eval_file', '--project-path', $ProjectPath, '--result-only', '--', '--file', $codePath)
    $reply = $null
    try { $reply = ConvertFrom-Json $out } catch { }
    if (-not $reply -or -not $reply.success) { throw "The editor refused the run: $out" }
    Write-Host "[shots] $($reply.result)"
} else {
    if (-not (Test-Path $UnityExe)) { throw "Unity not found at $UnityExe (pass -UnityExe)." }
    $log = Join-Path $ProjectPath "Logs/capture-screens-$stamp.log"
    Write-Host "[shots] launching Unity (log: $log)"
    $process = Start-Process -FilePath $UnityExe -PassThru -ArgumentList @(
        '-projectPath', "`"$ProjectPath`"",
        '-executeMethod', 'OpenSekai.EditorTools.Screenshots.ScreenshotSession.CaptureFromCommandLine',
        '-shotRequest', "`"$requestPath`"",
        '-logFile', "`"$log`"")
}

$state = $null
while ($true) {
    Start-Sleep -Seconds 2
    if (Test-Path $statusPath) {
        try { $state = Get-Content -Raw -LiteralPath $statusPath | ConvertFrom-Json } catch { $state = $null }
        if ($state -and $state.status -ne 'running') { break }
    }
    if ($process -and $process.HasExited) { break }
    if (((Get-Date) - $started).TotalSeconds -gt $TimeoutSec) { throw "Timed out after $TimeoutSec s; see the editor console." }
}
if ($process) { $process.WaitForExit(60000) | Out-Null }

$outFull = if ($state -and $state.outDir) { $state.outDir } else { Join-Path $ProjectPath $OutDir }
$reportPath = Join-Path $outFull 'report.json'
if (Test-Path $reportPath) {
    $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    foreach ($s in $report.shots) {
        $line = if ($s.ok) { "  ok   $($s.file)  $($s.detail)" } else { "  FAIL $($s.kind): $($s.note)" }
        if ($s.ok -and $s.note) { $line += "  ($($s.note))" }
        Write-Host $line
    }
}
Write-Host "[shots] $(if ($state) { $state.status } else { 'no status' }) -> $outFull"
if (-not $state -or $state.status -ne 'done') {
    if ($state -and $state.message) { Write-Host "[shots] $($state.message)" }
    exit 1
}
exit 0
