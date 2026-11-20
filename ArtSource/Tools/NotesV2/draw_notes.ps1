param([switch]$DamageOnly)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class AstralNotes {
    static Graphics g;
    static Color C(string s) { return ColorTranslator.FromHtml(s); }
    static Color A(int a, Color c) { return Color.FromArgb(a,c); }
    static void Line(Color c, float width, params PointF[] pts) {
        using(var p = new Pen(c,width)) { p.LineJoin=LineJoin.Round; g.DrawLines(p,pts); }
    }
    static PointF P(float x,float y) { return new PointF(x,y); }
    static Color Mix(Color a,Color b,float t) {
        return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));
    }
    static GraphicsPath Rounded(float x,float y,float w,float h,float r) {
        var p=new GraphicsPath();
        p.AddArc(x,y,r*2,r*2,180,90); p.AddArc(x+w-r*2,y,r*2,r*2,270,90);
        p.AddArc(x+w-r*2,y+h-r*2,r*2,r*2,0,90); p.AddArc(x,y+h-r*2,r*2,r*2,90,90);
        p.CloseFigure(); return p;
    }
    static GraphicsPath Bevel(float x,float y,float w,float h,float b) {
        var p=new GraphicsPath();
        p.AddPolygon(new [] {P(x+b,y),P(x+w-b,y),P(x+w,y+b),P(x+w,y+h-b),P(x+w-b,y+h),P(x+b,y+h),P(x,y+h-b),P(x,y+b)});
        return p;
    }
    static void Star(float x,float y,float r,Color c,bool cross) {
        using(var b=new SolidBrush(A(25,c))) g.FillEllipse(b,x-r*2,y-r*2,r*4,r*4);
        if(cross) {
            using(var b=new SolidBrush(c)) g.FillPolygon(b,new[]{P(x,y-r),P(x+r*.25f,y-r*.24f),P(x+r*.8f,y),P(x+r*.25f,y+r*.24f),P(x,y+r),P(x-r*.25f,y+r*.24f),P(x-r*.8f,y),P(x-r*.25f,y-r*.24f)});
        } else using(var b=new SolidBrush(c)) g.FillEllipse(b,x-r/2,y-r/2,r,r);
    }
    static void StretchMotif(float y,float h,Color ink,bool slim) {
        // A slender lens keeps the centre slightly thicker and tapers smoothly to both tips.
        float middle=y+h*.51f;
        using(var shape=new GraphicsPath()) {
            shape.AddBezier(P(91,middle),P(148,middle-1.2f),P(206,middle-1.2f),P(263,middle));
            shape.AddBezier(P(263,middle),P(206,middle+1.2f),P(148,middle+1.2f),P(91,middle));
            shape.CloseFigure();
            using(var brush=new SolidBrush(A(slim?110:65,ink))) g.FillPath(brush,shape);
        }
    }
    static void DrawRow(int row,Color accent,bool slim) {
        var state=g.Save(); g.TranslateTransform(0,row*188);
        // Seven fixed 188px atlas cells. The silhouette is fitted to the judge line as measured in game
        // (DEINOS PHAINEIN, 2800x1272, notes at their hit time): the judge slab covers cell rows 50.2..133.0,
        // so the face starts at row 50 and its 5px extrusion ends at row 133. The body ends sit 55px in
        // from both cell edges (inside the 100px 9-slice borders), 2px wider than before, which matches the
        // lane width on average; slim bodies keep their height and share the judge line centre.
        float y=slim?72:50, h=slim?40:78;
        const float L=55, R=354-L;
        Color ink=Mix(accent,C("#704875"),.48f);
        Color light=Mix(accent,Color.White,.83f);
        bool blueNote=row==2;
        Color blueTop=C("#ae81ff"), blueBottom=C("#65caff");
        // Gold keeps its approved halo; the other five notes receive a softer color-matched halo.
        if(row==0 || row==5) {
            using(var halo=Bevel(L,y,R-L,h,11)) {
                for(int width=48;width>=2;width-=2) {
                    float distance=width/2f;
                    int alpha=(int)((slim?18:24)*Math.Exp(-distance*distance/(2*10f*10f)));
                    using(var p=new Pen(A(alpha,C("#ffc41a")),width)) {
                        p.LineJoin=LineJoin.Round; g.DrawPath(p,halo);
                    }
                }
            }
        } else {
            using(var halo=Bevel(L,y,R-L,h,11)) {
                for(int width=44;width>=2;width-=2) {
                    float distance=width/2f;
                    int alpha=(int)((slim?8:11)*Math.Exp(-distance*distance/(2*9f*9f)));
                    if(alpha==0)continue;
                    using(var brush=new LinearGradientBrush(P(0,y),P(0,y+h),A(alpha,blueNote?blueTop:accent),A(alpha,blueNote?blueBottom:accent)))
                    using(var p=new Pen(brush,width)) {
                        p.LineJoin=LineJoin.Round;g.DrawPath(p,halo);
                    }
                }
            }
        }
        // A narrow extrusion below the face provides thickness without moving the face.
        float depth=slim?4:5;
        Color sideTop=Mix(accent,C("#30405f"),.23f);
        Color sideBottom=Mix(accent,C("#30405f"),.46f);
        if(blueNote) {
            sideTop=Mix(blueBottom,C("#30405f"),.23f);
            sideBottom=Mix(blueBottom,C("#30405f"),.46f);
        }
        using(var b=new LinearGradientBrush(P(0,y+h-11),P(0,y+h+depth),sideTop,sideBottom)) {
            g.FillPolygon(b,new[]{P(L,y+h-11),P(L+11,y+h),P(R-11,y+h),P(R,y+h-11),P(R,y+h-11+depth),P(R-11,y+h+depth),P(L+11,y+h+depth),P(L,y+h-11+depth)});
        }
        Line(A(200,sideBottom),.8f,P(L+11,y+h+depth),P(R-11,y+h+depth));
        // Bloom is drawn explicitly, without image generation or external assets.
        using(var body=Bevel(L,y,R-L,h,11)) {
            for(int i=8;i>=2;i-=2) using(var p=new Pen(A(slim?4:6,accent),i*2)) g.DrawPath(p,body);
            using(var b=new LinearGradientBrush(P(0,y),P(0,y+h),slim?Mix(accent,Color.White,.92f):blueNote?blueTop:Mix(accent,Color.White,.62f),slim?Mix(accent,Color.White,.70f):blueNote?blueBottom:accent)) g.FillPath(b,body);
        }
        using(var inset=Bevel(L+7,y+5,R-L-14,h-11,8)) {
            Color top=slim?Mix(accent,Color.White,.20f):blueNote?C("#ebdcff"):Mix(accent,Color.White,.94f);
            Color bottom=slim?Mix(accent,Color.White,.12f):blueNote?C("#d2f0ff"):Mix(accent,Color.White,.90f);
            Color middle=slim?Mix(accent,C("#30405f"),.055f):Mix(Mix(top,bottom,.5f),accent,.10f);
            using(var b=new LinearGradientBrush(P(0,y+5),P(0,y+h-6),top,bottom)) {
                var blend=new ColorBlend();blend.Positions=new[]{0f,.50f,1f};blend.Colors=new[]{top,middle,bottom};
                b.InterpolationColors=blend;g.FillPath(b,inset);
            }
            using(var p=new Pen(A(210,Color.White),1)) g.DrawPath(p,inset);
            var clip=g.Save(); g.SetClip(inset);
            StretchMotif(y,h,slim?Color.White:ink,slim);
            g.Restore(clip);
        }
        // Strong top and bottom rails preserve the wide, easily judged note silhouette.
        Line(A(240,Color.White),1.2f,P(L+13,y+1.6f),P(R-13,y+1.6f));
        Line(Mix(blueNote?blueBottom:accent,Color.White,.5f),slim?2:3,P(L+13,y+h-4),P(R-13,y+h-4));
        // Faceted side caps sit at the body ends.
        foreach(int side in new[]{0,1}) {
            var st=g.Save(); if(side==1) {g.TranslateTransform(354,0);g.ScaleTransform(-1,1);}
            using(var b=new LinearGradientBrush(P(0,y),P(0,y+h),slim?Mix(accent,Color.White,.86f):blueNote?blueTop:accent,slim?Mix(accent,Color.White,.68f):blueNote?blueBottom:accent)) g.FillPolygon(b,new[]{P(L+1,y+12),P(L+8,y+5),P(L+17,y+5),P(L+17,y+h-6),P(L+8,y+h-6),P(L+1,y+h-13)});
            Line(A(210,Color.White),1,P(L+4,y+15),P(L+4,y+h-15));
            g.Restore(st);
        }
        g.Restore(state);
    }
    public static void Render(string filename,string preview,bool damageOnly) {
        const int scale=4;
        using(var hi=new Bitmap(354*scale,1316*scale,PixelFormat.Format32bppArgb)) {
            g=Graphics.FromImage(hi); g.Clear(Color.Transparent);
            g.SmoothingMode=SmoothingMode.AntiAlias; g.ScaleTransform(scale,scale);
            Color[] colors={C("#ffcc46"),C("#2bf2c5"),C("#848fff"),C("#ff73b3"),C("#2bf2c5"),C("#ffcc46"),C("#ff73b3")};
            if(damageOnly) DrawRow(4,C("#b263ff"),true);
            else for(int i=0;i<7;i++) DrawRow(i,colors[i],i>=4);
            g.Dispose();
            using(var output=new Bitmap(354,1316,PixelFormat.Format32bppArgb)) {
                using(var down=Graphics.FromImage(output)) { down.CompositingMode=CompositingMode.SourceCopy; down.InterpolationMode=InterpolationMode.HighQualityBicubic; down.PixelOffsetMode=PixelOffsetMode.HighQuality; down.DrawImage(hi,new Rectangle(0,0,354,1316),0,0,hi.Width,hi.Height,GraphicsUnit.Pixel); }
                // Slightly dim only the lower three atlas cells; preserve hue and alpha.
                for(int y=188*4;y<output.Height;y++)for(int x=0;x<output.Width;x++) {
                    Color pixel=output.GetPixel(x,y);if(pixel.A==0)continue;
                    output.SetPixel(x,y,Color.FromArgb(pixel.A,(int)Math.Round(pixel.R*.92),(int)Math.Round(pixel.G*.92),(int)Math.Round(pixel.B*.92)));
                }
                // Bake in the subsequently approved 92% brightness for the entire note atlas.
                for(int y=0;y<output.Height;y++)for(int x=0;x<output.Width;x++) {
                    Color pixel=output.GetPixel(x,y);if(pixel.A==0)continue;
                    output.SetPixel(x,y,Color.FromArgb(pixel.A,(int)Math.Round(pixel.R*.92),(int)Math.Round(pixel.G*.92),(int)Math.Round(pixel.B*.92)));
                }
                output.Save(filename,ImageFormat.Png);
                if(damageOnly) return;
                // A separate viewing card never enters the game's texture folder.
                using(var card=new Bitmap(1160,720,PixelFormat.Format32bppArgb)) using(var cg=Graphics.FromImage(card)) {
                    cg.Clear(C("#080e1d")); cg.SmoothingMode=SmoothingMode.AntiAlias;
                    using(var f=new Font("Segoe UI",19,FontStyle.Bold)) using(var b=new SolidBrush(C("#e2e9ff"))) cg.DrawString("ASTRAL / NOTE STUDY 03",f,b,32,22);
                    using(var f=new Font("Segoe UI",10)) using(var b=new SolidBrush(C("#8a9dbb"))) cg.DrawString("PROCEDURAL ART    /    354 x 1316    /    RGBA PNG",f,b,34,61);
                    for(int i=0;i<7;i++) { int col=i%3, row=i/3; cg.DrawImage(output,new Rectangle(32+col*376,99+row*200,354,188),new Rectangle(0,i*188,354,188),GraphicsUnit.Pixel); }
                    card.Save(preview,ImageFormat.Png);
                }
            }
        }
    }
}
'@
Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
$root = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'NotesV2'
if($DamageOnly) {
    [AstralNotes]::Render((Join-Path $root 'notes_damage.png'), $null, $true)
    Write-Output 'Created notes_damage.png (354 x 1316, RGBA PNG)'
} else {
    [AstralNotes]::Render((Join-Path $root 'notes.png'), (Join-Path $PSScriptRoot 'notes-preview.png'), $false)
    Write-Output 'Created notes.png (354 x 1316, RGBA PNG) and notes-preview.png'
}
