$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'NotesV2'
$im=[System.Drawing.Bitmap]::FromFile((Join-Path $root 'notes.png'))
$card=New-Object System.Drawing.Bitmap(1160,1250)
$g=[System.Drawing.Graphics]::FromImage($card)
$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#10192c'))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$title=New-Object System.Drawing.Font('Segoe UI',20,[System.Drawing.FontStyle]::Bold)
$font=New-Object System.Drawing.Font('Segoe UI',11)
$brush=New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#dce7ff'))
$g.DrawString('CENTRE LINE / HORIZONTAL STRETCH',$title,$brush,22,20)
$g.DrawString('Fixed height / 0.55x, 1x, 2x, 3x width',$font,$brush,24,60)
$index=0
foreach($row in @(2,4)){
 foreach($factor in @(.55,1,2,3)){
  $y=100+$index*142
  $label=if($row -eq 2){'BLUE / '+$factor+'x'}else{'SLIM MINT / '+$factor+'x'}
  $g.DrawString($label,$font,$brush,[single]20,[single]($y+52))
  $dst=New-Object System.Drawing.RectangleF([single]250,[single]$y,[single](288*$factor),[single]128)
  $src=New-Object System.Drawing.RectangleF([single]33,[single]($row*188+32),[single]288,[single]128)
  $g.DrawImage($im,$dst,$src,[System.Drawing.GraphicsUnit]::Pixel)
  $index++
 }
}
$card.Save((Join-Path $PSScriptRoot 'notes-stretch-preview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$card.Dispose();$im.Dispose();$title.Dispose();$font.Dispose();$brush.Dispose()
