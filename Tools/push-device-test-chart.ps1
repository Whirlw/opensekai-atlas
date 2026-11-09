# Pushes the short baseline chart to a connected Android device so the on-device gate
# takes ~35 s instead of a full song.
#
# Usage:
#   pwsh Tools/push-device-test-chart.ps1
#   pwsh Tools/push-device-test-chart.ps1 -Adb "C:\...\adb.exe" -Package io.github.opensekai
param(
    [string]$Adb = "C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe",
    [string]$Package = "io.github.opensekai",
    [string]$Fixture = "Tools/fixtures/baseline_tap",
    [string]$PackageDirectoryName = "Baseline_Tap_120_BPM_codexbaseline20261101tap"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    if (-not (Test-Path $Adb)) {
        throw "adb not found at '$Adb'. Pass -Adb with the path to your SDK's adb.exe."
    }

    if (-not (Test-Path $Fixture)) {
        throw "Fixture not found at '$Fixture'. It ships with the repository under Tools/fixtures/."
    }

    $files = Get-ChildItem $Fixture -File | Where-Object { $_.Extension -ne ".meta" }
    if ($files.Count -eq 0) {
        throw "Fixture '$Fixture' has no files to push."
    }

    $destination = "/sdcard/Android/data/$Package/files/CustomMusicScores/$PackageDirectoryName"

    & $Adb shell mkdir -p $destination | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "adb shell mkdir failed. Is the device connected and visible to 'adb devices'?"
    }

    foreach ($file in $files) {
        & $Adb push $file.FullName "$destination/$($file.Name)" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "adb push failed for $($file.Name)."
        }
    }

    # Release builds are not debuggable, so when the app has never run there is no app-owned
    # CustomMusicScores folder yet and the one adb just created belongs to shell. The player then
    # fails with "Access to the path ... is denied" until the folder is world readable.
    & $Adb shell chmod -R 777 "/sdcard/Android/data/$Package/files/CustomMusicScores" 2>&1 | Out-Null

    Write-Host "Pushed $($files.Count) files to $destination"
    Write-Host "Restart the app, press refresh, then play the 'Test' row (221 notes)."
}
finally {
    Pop-Location
}
