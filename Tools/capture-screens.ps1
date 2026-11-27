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
#   # Export independent 4K PNG sequences for editing at 60 fps (run in the project folder)
#   powershell -File Tools/capture-screens.ps1 -Clip tier-upgrade,dan-award,achievement-unlock -Width 3840 -Height 2160 -Fps 60 -TimeoutSec 3600
#   powershell -File Tools/capture-screens.ps1 -Intro 0..3.1,1.0 -Result ap -ResultAt 0..3.4 -Fps 60
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
#   # Every result sample, and the tier-up sample 2.4 s, 2.7 s and 3 s into its opening
#   powershell -File Tools/capture-screens.ps1 -Result default,fc,ap,outside,auto,failed,retired,savefailed,loaderror
#   powershell -File Tools/capture-screens.ps1 -Result ap -ResultAt 2.4,2.7,3
#
#   # The home page, and the splash at 0.95 s (note falling), 1.12 s (the hit) and 2.6 s (wordmark)
#   powershell -File Tools/capture-screens.ps1 -HomePage -Intro 0.95,1.12,2.6
#
#   # Every menu page on the sample library, for the style guide (in a sample workspace, see below)
#   powershell -File Tools/capture-screens.ps1 -StyleGuide
#
#   # Anything else: write a request JSON yourself (format: Assets/Game/Editor/OpenSekai/Screenshots/ShotRequest.cs)
#   powershell -File Tools/capture-screens.ps1 -Request my-shots.json
#
# Parameters
#   -Clip         tier-upgrade | dan-award | achievement-unlock | achievement-card | special-medals.
#                 Override the range with e.g. -Clip tier-upgrade:-0.5..1.5.
#   -Fps          Sequence frame rate (1..240, default 60). Intro/Live/Info/Finish/ResultAt accept start..end.
#   -Alpha        Transparent background for tier-upgrade, achievement-card and special-medals.
#   -Clean        Hide the dan-award caption and buttons.
#   -Package      Package to play: id, folder name (or part of it), title, or a folder path.
#                 Installed packages are searched first, then Tools/fixtures.
#   -Difficulty   easy|normal|hard|expert|master|append; overrides the package's own (badge colours etc.).
#   -Live         Music times in seconds (the audio file's clock, filler included).
#   -Info         Seconds since the info page starts; it fades out at about 2.9 s
#                 (0.1 + 0.01 + JacketFadeOutDelaySec 2.35 + 0.45).
#   -Finish       Seconds after the last note is done; the end-of-live animation starts at +1 s.
#   -FinishAnimation  allperfect (default) | fullcombo | clear | finish | none.
#   -Result       Result page samples, comma-separated: default (first clear) | fc | ap (new chart,
#                 tier up) | outside (record outside the Best 30) | auto | failed | retired |
#                 savefailed | loaderror. Each shows where its opening settles.
#   -ResultAt     Seconds into the result page's opening, one frame each, instead of the settled page.
#   -HomePage     The home page at rest (the splash's last frame).
#   -Packs        The resource pack part, read-only: hand-over frames, overview, a pack and its
#                 preview, category pages, one slot's choices, the built-in note skin.
#   -ProfilePage  The second home page and the player profile part on a sample profile in a temp folder
#                 (never the player's own): page 2, mid-swipe, hand-over frames, Best 30, history,
#                 the edit card, a backup's merge preview, back to page 2, and an empty profile.
#   -DanPage      The 段位 part on sample courses in a temp folder (never the player's own): the home card and
#                 hand-over, a new, a passed and a failed course, the maker, a shared text and a broken one,
#                 the delete card, the preparation card, the rest screen, the pass result with three award
#                 ceremony frames, a failed and an abandoned result, and the profile wearing the plate.
#   -AchievementsPage  The 成就 part on a sample profile in a temp folder (never the player's own): the home
#                 card and hand-over, every achievement and each filter, two reward cards, an unlock frame by
#                 frame and one with a tap, an earlier level, rows with one to eight levels, the card for one
#                 goal reached, for three at once and mid-entrance, back to page 2, and a new profile.
#   -ShopPage     The 商店 part on a sample profile in a temp folder (never the player's own): the home card
#                 at three times and its hand-over, the page with nothing on sale (every kind's shelf, then
#                 one kind's), a sample catalog with each kind, the exchange card for something affordable,
#                 something owned and something too dear, one purchase landing, back to page 2, a new profile.
#   -MedalsPage   The achievements' medals and avatars on a sample profile in a temp folder: every series'
#                 emblem, the special medals at every size with their motions held at four times, the
#                 avatars, and the home card, profile, medal and avatar pickers and 成就 rows showing them.
#   -RankingTiers The seven Ranking Score tiers on an empty profile in a temp folder: the home card on each
#                 tier, the badges at every size the game draws them, and three frames of each tier's upgrade.
#   -Server       The server part: hand-over frames, list, add card, then UntitledCharts (needs the
#                 network): level home page, advanced search, every level, one level and its tabs. In a
#                 sample workspace it shows the sample server instead and needs no network.
#   -Sample       Installs the sample library first (fictional songs, covers drawn for this project).
#                 Only in a sample workspace, which Tools/new-sample-workspace.ps1 makes.
#   -StyleGuide   Everything outside the live on the sample library: -Sample and then every menu page. The
#                 pictures in docs/reference/ui-style-guide.md come from this run, through
#                 Tools/export-style-guide-images.ps1.
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
    [string[]]$ResultAt,
    [switch]$HomePage,
    [switch]$Server,
    [switch]$Packs,
    [switch]$ProfilePage,
    [switch]$DanPage,
    [switch]$AchievementsPage,
    [switch]$ShopPage,
    [switch]$MedalsPage,
    [switch]$RankingTiers,
    [switch]$Sample,
    [switch]$StyleGuide,
    [string[]]$Intro,
    [string[]]$Clip,
    [ValidateRange(1,240)][int]$Fps = 60,
    [switch]$Alpha,
    [switch]$Clean,
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
function Split-Values([string[]]$values) {
    $values | ForEach-Object { $_ -split '[,\s]+' } | Where-Object { $_ }
}
. (Join-Path $PSScriptRoot 'screenshot-time-ranges.ps1')
function Add-TimedShot([hashtable]$shot, $values) {
    foreach ($item in (ConvertTo-ScreenshotShots $shot $values $Fps)) { [void]$shots.Add($item) }
}
# Runs the Unity CLI and returns stdout only. Windows PowerShell turns a native command's stderr into
# an error under 'Stop', and the CLI writes hints there, so stderr is dropped here.
function Invoke-UnityCli([string[]]$arguments) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { return (& unity @arguments 2>$null | Out-String) } finally { $ErrorActionPreference = $previous }
}
$env:UNITY_NO_AGENT_HINTS = '1'

