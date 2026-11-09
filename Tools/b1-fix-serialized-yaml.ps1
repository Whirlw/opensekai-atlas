param(
    [Parameter(Mandatory = $true)][string]$MapFile,
    [switch]$Apply
)

# B1 helper: serialized fields are stored in prefabs/scenes/assets by field NAME, so renaming
# a [SerializeField] field must also rewrite the YAML keys of the components whose
# m_Script guid points at that script. Only keys inside the matching component block change.

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $MapFile)) { throw "map file not found: $MapFile" }

$entries = @()
foreach ($line in Get-Content $MapFile) {
    $parts = $line -split "`t"
    if ($parts.Count -lt 3) { continue }
    $script = $parts[0]
    $old = $parts[1]
    $new = $parts[2]
    $meta = "$script.meta"
    if (-not (Test-Path $meta)) { continue }
    $g = ([regex]::Match([System.IO.File]::ReadAllText($meta), '(?m)^guid: ([0-9a-f]{32})')).Groups[1].Value
    if (-not $g) { continue }
    $entries += [pscustomobject]@{ Script = $script; Guid = $g; Old = $old; New = $new }
}

Write-Output ("map entries: {0}" -f $entries.Count)
$byGuid = @{}
foreach ($e in $entries) {
    if (-not $byGuid.ContainsKey($e.Guid)) { $byGuid[$e.Guid] = @() }
    $byGuid[$e.Guid] += $e
}

$files = Get-ChildItem Assets -Recurse -File -Include *.prefab, *.unity, *.asset, *.mat, *.controller, *.anim
$touched = 0
$renamedKeys = 0

foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f.FullName)
    if ($text.IndexOf('m_Script:') -lt 0) { continue }
    $lines = $text -split "`n"
    $current = $null
    $changed = 0
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ($line.StartsWith('--- ')) { $current = $null; continue }
        $ms = [regex]::Match($line, 'm_Script:\s*\{fileID:\s*-?\d+, guid: ([0-9a-f]{32})')
        if ($ms.Success) {
            $g = $ms.Groups[1].Value
            $current = if ($byGuid.ContainsKey($g)) { $byGuid[$g] } else { $null }
            continue
        }
        if ($null -eq $current) { continue }
        foreach ($e in $current) {
            $pat = '^(?<indent>[ \t]+)' + [regex]::Escape($e.Old) + ':'
            if ($line -match $pat) {
                $lines[$i] = [regex]::Replace($line, $pat, ('${indent}' + $e.New + ':'))
                $changed++
            }
        }
    }
    if ($changed -gt 0) {
        $touched++
        $renamedKeys += $changed
        if ($Apply) { [System.IO.File]::WriteAllText($f.FullName, ($lines -join "`n"), (New-Object System.Text.UTF8Encoding($false))) }
        Write-Output ("{0} keys={1}" -f $f.FullName.Replace((Get-Location).Path + '\', ''), $changed)
    }
}

Write-Output ("assets touched={0} keys={1} apply={2}" -f $touched, $renamedKeys, [bool]$Apply)
