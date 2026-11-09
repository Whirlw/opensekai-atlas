param(
    [switch]$Apply,
    [string]$Root = 'Assets'
)

# B1: constants must be ALL_UPPER with underscores. Renames the declaration and every usage
# across the code base (constants are often referenced from other files). String literals and
# comments are masked so log text and docs keep their spelling.

$ErrorActionPreference = 'Stop'

function Convert-ToAllCaps([string]$name) {
    $n = $name.TrimStart('_')
    $n = $n -replace '^m_', ''
    $n = $n -creplace '([a-z0-9])([A-Z])', '${1}_${2}'
    $n = $n -creplace '([A-Z]+)([A-Z][a-z])', '${1}_${2}'
    $n = $n -replace '[^A-Za-z0-9]+', '_'
    return $n.ToUpperInvariant().Trim('_')
}

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
            $s = $i; $i++
            while ($i -lt $n) {
                if ($text[$i] -eq '\') { $i += 2; continue }
                if ($text[$i] -eq '"') { $i++; break }
                $i++
            }
            $map.Add([pscustomobject]@{ Index = $s; Length = $i - $s }); continue
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

$files = Get-ChildItem $Root -Recurse -File -Filter *.cs | ForEach-Object { $_.FullName }
Write-Output ("scanning {0} files" -f $files.Count)

$declPattern = '(?m)^[ \t]*(?:\[[^\]]*\][ \t]*\r?\n[ \t]*)*(?:public|private|protected|internal)[ \t]+(?:new[ \t]+)?const[ \t]+[A-Za-z_][\w<>,\.\[\]\? ]*[ \t]+([A-Za-z_][A-Za-z0-9_]*)[ \t]*='
$renames = @{}
$script:owners = @{}

foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f)
    foreach ($m in [regex]::Matches($text, $declPattern)) {
        $name = $m.Groups[1].Value
        if ($name -cmatch '^[A-Z][A-Z0-9_]*$') { continue }
        $new = Convert-ToAllCaps $name
        if (-not $new -or $new -eq $name) { continue }
        if (-not $renames.ContainsKey($name)) {
            $renames[$name] = $new
            $head = $text.Substring(0, $m.Index)
            $tm = [regex]::Matches($head, '(?:class|struct|interface)\s+([A-Za-z_][A-Za-z0-9_]*)')
            if ($tm.Count -gt 0) { $script:owners[$name] = $tm[$tm.Count - 1].Groups[1].Value }
        }
    }
}

Write-Output ("constants to rename: {0}" -f $renames.Count)

$collisions = @{}
foreach ($kv in $renames.GetEnumerator()) {
    if ($collisions.ContainsKey($kv.Value)) { Write-Output ("COLLISION {0} <- {1} and {2}" -f $kv.Value, $collisions[$kv.Value], $kv.Key) }
    else { $collisions[$kv.Value] = $kv.Key }
}

$totalHits = 0
foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f)
    $mask = Get-MaskMap $text
    $new = $text
    $hits = 0
    foreach ($kv in $renames.GetEnumerator()) {
        if (-not $script:owners.ContainsKey($kv.Key)) { continue }
        $type = $script:owners[$kv.Key]
        $qPattern = '\b' + [regex]::Escape($type) + '\s*\.\s*' + [regex]::Escape($kv.Key) + '\b'
        if ([regex]::IsMatch($new, $qPattern)) {
            $new = [regex]::Replace($new, $qPattern, ($type + '.' + $kv.Value))
            $hits++
            $mask = Get-MaskMap $new
        }
    }

    foreach ($kv in $renames.GetEnumerator()) {
        $pattern = '\b' + [regex]::Escape($kv.Key) + '\b'
        $sb = New-Object System.Text.StringBuilder
        $pos = 0
        $did = $false
        foreach ($m in [regex]::Matches($new, $pattern)) {
            if (Test-InMask $mask $m.Index) { continue }
            if ($m.Index -gt 0 -and $new[$m.Index - 1] -eq '.') { continue }
            [void]$sb.Append($new.Substring($pos, $m.Index - $pos))
            [void]$sb.Append($kv.Value)
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
        $totalHits += $hits
        if ($Apply) { [System.IO.File]::WriteAllText($f, $new, (New-Object System.Text.UTF8Encoding($false))) }
    }
}

Write-Output ("files touched={0} apply={1}" -f $totalHits, [bool]$Apply)
