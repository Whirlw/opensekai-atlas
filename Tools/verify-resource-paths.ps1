# Checks that every literal Resources.Load("...") in the project points at an asset that exists.
# Unity only fails these at runtime, and a missing one is silent apart from a null - which is exactly how
# the library-to-live transition broke once. Build a guard instead of trusting a green compile.
param(
    [string]$Assets = 'Assets',
    [string]$Report = 'Temp\resource-paths.txt'
)
$ErrorActionPreference = 'Stop'

$codeFiles = Get-ChildItem $Assets -Recurse -File -Include *.cs
$problems = @()
$checked = 0

# every Resources root in the project, with the prefix Unity strips
$roots = Get-ChildItem $Assets -Recurse -Directory -Filter 'Resources' | ForEach-Object { $_.FullName }

foreach ($file in $codeFiles) {
    $text = Get-Content $file.FullName -Raw
    foreach ($m in [regex]::Matches($text, 'Resources\.(?:Load|LoadAll)(?:<[^>]+>)?\(\s*"([^"]+)"')) {
        $call = $m.Groups[1].Value
        # paths built from constants cannot be resolved here
        if ($call.Contains('*') -or $call -eq '') { continue }
        $checked++

        $found = $false
        foreach ($root in $roots) {
            foreach ($ext in @('.prefab', '.asset', '.mat', '.png', '.ogg', '.wav', '.txt', '.json', '.bytes', '.otf', '.ttf', '.fnt', '')) {
                $candidate = Join-Path $root ($call + $ext)
                if (Test-Path $candidate) { $found = $true; break }
            }
            if ($found) { break }
            # LoadAll/A directory form
            if (Test-Path (Join-Path $root $call)) { $found = $true; break }
        }

        if (-not $found) {
            $problems += [pscustomobject]@{
                File = $file.FullName.Substring((Resolve-Path $Assets).Path.Length).TrimStart('\')
                Line = ($text.Substring(0, $m.Index) -split "`n").Count
                Path = $call
            }
        }
    }
}

New-Item -ItemType Directory -Force -Path (Split-Path $Report -Parent) | Out-Null
$lines = @("calls=$checked missing=$($problems.Count)") + ($problems | ForEach-Object { "$($_.File):$($_.Line)  Resources.Load(""$($_.Path)"")" })
Set-Content $Report ($lines -join "`n") -Encoding UTF8

"calls=$checked missing=$($problems.Count)"
$problems | Select-Object -First 12 | ForEach-Object { "  $($_.File):$($_.Line)  $($_.Path)" }
if ($problems.Count -gt 0) { exit 1 }
