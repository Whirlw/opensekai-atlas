[CmdletBinding()]
param(
    [switch]$RequireNoEmptyDirectories,
    [switch]$CheckProjects,
    [switch]$CheckAssemblies,
    [switch]$UpdateGeneratedAssets
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$issues = [System.Collections.Generic.List[string]]::new()
$anchors = @{}

# ---- generated assets -------------------------------------------------------------------------
# Unity and TextMeshPro rewrite a handful of tracked files while the game runs (font feature tables,
# glyph caches). Those files must stay in their cold state; Tools/generated-assets.txt lists them and
# is the only place that knows which files they are.
$generatedManifest = Join-Path $root 'Tools/generated-assets.txt'

function Read-GeneratedEntries([string]$Manifest) {
    $entries = [System.Collections.Generic.List[object]]::new()
    foreach ($line in Get-Content -LiteralPath $Manifest -Encoding UTF8) {
        if ($line -match '^\s*#' -or $line -notmatch '\S') { continue }
        if ($line -notmatch '^(?<hash>[0-9a-f]{64})\s+(?<size>\d+)\s+(?<path>.+?)\s*$') {
            $entries.Add([pscustomobject]@{ Bad = $line })
            continue
        }
        $entries.Add([pscustomobject]@{
            Hash = $Matches['hash']
            Size = [long]$Matches['size']
            Path = $Matches['path']
        })
    }
    return ,$entries
}

if ($UpdateGeneratedAssets) {
    if (!(Test-Path -LiteralPath $generatedManifest)) {
        Write-Host "Missing generated asset manifest: Tools/generated-assets.txt"
        exit 1
    }
    $written = [System.Collections.Generic.List[string]]::new()
    $changed = 0
    foreach ($line in Get-Content -LiteralPath $generatedManifest -Encoding UTF8) {
        if ($line -match '^\s*#' -or $line -notmatch '\S') { $written.Add($line); continue }
        if ($line -notmatch '^(?<hash>[0-9a-f]{64})\s+(?<size>\d+)\s+(?<path>.+?)\s*$') {
            Write-Host "Unreadable generated asset entry: $line"
            exit 1
        }
        $relative = $Matches['path']
        $target = Join-Path $root $relative
        if (!(Test-Path -LiteralPath $target -PathType Leaf)) {
            Write-Host "Generated asset listed but missing: $relative"
            exit 1
        }
        $entry = '{0}  {1}  {2}' -f (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant(), (Get-Item -LiteralPath $target).Length, $relative
        if ($entry -ne $line) { $changed++ }
        $written.Add($entry)
    }
    Set-Content -LiteralPath $generatedManifest -Value $written -Encoding UTF8
    Write-Host "Updated Tools/generated-assets.txt ($changed entries changed). Commit it with the assets."
    exit 0
}


function Get-HeadingAnchors([string]$Path) {
    if (!$anchors.ContainsKey($Path)) {
        $ids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
            if ($line -match '^ {0,3}#{1,6}\s+(.+?)\s*#*\s*$') {
                $heading = $Matches[1].ToLowerInvariant() -replace '[^\p{L}\p{M}\p{Nd}_\-\s]', '' -replace '\s', '-'
                $id = $heading
                $suffix = 0
                while (!$ids.Add($id)) {
                    $suffix++
                    $id = "$heading-$suffix"
                }
            }
        }
        $anchors[$Path] = $ids
    }
    return ,$anchors[$Path]
}

$markdown = @(Get-Item -LiteralPath (Join-Path $root README.md))
$markdown += Get-ChildItem -LiteralPath (Join-Path $root docs), (Join-Path $root Tools/fixtures) -File -Recurse -Filter '*.md'
foreach ($file in $markdown) {
    $relative = $file.FullName.Substring($root.Length + 1)
    $lineNumber = 0
    $fence = $null
    foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding UTF8) {
        $lineNumber++
        if ($line -match '^\s{0,3}(`{3,}|~{3,})') {
            $marker = $Matches[1]
            if (!$fence) { $fence = $marker }
            elseif ($marker[0] -eq $fence[0] -and $marker.Length -ge $fence.Length) { $fence = $null }
            continue
        }
        if ($fence) { continue }
        $text = $line -replace '`[^`]+`', ''
        $links = [regex]::Matches($text, '\]\((?<path><[^>]+>|[^\s)]+)(?:\s+"[^"]*")?\)|^\s{0,3}\[[^\]]+\]:\s*(?<path><[^>]+>|\S+)')
        foreach ($link in $links) {
            $destination = $link.Groups['path'].Value.Trim('<', '>')
            if ($destination -match '^(?:[a-z][a-z0-9+.-]*:|//)') { continue }
            $parts = $destination.Split('#', 2)
            $path = [uri]::UnescapeDataString($parts[0])
            if (!$path) { $target = $file.FullName }
            elseif ($path.StartsWith('/')) { $target = Join-Path $root $path.TrimStart('/') }
            else { $target = Join-Path $file.DirectoryName $path }
            $target = [IO.Path]::GetFullPath($target)
            if (!(Test-Path -LiteralPath $target)) {
                $issues.Add("${relative}:${lineNumber}: missing link target $destination")
            }
            elseif ($parts.Count -eq 2 -and $parts[1] -and $target.EndsWith('.md', [StringComparison]::OrdinalIgnoreCase)) {
                $id = [uri]::UnescapeDataString($parts[1])
                if (!(Get-HeadingAnchors $target).Contains($id)) {
                    $issues.Add("${relative}:${lineNumber}: missing heading $destination")
                }
            }
        }
    }
}

