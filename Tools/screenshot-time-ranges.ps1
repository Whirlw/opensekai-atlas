# Expand one time-based capture into stills and independent, half-open ranges.
function ConvertTo-ScreenshotShots([hashtable]$Shot, [string[]]$Values, [int]$Fps) {
    $single = @()
    foreach ($value in $Values) {
        $parts = $value -split '\.\.'
        if ($parts.Count -gt 2) { throw "Invalid range '$value'." }
        $numbers = foreach ($part in $parts) {
            $number = [double]::Parse($part, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture)
            if ([double]::IsNaN($number) -or [double]::IsInfinity($number)) { throw "Invalid time '$part'." }
            $number
        }
        if ($parts.Count -eq 1) {
            $single += $numbers
            continue
        }
        if ($single.Count) {
            $still = $Shot.Clone()
            $still.times = $single
            $still
            $single = @()
        }
        $range = $Shot.Clone()
        $range.from = $numbers[0]
        $range.to = $numbers[1]
        $range.fps = $Fps
        $range
    }
    if ($single.Count -or -not $Values) {
        $still = $Shot.Clone()
        if ($single.Count) { $still.times = $single }
        $still
    }
}
