<#
.SYNOPSIS
    Repairs the compiler fallout of the public-field conversion (CS0122).

.DESCRIPTION
    The Roslyn pass rewrites a public field into
        [SerializeField] private <type> <name>;
        public <type> <Prop> { get => <name>; set => <name> = value; }
    and rewrites the member accesses it can resolve. A handful of accesses cannot be
    resolved statically (generic instantiation, dynamic receivers, unresolved references),
    which surfaces as

        error CS0122: 'Type.field' is inaccessible due to its protection level

    For every such error this script either
      * makes the generated property public (the usual case: the property exists but the
        pass emitted the value without the modifier), or
      * turns the field back into a public field (when no property was generated at all).

    Both outcomes compile; the second one keeps the old API and is reported so the
    remaining non-compliant fields stay visible.
#>
[CmdletBinding()]
param(
    [string] $LogPath = "$env:LOCALAPPDATA\Unity\Editor\Editor.log",
    [int]    $TailLines = 400,
    [string] $AssetsRoot = "Assets/Game"
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

if (-not (Test-Path $LogPath)) { throw "Unity log not found: $LogPath" }

$lines = Get-Content $LogPath
$tail = $lines[[Math]::Max(0, $lines.Count - $TailLines)..($lines.Count - 1)]

$pattern = "error CS0122: '([^']+)' is inaccessible"
$hits = foreach ($line in $tail) {
    $m = [regex]::Match($line, $pattern)
    if ($m.Success) {
        $parts = $m.Groups[1].Value.Split('.')
        if ($parts.Length -ge 2) {
            [pscustomobject]@{
                Type  = $parts[$parts.Length - 2]
                Field = $parts[$parts.Length - 1]
            }
        }
    }
}

$hits = $hits | Sort-Object Type, Field -Unique
if (-not $hits) { Write-Host 'No CS0122 errors in the log tail.'; exit 0 }

$files = Get-ChildItem $AssetsRoot -Recurse -Filter *.cs
$fixedProps = 0
$revertedFields = 0
$unresolved = @()

foreach ($hit in $hits) {
    $prop = $hit.Field.Substring(0, 1).ToUpperInvariant() + $hit.Field.Substring(1)

    $decl = $null
    foreach ($file in $files) {
        $text = [System.IO.File]::ReadAllText($file.FullName)
        $declPattern = "(?m)^[ \t]*(?:\[SerializeField\][ \t]*\r?\n[ \t]*)?(?:private|public)[ \t]+(?<type>[\w\.<>, \[\]\?]+)[ \t]+" +
                       [regex]::Escape($hit.Field) + "[ \t]*(?:=[^;]*)?;"
        $m = [regex]::Match($text, $declPattern)
        if ($m.Success -and $text -match "(?m)^[ \t]*(?:public|internal|private|protected)[^{]*\bclass\s+" + [regex]::Escape($hit.Type) + "\b") {
            if ($text -match "\s" + [regex]::Escape($prop) + "\s*\{") {
                $decl = [pscustomobject]@{ File = $file; Text = $text; Match = $m }
                break
            }
        }
    }

    if (-not $decl) { $unresolved += "$($hit.Type).$($hit.Field)"; continue }

    $text = $decl.Text
    $propPattern = "(?m)^([ \t]*)((?!public)(?:internal|private|protected)?[ \t]*)([\w\.<>, \[\]\?]+[ \t]+" + [regex]::Escape($prop) + "\s*\{)"
    $propMatch = [regex]::Match($text, $propPattern)
    if ($propMatch.Success) {
        $text = $text.Remove($propMatch.Index, $propMatch.Length).Insert(
            $propMatch.Index, $propMatch.Groups[1].Value + 'public ' + $propMatch.Groups[3].Value)
        [System.IO.File]::WriteAllText($decl.File.FullName, $text, (New-Object System.Text.UTF8Encoding($false)))
        $fixedProps++
        continue
    }

    $fieldDecl = $decl.Match.Value -replace '(?m)^([ \t]*)(?:\[SerializeField\][ \t]*\r?\n[ \t]*)?(?:private|public)', '$1public'
    $text = $text.Remove($decl.Match.Index, $decl.Match.Length).Insert($decl.Match.Index, $fieldDecl)
    [System.IO.File]::WriteAllText($decl.File.FullName, $text, (New-Object System.Text.UTF8Encoding($false)))
    $revertedFields++
}

Write-Host "[fix-fields] distinct CS0122 targets: $($hits.Count)"
Write-Host "[fix-fields] properties made public: $fixedProps"
Write-Host "[fix-fields] fields reverted to public: $revertedFields"
if ($unresolved.Count -gt 0) {
    Write-Host "[fix-fields] unresolved: $($unresolved -join ', ')"
}
