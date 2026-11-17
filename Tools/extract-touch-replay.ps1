#Requires -Version 7.0
<#
Turns one latency capture into a gzipped replay stream for the EditMode stall replay.

Rows (tab separated):
  #  screen=<w>x<h> logic_offset_s=<display minus logic> source=<capture file>
  F  <game_s> <logic_s> <delta_s>                      one Unity frame, in capture order
  T  <touch_id> <finger> <phase> <time_s> <x> <y> <dx> <dy>   a sample the game consumed in that frame
  J  <note_id> <result> <logic_s> <skip>               a judgment the device published
  P  <note_id> <touch_id> <error_ms> <result>          a press the device spent on a note

Positions are Unity screen pixels from the EnhancedTouch callback. Deltas are the change since the
previous sample consumed for the same touch, which is what EnhancedTouch reports per frame.

Usage:
  ./Tools/extract-touch-replay.ps1 TestResults/latency-....tsv Tools/fixtures/<name>/replay.tsv.gz
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Capture,
    [Parameter(Mandatory, Position = 1)][string]$Output
)

$ErrorActionPreference = 'Stop'
$culture = [Globalization.CultureInfo]::InvariantCulture
$file = (Resolve-Path -LiteralPath $Capture).ProviderPath
$reader = [IO.StreamReader]::new($file)
$meta = $reader.ReadLine()
[void]$reader.ReadLine()
$end = $reader.ReadLine()
if ($end -notmatch 'overflow=0(?: probe_overflow=0)?$') { throw "Capture has dropped rows: $end" }
if ($meta -notmatch ' resolution=(\d+)x(\d+) ') { throw 'Capture header has no resolution.' }
$width = $Matches[1]; $height = $Matches[2]
$columns = $reader.ReadLine().Split("`t")
$c = @{}
for ($i = 0; $i -lt $columns.Count; $i++) { $c[$columns[$i]] = $i }
foreach ($required in 'x', 'y', 'song_s', 'source_s') { if (-not $c.ContainsKey($required)) { throw "Capture lacks column $required" } }

function N([string]$value) { [double]::Parse($value, $culture) }

$frames = [System.Collections.Generic.List[object]]::new()
$frameIndex = @{}
$received = @{}
$consumed = [System.Collections.Generic.List[string[]]]::new()
$judged = [System.Collections.Generic.List[string[]]]::new()
$spent = [System.Collections.Generic.List[string[]]]::new()
while ($null -ne ($line = $reader.ReadLine())) {
    $r = $line.Split("`t")
    switch ($r[$c.stage]) {
        'frame' {
            $frameIndex[$r[$c.frame]] = $frames.Count
            $frames.Add([pscustomobject]@{ Frame = $r[$c.frame]; Game = N $r[$c.source_s]; Display = N $r[$c.song_s]; Delta = N $r[$c.value]; Touches = [System.Collections.Generic.List[string]]::new(); Logic = [double]::NaN })
        }
        'received' { $received["$($r[$c.touch_id])/$($r[$c.phase])/$($r[$c.source_s])"] = $r }
        'consumed' { if ($r[$c.detail] -ne 'editor_mouse') { $consumed.Add($r) } }
        'judged' { $judged.Add($r) }
        'press_spent' { if ($r[$c.result] -ne 'None') { $spent.Add($r) } }
    }
}
$reader.Dispose()

# The logic clock is the display clock minus a constant (filler and offsets); recover it from consumption.
$offsets = [System.Collections.Generic.List[double]]::new()
$last = @{}
$missingPosition = 0
foreach ($r in $consumed) {
    if (-not $frameIndex.ContainsKey($r[$c.frame])) { continue }
    $frame = $frames[$frameIndex[$r[$c.frame]]]
    $source = N $r[$c.source_s]
    $lag = [Math]::Min([Math]::Max($source - $frame.Game, -0.25), 0)
    $offsets.Add($frame.Display - ((N $r[$c.song_s]) - $lag))

    $touch = $r[$c.touch_id]
    $rec = $received["$touch/$($r[$c.phase])/$($r[$c.source_s])"]
    if ($rec) { $x = N $rec[$c.x]; $y = N $rec[$c.y] }
    elseif ($last.ContainsKey($touch)) { $x = $last[$touch][0]; $y = $last[$touch][1]; $missingPosition++ }
    else { $missingPosition++; continue }
    $dx = 0.0; $dy = 0.0
    if ($r[$c.phase] -ne 'Began' -and $last.ContainsKey($touch)) { $dx = $x - $last[$touch][0]; $dy = $y - $last[$touch][1] }
    $last[$touch] = @($x, $y)
    $frame.Touches.Add([string]::Join("`t", 'T', $touch, $r[$c.finger], $r[$c.phase], $r[$c.source_s],
        $x.ToString('R', $culture), $y.ToString('R', $culture), $dx.ToString('R', $culture), $dy.ToString('R', $culture)))
}
$offsets.Sort()
$offset = $offsets[[int]($offsets.Count / 2)]
$spread = ($offsets[[int]($offsets.Count * 0.99)] - $offsets[[int]($offsets.Count * 0.01)]) * 1000
Write-Host ([string]::Format($culture, 'logic offset {0:F6} s, 1-99% spread {1:F3} ms over {2} samples; samples without a callback position: {3}',
    $offset, $spread, $offsets.Count, $missingPosition))
if ($spread -gt 1.5) { Write-Warning 'Logic offset is not constant; the display clock is not a fixed shift of the logic clock.' }

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# screen=${width}x$height logic_offset_s=$($offset.ToString('R', $culture)) source=$([IO.Path]::GetFileName($file))")
foreach ($frame in $frames) {
    $logic = [float]($frame.Display - $offset)
    $lines.Add([string]::Join("`t", 'F', $frame.Game.ToString('R', $culture), $logic.ToString('R', $culture), $frame.Delta.ToString('R', $culture)))
    $lines.AddRange($frame.Touches)
}
foreach ($r in $judged) { $lines.Add([string]::Join("`t", 'J', $r[$c.note_id], $r[$c.result], $r[$c.song_s], ($r[$c.detail] -eq 'skip'))) }
foreach ($r in $spent) { $lines.Add([string]::Join("`t", 'P', $r[$c.note_id], $r[$c.touch_id], $r[$c.value], $r[$c.result])) }

$target = [IO.Path]::GetFullPath($Output)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
$stream = [IO.File]::Create($target)
try {
    $gzip = [IO.Compression.GZipStream]::new($stream, [IO.Compression.CompressionLevel]::SmallestSize)
    $writer = [IO.StreamWriter]::new($gzip, [Text.UTF8Encoding]::new($false))
    $writer.NewLine = "`n"
    foreach ($l in $lines) { $writer.WriteLine($l) }
    $writer.Dispose()
} finally { $stream.Dispose() }
Write-Host ("wrote {0}: {1} frames, {2} touch samples, {3} judgments, {4} spent presses, {5:N0} bytes" -f
    $Output, $frames.Count, ($frames | ForEach-Object { $_.Touches.Count } | Measure-Object -Sum).Sum, $judged.Count, $spent.Count, (Get-Item $target).Length)
