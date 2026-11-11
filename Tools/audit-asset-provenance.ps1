# Prints the asset-provenance table from the working tree, so the numbers in
# docs/operations/asset-provenance.md can be refreshed instead of remembered.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/audit-asset-provenance.ps1
#
# Output is a markdown table: | group | files | MB | pngs | status |
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-Location $Root

# group name -> path (relative), plus the provenance verdict this project has agreed on.
$groups = @(
    @{ Name = '音符皮肤 custom01';   Path = 'Assets/Game/Resources/live/note/custom01';  Judgement = '自制（画包）' }
    @{ Name = '音符皮肤 custom02';   Path = 'Assets/Game/Resources/live/note/custom02';  Judgement = '自制（画包）' }
    @{ Name = 'live 打击音';        Path = 'Assets/Game/AssetBundles/resources/startapp/live/sound'; Judgement = '自制 CC0 wav' }
    @{ Name = 'tap 音效';           Path = 'Assets/Game/AssetBundles/resources/startapp/live/tap_se'; Judgement = '自制 CC0 wav' }
    @{ Name = 'live 背景 2dmode';    Path = 'Assets/Game/AssetBundles/resources/startapp/live/2dmode'; Judgement = '自制（画包）' }
    @{ Name = '加载背景 longLoading'; Path = 'Assets/Graphics/Resources/loading';        Judgement = '已删除' }
    @{ Name = 'HUD / UI 大图';        Path = 'Assets/Graphics/Resources/screen';         Judgement = '自制' }
    @{ Name = '通用背景 commonbg';    Path = 'Assets/Graphics/Resources/commonbg';       Judgement = '已删除' }
    @{ Name = 'live UI 预制';         Path = 'Assets/Graphics/Resources/live';           Judgement = '自制（画包）' }
    @{ Name = 'HUD sprite';           Path = 'Assets/Graphics/Sprites';                  Judgement = '自制（画包 + 程序化替身）' }
    @{ Name = '字体';                 Path = 'Assets/Game/Resources/Fonts';              Judgement = 'OFL' }
    @{ Name = '音频 wav 包';          Path = 'Assets/StreamingAssets/sound';             Judgement = '自制 CC0' }
    @{ Name = '代码';                 Path = 'Assets/Game/Runtime';                      Judgement = 'MIT' }
)

'| 资源组 | 路径 | 文件 | 体积 | 来源 / 许可 |'
'| --- | --- | --- | --- | --- |'
foreach ($g in $groups) {
    if (-not (Test-Path $g.Path)) { continue }
    $files = Get-ChildItem $g.Path -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -ne '.meta' }
    $count = ($files | Measure-Object).Count
    $mb = if ($count -gt 0) { [math]::Round((($files | Measure-Object Length -Sum).Sum / 1MB), 2) } else { 0 }
    '| {0} | `{1}` | {2} | {3} MB | {4} |' -f $g.Name, $g.Path, $count, $mb, $g.Judgement
}

''
'参考：代码 ' + (Get-ChildItem 'Assets/Game/Runtime' -Recurse -File -Include *.cs | Measure-Object).Count + ' 个 .cs'
