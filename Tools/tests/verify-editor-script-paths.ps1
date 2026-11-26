#requires -Version 7.3
# Run with PowerShell and the Unity Editor's .NET 8 SDK on a case-sensitive filesystem.
param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$project = Join-Path ([IO.Path]::GetTempPath()) ("editor-script-paths-" + [guid]::NewGuid())
try {
    $sourceDir = Join-Path $project 'Assets/Game'
    $output = Join-Path $project 'Library/ScriptAssemblies'
    $tools = Join-Path $project 'Tools'
    $sourceDir, $output, $tools, (Join-Path $project 'Assets/Tests') | ForEach-Object { [void][IO.Directory]::CreateDirectory($_) }
    $verifier = Join-Path $tools 'verify-editor-scripts.ps1'
    Copy-Item (Join-Path $PSScriptRoot '../verify-editor-scripts.ps1') $verifier
    $original = Join-Path $sourceDir 'Note.cs'
    $renamed = Join-Path $sourceDir 'note.cs'
    Set-Content $original 'public class Note {}'
    if ([IO.File]::Exists($renamed)) { Write-Host 'Skipped on a case-insensitive filesystem.'; return }

    Set-Content (Join-Path $sourceDir 'OpenSekai.Sample.asmdef') '{"name":"OpenSekai.Sample"}'
    $csproj = Join-Path $project 'OpenSekai.Sample.csproj'
    Set-Content $csproj '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>'
    & $DotNet build $csproj --nologo --verbosity quiet --output $output
    if ($LASTEXITCODE) { throw 'Could not build the DLL/PDB fixture.' }
    $module = [Reflection.Assembly]::LoadFile((Join-Path $output 'OpenSekai.Sample.dll')).ManifestModule
    # Substitute the editor's loaded-module response while exercising the complete disk and PDB check.
    function unity {
        $global:LASTEXITCODE = 0
        @{ success = $true; result = "OpenSekai.Sample=$($module.ModuleVersionId)" } | ConvertTo-Json
    }
    & $verifier
    [IO.File]::Move($original, $renamed)
    try { & $verifier } catch {
        if ($_.Exception.Message.Contains($original) -and $_.Exception.Message.Contains($renamed)) {
            Write-Host 'Detected both the added and removed path after a case-only rename.'
            return
        }
        throw
    }
    throw 'The verifier accepted an assembly with a case-only source rename.'
} finally {
    if ([IO.Directory]::Exists($project)) { [IO.Directory]::Delete($project, $true) }
}
