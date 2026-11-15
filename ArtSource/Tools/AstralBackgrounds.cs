using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class AstralBackgrounds {
    static string Root,Target;
    static readonly System.Collections.Generic.Dictionary<string, Size> Sizes = new System.Collections.Generic.Dictionary<string, Size> {
        { "bg_sekai.png", new Size(2048, 1170) },
        { "default.png", new Size(2048, 2048) },
        { "eff_bgcommon_circle_triangle.png", new Size(224, 224) },
        { "ui_scorerank_plate.png", new Size(500, 684) },
    };

    static Color Mint=C("#2bf2c5"),Blue=C("#65caff"),Purple=C("#ae81ff"),Gold=C("#ffcc46"),Pink=C("#ff73b3");
    static Color C(string s){return ColorTranslator.FromHtml(s);}
    static Color A(int a,Color c){return Color.FromArgb(Math.Max(0,Math.Min(255,a)),c);}
    static PointF P(float x,float y){return new PointF(x,y);}
    static Color Mix(Color a,Color b,float t){t=Math.Max(0,Math.Min(1,t));return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
    static int Clamp(double value){return Math.Max(0,Math.Min(255,(int)Math.Round(value)));}
    static GraphicsPath Poly(params PointF[] p){var path=new GraphicsPath();path.AddPolygon(p);return path;}
    static GraphicsPath Bevel(float x,float y,float w,float h,float cut){return Poly(P(x+cut,y),P(x+w-cut,y),P(x+w,y+cut),P(x+w,y+h-cut),P(x+w-cut,y+h),P(x+cut,y+h),P(x,y+h-cut),P(x,y+cut));}
    // The plate's chamfer, the octagon cut the HUD uses everywhere (AstralHud.Bevel), sized to its 0.22 on-screen scale.
    const float PlateCut=44;
    static void Line(Graphics g,Color c,float width,params PointF[] p){using(var pen=new Pen(c,width)){pen.LineJoin=LineJoin.Bevel;g.DrawLines(pen,p);}}
    static void Star(Graphics g,float x,float y,float r,Color c){using(var p=Poly(P(x,y-r),P(x+r*.19f,y-r*.19f),P(x+r*.7f,y),P(x+r*.19f,y+r*.19f),P(x,y+r),P(x-r*.19f,y+r*.19f),P(x-r*.7f,y),P(x-r*.19f,y-r*.19f)))using(var b=new SolidBrush(c))g.FillPath(b,p);}
    static double Hash(int x,int y){unchecked{uint n=(uint)(x*374761393+y*668265263);n=(n^(n>>13))*1274126177;return (n^(n>>16))/(double)uint.MaxValue;}}
    static double Noise(double x,double y){int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);double fx=x-ix,fy=y-iy;fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);return (Hash(ix,iy)*(1-fx)+Hash(ix+1,iy)*fx)*(1-fy)+(Hash(ix,iy+1)*(1-fx)+Hash(ix+1,iy+1)*fx)*fy;}
    static double Cloud(double x,double y){return Noise(x,y)*.58+Noise(x*2.17+17,y*2.17+9)*.29+Noise(x*4.3+41,y*4.3+12)*.13;}
    static double Blob(double x,double y,double cx,double cy,double sx,double sy){double dx=(x-cx)/sx,dy=(y-cy)/sy;return Math.Exp(-dx*dx-dy*dy);}
    static Bitmap Field(int w,int h,int mode){
        var bmp=new Bitmap(w,h,PixelFormat.Format32bppArgb);var pixels=new int[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            double u=x/(double)(w-1),v=y/(double)(h-1),n=Cloud(u*4.4,v*4.1),// Triangular dither spanning +-1 code value: the dark field only has a few dozen 8-bit levels per
            // channel, and the old +-0.4 noise rounded away, leaving flat bands up to ~80 px wide.
            dither=Hash(x,y)+Hash(x+4099,y+7919)-1;
            double r,g,b;
            if(mode==0){
                double left=Blob(u,v,.12,.22,.32,.28),right=Blob(u,v,.9,.2,.26,.32),below=Blob(u,v,.53,.84,.7,.20),mist=Math.Pow(n,2.2);
                r=8+left*36*mist+below*65*mist;
                g=16+right*49*mist+below*48*mist;
                b=38+left*65*mist+right*40*mist+below*155*mist;
                // Selected cloud filaments sit above the broad, quieter far-field mist.
                double ridge=Math.Pow(Math.Max(0,1-Math.Abs(Noise(u*15+61,v*11.5+27)-.52)*3),3);
                r+=ridge*mist*(left*18+below*20);
                g+=ridge*mist*(right*24+below*12);
                b+=ridge*mist*(left*46+right*32+below*44);
                double quiet=1-.22*Blob(u,v,.5,.39,.25,.27);
                double edge=Math.Pow(Math.Sin(Math.PI*u)*Math.Sin(Math.PI*v),.22)*.90+.10;
                r*=quiet*edge;g*=quiet*edge;b*=quiet*edge;
                double luminance=.2126*r+.7152*g+.0722*b;
                r=(luminance+(r-luminance)*1.15)*1.18+1.5;
                g=(luminance+(g-luminance)*1.15)*1.18+1.5;
                b=(luminance+(b-luminance)*1.15)*1.18+1.5;
            }else{
                double centre=Blob(u,v,.48,.57,.55,.56),violet=Blob(u,v,.88,.1,.42,.5),aqua=Blob(u,v,.1,.25,.4,.5),warm=Blob(u,v,.15,.93,.35,.24);
                r=211+centre*35-violet*5+warm*12+(n-.5)*6;g=221+centre*27-violet*13+aqua*5+(n-.5)*5;b=245+centre*10-aqua*3-warm*7;
            }
            pixels[y*w+x]=Color.FromArgb(255,Clamp(r+dither),Clamp(g+dither),Clamp(b+dither)).ToArgb();
        }
        var data=bmp.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);Marshal.Copy(pixels,0,data.Scan0,pixels.Length);bmp.UnlockBits(data);return bmp;
    }
    static void StarField(Graphics g,int w,int h,bool dark,int seed,float strength=1){
        var rng=new Random(seed);int count=dark?640:200;
        for(int i=0;i<count;i++){
            float x=(float)rng.NextDouble()*w,y=(float)rng.NextDouble()*h;
            if(dark&&x>w*.29f&&x<w*.71f&&y>h*.13f&&y<h*.62f&&i%8!=0)continue;
            if(!dark&&x>w*.25f&&x<w*.75f&&y>h*.18f&&y<h*.8f)continue;
            float radius=i%41==0?2.4f:i%7==0?1.1f:.55f;
            Color hue=i%9==0?Gold:i%5==0?Mint:i%3==0?Purple:Blue;
            Color color=dark?A(45+rng.Next(105),Mix(hue,Color.White,.76f)):A((int)((35+rng.Next(35))*strength),Mix(C("#617ba9"),hue,.25f));
            radius*=strength>1?1.35f:1;
            if(i%41==0){
                using(var b=new SolidBrush(A(dark?12:8,hue)))g.FillEllipse(b,x-8,y-8,16,16);
                Star(g,x,y,4.4f,color);
            }
            else using(var b=new SolidBrush(color))g.FillEllipse(b,x-radius,y-radius,radius*2,radius*2);
        }
    }
    static void Constellation(Graphics g,float cx,float cy,float size,int type,Color color,float weight=1){
        // Stylised Orion, Cassiopeia, and Cygnus, used as restrained corner ornament.
        PointF[] p;int[][] links;
        if(type==0){p=new[]{P(-.45f,-.8f),P(.34f,-.7f),P(-.24f,-.08f),P(0,0),P(.22f,.07f),P(-.44f,.75f),P(.49f,.68f)};links=new[]{new[]{0,2,5},new[]{1,4,6},new[]{2,3,4}};}
        else if(type==1){p=new[]{P(-.9f,-.25f),P(-.45f,.23f),P(-.02f,-.36f),P(.43f,.15f),P(.88f,-.28f)};links=new[]{new[]{0,1,2,3,4}};}
        else{p=new[]{P(0,-.85f),P(0,-.2f),P(-.7f,.08f),P(.65f,.08f),P(.08f,.78f)};links=new[]{new[]{0,1,4},new[]{2,1,3}};}
        for(int i=0;i<p.Length;i++)p[i]=P(cx+p[i].X*size,cy+p[i].Y*size);
        foreach(var indices in links){var line=new PointF[indices.Length];for(int i=0;i<indices.Length;i++)line[i]=p[indices[i]];Line(g,A(weight>1?(int)(color.A*.70f):color.A/2,color),.85f*weight,line);}
        foreach(var point in p){using(var b=new SolidBrush(A(color.A/9,color)))g.FillEllipse(b,point.X-6,point.Y-6,12,12);Star(g,point.X,point.Y,3.7f*(weight>1?1.5f:1),color);}
    }
    static void Orbit(Graphics g,float cx,float cy,float rx,float ry,float angle,Color color,bool ticks,float weight=1){
        var st=g.Save();g.TranslateTransform(cx,cy);g.RotateTransform(angle);
        using(var p=new Pen(color,weight))g.DrawEllipse(p,-rx,-ry,rx*2,ry*2);
        using(var p=new Pen(A(color.A/3,color),.7f*weight))g.DrawEllipse(p,-rx+7,-ry+7,rx*2-14,ry*2-14);
        if(ticks)for(int i=0;i<48;i++){
            double a=i*Math.PI/24;float k=i%4==0?1.045f:1.022f;
            Line(g,A(Math.Min(255,color.A+10),color),.8f*weight,P((float)Math.Cos(a)*rx,(float)Math.Sin(a)*ry),P((float)Math.Cos(a)*rx*k,(float)Math.Sin(a)*ry*k));
        }g.Restore(st);
    }
    static void Moon(Graphics g,float cx,float cy,float r){
        using(var p=new GraphicsPath()){p.AddEllipse(cx-r,cy-r,r*2,r*2);using(var b=new PathGradientBrush(p)){b.CenterPoint=P(cx-r*.42f,cy-r*.32f);b.CenterColor=A(100,C("#87a5bd"));b.SurroundColors=new[]{A(2,C("#23324a"))};g.FillPath(b,p);}}
        using(var pen=new Pen(A(85,C("#bfdce8")),1.3f))g.DrawArc(pen,cx-r,cy-r,r*2,r*2,183,138);
        using(var pen=new Pen(A(28,Blue),.8f))g.DrawEllipse(pen,cx-r-5,cy-r-5,(r+5)*2,(r+5)*2);
    }
    static void Sky(Graphics g,int w,int h,int mode){
        bool dark=mode==0;StarField(g,w,h,dark,mode+117,mode==1?1.65f:1);
        Color ink=dark?A(65,Blue):A(60,C("#6e88b6"));
        if(dark){
            Orbit(g,w*.15f,h*.30f,w*.23f,h*.11f,-28,A(27,Purple),true);
            Orbit(g,w*.87f,h*.34f,w*.24f,h*.13f,32,A(23,Blue),true);
            Orbit(g,w*.5f,h*.67f,w*.66f,h*.13f,0,A(32,Blue),true);
            Orbit(g,w*.5f,h*.67f,w*.44f,h*.075f,0,A(22,Purple),false);
            Moon(g,w*.83f,h*.19f,w*.067f);
            Constellation(g,w*.15f,h*.34f,w*.073f,0,A(115,C("#c8d5e9")));
            Constellation(g,w*.80f,h*.38f,w*.078f,1,A(100,C("#b3cbe6")));
            Constellation(g,w*.18f,h*.78f,w*.067f,2,A(58,Blue));
            // Perspective observatory floor beneath the horizon; keep the central note lane quiet.
            for(int i=0;i<13;i++){float y=h*(.67f+i*i*.0019f);if(y<h)Line(g,A(7+i,Blue),.8f,P(0,y),P(w,y));}
            for(int i=-8;i<=8;i++)Line(g,A(16,Purple),.8f,P(w*.5f+i*w*.022f,h*.66f),P(w*.5f+i*w*.17f,h));
            Line(g,A(30,Mint),1.1f,P(w*.08f,h*.643f),P(w*.35f,h*.643f));
            Line(g,A(25,Purple),1.1f,P(w*.65f,h*.643f),P(w*.92f,h*.643f));
        }else if(mode==1){
            // Thicker, higher-contrast etching remains visible at typical UI downscales.
            Orbit(g,w*.05f,h*.12f,w*.30f,h*.31f,-23,A(135,C("#617cac")),true,2.4f);
            Orbit(g,w*.91f,h*.69f,w*.29f,h*.41f,29,A(120,C("#7c76a4")),true,2.4f);
            Orbit(g,w*.56f,h*1.15f,w*.58f,h*.39f,-5,A(115,C("#5d859d")),true,2.2f);
            Constellation(g,w*.15f,h*.34f,w*.065f,0,A(185,C("#5d759f")),2.2f);
            Constellation(g,w*.83f,h*.25f,w*.071f,1,A(175,C("#7c6c9c")),2.2f);
            Constellation(g,w*.85f,h*.76f,w*.063f,2,A(170,C("#517e91")),2.2f);
        }else{
            Orbit(g,w*.05f,h*.12f,w*.30f,h*.31f,-23,A(38,C("#6b88b9")),true);
            Orbit(g,w*.91f,h*.69f,w*.29f,h*.41f,29,A(32,C("#8593bc")),true);
            Orbit(g,w*.56f,h*1.15f,w*.58f,h*.39f,-5,A(29,C("#648cae")),true);
            Constellation(g,w*.15f,h*.34f,w*.065f,0,A(85,C("#748db4")));
            Constellation(g,w*.83f,h*.25f,w*.071f,1,A(78,C("#8983af")));
            Constellation(g,w*.85f,h*.76f,w*.063f,2,A(70,C("#668d9f")));
        }
    }
    // Half saturation (HSV) at the same brightness, as AstralHud.Soft does for every HUD rim.
    static Color Soft(Color c){int m=Math.Max(c.R,Math.Max(c.G,c.B));return Color.FromArgb((c.R+m)/2,(c.G+m)/2,(c.B+m)/2);}
    static void RimPen(Graphics g,GraphicsPath p,float width,int alpha,Color[] colors,int w,int h){
        using(var b=new LinearGradientBrush(new RectangleF(-4,-4,w+8,h+8),Color.Black,Color.Black,0f)){
            b.InterpolationColors=new ColorBlend{Positions=new[]{0f,.5f,1f},Colors=new[]{A(alpha,colors[0]),A(alpha,colors[1]),A(alpha,colors[2])}};
            using(var pen=new Pen(b,width))g.DrawPath(pen,p);
        }
    }
    static Bitmap Plate(int w,int h){
        var image=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(image)){
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var silhouette=Bevel(1,1,w-2,h-2,PlateCut))g.SetClip(silhouette);
            // Compact in-game rank tile in the HUD's dark glass (AstralHud gauge capsules): the large dynamic
            // letter and the RANK word own the centre.
            using(var b=new LinearGradientBrush(P(0,0),P(0,h),C("#1d2d4d"),C("#0a1226")))g.FillRectangle(b,0,0,w,h);
            using(var b=new SolidBrush(A(20,Color.White)))g.FillRectangle(b,0,0,w,h*.37f);
            using(var b=new LinearGradientBrush(P(0,h*.78f),P(0,h),A(0,C("#071622")),A(80,C("#071622"))))g.FillRectangle(b,0,h*.78f,w,h*.22f);
            g.ResetClip();
            // The score capsule (AstralHud bg_scoreGauge) runs under the plate's right edge, the way the life
            // capsule runs under the pause disc. The rim is a horizontal gradient that ends in the capsule rim's
            // first colour (rose) where the two meet, and the inner ring repeats it, as on the HUD buttons.
            Color[] rim={Soft(Mix(Blue,Color.White,.3f)),Soft(Mix(Purple,Color.White,.3f)),Soft(Mix(Pink,Color.White,.3f))};
            using(var p=Bevel(5,5,w-10,h-10,PlateCut-4*.41421f))RimPen(g,p,8f,240,rim,w,h);
            using(var p=Bevel(17,17,w-34,h-34,PlateCut-16*.41421f))RimPen(g,p,3.5f,150,rim,w,h);
            // Gems on both sides at the capsule's centre line (plate row 323 = world y -0.523), as on the pause disc.
            // They are the pause disc's gem size on screen, centred on the inner ring so they stay inside the texture.
            foreach(float x in new[]{17f,w-17f})using(var p=Poly(P(x,323-32),P(x+15,323),P(x,323+32),P(x-15,323))){
                using(var pen=new Pen(A(170,C("#101a32")),6f)){pen.LineJoin=LineJoin.Miter;g.DrawPath(pen,p);}
                using(var br=new SolidBrush(Color.White))g.FillPath(br,p);
            }
        }return image;
    }
    static Bitmap ParticleAtlas(){
        const int scale=4;var hi=new Bitmap(224*scale,224*scale,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(hi)){
            g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.ScaleTransform(scale,scale);
            for(int cell=0;cell<2;cell++){
                var st=g.Save();g.TranslateTransform(cell*112,0);g.SetClip(new RectangleF(0,0,112,112),CombineMode.Intersect);
                Color color=cell==0?Color.White:Pink;
                using(var pen=new Pen(color,1.8f))g.DrawEllipse(pen,6,6,100,100);
                using(var pen=new Pen(A(140,color),.7f))for(int i=0;i<6;i++)g.DrawArc(pen,10,10,92,92,i*60+8,32);
                for(int i=0;i<12;i++){double a=i*Math.PI/6;Line(g,A(190,color),.8f,P(56+(float)Math.Cos(a)*48,56+(float)Math.Sin(a)*48),P(56+(float)Math.Cos(a)*52,56+(float)Math.Sin(a)*52));}
                g.Restore(st);
            }
            string skin=Path.Combine(Root,"Notes");
            string[] names={"notes_friction_among_flick.png","notes_friction_among_long.png"};
            for(int cell=0;cell<2;cell++)using(var crystal=new Bitmap(Path.Combine(skin,names[cell]))){
                var st=g.Save();g.SetClip(new RectangleF(cell*112,112,112,112),CombineMode.Intersect);
                // These are the current code-rendered compound tetrahedra; retain their facet shading.
                g.DrawImage(crystal,new RectangleF(cell*112+2,114,108,108),new RectangleF(10,11,84,90),GraphicsUnit.Pixel);
                g.Restore(st);
            }
        }
        var output=new Bitmap(224,224,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(output)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(hi,new Rectangle(0,0,224,224),0,0,hi.Width,hi.Height,GraphicsUnit.Pixel);}hi.Dispose();return output;
    }
    static void DrawDefaultTexture(Bitmap output){
        int w=output.Width,h=output.Height;
        using(var texture=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            using(var g=Graphics.FromImage(texture)){g.SmoothingMode=SmoothingMode.AntiAlias;Sky(g,w,h,0);}
            // One modest gain for the complete texture drawing, with no singled-out rings or stars.
            var pixels=new int[w*h];var bits=texture.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            Marshal.Copy(bits.Scan0,pixels,0,pixels.Length);
            for(int i=0;i<pixels.Length;i++){
                int alpha=Clamp(((uint)pixels[i]>>24)*1.18);
                pixels[i]=(pixels[i]&0x00ffffff)|(alpha<<24);
            }
            Marshal.Copy(pixels,0,bits.Scan0,pixels.Length);texture.UnlockBits(bits);
            using(var g=Graphics.FromImage(output))g.DrawImageUnscaled(texture,0,0);
        }
    }
    public static void Run(string root){Run(root,null);}
    public static void Run(string root,string[] only){
        Root=root;Target=Path.Combine(root,"Backgrounds");Directory.CreateDirectory(Target);
        foreach(string name in Sizes.Keys){
            if(only!=null&&only.Length>0&&Array.IndexOf(only,name)<0)continue;
            int w=Sizes[name].Width,h=Sizes[name].Height;
            Bitmap output;
            if(name=="eff_bgcommon_circle_triangle.png")output=ParticleAtlas();
            else if(name=="ui_scorerank_plate.png")output=Plate(w,h);
            else{
                int mode=name=="default.png"?0:1;
                output=Field(w,h,mode);
                if(mode==0)DrawDefaultTexture(output);
                else using(var g=Graphics.FromImage(output)){g.SmoothingMode=SmoothingMode.AntiAlias;Sky(g,w,h,mode);}
            }
            output.Save(Path.Combine(Target,name),ImageFormat.Png);output.Dispose();Console.WriteLine(name+" "+w+"x"+h);
        }
    }
}