$runtime = Join-Path $root Assets/Game/Runtime
$empty = @(Get-ChildItem -LiteralPath $runtime -Directory -Recurse | Where-Object {
    !(Get-ChildItem -LiteralPath $_.FullName -Force | Select-Object -First 1)
})
foreach ($directory in $empty) {
    $message = 'Empty runtime directory: ' + $directory.FullName.Substring($root.Length + 1)
    if ($RequireNoEmptyDirectories) { $issues.Add($message) } else { Write-Host $message }
}

if ($CheckProjects) {
    foreach ($project in Get-ChildItem -LiteralPath $root -File -Filter '*.csproj') {
        $xml = [xml](Get-Content -LiteralPath $project.FullName -Raw)
        foreach ($item in $xml.SelectNodes("//*[local-name()='Compile' and @Include]")) {
            $source = $item.GetAttribute('Include')
            if ($source -match '[*?\$%]') { continue }
            if (!(Test-Path -LiteralPath (Join-Path $root $source) -PathType Leaf)) {
                $issues.Add("$($project.Name): missing Compile source $source")
            }
        }
    }
}

if ($CheckAssemblies) {
    $definitions = @{}
    $namesByGuid = @{}
    $definitionRoots = @('Assets', 'Packages', 'Library/PackageCache') |
        ForEach-Object { Join-Path $root $_ } | Where-Object { Test-Path -LiteralPath $_ }
    foreach ($file in Get-ChildItem -LiteralPath $definitionRoots -Recurse -File -Filter '*.asmdef') {
        $definition = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $definitions[$definition.name] = $definition
        $metaPath = $file.FullName + '.meta'
        if (Test-Path -LiteralPath $metaPath) {
            $guid = Select-String -LiteralPath $metaPath -Pattern '^guid: ([a-f0-9]{32})$'
            if ($guid) { $namesByGuid[$guid.Matches[0].Groups[1].Value] = $definition.name }
        }
    }

    foreach ($name in @('OpenSekai.Core', 'OpenSekai.Game', 'OpenSekai.App')) {
        if (!$definitions.ContainsKey($name)) { $issues.Add("Missing assembly definition: $name") }
    }

    $core = $definitions['OpenSekai.Core']
    if ($core -and (!$core.noEngineReferences -or !$core.overrideReferences)) {
        $issues.Add('OpenSekai.Core must opt out of implicit Unity engine and plugin references.')
    }
    foreach ($reference in $core.precompiledReferences) {
        if ($reference -match '^Unity') {
            $issues.Add("OpenSekai.Core references a Unity library: $reference")
        }
    }

    $coreTests = $definitions['OpenSekai.Tests.Core']
    if ($coreTests -and @($coreTests.references).Count -ne 1) {
        $issues.Add('OpenSekai.Tests.Core must reference exactly one project assembly.')
    }

    foreach ($definition in $definitions.Values) {
        $name = $definition.name
        if ($name -like 'OpenSekai.Tests.*' -and $definition.autoReferenced -ne $false) {
            $issues.Add("$name must set autoReferenced to false.")
        }

        if ($name -notin @('OpenSekai.Core', 'OpenSekai.Game', 'OpenSekai.App', 'OpenSekai.Tests.Core')) {
            continue
        }
        foreach ($reference in $definition.references) {
            $target = $reference
            if ($reference -match '^GUID:(?<id>[a-f0-9]{32})$') {
                $target = $namesByGuid[$Matches['id']]
                if (!$target) {
                    $issues.Add("$name has an unresolved assembly reference: $reference")
                    continue
                }
            }

            $invalid = switch ($name) {
                'OpenSekai.Core' { $true }
                'OpenSekai.Game' { $target -in @('OpenSekai.App', 'OpenSekai.Editor') -or $target -like 'OpenSekai.Tests.*' }
                'OpenSekai.App' { $target -eq 'OpenSekai.Editor' -or $target -like 'OpenSekai.Tests.*' }
                'OpenSekai.Tests.Core' { $target -ne 'OpenSekai.Core' }
            }
            if ($invalid) { $issues.Add("$name has a forbidden assembly reference: $target") }
        }
    }
    Write-Host 'Checked OpenSekai assembly boundaries.'
}

