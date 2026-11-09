# Finds assets that nothing in the shipped build can reach.
# Two different notions of "used" have to be checked, because Unity ships them differently:
#   * normal assets  -> must be referenced by GUID from a prefab/scene/material/asset
#   * Resources/**   -> loaded by string, so the path has to appear in code (Resources.Load / ...Path)
param(
    [string]$Assets = 'Assets',
    [switch]$Delete,
    [string]$Report = 'Temp\dead-assets.txt'
)
$ErrorActionPreference = 'Stop'

$referenceHolders = Get-ChildItem $Assets -Recurse -File -Include *.prefab, *.unity, *.mat, *.asset | Where-Object {
    $_.Extension -ne '.meta' -and $_.FullName -notmatch '[\\/]StreamingAssets[\\/]'
}
$codeFiles = Get-ChildItem $Assets -Recurse -File -Include *.cs

# whole-file text of every holder is what we search; keep it in memory once
$holderText = @{}
foreach ($h in $referenceHolders) { $holderText[$h.FullName] = Get-Content $h.FullName -Raw }
$codeText = ($codeFiles | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"

# Mechanisms that keep files alive without any reference this script can see:
#   *.asmdef      - assemblies are referenced by path/name from other asmdefs
#   link.xml      - IL2CPP trim whitelist, keyed by type name
#   Plugins/Android - manifests and gradle templates are picked up by path
$mechanismWhitelist = @('\.asmdef$', 'link\.xml$', '^Plugins/Android/', '\.rsp$')
function Test-Mechanism([string]$relative) {
    foreach ($pattern in $mechanismWhitelist) { if ($relative -match $pattern) { return $true } }
    return $false
}

# The screen layers are fetched by enum, not by GUID or by path: PlayService asks
# ScreenManager.TryGetScreenLayerData(MenuScreenType.LiveLoading). Map each enum value to the
# ScreenLayerData asset that carries it, so those stay alive.
$enumLayerNames = [regex]::Matches($codeText, 'MenuScreenType\.([A-Za-z0-9_]+)') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique
$enumAliveNames = @{}
foreach ($name in $enumLayerNames) {
    $dataAsset = Get-ChildItem $Assets -Recurse -File -Filter "$name.asset" -ErrorAction SilentlyContinue |
        Where-Object { (Get-Content $_.FullName -Raw) -match 'm_Name:\s*' + [regex]::Escape($name) }
    foreach ($asset in $dataAsset) {
        $enumAliveNames[$asset.FullName] = $true
        $ref = Get-Content $asset.FullName -Raw
        foreach ($g in [regex]::Matches($ref, 'guid: ([0-9a-f]{32})')) {
            $meta = Get-ChildItem $Assets -Recurse -File -Filter '*.prefab.meta' -ErrorAction SilentlyContinue |
                Where-Object { (Get-Content $_.FullName -Raw).Contains($g.Groups[1].Value) } | Select-Object -First 1
            if ($meta) { $enumAliveNames[$meta.FullName -replace '\.meta$', ''] = $true }
        }
    }
}

# Scenes are loaded by name (SceneManager.LoadScene / build settings), never by GUID.
$sceneAlive = @{}
$buildSettings = Get-Content 'ProjectSettings/EditorBuildSettings.asset' -Raw -ErrorAction SilentlyContinue
if ($buildSettings) {
    foreach ($guidMatch in [regex]::Matches($buildSettings, 'guid: ([0-9a-f]{32})')) {
        $meta = Get-ChildItem $Assets -Recurse -File -Filter '*.unity.meta' -ErrorAction SilentlyContinue | Where-Object { (Get-Content $_.FullName -Raw).Contains($guidMatch.Groups[1].Value) } | Select-Object -First 1
        if ($meta) { $sceneAlive[$meta.FullName -replace '\.meta$', ''] = $true }
    }
}
foreach ($scene in Get-ChildItem $Assets -Recurse -File -Filter '*.unity' -ErrorAction SilentlyContinue) {
    $leaf = [System.IO.Path]::GetFileNameWithoutExtension($scene.Name)
    if ($codeText -match ('LoadScene[^;]*"' + [regex]::Escape($leaf) + '"')) { $sceneAlive[$scene.FullName] = $true }
}

$rows = @()
foreach ($file in Get-ChildItem $Assets -Recurse -File | Where-Object { $_.Extension -ne '.meta' -and $_.Extension -ne '.cs' -and $_.FullName -notmatch '[\\/](StreamingAssets|AssetBundles)[\\/]' }) {
    $relative = $file.FullName.Substring((Resolve-Path $Assets).Path.Length).TrimStart('\') -replace '\\', '/'
    $isResource = $relative -like 'Resources/*' -or $relative -like '*/Resources/*'

    $used = $false
    $how = ''
    if (Test-Mechanism $relative) { $used = $true; $how = 'mechanism' }
    elseif ($enumAliveNames.ContainsKey($file.FullName)) { $used = $true; $how = 'enum layer' }
	elseif ($sceneAlive.ContainsKey($file.FullName)) { $used = $true; $how = 'scene by name' }
    if (-not $used -and $isResource) {
        # asset path as Unity would spell it: everything after the last /Resources/, without extension
        $index = $relative.LastIndexOf('/Resources/')
        $unityPath = if ($index -ge 0) { $relative.Substring($index + '/Resources/'.Length) } else { $relative.Substring('Resources/'.Length) }
        $unityPath = $unityPath.Substring(0, $unityPath.Length - [System.IO.Path]::GetExtension($unityPath).Length)
        if ($codeText.Contains('"' + $unityPath + '"')) { $used = $true; $how = 'Resources.Load' }
        else {
            # the loading screens build the name from a unit id, so a prefix match keeps them alive
            $leaf = [System.IO.Path]::GetFileNameWithoutExtension($unityPath)
            if ($codeText.Contains($leaf)) { $used = $true; $how = 'name match' }
        }
    }

    if (-not $used) {
        $meta = $file.FullName + '.meta'
        if (Test-Path $meta) {
            $guid = [regex]::Match((Get-Content $meta -Raw), 'guid: ([0-9a-f]{32})').Groups[1].Value
            if ($guid) {
                foreach ($pair in $holderText.GetEnumerator()) {
                    if ($pair.Key -eq $file.FullName) { continue }
                    if ($pair.Value.Contains($guid)) { $used = $true; $how = 'GUID'; break }
                }
            }
        }
    }

    $rows += [pscustomobject]@{
        Path   = $relative
        SizeMB = [math]::Round($file.Length / 1MB, 3)
        Used   = $used
        How    = $how
        IsRes  = $isResource
    }
}

$dead = $rows | Where-Object { -not $_.Used }
$deadMB = [math]::Round(($dead | Measure-Object SizeMB -Sum).Sum, 2)
$lines = @("dead files=$($dead.Count) totalMB=$deadMB") + ($dead | Sort-Object SizeMB -Descending | ForEach-Object { "{0,8:N3} MB  {1}" -f $_.SizeMB, $_.Path })
New-Item -ItemType Directory -Force -Path (Split-Path $Report -Parent) | Out-Null
Set-Content $Report ($lines -join "`n") -Encoding UTF8
$lines | Select-Object -First 1
$dead | Sort-Object SizeMB -Descending | Select-Object -First 12 | ForEach-Object { "{0,8:N3} MB  {1}" -f $_.SizeMB, $_.Path }
$dead | Group-Object { ($_.Path -split '/')[0..2] -join '/' } | Sort-Object Count -Descending | Select-Object -First 8 | ForEach-Object { "  {0,4}  {1}" -f $_.Count, $_.Name }
if ($Delete) { "delete mode not implemented in this script; read $Report and pass paths explicitly" }