param(
    [Parameter(Mandatory = $true)][string[]]$Path,
    [Parameter(Mandatory = $true)][string[]]$Name,
    [switch]$Apply
)

# B1 helper: propagate a field rename into files that use an inherited field.
# Names come from compiler errors ("The name 'x' does not exist in the current context")
# after the declaring file was renamed.

$ErrorActionPreference = 'Stop'

function Get-MaskMap([string]$text) {
    $map = New-Object System.Collections.Generic.List[object]
    $patterns = @(
        '(?s)@"(?:[^"]|"")*"',
        '(?s)"(?:[^"\\]|\\.)*"',
        "(?s)'(?:[^'\\]|\\.)*'",
        '(?s)//[^\r\n]*',
        '(?s)/\*.*?\*/'
    )
    foreach ($p in $patterns) {
        foreach ($m in [regex]::Matches($text, $p)) {
            $prev = if ($m.Index -gt 0) { $text[$m.Index - 1] } else { ' ' }
            $prev2 = if ($m.Index -gt 1) { $text[$m.Index - 2] } else { ' ' }
            if ($prev -eq '$' -or $prev2 -eq '$') { continue }
            $map.Add([pscustomobject]@{ Index = $m.Index; Length = $m.Length })
        }
    }
    return ,$map
}

function Test-InMask($map, [int]$index) {
    foreach ($seg in $map) {
        if ($index -ge $seg.Index -and $index -lt ($seg.Index + $seg.Length)) { return $true }
    }
    return $false
}

$total = 0
foreach ($p in $Path) {
    if (-not (Test-Path $p)) { continue }
    $text = [System.IO.File]::ReadAllText($p)
    $mask = Get-MaskMap $text
    $new = $text
    $hits = 0
    foreach ($n in $Name) {
        $pattern = '\b' + [regex]::Escape($n) + '\b'
        $sb = New-Object System.Text.StringBuilder
        $pos = 0
        $did = $false
        foreach ($m in [regex]::Matches($new, $pattern)) {
            if (Test-InMask $mask $m.Index) { continue }
            if ($m.Index -gt 0 -and $new[$m.Index - 1] -eq '.') {
                $j = $m.Index - 2
                while ($j -ge 0 -and ($new[$j] -match '[A-Za-z0-9_]')) { $j-- }
                $qualifier = if ($j + 1 -le $m.Index - 2) { $new.Substring($j + 1, $m.Index - 1 - ($j + 1)) } else { '' }
                if ($qualifier -ne 'this' -and $qualifier -ne 'base') { continue }
            }
            [void]$sb.Append($new.Substring($pos, $m.Index - $pos))
            [void]$sb.Append('_' + $n)
            $pos = $m.Index + $m.Length
            $did = $true
        }
        if ($did) {
            [void]$sb.Append($new.Substring($pos))
            $new = $sb.ToString()
            $hits++
            $mask = Get-MaskMap $new
        }
    }
    if ($hits -gt 0) {
        $total += $hits
        if ($Apply) { [System.IO.File]::WriteAllText($p, $new, (New-Object System.Text.UTF8Encoding($false))) }
        Write-Output ("{0} ({1} name(s))" -f $p, $hits)
    }
}
Write-Output ("propagated hits={0} apply={1}" -f $total, [bool]$Apply)
