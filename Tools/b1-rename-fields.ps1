param(
    [Parameter(Mandatory = $true)][string[]]$Path,
    [switch]$Apply,
    [string]$MapFile
)

# B1: rename private/serialized fields to _camelCase inside the given files.
# Only touches code regions (string literals and comments are masked first), so log text,
# JSON keys and doc comments keep their spelling.

$ErrorActionPreference = 'Stop'

function Get-MaskMap([string]$text) {
    $map = New-Object System.Collections.Generic.List[object]
    $n = $text.Length
    $i = 0
    while ($i -lt $n) {
        $c = $text[$i]
        $next = if ($i + 1 -lt $n) { $text[$i + 1] } else { [char]0 }
        if ($c -eq '/' -and $next -eq '/') {
            $s = $i
            while ($i -lt $n -and $text[$i] -ne "`n") { $i++ }
            $map.Add([pscustomobject]@{ Index = $s; Length = $i - $s }); continue
        }
        if ($c -eq '/' -and $next -eq '*') {
            $s = $i; $i += 2
            while ($i + 1 -lt $n -and -not ($text[$i] -eq '*' -and $text[$i + 1] -eq '/')) { $i++ }
            $i = [Math]::Min($n, $i + 2)
            $map.Add([pscustomobject]@{ Index = $s; Length = $i - $s }); continue
        }
        if ($c -eq '@' -and $next -eq '"') {
            $s = $i; $i += 2
            while ($i -lt $n) {
                if ($text[$i] -eq '"' -and $i + 1 -lt $n -and $text[$i + 1] -eq '"') { $i += 2; continue }
                if ($text[$i] -eq '"') { $i++; break }
                $i++
            }
            $map.Add([pscustomobject]@{ Index = $s; Length = $i - $s }); continue
        }
        if ($c -eq '"') {
            $interp = ($i -gt 0 -and $text[$i - 1] -eq '$')
            $s = $i; $i++
            while ($i -lt $n) {
                if ($text[$i] -eq '\') { $i += 2; continue }
                if ($text[$i] -eq '"') { $i++; break }
                $i++
            }
            if (-not $interp) { $map.Add([pscustomobject]@{ Index = $s; Length = $i - $s }) }
            continue
        }
        if ($c -eq "'") {
            $s = $i; $i++
            while ($i -lt $n) {
                if ($text[$i] -eq '\') { $i += 2; continue }
                if ($text[$i] -eq "'") { $i++; break }
                $i++
            }
            $map.Add([pscustomobject]@{ Index = $s; Length = $i - $s }); continue
        }
        $i++
    }
    return ,$map
}
function Test-InMask($map, [int]$index) {
    foreach ($seg in $map) {
        if ($index -ge $seg.Index -and $index -lt ($seg.Index + $seg.Length)) { return $true }
    }
    return $false
}

$declPatterns = @(
    '(?m)^[ \t]*(?:\[[^\]]*\][ \t]*\r?\n[ \t]*)*private[ \t]+(?:(?:static|readonly|volatile)[ \t]+)*[A-Za-z_][\w<>,\.\[\]\? ]*[ \t]+([a-z][A-Za-z0-9_]*)[ \t]*(?:=[^;]*)?;[ \t]*\r?$',
    '(?m)^[ \t]*\[SerializeField\][ \t]*\r?\n[ \t]*(?:private[ \t]+)?(?:(?:static|readonly)[ \t]+)*(?!(?:protected|internal|public)[ \t])[A-Za-z_][\w<>,\.\[\]\? ]*[ \t]+([a-z][A-Za-z0-9_]*)[ \t]*(?:=[^;]*)?;[ \t]*\r?$'
)

$renamedTotal = 0
$fileCount = 0
foreach ($p in $Path) {
    if (-not (Test-Path $p)) { continue }
    $text = [System.IO.File]::ReadAllText($p)
    $mask = Get-MaskMap $text
    $names = New-Object System.Collections.Generic.HashSet[string]
    foreach ($dp in $declPatterns) {
        foreach ($m in [regex]::Matches($text, $dp)) {
            $name = $m.Groups[1].Value
            if ($name -and -not $name.StartsWith('_')) { [void]$names.Add($name) }
        }
    }
    if ($names.Count -eq 0) { continue }

    $new = $text
    $changed = New-Object System.Collections.Generic.HashSet[string]
    foreach ($name in $names) {
        $target = if ($name -match '^m_([A-Za-z].*)$') { '_' + $Matches[1].Substring(0, 1).ToLower() + $Matches[1].Substring(1) } else { '_' + $name }
        $pattern = '\b' + [regex]::Escape($name) + '\b'
        # replace only code-region matches
        $sb = New-Object System.Text.StringBuilder
        $pos = 0
        $didReplace = $false
        foreach ($m in [regex]::Matches($new, $pattern)) {
            if (Test-InMask $mask $m.Index) { continue }
            if ($m.Index -gt 0 -and $new[$m.Index - 1] -eq '.') {
                $j = $m.Index - 2
                while ($j -ge 0 -and ($new[$j] -match '[A-Za-z0-9_]')) { $j-- }
                $qualifier = if ($j + 1 -le $m.Index - 2) { $new.Substring($j + 1, $m.Index - 1 - ($j + 1)) } else { '' }
                if ($qualifier -ne 'this' -and $qualifier -ne 'base') { continue }
            }
            [void]$sb.Append($new.Substring($pos, $m.Index - $pos))
            [void]$sb.Append($target)
            $pos = $m.Index + $m.Length
            $didReplace = $true
        }
        if ($didReplace) {
            [void]$sb.Append($new.Substring($pos))
            $new = $sb.ToString()
            [void]$changed.Add($name)
            if ($Apply -and $MapFile) { Add-Content -Path $MapFile -Value ("{0}`t{1}`t{2}" -f $p, $name, $target) -Encoding UTF8 }
            # re-mask because offsets shifted
            $mask = Get-MaskMap $new
        }
    }
    if ($changed.Count -gt 0) {
        $fileCount++
        $renamedTotal += $changed.Count
        if ($Apply) {
            [System.IO.File]::WriteAllText($p, $new, (New-Object System.Text.UTF8Encoding($false)))
        }
        Write-Output ("{0} -> {1} field(s): {2}" -f $p, $changed.Count, (($changed | Sort-Object) -join ', '))
    }
}

Write-Output ("files={0} renames={1} apply={2}" -f $fileCount, $renamedTotal, [bool]$Apply)
