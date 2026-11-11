[CmdletBinding()]
param(
    [switch]$RequireNoEmptyDirectories,
    [switch]$CheckProjects
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$issues = [System.Collections.Generic.List[string]]::new()
$anchors = @{}

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

Write-Host "Checked $($markdown.Count) Markdown files and found $($empty.Count) empty runtime directories."
if ($issues.Count -gt 0) {
    $issues | ForEach-Object { Write-Host $_ }
    exit 1
}