if (!(Test-Path -LiteralPath $generatedManifest)) {
    $issues.Add('Missing generated asset manifest: Tools/generated-assets.txt')
} else {
    $generatedChecked = 0
    $generatedDrifted = 0
    foreach ($entry in (Read-GeneratedEntries $generatedManifest)) {
        if ($entry.Bad) {
            $issues.Add("Tools/generated-assets.txt: unreadable entry '$($entry.Bad)'")
            $generatedDrifted++
            continue
        }
        $generatedChecked++
        $target = Join-Path $root $entry.Path
        if (!(Test-Path -LiteralPath $target -PathType Leaf)) {
            $issues.Add("Generated asset is missing: $($entry.Path)")
            $generatedDrifted++
            continue
        }
        $size = (Get-Item -LiteralPath $target).Length
        if ($size -ne $entry.Size) {
            $issues.Add("Generated asset drifted: $($entry.Path) ($size bytes, expected $($entry.Size))")
            $generatedDrifted++
            continue
        }
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Hash) {
            $issues.Add("Generated asset drifted: $($entry.Path) (same size, different content)")
            $generatedDrifted++
        }
    }
    Write-Host "Checked $generatedChecked generated assets against Tools/generated-assets.txt."
    if ($generatedDrifted -gt 0) {
        $issues.Add('Unity/TextMeshPro rewrote these generated files while the game ran; their baked cache is not source.')
        $issues.Add('  accidental drift : powershell -NoProfile -ExecutionPolicy Bypass -File Tools/restore-generated-assets.ps1')
        $issues.Add('  intentional bake : powershell -NoProfile -ExecutionPolicy Bypass -File Tools/verify-structure.ps1 -UpdateGeneratedAssets')
    }
}

