param([switch]$Apply, [string]$Root = 'Assets/Game')

# B1: one attribute per line. `[A, B]` becomes two separate attribute lines with the same indent.
# Commas inside (..) or <..> or strings are not treated as separators.

$ErrorActionPreference = 'Stop'

function Split-Attributes([string]$body) {
    $parts = New-Object System.Collections.Generic.List[string]
    $depth = 0
    $inString = $false
    $current = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $body.Length; $i++) {
        $c = $body[$i]
        if ($c -eq '"' -and ($i -eq 0 -or $body[$i - 1] -ne '\')) { $inString = -not $inString }
        if (-not $inString) {
            if ($c -eq '(' -or $c -eq '<' -or $c -eq '[') { $depth++ }
            elseif ($c -eq ')' -or $c -eq '>' -or $c -eq ']') { $depth-- }
            elseif ($c -eq ',' -and $depth -eq 0) { $parts.Add($current.ToString().Trim()); [void]$current.Clear(); continue }
        }
        [void]$current.Append($c)
    }
    if ($current.Length -gt 0) { $parts.Add($current.ToString().Trim()) }
    return ,$parts
}

$files = Get-ChildItem $Root -Recurse -File -Filter *.cs | ForEach-Object { $_.FullName }
$touchedFiles = 0
$splitCount = 0

foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f)
    if ($text.IndexOf('[') -lt 0) { continue }
    $lines = $text -split "`n"
    $changed = $false
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i].TrimEnd("`r")
        $m = [regex]::Match($line, '^(?<indent>[ \t]*)\[(?<body>[^\[\]]+)\](?<tail>[ \t]*)$')
        if (-not $m.Success) { continue }
        $body = $m.Groups['body'].Value.Trim()
        if (-not $body -or $body -match '^\s*assembly:') { continue }
        $parts = Split-Attributes $body
        if ($parts.Count -le 1) { continue }
        $indent = $m.Groups['indent'].Value
        $newLines = @()
        foreach ($p in $parts) { $newLines += ($indent + '[' + $p + ']') }
        $lines[$i] = ($newLines -join "`n")
        $splitCount += ($parts.Count - 1)
        $changed = $true
    }
    if ($changed) {
        $touchedFiles++
        if ($Apply) { [System.IO.File]::WriteAllText($f, ($lines -join "`n"), (New-Object System.Text.UTF8Encoding($false))) }
    }
}

Write-Output ("files={0} attributes={1} apply={2}" -f $touchedFiles, $splitCount, [bool]$Apply)
