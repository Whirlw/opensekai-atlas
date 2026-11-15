param([Parameter(Mandatory=$true)][string]$BeforeDirectory,[string]$AfterLabel='AFTER / BRIGHTNESS 92%',[string]$ComparisonName='notes-brightness-comparison.png')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Notes'
$before=[System.Drawing.Bitmap]::FromFile((Join-Path $BeforeDirectory 'notes.png'))
$after=[System.Drawing.Bitmap]::FromFile((Join-Path $root 'notes.png'))
$font=New-Object System.Drawing.Font('Segoe UI',18,[System.Drawing.FontStyle]::Bold)
$small=New-Object System.Drawing.Font('Segoe UI',10)
$brush=New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#e2e9ff'))
$card=New-Object System.Drawing.Bitmap(1160,720);$g=[System.Drawing.Graphics]::FromImage($card)
$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#080e1d'))
$g.DrawString('ASTRAL / NOTE STUDY',$font,$brush,32,22)
$g.DrawString('ALL NOTES / BRIGHTNESS 92% / ORIGINAL ALPHA',$small,$brush,34,61)
for($i=0;$i -lt 7;$i++){
 $dest=New-Object System.Drawing.Rectangle((32+($i%3)*376),(99+[int][Math]::Floor($i/3)*200),354,188)
 $src=New-Object System.Drawing.Rectangle(0,($i*188),354,188)
 $g.DrawImage($after,$dest,$src,[System.Drawing.GraphicsUnit]::Pixel)
}
$card.Save((Join-Path $PSScriptRoot 'notes-preview.png'),[System.Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$card.Dispose()
$card=New-Object System.Drawing.Bitmap(900,1480);$g=[System.Drawing.Graphics]::FromImage($card)
$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#10192c'))
$g.DrawString('BEFORE',$font,$brush,45,25);$g.DrawString($AfterLabel,$font,$brush,465,25)
for($i=0;$i -lt 7;$i++){
 $src=New-Object System.Drawing.Rectangle(0,($i*188),354,188)
 $g.DrawImage($before,(New-Object System.Drawing.Rectangle(35,(75+$i*200),354,188)),$src,[System.Drawing.GraphicsUnit]::Pixel)
 $g.DrawImage($after,(New-Object System.Drawing.Rectangle(485,(75+$i*200),354,188)),$src,[System.Drawing.GraphicsUnit]::Pixel)
}
$card.Save((Join-Path $PSScriptRoot $ComparisonName),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$card.Dispose();$before.Dispose();$after.Dispose();$font.Dispose();$small.Dispose();$brush.Dispose()
