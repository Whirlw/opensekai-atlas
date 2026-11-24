# Makes a sample workspace: a copy of the project (a git worktree) that keeps its data in a folder of its own,
# so the sample library can be installed, pictured and recorded without touching your songs, scores or settings.
#
# The copy's company name is OpenSekaiSample, which is also how the editor recognises a sample workspace: the
# sample library only installs there and the sample server only answers there
# (Assets/Game/Editor/OpenSekai/Screenshots/SampleLibrary.cs, SampleServer.cs).
#
#   powershell -File Tools/new-sample-workspace.ps1                      next to the project, <project>-sample
#   powershell -File Tools/new-sample-workspace.ps1 -Path D:/promo       somewhere else (keep the path short)
#   powershell -File Tools/new-sample-workspace.ps1 -Ref dev             another commit; default HEAD
#
# Then open it in Unity once (the first import takes several minutes) and, inside it, run
#   powershell -File Tools/capture-screens.ps1 -StyleGuide
#
# It copies committed files only. To remove it: close its editor, then git worktree remove --force <path>.
# The data stays in %USERPROFILE%/AppData/LocalLow/OpenSekaiSample until you delete that folder.

[CmdletBinding()]
param(
    [string]$Path,
    [string]$Ref = 'HEAD'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Path) { $Path = "$root-sample" }
if (-not [IO.Path]::IsPathRooted($Path)) { $Path = Join-Path (Get-Location).Path $Path }
$Path = [IO.Path]::GetFullPath($Path)
if (Test-Path -LiteralPath $Path) { throw "$Path already exists." }

# Unity writes paths of about 150 characters under Library/PackageCache; Windows stops at 260.
if ($Path.Length -gt 90) { throw "Pick a shorter path than $Path ($($Path.Length) characters)." }

& git -C $root worktree add --detach $Path $Ref
if ($LASTEXITCODE -ne 0) { throw 'git worktree add failed.' }

$settings = Join-Path $Path 'ProjectSettings/ProjectSettings.asset'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$text = [IO.File]::ReadAllText($settings, $utf8)
$changes = [ordered]@{
    '(?m)^  companyName: .*$'     = '  companyName: OpenSekaiSample'
    '(?m)^  productName: .*$'     = '  productName: OpenSekai Sample'
    # The editor keeps drawing while another window has the focus.
    '(?m)^  runInBackground: .*$' = '  runInBackground: 1'
}
foreach ($pattern in $changes.Keys) {
    if ($text -notmatch $pattern) { throw "ProjectSettings.asset has no line for $($changes[$pattern].Trim())." }
    $text = $text -replace $pattern, $changes[$pattern]
}
[IO.File]::WriteAllText($settings, $text, $utf8)

$cache = Join-Path $root 'Library/PackageCache'
if (Test-Path -LiteralPath $cache) {
    Write-Host '[sample] copying the package cache, so the first import does not download it again'
    New-Item -ItemType Directory -Force -Path (Join-Path $Path 'Library') | Out-Null
    Copy-Item -LiteralPath $cache -Destination (Join-Path $Path 'Library') -Recurse
}

Write-Host "[sample] ready: $Path"
Write-Host '[sample] its ProjectSettings.asset differs from the project on purpose; do not commit that file from there.'
