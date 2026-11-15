param(
    [string]$SkinRoot='',
    [string]$Title='ASTRAL / FULL TEXTURE COLLECTION',
    [string]$Subtitle='32 new textures  /  original dimensions  /  transparent PNG  /  procedural drawing'
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'NotesV2'
if($SkinRoot){$root=(Resolve-Path -LiteralPath $SkinRoot).Path}
$target=Join-Path $root '.'
$reference=Join-Path (Split-Path $root -Parent) '..\Assets\Game\Resources\live\note\custom01\textures'
if(-not(Test-Path -LiteralPath $reference)){$reference=Join-Path (Split-Path (Split-Path $root -Parent) -Parent) 'custom01\textures'}
$files=@(Get-ChildItem $reference -Recurse -File -Filter *.png)
$checked=0
foreach($file in $files) {
    $rel=$file.FullName.Substring($reference.Length+1)
    $outPath=Join-Path $target $rel
    if(-not(Test-Path -LiteralPath $outPath)){throw "Missing: $rel"}
    $src=[System.Drawing.Bitmap]::FromFile($file.FullName)
    $dst=[System.Drawing.Bitmap]::FromFile($outPath)
    try {
        if($src.Width -ne $dst.Width -or $src.Height -ne $dst.Height){throw "Size mismatch: $rel"}
        if($dst.RawFormat.Guid -ne [System.Drawing.Imaging.ImageFormat]::Png.Guid){throw "Wrong format: $rel"}
        if($dst.PixelFormat -ne [System.Drawing.Imaging.PixelFormat]::Format32bppArgb){throw "Missing RGBA: $rel"}
        $checked++
    } finally {$src.Dispose();$dst.Dispose()}
}
$extras=@(Get-ChildItem $target -Recurse -File -Filter *.png)
if($extras.Count -ne $files.Count){throw 'Unexpected texture file count'}
Write-Output "PASS: $checked/$($files.Count) matching filenames, dimensions, PNG and RGBA."

$tiles=@($files | Where-Object {$_.Name -ne 'notes.png'} | Sort-Object @{Expression={if($_.Name -like 'notes_flick_arrow*'){0}elseif($_.Name -like 'notes_*'){1}else{2}}},Name)
$card=New-Object System.Drawing.Bitmap(1440,1812)
$g=[System.Drawing.Graphics]::FromImage($card)
$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#101728'))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$heading=New-Object System.Drawing.Font('Segoe UI',22,[System.Drawing.FontStyle]::Bold)
$label=New-Object System.Drawing.Font('Segoe UI',10)
$sub=New-Object System.Drawing.Font('Segoe UI',11)
$bright=New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#eaf0ff'))
$muted=New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#a4b3cc'))
$panel=New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#182136'))
$g.DrawString($Title,$heading,$bright,24,16)
$g.DrawString($Subtitle,$sub,$muted,26,54)
for($i=0;$i -lt $tiles.Count;$i++) {
    $x=($i%4)*360;$y=92+[Math]::Floor($i/4)*215
    $g.FillRectangle($panel,[int]($x+8),[int]($y+4),344,203)
    $im=[System.Drawing.Bitmap]::FromFile((Join-Path $target $tiles[$i].FullName.Substring($reference.Length+1)))
    $scale=[Math]::Min([double]1,[Math]::Min([double]310/$im.Width,[double]157/$im.Height))
    $w=[int]($im.Width*$scale);$h=[int]($im.Height*$scale)
    $g.DrawImage($im,[int]($x+180-$w/2),[int]($y+84-$h/2),$w,$h)
    $g.DrawString($tiles[$i].BaseName,$label,$bright,[single]($x+18),[single]($y+168))
    $g.DrawString("$($im.Width) x $($im.Height)",$label,$muted,[single]($x+18),[single]($y+185))
    $im.Dispose()
}
$card.Save((Join-Path $PSScriptRoot 'textures-preview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$card.Dispose();$heading.Dispose();$label.Dispose();$sub.Dispose();$bright.Dispose();$muted.Dispose();$panel.Dispose()
Write-Output 'Created textures-preview.png'
