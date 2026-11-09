# Cross-checks the audio that ships against the credit table, so no third-party sound can slip in
# unrecorded. Reads the expected cue list (docs/reference/audio-cues.csv, generated from the ACBs),
# the credit table (docs/reference/audio-credits.md) and whatever loose wav files exist under
# StreamingAssets/sound. Fails when a shipped cue has no credit row, or when the row is still 待定.
param(
    [string]$CueList = 'docs\reference\audio-cues.csv',
    [string]$Credits = 'docs\reference\audio-credits.md',
    [string]$LooseRoot = 'Assets\StreamingAssets\sound',
    [switch]$AllowPending
)
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $CueList)) { throw "cue list not found: $CueList" }
if (-not (Test-Path $Credits)) { throw "credit table not found: $Credits" }

# expected: (bundle, cue) pairs that the build can play
$expected = @{}
foreach ($r in (Import-Csv $CueList)) {
    $expected[$r.bundle + '|' + $r.cue] = $r
}

# recorded: rows of the markdown table, columns bundle | cue | source | author | licence | url | date | change
$recorded = @{}
$pending = @()
foreach ($line in Get-Content $Credits) {
    if ($line -notmatch '^\|') { continue }
    if ($line -match '^\|\s*-{2,}') { continue }
    if ($line -match '^\|\s*bundle\s*\|') { continue }
    $cells = $line.Trim('|').Split('|') | ForEach-Object { $_.Trim() }
    if ($cells.Count -lt 3) { continue }
    $bundle = $cells[0]
    $cue = $cells[1].Trim('`', ' ')
    $source = $cells[2]
    $key = $bundle + '|' + $cue
    $recorded[$key] = $source
    if ($source -eq '待定' -or [string]::IsNullOrWhiteSpace($source)) { $pending += $key }
}

$missing = @()
foreach ($key in $expected.Keys) { if (-not $recorded.ContainsKey($key)) { $missing += $key } }

$orphan = @()
foreach ($key in $recorded.Keys) { if (-not $expected.ContainsKey($key)) { $orphan += $key } }

$loose = @()
if (Test-Path $LooseRoot) {
    $loose = Get-ChildItem $LooseRoot -Recurse -File -Include *.wav, *.ogg | ForEach-Object {
        $rel = $_.FullName.Substring((Resolve-Path $LooseRoot).Path.Length).TrimStart('\')
        $rel -replace '\\', '/'
    }
}

"expected=$($expected.Count) recorded=$($recorded.Count) pending=$($pending.Count) missing=$($missing.Count) orphan=$($orphan.Count) looseFiles=$($loose.Count)"
if ($missing.Count) { '--- cues that ship but have no credit row ---'; $missing | Select-Object -First 20 }
if ($orphan.Count) { '--- credit rows that no longer match a cue ---'; $orphan | Select-Object -First 20 }
if ($loose.Count) { '--- loose audio files present (must be covered above) ---'; $loose | Select-Object -First 20 }

$failed = $false
if ($missing.Count) { $failed = $true }
if ($orphan.Count) { $failed = $true }
if (-not $AllowPending -and $pending.Count) { $failed = $true }

if ($failed) {
    "AUDIT FAILED"
    exit 1
}
"AUDIT OK"
