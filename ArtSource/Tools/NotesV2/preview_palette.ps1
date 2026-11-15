$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'NotesV2'
$kit=Split-Path $root -Parent
$oldRoot=Join-Path $kit 'Notes'
$newRoot=Join-Path $root '.'
$font=New-Object System.Drawing.Font('Segoe UI',19,[System.Drawing.FontStyle]::Bold)
$small=New-Object System.Drawing.Font('Segoe UI',10)
$ink=New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#e7eeff'))
$panel=New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#192439'))
function Write-PaletteComparison([string[]]$files,[string]$title,[string]$output,[int]$height){
 $rows=[int][Math]::Ceiling($files.Count/3.0)
 $card=New-Object System.Drawing.Bitmap(1440,(90+$rows*$height));$g=[System.Drawing.Graphics]::FromImage($card)
 $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#0d1628'));$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $g.DrawString($title,$font,$ink,22,14);$g.DrawString('Each pair: current new_notes / new_notes_v2. Identical canvas, scale and alpha.',$small,$ink,24,52)
 for($i=0;$i -lt $files.Count;$i++){
  $x=($i%3)*480;$y=90+[int][Math]::Floor($i/3)*$height;$name=Split-Path $files[$i] -Leaf
  $label=$name.Replace('notes_flick_arrow_','ARROW ').Replace('notes_friction_among_','TRACE ').Replace('notes_long_among','LONG').Replace('.png','')
  $g.DrawString($label,$small,$ink,[single]($x+14),[single]$y)
  foreach($version in 0..1){
   $left=$x+12+$version*236;$viewH=$height-55
   $g.FillRectangle($panel,$left,($y+24),224,$viewH)
   $folder=if($version -eq 0){$oldRoot}else{$newRoot};$im=[System.Drawing.Bitmap]::FromFile((Join-Path $folder $files[$i]))
   $scale=[Math]::Min(1.0,[Math]::Min(212.0/$im.Width,($viewH-10.0)/$im.Height));$w=[int]($im.Width*$scale);$h=[int]($im.Height*$scale)
   $g.DrawImage($im,[int]($left+112-$w/2),[int]($y+24+($viewH-$h)/2),$w,$h);$im.Dispose()
   $g.DrawString($(if($version -eq 0){'CURRENT'}else{'V2'}),$small,$ink,[single]($left+7),[single]($y+$height-24))
  }
 }
 $card.Save((Join-Path $PSScriptRoot $output),[System.Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$card.Dispose()
}
$arrows=@(Get-ChildItem -LiteralPath $oldRoot -File -Filter 'notes_flick_arrow_*.png' | Sort-Object Name | ForEach-Object {$_.Name})
Write-PaletteComparison $arrows 'ARROWS / CURRENT AND V2' 'arrows-palette-comparison.png' 225
$nodes=@('notes_long_among.png','notes_long_among_crtcl.png','notes_friction_among_long.png','notes_friction_among_flick.png','notes_friction_among_crtcl.png')
Write-PaletteComparison $nodes 'CONNECTION NODES / CURRENT AND V2' 'nodes-palette-comparison.png' 240
Write-PaletteComparison @('longNoteLine.png','traceLine_eff.png','simultaneousLine.png') 'CONNECTION STRIPS / CURRENT AND V2' 'lines-palette-comparison.png' 300

$card=New-Object System.Drawing.Bitmap(1480,1120);$g=[System.Drawing.Graphics]::FromImage($card)
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$bg=[System.Drawing.Bitmap]::FromFile((Join-Path $kit 'Backgrounds\default.png'))
$g.DrawImage($bg,(New-Object System.Drawing.Rectangle(0,0,1480,1120)),(New-Object System.Drawing.Rectangle(0,230,2048,1550)),[System.Drawing.GraphicsUnit]::Pixel);$bg.Dispose()
foreach($item in @(@('bg_scoreGauge.png',20,22,280),@('bg_lifeGauge.png',1050,22,280),@('btn_stop.png',1366,17,85))){
 $im=[System.Drawing.Bitmap]::FromFile((Join-Path $kit ('HUD\'+$item[0])));$w=[int]$item[3];$h=[int]($im.Height*$w/$im.Width)
 $g.DrawImage($im,[int]$item[1],[int]$item[2],$w,$h);$im.Dispose()
}
$g.DrawString('ASTRAL V2 / NEW NOTE PALETTE',$font,$ink,350,50)
$g.DrawString('Current HUD and background / original geometry and alpha.',$small,$ink,352,89)
$notes=[System.Drawing.Bitmap]::FromFile((Join-Path $newRoot 'notes.png'))
$labels=@('CRITICAL / PLATINUM','HOLD / COPPER','TAP / INDIGO','FLICK / RUBY')
foreach($i in 0..3){
 $x=10+$i*370;$g.DrawString($labels[$i],$small,$ink,[single]($x+54),[single]220)
 $g.DrawImage($notes,(New-Object System.Drawing.Rectangle($x,245,354,188)),(New-Object System.Drawing.Rectangle(0,($i*188),354,188)),[System.Drawing.GraphicsUnit]::Pixel)
}
foreach($pair in @(@(0,5),@(1,4),@(3,6))){
 $x=10+$pair[0]*370;$row=$pair[1]
 $g.DrawImage($notes,(New-Object System.Drawing.Rectangle($x,420,354,188)),(New-Object System.Drawing.Rectangle(0,($row*188),354,188)),[System.Drawing.GraphicsUnit]::Pixel)
}
$notes.Dispose()
$g.DrawString('FACETED NODES / TOP-DOWN WATERLINE',$small,$ink,36,625)
for($i=0;$i -lt $nodes.Count;$i++){
 $im=[System.Drawing.Bitmap]::FromFile((Join-Path $newRoot $nodes[$i]));$cx=148+$i*296
 $g.DrawImage($im,[int]($cx-$im.Width/2),[int](744-$im.Height/2),$im.Width,$im.Height);$im.Dispose()
}
$g.DrawString('ARROWS / WHITE CONSTELLATION TRAILS',$small,$ink,36,858)
$examples=@('notes_flick_arrow_crtcl_03.png','notes_flick_arrow_crtcl_03_diagonal.png','notes_flick_arrow_03.png','notes_flick_arrow_03_diagonal.png')
for($i=0;$i -lt $examples.Count;$i++){
 $im=[System.Drawing.Bitmap]::FromFile((Join-Path $newRoot $examples[$i]));$scale=[Math]::Min(320.0/$im.Width,200.0/$im.Height);$w=[int]($im.Width*$scale);$h=[int]($im.Height*$scale)
 $g.DrawImage($im,[int](185+$i*370-$w/2),[int](990-$h/2),$w,$h);$im.Dispose()
}
$card.Save((Join-Path $PSScriptRoot 'palette-overview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$card.Dispose();$font.Dispose();$small.Dispose();$ink.Dispose();$panel.Dispose()
Write-Output 'Created three category comparison sheets and palette-overview.png with the current UI.'
