using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Sprites for the song info page shown before a live (MusicInfoView in FrontUIView.prefab).
// Same palette and shapes as the HUD (AstralHud.cs): bevelled corners, pastel rims running
// rose -> violet -> blue -> mint, four-point sparkles. Everything is drawn white or in those
// hues so the prefab can tint it. Output: ArtSource/MusicInfo, copied to
// Assets/Graphics/Textures/MusicInfo by render_musicinfo.ps1.
public static class MusicInfoArt {
    static string Target;
    // The HUD's hues at half their HSL saturation: the page reads softer than the live HUD.
    const float Saturation=.5f;
    static Color Mint=Desat(C("#2bf2c5")),Purple=Desat(C("#ae81ff")),Blue=Desat(C("#65caff")),Pink=Desat(C("#ff73b3")),Navy=C("#101a32"),Deep=C("#1a1440");
    static Color Desat(Color c){
        float h=c.GetHue()/360f,l=c.GetBrightness(),sat=c.GetSaturation()*Saturation;
        if(sat<=0)return c;
        float q=l<.5f?l*(1+sat):l+sat-l*sat,p=2*l-q;
        return Color.FromArgb(c.A,Channel(p,q,h+1/3f),Channel(p,q,h),Channel(p,q,h-1/3f));
    }
    static int Channel(float p,float q,float t){if(t<0)t+=1;if(t>1)t-=1;float v=t<1/6f?p+(q-p)*6*t:t<.5f?q:t<2/3f?p+(q-p)*(2/3f-t)*6:p;return (int)Math.Round(Math.Max(0,Math.Min(1,v))*255);}
    static Color C(string s){return ColorTranslator.FromHtml(s);}
    static Color A(int a,Color c){return Color.FromArgb(Math.Max(0,Math.Min(255,a)),c);}
    static Color Mix(Color a,Color b,float t){return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
    static Color Lt(Color c){return Mix(c,Color.White,.3f);}
    static PointF P(float x,float y){return new PointF(x,y);}
    static GraphicsPath Polygon(params PointF[] p){var path=new GraphicsPath();path.AddPolygon(p);return path;}
    static GraphicsPath Bevel(float x,float y,float w,float h,float b){b=Math.Min(b,Math.Min(w,h)/2);return Polygon(P(x+b,y),P(x+w-b,y),P(x+w,y+b),P(x+w,y+h-b),P(x+w-b,y+h),P(x+b,y+h),P(x,y+h-b),P(x,y+b));}
    static void Fill(Graphics g,GraphicsPath path,Color c){using(var b=new SolidBrush(c))g.FillPath(b,path);}
    static void Stroke(Graphics g,GraphicsPath path,Color c,float width){using(var p=new Pen(c,width)){p.LineJoin=LineJoin.Bevel;g.DrawPath(p,path);}}
    static void Rim(Graphics g,GraphicsPath path,float width,int alpha,float angle,params Color[] colors){
        RectangleF r=path.GetBounds();r.Inflate(width+1,width+1);
        var positions=new float[colors.Length];var stops=new Color[colors.Length];
        for(int i=0;i<colors.Length;i++){positions[i]=i/(float)(colors.Length-1);stops[i]=A(alpha,colors[i]);}
        using(var b=new LinearGradientBrush(r,Color.Black,Color.Black,angle)){
            b.InterpolationColors=new ColorBlend{Positions=positions,Colors=stops};
            using(var pen=new Pen(b,width)){pen.LineJoin=LineJoin.Bevel;g.DrawPath(pen,path);}
        }
    }
    static void Bloom(Graphics g,GraphicsPath path,Color c,float strength,float radius){for(int i=(int)(radius*2);i>=2;i-=2){int alpha=(int)(strength*Math.Exp(-i*i/(2*radius*radius)));if(alpha>0)Stroke(g,path,A(alpha,c),i);}}
    static GraphicsPath StarPath(float x,float y,float r,float waist){return Polygon(P(x,y-r),P(x+r*waist,y-r*waist),P(x+r,y),P(x+r*waist,y+r*waist),P(x,y+r),P(x-r*waist,y+r*waist),P(x-r,y),P(x-r*waist,y-r*waist));}
    static Bitmap Canvas(int w,int h,out Graphics g){var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);g=Graphics.FromImage(b);g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;return b;}
    static void Save(Bitmap b,string name){b.Save(Path.Combine(Target,name),ImageFormat.Png);Console.WriteLine(name+" "+b.Width+"x"+b.Height);b.Dispose();}
    static Color[] Rainbow{get{return new[]{Lt(Pink),Lt(Purple),Lt(Blue),Lt(Mint)};}}

