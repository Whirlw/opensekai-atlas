# Runs the EditMode suite headless and fails with a non-zero exit code when a test fails.
#
# The Unity CLI refuses to start when the project is already open in an editor, so this script is
# meant for CI or for a local run after closing the editor. Inside an open editor use the menu
# OpenSekai/Dev/Run EditMode Tests instead.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run-editmode-tests.ps1

[CmdletBinding()]
param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$ReportPath = 'TestResults/editmode-ci.xml',
    [string]$UnityExe = 'unity'
)

$ErrorActionPreference = 'Stop'

$absoluteReport = Join-Path $ProjectPath $ReportPath
$reportDirectory = Split-Path -Parent $absoluteReport
if ($reportDirectory -and -not (Test-Path $reportDirectory)) {
    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
}
if (Test-Path $absoluteReport) {
    Remove-Item -Force $absoluteReport
}

Write-Host "[tests] project: $ProjectPath"
Write-Host "[tests] report : $absoluteReport"

& $UnityExe test $ProjectPath --mode EditMode --output $absoluteReport --report-format nunit --non-interactive
$exit = $LASTEXITCODE

if (Test-Path $absoluteReport) {
    $run = ([xml](Get-Content $absoluteReport -Raw)).'test-run'
    Write-Host ("[tests] total={0} passed={1} failed={2} skipped={3} duration={4}s" -f `
        $run.total, $run.passed, $run.failed, $run.skipped, $run.duration)
    foreach ($case in $run.SelectNodes("//test-case[@result='Failed']")) {
        Write-Host ("[tests] FAILED: " + $case.name)
    }
} else {
    Write-Warning "[tests] no report was written; the run probably never started (is the editor still open?)"
}

if ($exit -ne 0) {
    Write-Host "[tests] exit code $exit"
}
exit $exit
