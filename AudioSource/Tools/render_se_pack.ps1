# Regenerate the five shared live sounds in live_se_common from the samples in Tools/samples.
# Overwrites the finished files; run AudioSource/Import-All.bat afterwards.
param([string]$OutDir)
$ErrorActionPreference = 'Stop'
$py = (Get-Command python -ErrorAction SilentlyContinue).Source
if (-not $py) { $py = (Get-Command py -ErrorAction Stop).Source }
$script = Join-Path $PSScriptRoot 'make_se_pack.py'
if ($OutDir) { & $py -B $script $OutDir } else { & $py -B $script }
if ($LASTEXITCODE -ne 0) { throw "make_se_pack.py failed ($LASTEXITCODE)" }