$liveTimes = Split-Values $Live
$infoTimes = Split-Values $Info
$finishTimes = Split-Values $Finish
$resultTimes = Split-Values $ResultAt
$introDefault = $Intro -and (($Intro -join ',') -eq 'default')
$introTimes = if (-not $introDefault) { Split-Values $Intro }
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
    if ($StyleGuide) {
        $Sample = $true; $HomePage = $true; $Server = $true; $Packs = $true; $ProfilePage = $true; $DanPage = $true
        $AchievementsPage = $true; $ShopPage = $true; $MedalsPage = $true; $RankingTiers = $true; $SettingsTrace = $true; $introDefault = $true
        if (-not $Result) { $Result = 'default,fc,ap,outside,auto,failed,retired,savefailed,loaderror' }
        if (-not $infoTimes) { $infoTimes = @(0.3, 1.5) }
        if (-not $Package) { $Package = 'Aurora Line' }
    }
    $shots = New-Object System.Collections.ArrayList
    # First: everything after it shows the sample songs.
    if ($Sample) { [void]$shots.Add(@{ kind = 'sample' }) }
    if ($StyleGuide) {
        foreach ($kind in 'library-states', 'about', 'actions', 'actions-armed', 'loading') {
            $name = if ($kind -eq 'library-states') { 'library' } else { $kind }
            [void]$shots.Add(@{ kind = $kind; name = $name })
        }
    }
    if ($HomePage) { [void]$shots.Add(@{ kind = 'home' }) }
    if ($Server) { [void]$shots.Add(@{ kind = 'server' }) }
    if ($Packs) { [void]$shots.Add(@{ kind = 'packs' }) }
    if ($ProfilePage) { [void]$shots.Add(@{ kind = 'profile' }) }
    if ($DanPage) { [void]$shots.Add(@{ kind = 'dan' }) }
    if ($AchievementsPage) { [void]$shots.Add(@{ kind = 'achievements' }) }
    if ($ShopPage) { [void]$shots.Add(@{ kind = 'shop' }) }
    if ($MedalsPage) { [void]$shots.Add(@{ kind = 'medals' }) }
    if ($RankingTiers) { [void]$shots.Add(@{ kind = 'ranking' }) }
    if ($introDefault) { [void]$shots.Add(@{ kind = 'intro' }) }
    elseif ($null -ne $introTimes) { Add-TimedShot @{ kind = 'intro' } $introTimes }
    foreach ($item in (Split-Values $Clip)) {
        $parts = $item -split ':', 2
        $shot = @{ kind = 'clip'; name = $parts[0].ToLowerInvariant(); fps = $Fps }
        if ($Alpha -and $shot.name -in 'tier-upgrade','special-medals','achievement-card') { $shot.alpha = $true }
        if ($Clean -and $shot.name -eq 'dan-award') { $shot.clean = $true }
        if ($parts.Count -eq 2 -and $parts[1] -notmatch '\.\.') { throw "Expected a range in '$item'." }
        Add-TimedShot $shot $parts[1]
    }
    if ($Library) { [void]$shots.Add(@{ kind = 'library' }) }
    if ($Settings) { [void]$shots.Add(@{ kind = 'settings' }) }
    if ($SettingsTrace) { [void]$shots.Add(@{ kind = 'settings-trace' }) }
    if ($Result) {
        # Each preset is one of the backend's SettlementSamples cases (OpenSekai.App.Playback).
        $presets = @{
            default    = @{ settlement = 'FirstClear' }
            fc         = @{ settlement = 'SameChartFullCombo' }
            ap         = @{ settlement = 'NewChartAllPerfectTierUp' }
            outside    = @{ settlement = 'OutsideBest30' }
            auto       = @{ settlement = 'Auto' }
            failed     = @{ settlement = 'Failed' }
            retired    = @{ settlement = 'Retired' }
            savefailed = @{ settlement = 'SaveFailed' }
            loaderror  = @{ kind = 'LoadError' }
        }
        foreach ($name in ($Result -split '[,\s]+' | Where-Object { $_ })) {
            $name = $name.ToLowerInvariant()
            if (-not $presets.ContainsKey($name)) { throw "Unknown -Result preset '$name'." }
            $shot = @{ kind = 'result'; name = "result-$name"; result = $presets[$name] }
            Add-TimedShot $shot $resultTimes
        }
    }
    if ($infoTimes -or $liveTimes -or $finishTimes) {
        if (-not $Package) { throw '-Live, -Info and -Finish need -Package.' }
    }
    if ($infoTimes) {
        $shot = @{ kind = 'info'; package = $Package }
        if ($Difficulty) { $shot.difficulty = $Difficulty }
        Add-TimedShot $shot $infoTimes
    }
    if ($liveTimes) {
        $shot = @{ kind = 'live'; package = $Package; fakePerfect = (-not $PlainAuto) }
        if ($Difficulty) { $shot.difficulty = $Difficulty }
        Add-TimedShot $shot $liveTimes
    }
    if ($finishTimes) {
        $shot = @{ kind = 'finish'; package = $Package; finishAnimation = $FinishAnimation }
        if ($Difficulty) { $shot.difficulty = $Difficulty }
        Add-TimedShot $shot $finishTimes
    }
    if ($shots.Count -eq 0) { throw 'Nothing to capture: pass -All, -HomePage, -Server, -Packs, -ProfilePage, -DanPage, -AchievementsPage, -ShopPage, -MedalsPage, -RankingTiers, -Sample, -StyleGuide, -Intro, -Live, -Info, -Finish, -Library, -Settings, -SettingsTrace, -Result or -Request.' }

    $requestPath = Join-Path $requestDir "request-$stamp.json"
    $body = @{ outDir = $OutDir; width = $Width; height = $Height; shots = @($shots) }
    $body | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $requestPath -Encoding UTF8
}

