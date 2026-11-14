using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class NoteEffects {
    static string Target;
    static readonly System.Collections.Generic.Dictionary<string, Size> Sizes = new System.Collections.Generic.Dictionary<string, Size> {
        { "live_tap_effect_0_tex_note_common_all.png", new Size(1024, 1024) },
        { "live_tap_effect_0_tex_note_common_all_v2.png", new Size(1024, 1024) },
        { "live_tap_effect_0_tex_note_common_lane.png", new Size(512, 512) },
        { "live_tap_effect_0_tex_note_tail_01_v2.png", new Size(64, 64) },
        { "live_tap_effect_0_tex_ove_damage_01.png", new Size(256, 256) },
    };

    static Color Gold=C("#ffcc46"),Mint=C("#2bf2c5"),Purple=C("#ae81ff"),Blue=C("#65caff"),Pink=C("#ff73b3");
    static Bitmap CrystalSource;
    static int AtlasMode;
    static string SourceAtlasName;
    static int SourceCellX,SourceCellY;
    static Dictionary<int,Bitmap> Crystals=new Dictionary<int,Bitmap>();
    static Color C(string s){return ColorTranslator.FromHtml(s);}
    static Color A(int a,Color c){return Color.FromArgb(Math.Max(0,Math.Min(255,a)),c);}
    static Color Mix(Color a,Color b,float t){t=Math.Max(0,Math.Min(1,t));return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
    static PointF P(float x,float y){return new PointF(x,y);}
    static GraphicsPath Poly(params PointF[] points){var p=new GraphicsPath();p.AddPolygon(points);return p;}
    static GraphicsPath Bevel(float x,float y,float w,float h,float b){return Poly(P(x+b,y),P(x+w-b,y),P(x+w,y+b),P(x+w,y+h-b),P(x+w-b,y+h),P(x+b,y+h),P(x,y+h-b),P(x,y+b));}
    static void Line(Graphics g,Color c,float width,params PointF[] points){using(var p=new Pen(c,width)){p.LineJoin=LineJoin.Bevel;g.DrawLines(p,points);}}
    static void Fill(Graphics g,GraphicsPath p,Color c){using(var b=new SolidBrush(c))g.FillPath(b,p);}
    static void Stroke(Graphics g,GraphicsPath p,Color c,float width){using(var b=new Pen(c,width)){b.LineJoin=LineJoin.Bevel;g.DrawPath(b,p);}}
    static void Glow(Graphics g,GraphicsPath p,Color c,float gain,float radius){gain*=AtlasMode>0?1.85f:AtlasMode<0?.75f:1;radius*=AtlasMode>0?1.12f:1;for(int i=(int)radius*2;i>=2;i-=2){int alpha=(int)(gain*Math.Exp(-i*i/(2*radius*radius)));if(alpha>0)Stroke(g,p,A(alpha,c),i);}}
    static void Star(Graphics g,float x,float y,float r,Color c,int rays){
        var points=new PointF[rays*2];for(int i=0;i<points.Length;i++){double angle=-Math.PI/2+i*Math.PI/rays;float length=i%2==0?r:r*(rays==4?.16f:.23f);points[i]=P(x+(float)Math.Cos(angle)*length,y+(float)Math.Sin(angle)*length);}
        using(var p=Poly(points)){Glow(g,p,c,5,r*.35f);Fill(g,p,c);}
    }
    static void Cell(Graphics g,int x,int y,int w,int h,Action<Graphics,int,int> draw){var st=g.Save();int previousX=SourceCellX,previousY=SourceCellY;SourceCellX=x;SourceCellY=y;g.TranslateTransform(x,y);g.SetClip(new RectangleF(0,0,w,h),CombineMode.Intersect);draw(g,w,h);g.Restore(st);SourceCellX=previousX;SourceCellY=previousY;}
    static void Orb(Graphics g,int w,int h,Color c,float intensity,bool pin){
        using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                double dx=(x+.5-w*.5)/(w*.5),dy=(y+.5-h*.5)/(h*.5),r=Math.Sqrt(dx*dx+dy*dy);
                double core=Math.Exp(-r*r/(pin?.023:.105)),halo=Math.Exp(-r*r/.29);
                int alpha=(int)(255*intensity*(core*.88+halo*.36)*Math.Pow(Math.Max(0,1-r),.4));
                Color color=Mix(c,Color.White,(float)Math.Pow(core,.52));b.SetPixel(x,y,A(alpha,color));
            }g.DrawImage(b,new Rectangle(0,0,w,h));
        }
        if(pin)Star(g,w*.5f,h*.5f,Math.Min(w,h)*.20f,A((int)(190*intensity),Color.White),4);
    }
    static void Pulse(Graphics g,int w,int h,Color c,bool cross){
        float cx=w*.5f,cy=h*.5f,r=Math.Min(w,h)*.38f;
        using(var orbit=new GraphicsPath()){orbit.AddEllipse(cx-r,cy-r,r*2,r*2);Glow(g,orbit,c,6,8);Stroke(g,orbit,A(200,c),1.2f);}
        using(var pen=new Pen(A(175,Color.White),.8f)){
            for(int i=0;i<6;i++)g.DrawArc(pen,cx-r*.85f,cy-r*.85f,r*1.7f,r*1.7f,i*60+6,37);
        }
        for(int i=0;i<24;i++){double a=i*Math.PI/12;float r0=r*(i%3==0?.94f:1.01f),r1=r*1.10f;Line(g,A(i%3==0?190:105,c),.8f,P(cx+(float)Math.Cos(a)*r0,cy+(float)Math.Sin(a)*r0),P(cx+(float)Math.Cos(a)*r1,cy+(float)Math.Sin(a)*r1));}
        if(cross){Star(g,cx,cy,r*.52f,A(220,Color.White),4);Line(g,A(120,c),.8f,P(cx-r*.8f,cy),P(cx+r*.8f,cy));Line(g,A(120,c),.8f,P(cx,cy-r*.8f),P(cx,cy+r*.8f));}
    }
    // Frame positions in the atlas cell coordinate system.
    static readonly Dictionary<string, RectangleF> FrameBounds = new Dictionary<string, RectangleF> {
        { "live_tap_effect_0_tex_note_common_all.png/128,256", new RectangleF(23.5f, 23.5f, 82f, 81.39129f) },
        { "live_tap_effect_0_tex_note_common_all.png/128,384", new RectangleF(23.5f, 23.5f, 81.56128f, 81f) },
        { "live_tap_effect_0_tex_note_common_all.png/128,896", new RectangleF(27.0221558f, 27.0221558f, 74.9778442f, 74.9778442f) },
        { "live_tap_effect_0_tex_note_common_all.png/256,640", new RectangleF(27f, 27f, 75f, 75f) },
        { "live_tap_effect_0_tex_note_common_all.png/256,896", new RectangleF(26.5f, 26.5f, 75f, 75f) },
        { "live_tap_effect_0_tex_note_common_all.png/384,256", new RectangleF(23.5f, 23.5f, 81.50376f, 81f) },
        { "live_tap_effect_0_tex_note_common_all.png/384,384", new RectangleF(23.5f, 23.1372871f, 81.62258f, 81.72542f) },
        { "live_tap_effect_0_tex_note_common_all_v2.png/128,256", new RectangleF(23.5f, 23.5f, 81f, 81f) },
        { "live_tap_effect_0_tex_note_common_all_v2.png/128,384", new RectangleF(23.5f, 23.5f, 82f, 81f) },
        { "live_tap_effect_0_tex_note_common_all_v2.png/128,896", new RectangleF(27.5f, 27.5f, 73f, 73f) },
        { "live_tap_effect_0_tex_note_common_all_v2.png/256,640", new RectangleF(27.5f, 27.5f, 73f, 73f) },
        { "live_tap_effect_0_tex_note_common_all_v2.png/256,896", new RectangleF(27.5f, 27.5f, 73f, 73f) },
        { "live_tap_effect_0_tex_note_common_all_v2.png/384,256", new RectangleF(23.5f, 23.5f, 81f, 81f) },
        { "live_tap_effect_0_tex_note_common_all_v2.png/384,384", new RectangleF(23.5f, 23.5f, 81f, 81f) },
        { "live_tap_effect_0_tex_note_common_lane.png/0,0", new RectangleF(23.5f, 23.5f, 82f, 81.39129f) },
        { "live_tap_effect_0_tex_note_common_lane.png/0,128", new RectangleF(23.5f, 23.5f, 81.56128f, 81f) },
        { "live_tap_effect_0_tex_note_common_lane.png/128,0", new RectangleF(23.5f, 23.5f, 81.50376f, 81f) },
        { "live_tap_effect_0_tex_note_common_lane.png/128,128", new RectangleF(23.5f, 23.1372871f, 81.62258f, 81.72542f) },
    };
    static RectangleF SourceFrameBounds(){
        return FrameBounds[SourceAtlasName+"/"+SourceCellX+","+SourceCellY];
    }
    static void Frame(Graphics g,int w,int h,Color c,bool intense){
        RectangleF box=SourceFrameBounds();float size=Math.Min(box.Width,box.Height);
        using(var p=Bevel(box.X,box.Y,box.Width,box.Height,12*size/84)){
            Glow(g,p,c,intense?10:6,12);Fill(g,p,A(12,c));Stroke(g,p,A(235,c),2.3f);Stroke(g,p,A(220,Color.White),.85f);
        }
        for(int i=0;i<4;i++){
            var st=g.Save();g.TranslateTransform(box.X+box.Width*.5f,box.Y+box.Height*.5f);g.RotateTransform(i*90);
            float half=(i%2==0?box.Height:box.Width)*.5f;
            Line(g,A(165,c),.75f,P(-size*.262f,-half-2.8f),P(size*.1905f,-half-2.8f));Star(g,0,-half-1.52f,3.3f,A(200,Color.White),4);g.Restore(st);
        }
    }
    static Bitmap TintedCrystal(Color c){
        int key=c.ToArgb();if(Crystals.ContainsKey(key))return Crystals[key];
        var image=new Bitmap(128,128,PixelFormat.Format32bppArgb);
        for(int y=0;y<128;y++)for(int x=0;x<128;x++){
            Color old=CrystalSource.GetPixel(x+15,y+16);if(old.A==0)continue;
            int max=Math.Max(old.R,Math.Max(old.G,old.B)),min=Math.Min(old.R,Math.Min(old.G,old.B));
            float saturation=max==0?0:(max-min)/(float)max;
            float white=Math.Max(0,1-saturation/.73f);
            Color tinted=Mix(c,Color.White,white);
            float value=max/255f;
            image.SetPixel(x,y,Color.FromArgb(old.A,(int)(tinted.R*value),(int)(tinted.G*value),(int)(tinted.B*value)));
        }
        Crystals[key]=image;return image;
    }
    static PointF[] Stellar(float cx,float cy,float r){var p=new PointF[12];for(int i=0;i<12;i++){double a=-Math.PI/2+i*Math.PI/6;float radius=i%2==0?r:r*.61f;p[i]=P(cx+(float)Math.Cos(a)*radius,cy+(float)Math.Sin(a)*radius);}return p;}
    static void Gem(Graphics g,int w,int h,Color c,int stage){
        float cx=w*.5f,cy=h*.5f,r=Math.Min(w,h)*.43f;
        if(stage==0){g.DrawImage(TintedCrystal(c),new Rectangle(1,1,w-2,h-2));return;}
        using(var p=Poly(Stellar(cx,cy,r))){
            Glow(g,p,c,3,5);Stroke(g,p,A(155,c),.8f);
            var st=g.Save();g.SetClip(p,CombineMode.Intersect);
            Color top=A(stage==1?45:18,c),bottom=A(stage==2?42:15,c);
            using(var brush=new LinearGradientBrush(P(0,cy-r),P(0,cy+r),top,bottom))g.FillPath(brush,p);
            if(stage!=3)Line(g,A(125,Color.White),.7f,P(cx-r*.85f,cy),P(cx+r*.85f,cy));
            g.Restore(st);
        }
    }
    static void Beam(Graphics g,int w,int h,Color c,bool horizontal){
        float strength=horizontal?.72f:1f;
        using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                float t=(y+.5f)/h;
                double profile=Math.Pow(t,1.7);
                float edge=Math.Min(1,Math.Min((x+.5f)/2,(w-x-.5f)/2));
                Color color=Mix(c,Color.White,(float)(profile*.85));b.SetPixel(x,y,A((int)(250*profile*edge*strength),color));
            }
            using(var sampling=new ImageAttributes()){
                sampling.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(b,new Rectangle(0,0,w,h),0,0,w,h,GraphicsUnit.Pixel,sampling);
            }
        }
        // Open spectral tracks stay readable when the particle is stretched by the engine.
        using(var brush=new LinearGradientBrush(P(0,0),P(0,h),A(0,Color.White),A((int)(125*strength),Color.White)))
        using(var pen=new Pen(brush,.7f)){
            g.DrawBezier(pen,P(w*.27f,2),P(w*.38f,h*.35f),P(w*.16f,h*.65f),P(w*.28f,h-3));
            g.DrawBezier(pen,P(w*.73f,2),P(w*.62f,h*.35f),P(w*.84f,h*.65f),P(w*.72f,h-3));
        }
        if(!horizontal)for(int i=0;i<3;i++){float yy=h*(.45f+i*.18f);Line(g,A(65+i*22,Color.White),.6f,P(w*.28f,yy),P(w*.39f,yy-4),P(w*.62f,yy+3),P(w*.72f,yy));}
    }
    static void OvalSpark(Graphics g,int w,int h,Color c,int index){
        float strength=1-index*.21f;
        var st=g.Save();g.TranslateTransform(w*.5f,h*.5f);g.ScaleTransform(1,.95f);
        using(var p=Poly(P(0,-h*.31f),P(w*.17f,-h*.10f),P(w*.21f,0),P(0,h*.31f),P(-w*.21f,0),P(-w*.17f,-h*.10f))){Glow(g,p,c,4*strength,5);Fill(g,p,A((int)(245*strength),Color.White));Stroke(g,p,A((int)(190*strength),c),.7f);}g.Restore(st);
    }
    static void Spike(Graphics g,int w,int h,Color c){
        using(var p=Poly(P(w*.5f,h*.07f),P(w*.58f,h*.65f),P(w*.72f,h*.83f),P(w*.5f,h*.93f),P(w*.28f,h*.83f),P(w*.42f,h*.65f))){
            Glow(g,p,c,8,10);using(var brush=new LinearGradientBrush(P(0,h*.08f),P(0,h*.94f),A(100,c),Color.White))g.FillPath(brush,p);Stroke(g,p,A(165,Color.White),.8f);
        }
        Line(g,A(190,Color.White),.75f,P(w*.5f,h*.20f),P(w*.5f,h*.85f));
        Line(g,A(125,c),.6f,P(w*.32f,h*.81f),P(w*.5f,h*.63f),P(w*.68f,h*.81f));
    }
    static void Fan(Graphics g,int w,int h,Color c){
        using(var p=Poly(P(w*.1f,h*.5f),P(w*.85f,h*.12f),P(w*.55f,h*.5f),P(w*.85f,h*.88f))){
            Glow(g,p,c,4,7);using(var b=new LinearGradientBrush(P(0,0),P(w,0),A(220,Color.White),A(0,c)))g.FillPath(b,p);
        }
        Line(g,A(180,Color.White),.8f,P(w*.15f,h*.5f),P(w*.7f,h*.5f));
    }
    static void Rail(Graphics g,int w,int h,Color c){
        using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                double dx=(x+.5-w*.5)/(w*.5),t=(y+.5)/h;
                double v=Math.Exp(-dx*dx/.025)*Math.Pow(Math.Sin(t*Math.PI),.4);
                b.SetPixel(x,y,A((int)(245*v),Mix(c,Color.White,(float)Math.Pow(v,.7))));
            }g.DrawImage(b,new Rectangle(0,0,w,h));
        }
    }
    static void Arc(Graphics g,int w,int h,Color c){
        using(var p=new GraphicsPath()){p.AddArc(12,12,w-24,h-24,140,260);Glow(g,p,c,5,8);Stroke(g,p,A(210,c),2);Stroke(g,p,A(190,Color.White),.7f);}
        using(var pen=new Pen(A(130,c),.7f))g.DrawArc(pen,20,20,w-40,h-40,145,250);
        for(int i=0;i<7;i++){double angle=(160+i*30)*Math.PI/180;float x=w*.5f+(float)Math.Cos(angle)*w*.43f,y=h*.5f+(float)Math.Sin(angle)*h*.43f;Star(g,x,y,2.5f,A(160,Color.White),4);}
    }
    static void Music(Graphics g,int w,int h){
        using(var p=Poly(P(19,16),P(46,10),P(46,39),P(42,39),P(42,20),P(23,24),P(23,45),P(19,45))){Fill(g,p,Color.White);Stroke(g,p,A(160,Mint),1.2f);}
        using(var b=new SolidBrush(Color.White)){g.FillEllipse(b,10,38,13,10);g.FillEllipse(b,33,32,13,10);}
        Star(g,49,16,3,A(180,Mint),4);
    }
    static void AllAtlas(Graphics g,bool v2){
        // The following rectangles are the original packed sprite slots, in PNG coordinates.
        Cell(g,0,0,64,64,(q,w,h)=>Gem(q,w,h,Mint,0));
        Cell(g,64,0,64,64,(q,w,h)=>Gem(q,w,h,Color.White,0));
        Cell(g,128,0,64,64,Music);
        Cell(g,192,0,64,64,(q,w,h)=>Orb(q,w,h,Color.White,.95f,false));
        Cell(g,0,64,64,64,(q,w,h)=>Gem(q,w,h,Gold,3));
        Cell(g,64,64,64,64,(q,w,h)=>Star(q,w*.5f,h*.5f,21,Color.White,4));
        Cell(g,128,64,64,64,(q,w,h)=>Star(q,w*.5f,h*.5f,23,Color.White,8));
        Cell(g,192,64,64,64,(q,w,h)=>Star(q,w*.5f,h*.5f,25,A(180,Mint),4));
        Cell(g,0,128,64,64,(q,w,h)=>Gem(q,w,h,Mint,1));
        Cell(g,64,128,64,64,(q,w,h)=>Gem(q,w,h,Pink,1));
        Cell(g,128,128,64,64,(q,w,h)=>Gem(q,w,h,Gold,0));
        Cell(g,192,128,64,64,(q,w,h)=>Orb(q,w,h,Blue,.8f,true));
        Color[] sparks={Blue,Pink,Mint,Gold};
        for(int i=0;i<4;i++){Color c=sparks[i];Cell(g,i*64,192,64,64,(q,w,h)=>{Orb(q,w,h,c,.35f,true);Star(q,w*.5f,h*.5f,19,Mix(c,Color.White,.9f),6);});}
        Cell(g,256,0,64,64,(q,w,h)=>Orb(q,w,h,Pink,1,true));
        Cell(g,320,0,64,64,(q,w,h)=>Orb(q,w,h,Gold,1,true));
        Cell(g,384,0,64,64,(q,w,h)=>Orb(q,w,h,Color.White,.9f,false));
        Cell(g,256,64,64,64,(q,w,h)=>Fan(q,w,h,Mint));
        Cell(g,256,128,128,128,(q,w,h)=>Rail(q,w,h,Mint));
        if(v2)Cell(g,384,128,128,128,(q,w,h)=>Pulse(q,w,h,Purple,true));
        Color[] columns={Color.White,Blue,Color.White,Mint};
        for(int i=0;i<4;i++){Color c=columns[i];Cell(g,i*32,256,32,128,(q,w,h)=>Beam(q,w,h,c,false));}
        Cell(g,128,256,128,128,(q,w,h)=>Frame(q,w,h,Purple,!v2));
        Cell(g,384,256,128,128,(q,w,h)=>Frame(q,w,h,Pink,!v2));
        Cell(g,256,256,64,128,(q,w,h)=>Beam(q,w,h,Color.White,false));
        Cell(g,320,256,64,128,(q,w,h)=>Beam(q,w,h,Color.White,true));
        Cell(g,0,384,128,128,(q,w,h)=>Arc(q,w,h,Purple));
        Cell(g,128,384,128,128,(q,w,h)=>Frame(q,w,h,Gold,!v2));
        Cell(g,384,384,128,128,(q,w,h)=>Frame(q,w,h,Mint,!v2));
        Cell(g,256,384,64,128,(q,w,h)=>Beam(q,w,h,Gold,false));
        Cell(g,320,384,64,128,(q,w,h)=>Beam(q,w,h,Pink,false));
        for(int group=0;group<2;group++){
            int y=512+group*256;Color c=group==0?Mint:Gold;
            Cell(g,0,y,128,128,(q,w,h)=>Gem(q,w,h,c,0));
            Cell(g,128,y,128,128,(q,w,h)=>Gem(q,w,h,c,1));
            Cell(g,0,y+128,128,128,(q,w,h)=>Gem(q,w,h,c,2));
            if(group==0)Cell(g,128,y+128,128,128,(q,w,h)=>Gem(q,w,h,c,3));
            Cell(g,256,y,128,128,(q,w,h)=>Pulse(q,w,h,c,false));
            Cell(g,384,y,128,128,(q,w,h)=>Orb(q,w,h,c,.8f,false));
            Cell(g,384,y+128,64,128,(q,w,h)=>Beam(q,w,h,c,false));
            Cell(g,448,y+128,64,128,(q,w,h)=>Beam(q,w,h,c,true));
        }
        Cell(g,256,640,128,128,(q,w,h)=>Frame(q,w,h,Mint,false));
        Cell(g,128,896,128,128,(q,w,h)=>Frame(q,w,h,v2?Pink:Gold,false));
        Cell(g,256,896,128,128,(q,w,h)=>Frame(q,w,h,Purple,v2));
        for(int group=0;group<2;group++){
            int y=group*256;Color c=group==0?Purple:Gold;
            Cell(g,512,y,128,128,(q,w,h)=>Gem(q,w,h,c,0));
            Cell(g,640,y,128,128,(q,w,h)=>Gem(q,w,h,c,1));
            Cell(g,512,y+128,128,128,(q,w,h)=>Gem(q,w,h,c,2));
            Cell(g,640,y+128,128,128,(q,w,h)=>Gem(q,w,h,c,3));
        }
        for(int group=0;group<2;group++){
            int y=512+group*256;Color c=Gold;
            Cell(g,512,y,128,128,(q,w,h)=>Gem(q,w,h,c,0));
            Cell(g,512,y+128,128,128,(q,w,h)=>Gem(q,w,h,c,2));
            for(int j=0;j<2;j++)for(int i=0;i<4;i++){
                int index=i;Color spark=group==0?Pink:Gold;
                Cell(g,640+i*32,y+j*64,32,64,(q,w,h)=>OvalSpark(q,w,h,spark,index+(j==1?1:0)));
            }
            Cell(g,640,y+128,64,128,(q,w,h)=>Fan(q,w,h,Pink));
            Cell(g,704,y+128,64,128,(q,w,h)=>Beam(q,w,h,group==0?Pink:Gold,true));
            Cell(g,768,y,128,128,(q,w,h)=>Orb(q,w,h,Gold,.8f,false));
        }
        Cell(g,896,0,128,128,(q,w,h)=>Orb(q,w,h,Blue,.85f,false));
        Cell(g,896,128,64,128,(q,w,h)=>Beam(q,w,h,Purple,false));
        Cell(g,960,128,64,128,(q,w,h)=>Beam(q,w,h,Purple,true));
        Cell(g,896,256,128,128,(q,w,h)=>Orb(q,w,h,Gold,.85f,false));
        Cell(g,896,384,64,128,(q,w,h)=>Beam(q,w,h,Gold,false));
        Cell(g,960,384,64,128,(q,w,h)=>Beam(q,w,h,Gold,true));
        Cell(g,896,512,128,256,(q,w,h)=>Spike(q,w,h,Pink));
        Cell(g,896,768,128,256,(q,w,h)=>Spike(q,w,h,Gold));
    }
    static void LaneAtlas(Graphics g){
        Cell(g,0,0,128,128,(q,w,h)=>Frame(q,w,h,Purple,true));
        Cell(g,128,0,128,128,(q,w,h)=>Frame(q,w,h,Pink,true));
        Cell(g,0,128,128,128,(q,w,h)=>Frame(q,w,h,Gold,true));
        Cell(g,128,128,128,128,(q,w,h)=>Frame(q,w,h,Mint,true));
        Cell(g,256,0,64,128,(q,w,h)=>Beam(q,w,h,Color.White,false));
        Cell(g,320,0,64,128,(q,w,h)=>Beam(q,w,h,Color.White,true));
        Cell(g,256,128,64,128,(q,w,h)=>Beam(q,w,h,Gold,false));
        Cell(g,320,128,64,128,(q,w,h)=>Beam(q,w,h,Pink,false));
    }
    static void Tail(Graphics g){
        using(var b=new Bitmap(64,64,PixelFormat.Format32bppArgb)){
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){
                double dx=(x-27.0)/26,dy=(y-31.5)/3.2;
                int alpha=(int)(250*Math.Exp(-dx*dx*1.8-dy*dy)*Math.Max(0,1-Math.Abs(dx)));
                b.SetPixel(x,y,A(alpha,Mix(Blue,Color.White,.9f)));
            }g.DrawImage(b,0,0,64,64);
        }
        Line(g,A(155,Color.White),.6f,P(9,32),P(48,32));Star(g,24,32,4.6f,A(215,Color.White),4);
    }
    static void Damage(string name,int w,int h){
        // The lower-right corner meets the other quadrants at the transparent center.
        Color tint=C("#ff243e");
        using(var output=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                double u=1-x/(double)(w-1),v=1-y/(double)(h-1);
                double radius=Math.Sqrt(u*u+v*v);
                double fade=Math.Max(0,Math.Min(1,(radius-.24)/1.52));
                double opacity=fade*fade*(3-2*fade);
                int alpha=(int)Math.Round(181*opacity);
                output.SetPixel(x,y,Color.FromArgb(alpha,tint));
            }
            output.Save(Path.Combine(Target,name),ImageFormat.Png);
        }
    }
    static Color GradeSoftAtlas(Color pixel){
        // Contrast and shadow colour only: the source particle alpha remains untouched.
        double luminance=(.2126*pixel.R+.7152*pixel.G+.0722*pixel.B)/255;
        double target=Math.Pow(Math.Max(0,Math.Min(1,(luminance-.68)*1.22+.68)),1.13);
        double gain=luminance<.0001?0:target/luminance,shadow=1-luminance;
        double r=pixel.R*gain,g=pixel.G*gain,b=pixel.B*gain,centre=target*255;
        r=(centre+(r-centre)*1.10)*(1-.10*shadow);
        g=(centre+(g-centre)*1.10)*(1-.035*shadow);
        b=(centre+(b-centre)*1.10)*(1+.05*shadow);
        return Color.FromArgb(pixel.A,(int)Math.Round(Math.Max(0,Math.Min(255,r))),(int)Math.Round(Math.Max(0,Math.Min(255,g))),(int)Math.Round(Math.Max(0,Math.Min(255,b))));
    }
    static void GradeAtlas(Bitmap bmp){
        int w=bmp.Width,h=bmp.Height;
        if(AtlasMode!=0)for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            Color pixel=bmp.GetPixel(x,y);if(pixel.A==0)continue;
            if(AtlasMode<0)bmp.SetPixel(x,y,GradeSoftAtlas(pixel));
            else bmp.SetPixel(x,y,Color.FromArgb(Math.Min(255,(int)Math.Round(pixel.A*1.12f)),Math.Min(255,(int)Math.Round(pixel.R*1.10f)),Math.Min(255,(int)Math.Round(pixel.G*1.10f)),Math.Min(255,(int)Math.Round(pixel.B*1.10f))));
        }
    }
    static void Save(Bitmap hi,string name,int w,int h){using(var bmp=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
        using(var g=Graphics.FromImage(bmp)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(hi,new Rectangle(0,0,w,h),0,0,hi.Width,hi.Height,GraphicsUnit.Pixel);}
        GradeAtlas(bmp);
        bmp.Save(Path.Combine(Target,name),ImageFormat.Png);
    }}
    static int[] ReadPixels(Bitmap image){
        var pixels=new int[image.Width*image.Height];
        var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0,pixels,0,pixels.Length);image.UnlockBits(data);return pixels;
    }
    public static void UpdateBandBeams(string root){
        // Change only the affected packed rectangles; every other atlas pixel stays intact.
        string[] names={"tex_note_common_all.png","tex_note_common_all_v2.png","tex_note_common_lane.png"};
        int[,] locations={{320,256},{448,640},{448,896},{704,640},{704,896},{960,128},{960,384}};
        Color[] colors={Color.White,Mint,Gold,Pink,Gold,Purple,Gold};int changed=0;
        foreach(string stem in names){
            bool lane=stem.Contains("lane");AtlasMode=lane?0:stem.Contains("_v2")?-1:1;
            string file=Path.Combine(root,"new_effects","live_tap_effect_0_"+stem);int width,height;int[] pixels;
            using(var original=new Bitmap(file)){width=original.Width;height=original.Height;pixels=ReadPixels(original);}
            for(int i=0;i<(lane?1:locations.GetLength(0));i++){
                int left=lane?320:locations[i,0],top=lane?0:locations[i,1];
                using(var hi=new Bitmap(128,256,PixelFormat.Format32bppArgb))
                using(var tile=new Bitmap(64,128,PixelFormat.Format32bppArgb)){
                    using(var g=Graphics.FromImage(hi)){
                        g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.ScaleTransform(2,2);
                        Beam(g,64,128,lane?Color.White:colors[i],true);
                    }
                    using(var g=Graphics.FromImage(tile)){
                        g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                        using(var sampling=new ImageAttributes()){
                            sampling.SetWrapMode(WrapMode.TileFlipXY);
                            g.DrawImage(hi,new Rectangle(0,0,64,128),0,0,128,256,GraphicsUnit.Pixel,sampling);
                        }
                    }
                    GradeAtlas(tile);int[] replacement=ReadPixels(tile);
                    for(int y=0;y<128;y++)Array.Copy(replacement,y*64,pixels,(top+y)*width+left,64);
                }
                changed++;
            }
            using(var output=new Bitmap(width,height,PixelFormat.Format32bppArgb)){
                var data=output.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                System.Runtime.InteropServices.Marshal.Copy(pixels,0,data.Scan0,pixels.Length);output.UnlockBits(data);output.Save(file,ImageFormat.Png);
            }
            Console.WriteLine("Updated upward-fading weaker beams: "+stem);
        }
        Console.WriteLine("Patched "+changed+" beam rectangles in three atlases.");
    }
    public static void UpdateFrames(string root){
        Target=Path.Combine(root,"new_effects");int count=0;
        int[,] main={{128,256},{384,256},{128,384},{384,384},{256,640},{128,896},{256,896}},lane={{0,0},{128,0},{0,128},{128,128}};
        foreach(string stem in new[]{"all","all_v2","lane"}){
            bool isLane=stem=="lane",v2=stem=="all_v2";AtlasMode=isLane?0:v2?-1:1;
            SourceAtlasName="live_tap_effect_0_tex_note_common_"+stem+".png";
            string file=Path.Combine(Target,SourceAtlasName);int width,height;int[] pixels;
            using(var image=new Bitmap(file)){width=image.Width;height=image.Height;pixels=ReadPixels(image);}
            var slots=isLane?lane:main;
            for(int i=0;i<slots.GetLength(0);i++){
                SourceCellX=slots[i,0];SourceCellY=slots[i,1];
                Color color=i==0?Purple:i==1?Pink:i==2?Gold:i==3||i==4?Mint:i==5?(v2?Pink:Gold):Purple;
                bool intense=isLane||i<4&&!v2||i==6&&v2;
                using(var hi=new Bitmap(256,256,PixelFormat.Format32bppArgb))using(var tile=new Bitmap(128,128,PixelFormat.Format32bppArgb)){
                    using(var g=Graphics.FromImage(hi)){
                        g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.ScaleTransform(2,2);
                        Frame(g,128,128,color,intense);
                    }
                    using(var g=Graphics.FromImage(tile)){
                        g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                        using(var sampling=new ImageAttributes()){
                            sampling.SetWrapMode(WrapMode.TileFlipXY);
                            g.DrawImage(hi,new Rectangle(0,0,128,128),0,0,256,256,GraphicsUnit.Pixel,sampling);
                        }
                    }
                    GradeAtlas(tile);int[] replacement=ReadPixels(tile);
                    for(int y=0;y<128;y++)Array.Copy(replacement,y*128,pixels,(SourceCellY+y)*width+SourceCellX,128);
                }
                count++;
            }
            using(var output=new Bitmap(width,height,PixelFormat.Format32bppArgb)){
                var data=output.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                System.Runtime.InteropServices.Marshal.Copy(pixels,0,data.Scan0,pixels.Length);output.UnlockBits(data);output.Save(file,ImageFormat.Png);
            }
            Console.WriteLine("Aligned frame cells to original border positions: "+SourceAtlasName);
        }
        Console.WriteLine("Updated "+count+" frame cells in three atlases; other cells preserved byte for byte.");
    }
    public static void Run(string root){
        Target=Path.Combine(root,"new_effects");Directory.CreateDirectory(Target);
        CrystalSource=new Bitmap(Path.Combine(root,"new_notes","textures","noteskin","notes_long_among_crtcl.png"));
        string[] names={"tex_note_common_all.png","tex_note_common_all_v2.png","tex_note_common_lane.png","tex_note_tail_01_v2.png","tex_ove_damage_01.png"};
        foreach(string stem in names){
            AtlasMode=stem=="tex_note_common_all.png"?1:stem=="tex_note_common_all_v2.png"?-1:0;
            string name="live_tap_effect_0_"+stem;SourceAtlasName=name;int w=Sizes[name].Width,h=Sizes[name].Height;
            if(stem=="tex_ove_damage_01.png"){Damage(name,w,h);Console.WriteLine(name+" "+w+"x"+h);continue;}
            int scale=w>=512?2:4;
            using(var hi=new Bitmap(w*scale,h*scale,PixelFormat.Format32bppArgb)){
                using(var g=Graphics.FromImage(hi)){
                    g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.ScaleTransform(scale,scale);
                    if(stem=="tex_note_common_all.png")AllAtlas(g,false);
                    else if(stem=="tex_note_common_all_v2.png")AllAtlas(g,true);
                    else if(stem=="tex_note_common_lane.png")LaneAtlas(g);
                    else if(stem=="tex_note_tail_01_v2.png")Tail(g);
                    else throw new Exception("Unknown effect: "+stem);
                }Save(hi,name,w,h);
            }
            Console.WriteLine(name+" "+w+"x"+h);
        }
        CrystalSource.Dispose();foreach(var b in Crystals.Values)b.Dispose();Crystals.Clear();
    }
}