    // Dark band across the middle of the screen. Stretched horizontally, so only the vertical
    // profile matters: a deep violet core that thins out towards both edges. Dithered so the
    // long dark gradient does not band on device (see the uncompressed import in the builder).
    static void Band(){
        int w=8,h=512;var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);var rnd=new Random(7);
        for(int y=0;y<h;y++){
            float t=Math.Abs((y+.5f)/h*2-1);                // 0 centre .. 1 edge
            float core=(float)Math.Pow(1-t,0.55);          // flat-ish middle, soft shoulders
            float a=Math.Max(0,Math.Min(1,core*1.15f))*232;
            Color tint=Mix(Deep,Navy,t);
            for(int x=0;x<w;x++){
                float n=(float)rnd.NextDouble()-.5f;
                b.SetPixel(x,y,Color.FromArgb(Clamp(a+n*2),Clamp(tint.R+n),Clamp(tint.G+n),Clamp(tint.B+n)));
            }
        }
        Save(b,"mi_band.png");
    }
    static int Clamp(float v){return Math.Max(0,Math.Min(255,(int)Math.Round(v)));}

    // Thin pastel rule with a glow, fading out at both ends. Used for the band edges and the
    // separator under the title.
    static void RuleLine(){
        Graphics g;var b=Canvas(1024,24,out g);
        using(var path=new GraphicsPath()){path.AddLine(12,12,1012,12);
            for(int i=14;i>=2;i-=2){int alpha=(int)(70*Math.Exp(-i*i/(2*36.0)));Rim(g,path,i,alpha,0,Rainbow);}
            Rim(g,path,2.2f,255,0,Rainbow);
        }
        g.Dispose();
        // Fade the ends.
        for(int x=0;x<b.Width;x++){float t=x/(float)(b.Width-1);float f=Math.Min(1,Math.Min(t,1-t)/.18f);f=f*f*(3-2*f);
            for(int y=0;y<b.Height;y++){Color p=b.GetPixel(x,y);if(p.A==0)continue;b.SetPixel(x,y,Color.FromArgb((int)(p.A*f),p));}}
        Save(b,"mi_rule.png");
    }

    // Jacket frame for a 440 px jacket: bevelled glass rim with the gradient and a bloom, small
    // ticks at the corners. Canvas 520 so the bloom has room.
    const int FrameCanvas=520,FrameJacket=440,FrameBevel=26;
    static void Frame(){
        Graphics g;var b=Canvas(FrameCanvas,FrameCanvas,out g);float o=(FrameCanvas-FrameJacket)/2f;
        using(var outer=Bevel(o-8,o-8,FrameJacket+16,FrameJacket+16,FrameBevel+6))
        using(var inner=Bevel(o-1,o-1,FrameJacket+2,FrameJacket+2,FrameBevel)){
            Bloom(g,outer,Lt(Purple),46,14);
            Rim(g,outer,3f,255,45,Lt(Pink),Lt(Purple),Lt(Blue),Lt(Mint));
            Rim(g,inner,1.6f,150,225,Color.White,Lt(Blue),Lt(Purple));
        }
        // Corner ticks just outside the rim, like the lane frame's end markers.
        float e=o-22,f=FrameCanvas-o+22,l=34;
        using(var pen=new Pen(A(220,Color.White),2.4f)){
            g.DrawLines(pen,new[]{P(e,e+l),P(e,e),P(e+l,e)});
            g.DrawLines(pen,new[]{P(f-l,f),P(f,f),P(f,f-l)});
        }
        g.Dispose();Save(b,"mi_frame.png");
    }

    // Stencil for the jacket: same bevel as the frame's inner edge.
    static void JacketMask(){Graphics g;var b=Canvas(FrameJacket,FrameJacket,out g);using(var p=Bevel(0,0,FrameJacket,FrameJacket,FrameBevel))Fill(g,p,Color.White);g.Dispose();Save(b,"mi_jacket_mask.png");}

    // Diagonal light sweep that runs across the jacket once.
    static void Shine(){
        int w=256,h=640;var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            float d=(x-w/2f)+(y-h/2f)*.35f;                // slanted distance from the centre line
            float core=(float)Math.Exp(-d*d/(2*14f*14f)),halo=(float)Math.Exp(-d*d/(2*52f*52f));
            float vy=Math.Abs((y+.5f)/h*2-1);float fade=1-vy*vy;
            int a=Clamp((core*170+halo*70)*fade);b.SetPixel(x,y,Color.FromArgb(a,255,255,255));
        }
        Save(b,"mi_shine.png");
    }

    // Difficulty plate, sliced 22 px. White body with a slight vertical sheen so the tint reads
    // as glass, and a brighter rim.
    static void Tag(){
        Graphics g;var b=Canvas(96,56,out g);
        using(var body=Bevel(2,2,92,52,16)){
            using(var br=new LinearGradientBrush(P(0,2),P(0,54),Color.White,C("#c9c9d6")))g.FillPath(br,body);
            using(var hl=Bevel(5,5,86,22,12))using(var br=new LinearGradientBrush(P(0,5),P(0,27),A(120,Color.White),A(0,Color.White)))g.FillPath(br,hl);
            Stroke(g,body,A(255,Color.White),2.2f);
        }
        g.Dispose();Save(b,"mi_tag.png");
        // APPEND keeps its own colours: the HUD's rose-violet-blue sweep.
        b=Canvas(96,56,out g);
        using(var body=Bevel(2,2,92,52,16)){
            using(var br=new LinearGradientBrush(new RectangleF(0,0,96,56),Color.Black,Color.Black,0f)){
                br.InterpolationColors=new ColorBlend{Positions=new[]{0f,.5f,1f},Colors=new[]{Mix(Pink,Color.White,.1f),Mix(Purple,Color.White,.05f),Mix(Blue,Color.White,.1f)}};g.FillPath(br,body);}
            using(var hl=Bevel(5,5,86,22,12))using(var br=new LinearGradientBrush(P(0,5),P(0,27),A(110,Color.White),A(0,Color.White)))g.FillPath(br,hl);
            Stroke(g,body,Color.White,2.2f);
        }
        g.Dispose();Save(b,"mi_tag_append.png");
    }

    // Four-point sparkle with a soft halo, white so it can be tinted.
    static void Sparkle(){
        int s=96;Graphics g;var b=Canvas(s,s,out g);float c=s/2f;
        using(var gp=new GraphicsPath()){gp.AddEllipse(c-30,c-30,60,60);using(var br=new PathGradientBrush(gp)){br.CenterColor=A(90,Color.White);br.SurroundColors=new[]{A(0,Color.White)};g.FillPath(br,gp);}}
        using(var p=StarPath(c,c,44,.12f))Fill(g,p,A(235,Color.White));
        using(var p=StarPath(c,c,18,.3f))Fill(g,p,Color.White);
        g.Dispose();Save(b,"mi_sparkle.png");
    }

    // Soft violet-blue glow laid behind the jacket.
    static void Glow(){
        int s=256;var b=new Bitmap(s,s,PixelFormat.Format32bppArgb);var rnd=new Random(3);
        for(int y=0;y<s;y++)for(int x=0;x<s;x++){
            float dx=(x+.5f)/s*2-1,dy=(y+.5f)/s*2-1;float r=(float)Math.Sqrt(dx*dx+dy*dy);
            float a=(float)Math.Exp(-r*r*3.2)*(r<1?1:0);Color c=Mix(Lt(Purple),Lt(Blue),Math.Max(0,Math.Min(1,(dx+dy)*.5f+.5f)));
            float n=(float)rnd.NextDouble()-.5f;b.SetPixel(x,y,Color.FromArgb(Clamp(a*200+n*2),c));
        }
        Save(b,"mi_glow.png");
    }

    // Small line icons (white): a note for the song credits, a chart sheet for the score credits.
    static void Icons(){
        Graphics g;var b=Canvas(48,48,out g);
        using(var pen=new Pen(Color.White,3.2f)){pen.StartCap=pen.EndCap=LineCap.Round;
            g.DrawLine(pen,20,34,20,10);g.DrawLine(pen,20,10,36,6);g.DrawLine(pen,36,6,36,30);}
        g.FillEllipse(Brushes.White,10,29,13,10);g.FillEllipse(Brushes.White,26,25,13,10);
        g.Dispose();Save(b,"mi_icon_song.png");
        b=Canvas(48,48,out g);
        using(var sheet=Bevel(9,5,30,38,7))Stroke(g,sheet,Color.White,3f);
        using(var bar=Bevel(15,14,18,5,2))Fill(g,bar,Color.White);
        using(var bar=Bevel(15,23,18,5,2))Fill(g,bar,Color.White);
        using(var bar=Bevel(15,32,11,5,2))Fill(g,bar,Color.White);
        g.Dispose();Save(b,"mi_icon_chart.png");
    }

    public static void Run(string root){
        Target=Path.Combine(root,"MusicInfo");Directory.CreateDirectory(Target);
        Band();RuleLine();Frame();JacketMask();Shine();Tag();Sparkle();Glow();Icons();
    }
}
