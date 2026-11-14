param([string]$BackupName='before_node_tinted_shadows_20261108')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$backup=Join-Path (Split-Path $root -Parent) ('revision_backups\'+$BackupName)
$names=@('notes_long_among.png','notes_long_among_crtcl.png','notes_friction_among_long.png','notes_friction_among_flick.png','notes_friction_among_crtcl.png')
$labels=@('LONG / MINT','LONG / GOLD','TRACE / MINT','TRACE / PINK','TRACE / GOLD')
$card=New-Object System.Drawing.Bitmap(1100,880);$g=[System.Drawing.Graphics]::FromImage($card)
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font=New-Object System.Drawing.Font('Segoe UI',14,[System.Drawing.FontStyle]::Bold)
$small=New-Object System.Drawing.Font('Segoe UI',10)
$headings=@('BEFORE / DARK BACKGROUND','AFTER / TINTED SHADOWS','BEFORE / LIGHT BACKGROUND','AFTER / TINTED SHADOWS')
for($row=0;$row -lt 4;$row++){
 $y=$row*220;$light=$row -ge 2
 $background=[System.Drawing.ColorTranslator]::FromHtml($(if($light){'#dce4ee'}else{'#10192c'}))
 $ink=[System.Drawing.ColorTranslator]::FromHtml($(if($light){'#26354d'}else{'#dbe7ff'}))
 $b=New-Object System.Drawing.SolidBrush($background);$g.FillRectangle($b,0,$y,1100,220);$b.Dispose()
 $text=New-Object System.Drawing.SolidBrush($ink);$g.DrawString($headings[$row],$font,$text,18,[single]($y+10))
 for($i=0;$i -lt 5;$i++){
  $path=if($row%2 -eq 0){Join-Path $backup ('noteskin\'+$names[$i])}else{Join-Path $root ('textures\noteskin\'+$names[$i])}
  $image=[System.Drawing.Bitmap]::FromFile($path)
  $w=[int]($image.Width*1.1);$h=[int]($image.Height*1.1);$cx=110+$i*220
  $g.DrawImage($image,[int]($cx-$w/2),[int]($y+123-$h/2),$w,$h)
  $g.DrawString($labels[$i],$small,$text,[single]($cx-51),[single]($y+201));$image.Dispose()
 }
 $text.Dispose()
}
$card.Save((Join-Path $root 'nodes-shadow-comparison.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$card.Dispose();$font.Dispose();$small.Dispose()
