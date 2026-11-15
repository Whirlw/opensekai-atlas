param([Parameter(Mandatory=$true)][string]$SourceDirectory,[double]$Brightness=0.92)
$ErrorActionPreference='Stop'
if($Brightness -le 0 -or $Brightness -gt 1){throw 'Brightness must be between zero and one'}
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;using System.Drawing;using System.Drawing.Imaging;using System.Runtime.InteropServices;
public static class NoteBrightness {
 public static void Apply(string source,string target,double gain){
  using(var image=new Bitmap(source)){
   int w=image.Width,h=image.Height;
   var data=image.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
   var pixels=new int[w*h];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
   for(int i=0;i<pixels.Length;i++){
    uint p=(uint)pixels[i];if((p>>24)==0)continue;
    int r=(int)Math.Round(((p>>16)&255)*gain),g=(int)Math.Round(((p>>8)&255)*gain),b=(int)Math.Round((p&255)*gain);
    pixels[i]=(int)(p&0xff000000)|(r<<16)|(g<<8)|b;
   }
   Marshal.Copy(pixels,0,data.Scan0,pixels.Length);image.UnlockBits(data);
   image.Save(target,ImageFormat.Png);
  }
  using(var before=new Bitmap(source))using(var after=new Bitmap(target)){
   if(before.Size!=after.Size||after.PixelFormat!=PixelFormat.Format32bppArgb)throw new Exception("Format mismatch: "+target);
   for(int y=0;y<before.Height;y++)for(int x=0;x<before.Width;x++){
    Color a=before.GetPixel(x,y),b=after.GetPixel(x,y);
    if(a.A!=b.A)throw new Exception("Alpha mismatch: "+target);
    if(a.A!=0&&(b.R!=(int)Math.Round(a.R*gain)||b.G!=(int)Math.Round(a.G*gain)||b.B!=(int)Math.Round(a.B*gain)))throw new Exception("Brightness mismatch: "+target);
   }
  }
 }
}
'@
$root=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Notes'
$sourceRoot=(Resolve-Path -LiteralPath $SourceDirectory).Path.TrimEnd('\')
$targetRoot=(Resolve-Path -LiteralPath $root).Path.TrimEnd('\')
if($sourceRoot -eq $targetRoot){throw 'Use a separate source copy to avoid accumulating adjustments'}
$files=@(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter *.png)
if (($files | Group-Object Name | Where-Object Count -gt 1).Count) { throw 'Duplicate source filenames' }
foreach($file in $files){
 $relative=$file.Name
 $target=Join-Path $targetRoot $relative
 [NoteBrightness]::Apply($file.FullName,$target,$Brightness)
}
Write-Output "PASS: $($files.Count) textures at $($Brightness*100)% RGB brightness; dimensions, PNG format and every alpha value preserved."
