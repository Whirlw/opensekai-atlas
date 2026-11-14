# Compares two NUnit XML reports from the same test suite and fails when any test
# changed result. P4-2 uses it to prove that two consecutive EditMode runs agree;
# CI can use it the same way to catch order-dependent or leakage-prone tests.
#
#   Tools/compare-test-runs.ps1 -First TestResults/p4-2-editmode-run1.xml `
#                               -Second TestResults/p4-2-editmode-run2.xml
#
# Written as straight-line code on purpose: helpers that returned hashtables from a
# function behaved differently under Windows PowerShell 5.1 than under pwsh 7, and
# this script has to run in both.
param(
    [Parameter(Mandatory = $true)][string]$First,
    [Parameter(Mandatory = $true)][string]$Second
)

$ErrorActionPreference = 'Stop'
$Pattern = '<test-case id="\d+" name="[^"]*" fullname="([^"]+)"[^>]*result="(\w+)"'

foreach ($path in @($First, $Second)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Report not found: $path"
    }
}

$firstRaw = Get-Content -LiteralPath $First -Raw
$secondRaw = Get-Content -LiteralPath $Second -Raw

$firstEntries = [regex]::Matches($firstRaw, $Pattern)
$secondEntries = [regex]::Matches($secondRaw, $Pattern)
if ($firstEntries.Count -eq 0) { throw "No test cases found in $First" }
if ($secondEntries.Count -eq 0) { throw "No test cases found in $Second" }

Write-Output ("first  : {0} tests" -f $firstEntries.Count)
Write-Output ("second : {0} tests" -f $secondEntries.Count)

$secondResults = @{}
foreach ($entry in $secondEntries) {
    $secondResults[$entry.Groups[1].Value] = $entry.Groups[2].Value
}

$problems = 0
foreach ($entry in $firstEntries) {
    $name = $entry.Groups[1].Value
    $result = $entry.Groups[2].Value
    if (-not $secondResults.ContainsKey($name)) {
        Write-Output ("MISSING IN SECOND: {0} (was {1})" -f $name, $result)
        $problems++
    }
    elseif ($secondResults[$name] -ne $result) {
        Write-Output ("CHANGED: {0}: {1} -> {2}" -f $name, $result, $secondResults[$name])
        $problems++
    }
}

$firstNames = @{}
foreach ($entry in $firstEntries) {
    $firstNames[$entry.Groups[1].Value] = $true
}
foreach ($entry in $secondEntries) {
    if (-not $firstNames.ContainsKey($entry.Groups[1].Value)) {
        Write-Output ("NEW IN SECOND: {0} ({1})" -f $entry.Groups[1].Value, $entry.Groups[2].Value)
        $problems++
    }
}

if ($problems -gt 0) {
    Write-Output ("FAILED: {0} differences between the two runs" -f $problems)
    exit 1
}

Write-Output 'OK: both runs report the same result for every test'
exit 0