$statusPath = [IO.Path]::ChangeExtension($requestPath, $null).TrimEnd('.') + '.status.json'
if (Test-Path $statusPath) { Remove-Item $statusPath -Force }
Write-Host "[shots] request: $requestPath"

$estimate = 0
foreach ($shot in (Get-Content -Raw -Encoding UTF8 -LiteralPath $requestPath | ConvertFrom-Json).shots) {
    $start = [double]$shot.from
    $end = $shot.to
    $count = 1
    if ($shot.kind -eq 'clip') {
        switch ($shot.name) {
            'tier-upgrade' { $count = 7; if ($null -eq $end) { $end = 1.5; if ($null -eq $shot.from) { $start = -0.5 } } }
            'dan-award' { if ($null -eq $end) { $end = 4 } }
            'special-medals' { if ($null -eq $end) { $end = 4 } }
            'achievement-card' { $count = 2; if ($null -eq $end) { $end = 4.6 } }
            'achievement-unlock' { if ($null -eq $end) { $end = 2.6 } }
            default { throw "Unknown clip '$($shot.name)'." }
        }
    }
    if ($null -ne $end) {
        $rate = if ($null -eq $shot.fps) { 60 } else { $shot.fps }
        $frames = [Math]::Round(([double]$end - $start) * $rate, [MidpointRounding]::AwayFromZero)
        if ($rate -lt 1 -or $rate -gt 240 -or $frames -lt 1) { throw "Invalid sequence range or fps in $($shot.kind)." }
        $estimate += $frames * $count
    }
}
if ($estimate) { Write-Host "[shots] expecting $estimate sequence frames" }

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
    if (((Get-Date) - $started).TotalSeconds -gt $TimeoutSec) { throw "Timed out after $TimeoutSec s. The editor may still be exporting. Check its log and use a larger -TimeoutSec for long sequences." }
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
