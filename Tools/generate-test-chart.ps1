# Regenerates the short test chart used by the on-device and in-editor gates.
#
# The chart is 120 BPM 4/4 and walks every runtime note category (Normal, Long, Connection,
# Flick, Friction, FrictionLong, FrictionFlick) in both Default and Critical variants, so a
# single 30 second run exercises every note prefab in the pool.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/generate-test-chart.ps1
param(
    [string]$Output = "Tools/fixtures/baseline_tap/score.json",
    [int]$Bpm = 120,
    [double]$LeadInSeconds = 3
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    # 120 BPM 4/4 -> one beat is 480 ticks, one bar is 1920 ticks (one second at this tempo).
    $beatsPerBar = 4
    $ticksPerBeat = 480
    $barTicks = $beatsPerBar * $ticksPerBeat

    $notes = New-Object System.Collections.Generic.List[object]
    $nextId = 10

    function Add-Note {
        param(
            [long]$Ticks,
            [int]$LaneStart,
            [int]$LaneEnd,
            [int]$Category,
            [int]$Type,
            [int]$BaseType,
            [int]$Previous = -1,
            [int]$Next = -1
        )

        $script:nextId++
        $notes.Add([ordered]@{
                id                  = $script:nextId
                ticks               = $Ticks
                laneStart           = $LaneStart
                laneEnd             = $LaneEnd
                category            = $Category
                type                = $Type
                speedRatio          = 1
                noteLineType        = 0
                noteBaseType        = $BaseType
                previousConnectionId = $Previous
                nextConnectionId    = $Next
                direction           = 0
                isSkip              = $false
                isDecoration        = $false
            }) | Out-Null
        return $script:nextId
    }

    # Absolute tick of the first bar; LeadInSeconds is handled by the manifest fillerSec.
    $bar = 1

    function Next-Bar {
        $script:bar++
        return [long]($script:bar * $barTicks)
    }

    # Simple single-note categories: Normal(0/1), Flick(3/3), Friction(4/11),
    # FrictionFlick(8/4). Base type mirrors what the SUS converter emits.
    $simple = @(
        @{ Category = 0; BaseType = 1; Name = "Normal" },
        @{ Category = 3; BaseType = 3; Name = "Flick" },
        @{ Category = 4; BaseType = 11; Name = "Friction" },
        @{ Category = 8; BaseType = 4; Name = "FrictionFlick" }
    )

    $tick = [long]($bar * $barTicks)
    foreach ($kind in $simple) {
        foreach ($type in 0, 1) {
            $lane = if ($type -eq 0) { 3 } else { 8 }
            Add-Note -Ticks $tick -LaneStart $lane -LaneEnd $lane -Category $kind.Category -Type $type -BaseType $kind.BaseType | Out-Null
            $tick = Next-Bar
        }
    }

    # Paired categories: Long(2 / base 2 -> end base 1), Connection(2 / base 5),
    # FrictionLong(6 / base 8 -> end base 11).
    $paired = @(
        @{ Category = 1; StartBase = 2; EndBase = 1; Name = "Long" },
        @{ Category = 2; StartBase = 5; EndBase = 5; Name = "Connection" },
        @{ Category = 6; StartBase = 8; EndBase = 11; Name = "FrictionLong" }
    )

    foreach ($kind in $paired) {
        foreach ($type in 0, 1) {
            $lane = if ($type -eq 0) { 4 } else { 7 }
            $startId = Add-Note -Ticks $tick -LaneStart $lane -LaneEnd $lane -Category $kind.Category -Type $type -BaseType $kind.StartBase
            $endTick = $tick + ($barTicks / 2)
            $endId = Add-Note -Ticks $endTick -LaneStart $lane -LaneEnd $lane -Category $kind.Category -Type $type -BaseType $kind.EndBase -Previous $startId
            # Patch the start note so it points at its end.
            foreach ($n in $notes) {
                if ($n.id -eq $startId) { $n.nextConnectionId = $endId }
            }
            $tick = Next-Bar
        }
    }

    $notes = $notes | Sort-Object { $_.ticks }
    $maxTicks = ($notes | ForEach-Object { [long]$_.ticks } | Measure-Object -Maximum).Maximum + $barTicks

    $score = [ordered]@{
        VersionCode             = 1
        MusicId                 = 0
        MusicScoreTicksMax      = $maxTicks
        MusicScoreEventDataList = @(
            [ordered]@{ changeValue = $Bpm; id = 1; ticks = 0; eventType = 0 },
            [ordered]@{ changeValue = 1; id = 2; ticks = 0; eventType = 1 },
            [ordered]@{ changeValue = 1; id = 3; ticks = 0; eventType = 2 },
            [ordered]@{ changeValue = "4/4"; id = 4; ticks = 0; eventType = 3 }
        )
        NoteList                = @($notes)
    }

    $json = $score | ConvertTo-Json -Depth 6
    $target = Join-Path $repoRoot $Output
    [System.IO.File]::WriteAllText($target, $json, (New-Object System.Text.UTF8Encoding($false)))

    Write-Host ("Wrote {0}: {1} notes, ticks max {2}" -f $Output, $notes.Count, $maxTicks)
}
finally {
    Pop-Location
}