# ---- facade size and direct resource access ---------------------------------------------------
# P4-4: the two splits that this plan cares most about must not quietly grow back. Limits are
# non-empty lines, the same unit the plan uses everywhere (see the plan's 统计口径 note). They are
# ratchets: if a limit is genuinely too tight, raising it belongs in the same commit as the code that
# needs it, so the raise is visible in review.
$lineLimits = @(
    @{ Path = 'Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenManager.cs'; Limit = 600 },
    @{ Path = 'Assets/Game/Runtime/OpenSekai/App/UI/LibraryScreen.cs'; Limit = 100 },
    @{ Path = 'Assets/Game/Runtime/OpenSekai.Game/Live/Notes/LongNote.cs'; Limit = 500 }
)
foreach ($entry in $lineLimits) {
    $target = Join-Path $root $entry.Path
    if (!(Test-Path -LiteralPath $target -PathType Leaf)) {
        $issues.Add("Line-limit target is missing: $($entry.Path)")
        continue
    }
    $nonEmpty = @(Get-Content -LiteralPath $target -Encoding UTF8 | Where-Object { $_.Trim() -ne '' }).Count
    if ($nonEmpty -gt $entry.Limit) {
        $issues.Add("$($entry.Path) has $nonEmpty non-empty lines (limit $($entry.Limit)); split it instead of growing the facade.")
    }
}

# Every file below reaches Resources directly for a reason; anything else must go through
# ResourceCatalog (fixed assets) or ResourceLoader (dynamic keys) so the path stays in one place.
$resourcesAllowlist = @{
    'Assets/Game/Runtime/OpenSekai.Game/Assets/ResourceCatalog.cs' = 'the typed catalog'
    'Assets/Game/Runtime/OpenSekai.Game/Assets/ResourceLoader.cs' = 'dynamic loader'
    'Assets/Game/Runtime/OpenSekai.Game/Assets/AssetBundleUtility.cs' = 'bundle-then-Resources fallback'
    'Assets/Game/Runtime/OpenSekai.Game/Assets/AssetManager.cs' = 'asset manager'
    'Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenLayerData.cs' = 'page prefab named by data'
    'Assets/Game/Runtime/OpenSekai.Game/Screens/DialogService.cs' = 'dialog prefab named by the caller'
    'Assets/Game/Runtime/OpenSekai.Game/Screens/ScreenBlurController.cs' = 'serialized blur prefab path'
    'Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveViewFactory.cs' = 'view prefab chosen by config'
    'Assets/Game/Runtime/OpenSekai.Game/Live/View/LiveResultView.cs' = 'result prefab name'
    'Assets/Game/Runtime/OpenSekai.Game/Data/WordingManager.cs' = 'wording table path'
    'Assets/Game/Runtime/OpenSekai.Game/UI/UIAtlasImageLoader.cs' = 'atlas and sprite names'
    'Assets/Game/Runtime/OpenSekai.Game/Rendering/CameraUtility.cs' = 'camera prefab by path'
}
$resourcesOffenders = [System.Collections.Generic.List[string]]::new()
$runtimeRoot = Join-Path $root 'Assets/Game'
foreach ($file in Get-ChildItem -LiteralPath $runtimeRoot -Recurse -File -Filter '*.cs') {
    $relative = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
    if ($resourcesAllowlist.ContainsKey($relative)) { continue }
    if (Select-String -LiteralPath $file.FullName -Pattern 'Resources\.Load(All|Async)?(<|\()' -Quiet) {
        $resourcesOffenders.Add($relative)
    }
}
if ($resourcesOffenders.Count -gt 0) {
    $issues.Add('Direct Resources.Load in a file that is not on the allowlist; use ResourceCatalog or ResourceLoader,')
    $issues.Add('or add the file to $resourcesAllowlist in Tools/verify-structure.ps1 with the reason it must stay dynamic:')
    $resourcesOffenders | ForEach-Object { $issues.Add("  $_") }
}
Write-Host ("Checked {0} facade line limits and {1} allowlisted direct Resources.Load files." -f $lineLimits.Count, $resourcesAllowlist.Count)

Write-Host "Checked $($markdown.Count) Markdown files and found $($empty.Count) empty runtime directories."
if ($issues.Count -gt 0) {
    $issues | ForEach-Object { Write-Host $_ }
    exit 1
}
