using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class PaletteVariant {
    enum Family { Platinum, Copper, Indigo, Ruby, Neutral }
    struct HSV {
        public double H,S,V;
        public HSV(Color c) {
            H=c.GetHue();double max=Math.Max(c.R,Math.Max(c.G,c.B)),min=Math.Min(c.R,Math.Min(c.G,c.B));
            S=max==0?0:(max-min)/max;V=max/255.0;
        }
    }
    static HSV OldCritical=new HSV(ColorTranslator.FromHtml("#ffcc46")),Platinum=new HSV(ColorTranslator.FromHtml("#f2efe7"));
    static HSV OldHold=new HSV(ColorTranslator.FromHtml("#2bf2c5")),Copper=new HSV(ColorTranslator.FromHtml("#ff702e"));
    static HSV OldFlick=new HSV(ColorTranslator.FromHtml("#ff73b3")),Ruby=new HSV(ColorTranslator.FromHtml("#f04753"));
    static HSV OldIceTop=new HSV(ColorTranslator.FromHtml("#ae81ff")),OldIceBottom=new HSV(ColorTranslator.FromHtml("#65caff"));
    static HSV IndigoTop=new HSV(ColorTranslator.FromHtml("#7464ff")),IndigoBottom=new HSV(ColorTranslator.FromHtml("#435beb"));
    static double Clamp(double v,double low,double high){return Math.Max(low,Math.Min(high,v));}
    static double HueDifference(double a,double b){return (a-b+540)%360-180;}
    static Color FromHSV(int alpha,double h,double s,double v){
        h=(h%360+360)%360;s=Clamp(s,0,1);v=Clamp(v,0,1);
        double c=v*s,x=c*(1-Math.Abs(h/60%2-1)),m=v-c,r=0,g=0,b=0;
        if(h<60){r=c;g=x;}else if(h<120){r=x;g=c;}else if(h<180){g=c;b=x;}
        else if(h<240){g=x;b=c;}else if(h<300){r=x;b=c;}else{r=c;b=x;}
        return Color.FromArgb(alpha,(int)Math.Round((r+m)*255),(int)Math.Round((g+m)*255),(int)Math.Round((b+m)*255));
    }
    static Family Category(string name,int y){
        if(name=="notes.png")return new[]{Family.Platinum,Family.Copper,Family.Indigo,Family.Ruby,Family.Copper,Family.Platinum,Family.Ruby}[y/188];
        if(name=="longNoteLine.png")return y<8?Family.Copper:Family.Platinum;
        if(name=="traceLine_eff.png")return y<112?Family.Copper:Family.Platinum;
        if(name=="simultaneousLine.png")return Family.Neutral;
        if(name.Contains("crtcl"))return Family.Platinum;
        if(name.Contains("flick"))return Family.Ruby;
        if(name=="notes_long_among.png"||name=="notes_friction_among_long.png")return Family.Copper;
        throw new Exception("Unassigned note family: "+name);
    }
    static Color Recolor(Color pixel,Family family){
        if(pixel.A==0)return pixel;
        var hsv=new HSV(pixel);
        // Keep white stars, white arrow trails and neutral highlights neutral.
        if(hsv.S<.012)return pixel;
        if(family==Family.Neutral)return FromHSV(pixel.A,214,hsv.S*.35,hsv.V);
        double hue,sat,value;
        if(family==Family.Indigo){
            double t=Clamp(HueDifference(hsv.H,OldIceBottom.H)/HueDifference(OldIceTop.H,OldIceBottom.H),0,1);
            hue=IndigoBottom.H+(IndigoTop.H-IndigoBottom.H)*t;
            double oldSat=OldIceBottom.S+(OldIceTop.S-OldIceBottom.S)*t;
            double targetSat=IndigoBottom.S+(IndigoTop.S-IndigoBottom.S)*t;
            sat=Math.Pow(hsv.S,.66)*targetSat/oldSat;
            value=hsv.V*(IndigoBottom.V+(IndigoTop.V-IndigoBottom.V)*t);
        }else{
            HSV old=family==Family.Platinum?OldCritical:family==Family.Copper?OldHold:OldFlick;
            HSV target=family==Family.Platinum?Platinum:family==Family.Copper?Copper:Ruby;
            // Limit shading hue drift to its assigned family; never rotate a whole atlas across roles.
            hue=target.H+Clamp(HueDifference(hsv.H,old.H),-12,12)*.45;
            sat=Math.Pow(hsv.S,.66)*target.S/old.S;value=hsv.V*target.V/old.V;
            if(family==Family.Platinum)sat=Math.Min(.07,sat);
        }
        Color output=FromHSV(pixel.A,hue,sat,value);
        // The chromatic portions of the four families have disjoint hue bands.
        if(new HSV(output).S>.18){
            double actual=output.GetHue();
            bool allowed=family==Family.Copper?actual>=10&&actual<=27:
                family==Family.Indigo?actual>=225&&actual<=250:
                family==Family.Ruby?actual>=345||actual<=5:false;
            if(!allowed)throw new Exception("Palette family overlap: "+family+" hue="+actual);
        }
        return output;
    }
    public static void Build(string sourceRoot,string targetRoot){
        string[] files=Directory.GetFiles(sourceRoot,"*.png",SearchOption.AllDirectories);
        if(files.Length!=33)throw new Exception("Expected 33 source textures");
        int checkedPixels=0,changedPixels=0;
        foreach(string source in files){
            string relative=source.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            string name=Path.GetFileName(source),target=Path.Combine(targetRoot,relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using(var bitmap=new Bitmap(source)){
                int w=bitmap.Width,h=bitmap.Height;
                var data=bitmap.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
                var rgba=new int[w*h];Marshal.Copy(data.Scan0,rgba,0,rgba.Length);
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                    int index=y*w+x;Color before=Color.FromArgb(rgba[index]),after=Recolor(before,Category(name,y));
                    if(after.A!=before.A)throw new Exception("Alpha changed");
                    if(after.ToArgb()!=before.ToArgb())changedPixels++;
                    rgba[index]=after.ToArgb();checkedPixels++;
                }
                Marshal.Copy(rgba,0,data.Scan0,rgba.Length);bitmap.UnlockBits(data);bitmap.Save(target,ImageFormat.Png);
            }
            using(var before=new Bitmap(source))using(var after=new Bitmap(target)){
                if(before.Size!=after.Size||after.PixelFormat!=PixelFormat.Format32bppArgb||after.RawFormat.Guid!=ImageFormat.Png.Guid)throw new Exception("Image format changed: "+relative);
                for(int y=0;y<before.Height;y++)for(int x=0;x<before.Width;x++)if(before.GetPixel(x,y).A!=after.GetPixel(x,y).A)throw new Exception("Saved alpha differs: "+relative);
            }
            Console.WriteLine(relative);
        }
        if(Directory.GetFiles(targetRoot,"*.png",SearchOption.AllDirectories).Length!=33)throw new Exception("Unexpected output texture count");
        Console.WriteLine("PASS: 33 RGBA PNGs; "+checkedPixels+" alpha samples identical; "+changedPixels+" RGB pixels recolored; color families remain separate.");
    }
}
