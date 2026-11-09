# Regenerates Assets/OpenSekai/Resources/Fonts/NotoSansSC-UI.ttf
#
# The bundled UI font is a Noto Sans SC subset (OFL, see OFL.txt next to the font).
# Run this after adding UI text whose characters are not in the current subset.
# Requires uv (https://docs.astral.sh/uv/). Source font: any Noto Sans SC variable TTF,
# by default the one shipped with Windows.
#
#   pwsh -File Tools/subset-ui-font.ps1
#   pwsh -File Tools/subset-ui-font.ps1 -Source "D:\fonts\NotoSansSC-VF.ttf"

param(
    [string]$Source = "C:\Windows\Fonts\NotoSansSC-VF.ttf",
    [string]$Charset = "",   # optional extra characters, e.g. "设置结算暂停"
    [int]$Weight = 400
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$work = Join-Path $env:TEMP 'noto-subset'
$outDir = Join-Path $repo 'Assets\OpenSekai\Resources\Fonts'
$outFont = Join-Path $outDir 'NotoSansSC-UI.ttf'

if (-not (Test-Path $Source)) { throw "Source font not found: $Source" }
if (-not (Get-Command uvx -ErrorAction SilentlyContinue)) { throw "uvx not found. Install uv first." }

New-Item -ItemType Directory -Force -Path $work, $outDir | Out-Null

$static = Join-Path $work ("NotoSansSC-Regular-{0}.ttf" -f $Weight)
Write-Host "1/3 instancing $Source (wght=$Weight) -> $static"
uvx --from fonttools fonttools varLib.instancer -o $static $Source "wght=$Weight"

# GB2312: symbols (A1A1-A9FE) + level-1 hanzi (B0A1-D7F9) + printable ASCII.
$enc = [System.Text.Encoding]::GetEncoding(936)
$sb = New-Object System.Text.StringBuilder
for ($hi = 0xA1; $hi -le 0xD7; $hi++) {
    for ($lo = 0xA1; $lo -le 0xFE; $lo++) {
        $s = $enc.GetString([byte[]]@($hi, $lo))
        if ($s.Length -eq 1 -and $s[0] -ne [char]0xFFFD) { [void]$sb.Append($s) }
    }
}
for ($c = 0x20; $c -le 0x7E; $c++) { [void]$sb.Append([char]$c) }
[void]$sb.Append("—…·×÷≥≤→←↑↓℃￥")
if ($Charset) { [void]$sb.Append($Charset) }

$charsetFile = Join-Path $work 'charset.txt'
[System.IO.File]::WriteAllText($charsetFile, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("2/3 charset: {0} characters" -f $sb.Length)

Write-Host "3/3 subsetting -> $outFont"
uvx --from fonttools fonttools subset $static --text-file=$charsetFile --output-file=$outFont `
    --layout-features=* --name-IDs=* --name-languages=* --notdef-glyph --notdef-outline --recommended-glyphs

$size = (Get-Item $outFont).Length
Write-Host ("Done: {0:N0} KB" -f ($size / 1KB))
