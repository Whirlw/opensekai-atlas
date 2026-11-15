$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Notes'
$card=New-Object System.Drawing.Bitmap(1100,410)
$g=[System.Drawing.Graphics]::FromImage($card)
$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#10192c'))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$heading=New-Object System.Drawing.Font('Segoe UI',18,[System.Drawing.FontStyle]::Bold)
$label=New-Object System.Drawing.Font('Segoe UI',10)
$brush=New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#dbe7ff'))
$g.DrawString('COMPOUND STELLAR CRYSTALS / 3D FACETS',$heading,$brush,24,20)
$names=@('notes_long_among.png','notes_long_among_crtcl.png','notes_friction_among_long.png','notes_friction_among_flick.png','notes_friction_among_crtcl.png')
$labels=@('LONG / MINT','LONG / GOLD','TRACE / MINT','TRACE / PINK','TRACE / GOLD')
for($i=0;$i -lt $names.Count;$i++){
 $im=[System.Drawing.Bitmap]::FromFile((Join-Path $root (''+$names[$i])))
 $dw=[int]($im.Width*1.7);$dh=[int]($im.Height*1.7);$cx=110+$i*220
 $g.DrawImage($im,[int]($cx-$dw/2),[int](220-$dh/2),$dw,$dh)
 $g.DrawString($labels[$i],$label,$brush,[single]($cx-50),[single]365)
 $im.Dispose()
}
$card.Save((Join-Path $PSScriptRoot 'nodes-preview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$card.Dispose();$heading.Dispose();$label.Dispose();$brush.Dispose()
