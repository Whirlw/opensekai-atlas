using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

public static class AstralHud {
    static string Target;
    static Dictionary<string, Size> Sizes;
    static System.Windows.Media.GlyphTypeface Bold,Medium;
    static float DigitTop,DigitBottom;
    static Dictionary<string,float> DigitScales=new Dictionary<string,float>();
    static Color Gold=C("#ffcc46"),Mint=C("#2bf2c5"),Purple=C("#ae81ff"),Blue=C("#65caff"),Pink=C("#ff73b3"),Navy=C("#101a32");
    static Color C(string s){return ColorTranslator.FromHtml(s);}
    static Color A(int a,Color c){return Color.FromArgb(Math.Max(0,Math.Min(255,a)),c);}
    static Color Mix(Color a,Color b,float t){return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
    static PointF P(float x,float y){return new PointF(x,y);}
    static GraphicsPath Polygon(params PointF[] p){var path=new GraphicsPath();path.AddPolygon(p);return path;}
    static GraphicsPath Bevel(float x,float y,float w,float h,float b){b=Math.Min(b,Math.Min(w,h)/2);return Polygon(P(x+b,y),P(x+w-b,y),P(x+w,y+b),P(x+w,y+h-b),P(x+w-b,y+h),P(x+b,y+h),P(x,y+h-b),P(x,y+b));}
    static void Line(Graphics g,Color c,float width,params PointF[] points){using(var pen=new Pen(c,width)){pen.LineJoin=LineJoin.Bevel;g.DrawLines(pen,points);}}
    static void Fill(Graphics g,GraphicsPath path,Color c){using(var b=new SolidBrush(c))g.FillPath(b,path);}
    static void Stroke(Graphics g,GraphicsPath path,Color c,float width){using(var p=new Pen(c,width)){p.LineJoin=LineJoin.Bevel;g.DrawPath(p,path);}}
    // HUD rims: a pastel gradient in the backdrop's hues (rose and violet on the left of the screen, blue and
    // mint on the right) instead of a flat white line. `angle` is the gradient direction in degrees.
    static Color Lt(Color c){return Mix(c,Color.White,.3f);}
    // Every rim colour is drawn at half its saturation (HSV), keeping its brightness.
    static Color Soft(Color c){int m=Math.Max(c.R,Math.Max(c.G,c.B));return Color.FromArgb((c.R+m)/2,(c.G+m)/2,(c.B+m)/2);}
    static Color[] RimScore{get{return new[]{Lt(Pink),Lt(Purple),Lt(Blue)};}}
    static Color[] RimLife{get{return new[]{Lt(Purple),Lt(Blue),Lt(Mint)};}}
    static Color[] RimButton{get{return new[]{Lt(Mint),Lt(Blue),Lt(Purple)};}}
    static void Rim(Graphics g,GraphicsPath path,float width,int alpha,float angle,params Color[] colors){
        RectangleF r=path.GetBounds();r.Inflate(width+1,width+1);
        var positions=new float[colors.Length];var stops=new Color[colors.Length];
        for(int i=0;i<colors.Length;i++){positions[i]=i/(float)(colors.Length-1);stops[i]=A(alpha,Soft(colors[i]));}
        using(var b=new LinearGradientBrush(r,Color.Black,Color.Black,angle)){
            b.InterpolationColors=new ColorBlend{Positions=positions,Colors=stops};
            using(var pen=new Pen(b,width)){pen.LineJoin=LineJoin.Bevel;g.DrawPath(pen,path);}
        }
    }
    static void Gradient(Graphics g,GraphicsPath path,Color top,Color bottom){RectangleF r=path.GetBounds();using(var b=new LinearGradientBrush(P(0,r.Top),P(0,r.Bottom+.01f),top,bottom))g.FillPath(b,path);}
    static void Bloom(Graphics g,GraphicsPath path,Color c,float strength,float radius){for(int i=(int)(radius*2);i>=2;i-=2){int alpha=(int)(strength*Math.Exp(-i*i/(2*radius*radius)));if(alpha>0)Stroke(g,path,A(alpha,c),i);}}
    static GraphicsPath StarPath(float x,float y,float r){return Polygon(P(x,y-r),P(x+r*.22f,y-r*.22f),P(x+r*.72f,y),P(x+r*.22f,y+r*.22f),P(x,y+r),P(x-r*.22f,y+r*.22f),P(x-r*.72f,y),P(x-r*.22f,y-r*.22f));}
    static void Star(Graphics g,float x,float y,float r,Color c){using(var path=StarPath(x,y,r))Fill(g,path,c);}
    static Graphics Setup(Bitmap b,int scale){var g=Graphics.FromImage(b);g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.ScaleTransform(scale,scale);return g;}
    static void Save(Bitmap hi,string name,int w,int h){using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)){using(var g=Graphics.FromImage(b)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(hi,new Rectangle(0,0,w,h),0,0,hi.Width,hi.Height,GraphicsUnit.Pixel);}
        // Jacket stencils retain their exact cutout alpha while their RGB is subdued uniformly.
        if(name.StartsWith("tex_2dmode_jacket_"))for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            Color pixel=b.GetPixel(x,y);if(pixel.A==0)continue;
            b.SetPixel(x,y,Color.FromArgb(pixel.A,(int)Math.Round(pixel.R*.85),(int)Math.Round(pixel.G*.85),(int)Math.Round(pixel.B*.85)));
        }
        b.Save(Path.Combine(Target,name),ImageFormat.Png);}}

    static GraphicsPath Letters(string text,bool medium,bool italic,float tracking){
        var result=new GraphicsPath(FillMode.Winding);var font=medium?Medium:Bold;float cursor=0;
        foreach(char ch in text){ushort index;if(!font.CharacterToGlyphMap.TryGetValue(ch,out index))throw new Exception("Missing glyph: "+ch);
            var outline=font.GetGlyphOutline(index,100,100).GetFlattenedPathGeometry(.2,System.Windows.Media.ToleranceType.Absolute);
            foreach(var figure in outline.Figures){var p=new List<PointF>();p.Add(P(cursor+(float)figure.StartPoint.X,(float)figure.StartPoint.Y));
                foreach(var segment in figure.Segments){var poly=segment as System.Windows.Media.PolyLineSegment;if(poly!=null)foreach(var v in poly.Points)p.Add(P(cursor+(float)v.X,(float)v.Y));else{var line=segment as System.Windows.Media.LineSegment;if(line!=null)p.Add(P(cursor+(float)line.Point.X,(float)line.Point.Y));}}
                if(p.Count>2){result.StartFigure();result.AddLines(p.ToArray());if(figure.IsClosed)result.CloseFigure();}
            }
            cursor+=(float)font.AdvanceWidths[index]*100+tracking;
        }
        if(italic)using(var shear=new Matrix(1,0,-.14f,1,0,0))result.Transform(shear);
        return result;
    }
    static GraphicsPath FitLetters(string text,RectangleF box,bool medium,bool italic,float tracking){
        var path=Letters(text,medium,italic,tracking);RectangleF r=path.GetBounds();
        if(r.Width<.01||r.Height<.01)return path;
        using(var m=new Matrix(box.Width/r.Width,0,0,box.Height/r.Height,box.X-r.X*box.Width/r.Width,box.Y-r.Y*box.Height/r.Height))path.Transform(m);
        return path;
    }
    static void InitDigitMetrics(){
        DigitScales.Clear();DigitTop=float.MaxValue;DigitBottom=float.MinValue;
        foreach(char digit in "0123456789")using(var path=Letters(digit.ToString(),false,false,0)){
            RectangleF bounds=path.GetBounds();DigitTop=Math.Min(DigitTop,bounds.Top);DigitBottom=Math.Max(DigitBottom,bounds.Bottom);
        }
    }
    static float DigitScale(string family){
        float cached;if(DigitScales.TryGetValue(family,out cached))return cached;
        float scale=float.MaxValue;int count=family=="level"?4:family=="countdown"?3:10;
        for(int i=0;i<count;i++){
            string digit=family=="skill"?((i+1)%10).ToString():family=="level"||family=="countdown"?(i+1).ToString():i.ToString();
            string name=family=="score"?"number_48px_"+digit+"_wh.png":family=="skill"?"skillEffect_text_"+i+".png":family=="countdown"?"tex_live_countdown_"+digit+".png":i==0?"skillLv_40px_1_wh.png":"skillLv_40px_shadow_"+digit+".png";
            using(var glyph=Letters(digit,false,false,0)){
                float w=Sizes[name].Width,h=Sizes[name].Height;
                if(family=="level"&&i>0){w-=12;h-=12;}
                float pad=family=="countdown"?Math.Max(8,w*.06f):family=="score"?(digit=="1"?.75f:2.5f):family=="level"?(digit=="1"?1.5f:3f):3f;
                float availableH=h-(family=="skill"?10:family=="countdown"?30:4);
                scale=Math.Min(scale,Math.Min((w-pad*2)/glyph.GetBounds().Width,availableH/(DigitBottom-DigitTop)));
            }
        }
        DigitScales[family]=scale;return scale;
    }
    static GraphicsPath NaturalDigitPath(string digit,RectangleF box,float scale){
        var path=Letters(digit,false,false,0);RectangleF bounds=path.GetBounds();
        // A common font size/baseline per family, using exactly the same scale on both axes.
        using(var transform=new Matrix(scale,0,0,scale,box.X+box.Width*.5f-(bounds.X+bounds.Width*.5f)*scale,box.Y+box.Height*.5f-(DigitTop+DigitBottom)*.5f*scale))path.Transform(transform);
        return path;
    }
    static void Plain(Graphics g,string text,RectangleF box,Color color,bool medium){using(var p=FitLetters(text,box,medium,false,4))Fill(g,p,color);}
    static void NaturalPlain(Graphics g,string text,RectangleF box,Color color){
        using(var path=Letters(text,false,false,4)){
            RectangleF bounds=path.GetBounds();float scale=Math.Min(box.Width/bounds.Width,box.Height/bounds.Height);
            using(var transform=new Matrix(scale,0,0,scale,box.X+(box.Width-bounds.Width*scale)*.5f-bounds.X*scale,box.Y+(box.Height-bounds.Height*scale)*.5f-bounds.Y*scale))path.Transform(transform);
            Fill(g,path,color);
        }
    }
    static void AstralType(Graphics g,string text,RectangleF box,Color accent,bool rainbow,bool monochrome,bool elaborate,bool vivid=false,float naturalScale=0){
        using(var p=naturalScale>0?NaturalDigitPath(text,box,naturalScale):FitLetters(text,box,false,true,text.Length>1?4:0)){
            float rim=Math.Max(.7f,Math.Min(2.2f,box.Height*.022f));
            var st=g.Save();g.TranslateTransform(0,Math.Max(1,box.Height*.028f));Stroke(g,p,monochrome?A(150,Color.White):Mix(accent,Navy,.55f),rim*2);g.Restore(st);
            if(elaborate)Bloom(g,p,monochrome?Color.White:accent,4,Math.Min(9,box.Height*.07f));
            if(rainbow){using(var b=new LinearGradientBrush(P(0,box.Top),P(0,box.Bottom),Pink,Mint)){var blend=new ColorBlend();blend.Positions=new[]{0f,.34f,.68f,1f};blend.Colors=new[]{Mix(Pink,Color.White,.35f),Mix(Purple,Color.White,.4f),Mix(Blue,Color.White,.4f),Mix(Mint,Color.White,.4f)};b.InterpolationColors=blend;g.FillPath(b,p);}}
            else Gradient(g,p,monochrome?Color.White:Mix(accent,Color.White,vivid?.5f:.92f),monochrome?A(225,Color.White):Mix(accent,Color.White,vivid?.05f:.48f));
            Stroke(g,p,monochrome?Color.White:accent,rim);
            // Etched lower facets stay inside the letter strokes, never across their counters.
            if(elaborate&&box.Height>55){var clip=g.Save();g.SetClip(p);Line(g,monochrome?A(100,Color.White):A(100,accent),1,P(box.Left,box.Top+box.Height*.71f),P(box.Right,box.Top+box.Height*.71f));
                for(float x=box.Left+17;x<box.Right;x+=39)Star(g,x,box.Top+box.Height*.77f,2.2f,monochrome?Color.White:A(160,accent));g.Restore(clip);}
        }
    }
    static void Constellation(Graphics g,RectangleF r,Color color,int seed,int count){
        var rng=new Random(seed);var points=new List<PointF>();
        for(int i=0;i<count;i++){PointF p=P(r.X+4+(float)rng.NextDouble()*Math.Max(1,r.Width-8),r.Y+4+(float)rng.NextDouble()*Math.Max(1,r.Height-8));points.Add(p);
            if(i%4==0)Star(g,p.X,p.Y,2+(float)rng.NextDouble()*2,A(175,color));else using(var b=new SolidBrush(A(110+rng.Next(80),color)))g.FillEllipse(b,p.X-.7f,p.Y-.7f,1.4f,1.4f);
        }
        for(int i=0;i<points.Count;i++){int best=-1;float dmin=r.Width*r.Width*.1f;for(int j=0;j<i;j++){float dx=points[j].X-points[i].X,dy=points[j].Y-points[i].Y,d=dx*dx+dy*dy;if(d<dmin){dmin=d;best=j;}}if(best>=0&&i%5!=0)Line(g,A(65,color),.7f,points[i],points[best]);}
    }
    static GraphicsPath RR(float x,float y,float w,float h,float r){
        r=Math.Min(r,Math.Min(w,h)/2);float d=r*2;var p=new GraphicsPath();
        p.AddArc(x,y,d,d,180,90);p.AddArc(x+w-d,y,d,d,270,90);p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();return p;
    }
    static void Diamond(Graphics g,float x,float y,float hw,float hh,Color fill,Color edge){
        using(var p=Polygon(P(x,y-hh),P(x+hw,y),P(x,y+hh),P(x-hw,y))){if(edge.A>0)Stroke(g,p,edge,2);Fill(g,p,fill);}
    }
    // The note skin's material (Tools/Notes/draw_skin.ps1): a saturated vertical gradient, a darker lower
    // third, a gloss band on the upper half and a frame that runs from white into a pale tint of the glass.
    static void PrismGlass(Graphics g,float x,float y,float w,float h,float r,Color top,Color mid,Color bot,float frame){
        Color deep=Mix(bot,C("#1b2247"),.55f);
        using(var face=RR(x,y,w,h,r)){
            using(var b=new LinearGradientBrush(P(0,y-1),P(0,y+h+1),top,bot)){b.InterpolationColors=new ColorBlend{Positions=new[]{0f,.5f,1f},Colors=new[]{top,mid,bot}};g.FillPath(b,face);}
            var clip=g.Save();g.SetClip(face);
            using(var b=new SolidBrush(A(54,deep)))g.FillRectangle(b,x,y+h*.64f,w,h*.36f);
            g.Restore(clip);
        }
        float inset=Math.Max(1.5f,h*.11f)+frame*.5f;
        using(var gloss=RR(x+inset,y+inset,w-inset*2,h*.38f,Math.Max(1,r-inset)))
        using(var b=new LinearGradientBrush(P(0,y+inset-1),P(0,y+inset+h*.38f+1),A(150,Color.White),A(30,Color.White)))g.FillPath(b,gloss);
        if(frame>0)using(var p=RR(x+frame/2,y+frame/2,w-frame,h-frame,r-frame/2))Rim(g,p,frame,245,90,Color.White,Mix(top,Color.White,.6f),Mix(bot,Color.White,.38f));
    }
    static void Compass(Graphics g,float x,float y,float radius,Color c,bool fill){
        var points=new PointF[12];for(int i=0;i<12;i++){double a=-Math.PI/2+i*Math.PI/6;float r=i%2==0?radius:radius*.63f;points[i]=P(x+(float)Math.Cos(a)*r,y+(float)Math.Sin(a)*r);}
        using(var p=Polygon(points)){if(fill)Gradient(g,p,Mix(c,Color.White,.2f),c);Stroke(g,p,Mix(c,Color.White,.8f),1.2f);}
        Star(g,x,y,radius*.35f,Color.White);
    }
    static void Heart(Graphics g,RectangleF r,Color c){using(var p=new GraphicsPath()){
        // A symmetric glass heart: saturated gradient, a gloss spot on the left lobe and a white-to-tint frame.
        float cx=r.X+r.Width*.5f,w=r.Width,h=r.Height;
        p.AddBezier(cx,r.Y+h*.26f,cx-w*.14f,r.Y-h*.06f,r.X-w*.04f,r.Y+h*.02f,r.X+w*.03f,r.Y+h*.36f);
        p.AddBezier(r.X+w*.03f,r.Y+h*.36f,r.X+w*.08f,r.Y+h*.62f,cx-w*.2f,r.Y+h*.8f,cx,r.Bottom);
        p.AddBezier(cx,r.Bottom,cx+w*.2f,r.Y+h*.8f,r.Right-w*.08f,r.Y+h*.62f,r.Right-w*.03f,r.Y+h*.36f);
        p.AddBezier(r.Right-w*.03f,r.Y+h*.36f,r.Right+w*.04f,r.Y+h*.02f,cx+w*.14f,r.Y-h*.06f,cx,r.Y+h*.26f);p.CloseFigure();
        Gradient(g,p,Mix(c,Color.White,.4f),Mix(c,C("#3a2f6b"),.18f));
        var clip=g.Save();g.SetClip(p);
        using(var b=new SolidBrush(A(120,Color.White)))g.FillEllipse(b,r.X+w*.14f,r.Y+h*.14f,w*.27f,h*.2f);
        g.Restore(clip);
        Rim(g,p,1.6f,245,90,Color.White,Mix(c,Color.White,.45f));
    }}

    static readonly HashSet<string> Keep=new HashSet<string>{"16_9_white.png","bg_base_wh.png","bg_base_ingame_wh.png","bg_base_ingame_wh_2.png","white6x6.png","bg_hiLight_mask.png","mask_line.png"};
    static readonly Rectangle[] SkillAtlasSlots = {
        new Rectangle(9, 8, 17, 32),
        new Rectangle(40, 7, 24, 35),
        new Rectangle(80, 6, 25, 35),
        new Rectangle(120, 7, 26, 33),
        new Rectangle(162, 7, 25, 34),
        new Rectangle(201, 6, 26, 35),
        new Rectangle(242, 7, 23, 33),
        new Rectangle(281, 6, 26, 35),
        new Rectangle(321, 5, 27, 36),
        new Rectangle(362, 5, 27, 36),
        new Rectangle(404, 32, 8, 8),
        new Rectangle(428, 12, 29, 28),
        new Rectangle(473, 14, 29, 26)
    };
    static readonly Rectangle[] SkillPunctuation = { new Rectangle(3, 32, 8, 8), new Rectangle(4, 9, 29, 28), new Rectangle(4, 9, 29, 25) };
    static bool TextAsset(Graphics g,string n,int w,int h){
        if(n.StartsWith("judge_")&&!n.StartsWith("judge_timing")){
            string text=n.Substring(6).Replace(".png","").ToUpperInvariant();
            Color c=text=="AUTO"?Gold:text=="BAD"?Mint:text=="GOOD"?Blue:text=="GREAT"?Purple:text=="MISS"?C("#c3cddd"):Pink;
            AstralType(g,text,new RectangleF(5,11,w-10,h-29),c,text=="PERFECT",false,true,true);
            Line(g,A(160,c),.8f,P(w*.17f,h-8),P(w*.45f,h-8));Line(g,A(160,c),.8f,P(w*.55f,h-8),P(w*.83f,h-8));Star(g,w*.5f,h-8,3.5f,Mix(c,Color.White,.5f));
            return true;
        }
        if(n.StartsWith("judge_timing")){
            string text=n.Substring(13).Replace(".png","").ToUpperInvariant();Color c=text=="FAST"?Blue:text=="LATE"?Pink:Gold;
            using(var p=Bevel(2,13,w-4,h-16,8)){Fill(g,p,A(105,c));Stroke(g,p,A(210,Color.White),.8f);}
            Star(g,15,h*.56f,6,Color.White);AstralType(g,text,new RectangleF(31,5,w-42,h-16),c,false,true,false);return true;
        }
        if(n=="label_autolive.png"){
            using(var p=Bevel(3,8,w-6,h-16,15)){Gradient(g,p,A(230,Navy),A(230,C("#174452")));Stroke(g,p,Mint,1.6f);}
            Compass(g,31,h*.5f,19,Mint,false);AstralType(g,"AUTO LIVE",new RectangleF(65,21,w-84,29),Mint,false,false,false);return true;
        }
        if(n.StartsWith("number_48px")){
            string digit=Regex.Match(n,@"(\d)(?:_wh)?\.png$").Groups[1].Value;bool shadow=n.Contains("shadow");
            RectangleF box=new RectangleF(0,0,w,h);
            if(shadow){var face=Sizes["number_48px_"+digit+"_wh.png"];box=new RectangleF((w-face.Width)/2f,(h-face.Height)/2f,face.Width,face.Height);}
            using(var p=NaturalDigitPath(digit,box,DigitScale("score"))){
                if(shadow){Bloom(g,p,Color.Black,13,5);Fill(g,p,A(185,Color.Black));}else Fill(g,p,Color.White);
            }return true;
        }
        if(n.StartsWith("number_combo")){
            string digit=Regex.Match(n,@"_(\d)_(?:pr|rbw)\.png$").Groups[1].Value;
            AstralType(g,digit,new RectangleF(6,5,w-12,h-15),Purple,n.Contains("_ap_"),false,true);return true;
        }
        if(Regex.IsMatch(n,@"^eff_\d_150")){
            string digit=n.Substring(4,1);
            var face=Sizes["number_combo_154px_"+digit+"_pr.png"];
            using(var p=FitLetters(digit,new RectangleF((w-face.Width)/2f+6,(h-face.Height)/2f+5,face.Width-12,face.Height-15),false,true,0)){
                Bloom(g,p,Color.White,5.5f,25);Fill(g,p,A(65,Color.White));
            }return true;
        }
        if(n=="eff_combo.png"){
            var face=Sizes["txt_combo_pr.png"];
            using(var p=FitLetters("COMBO",new RectangleF((w-face.Width)/2f+5,(h-face.Height)/2f+6,face.Width-10,face.Height-15),false,true,4)){Bloom(g,p,Color.White,5,16);Fill(g,p,A(80,Color.White));}return true;
        }
        if(n.StartsWith("txt_combo")){
            AstralType(g,"COMBO",new RectangleF(5,6,w-10,h-15),Purple,n.Contains("_ap_"),false,false);return true;
        }
        if(n=="skillEffect_text.png"){
            string[] chars={"1","2","3","4","5","6","7","8","9","0",".","秒","%"};
            for(int i=0;i<SkillAtlasSlots.Length;i++){
                Rectangle r=SkillAtlasSlots[i];
                if(i<10){using(var p=NaturalDigitPath(chars[i],new RectangleF(r.X,0,r.Width,h),DigitScale("skill")))Fill(g,p,Color.White);}
                else using(var p=FitLetters(chars[i],r,true,false,0))Fill(g,p,Color.White);
            }return true;
        }
        if(n.StartsWith("skillEffect_text_")){
            int index=int.Parse(Regex.Match(n,@"_(\d+)\.png$").Groups[1].Value);string text=index<10?((index+1)%10).ToString():index==10?".":index==11?"秒":"%";
            if(index<10){using(var p=NaturalDigitPath(text,new RectangleF(0,0,w,h),DigitScale("skill")))Fill(g,p,Color.White);}
            else{Rectangle r=SkillPunctuation[index-10];using(var p=FitLetters(text,r,true,false,0))Fill(g,p,Color.White);}
            return true;
        }
        if(n.StartsWith("skillLv")){
            bool shadow=n.Contains("shadow");string text=n.Contains("_Lv")?"Lv.":Regex.Match(n,@"_(\d)(?:_wh)?\.png$").Groups[1].Value;
            RectangleF box=shadow?new RectangleF(6,6,w-12,h-12):new RectangleF(1,1,w-2,h-2);
            if(text!="Lv."){
                box=shadow?new RectangleF(6,6,w-12,h-12):new RectangleF(0,0,w,h);
                if(shadow&&text=="1"){var face=Sizes["skillLv_40px_1_wh.png"];box=new RectangleF((w-face.Width)*.5f,(h-face.Height)*.5f,face.Width,face.Height);}
            }
            using(var p=text=="Lv."?FitLetters(text,box,true,false,0):NaturalDigitPath(text,box,DigitScale("level"))){if(shadow){Bloom(g,p,Color.Black,10,5);Fill(g,p,A(185,Color.Black));}else Fill(g,p,Color.White);}return true;
        }
        if(n.StartsWith("txt_skill")){
            string text=n.Contains("judge")?"判定強化":n.Contains("life")?"ライフ回復":"スコアUP";Color c=n.Contains("judge")?Purple:n.Contains("life")?Mint:Blue;
            AstralType(g,text,new RectangleF(2,4,w-4,h-10),c,false,false,false);return true;
        }
        if(n=="txt_cutin_skill_wh.png"||n=="txt_scoreRank_ingame_wh.png"){
            Plain(g,n.Contains("scoreRank")?"SCORERANK":"SKILL",new RectangleF(1,1,w-2,h-2),Color.White,true);return true;
        }
        if(Regex.IsMatch(n,@"^[ABCS]\.png$")||n.StartsWith("score_")){
            string text=n.StartsWith("score_")?n.Substring(6,1):n.Substring(0,1);bool large=n.StartsWith("score_");
            Color c=text=="S"?Gold:text=="A"?Purple:text=="B"?Blue:Mint;
            if(large){AstralType(g,text,new RectangleF(6,6,w-12,h-20),c,false,false,false);Line(g,A(180,c),1,P(w*.2f,h-6),P(w*.8f,h-6));Star(g,w*.5f,h-6,2.6f,c);}
            else{using(var p=RR(1.5f,1.5f,w-3,h-3,8)){Gradient(g,p,A(240,C("#22355a")),A(240,C("#0b1328")));Rim(g,p,1.5f,240,60,Mix(c,Color.White,.62f),Lt(c),Mix(c,Purple,.4f));}Plain(g,text,new RectangleF(8,7,w-16,h-14),Mix(c,Color.White,.3f),false);}return true;
        }
        if(n.StartsWith("tex_live_countdown")){
            string digit=Regex.Match(n,@"_(\d)\.png$").Groups[1].Value;
            float pad=Math.Max(8,w*.06f);
            AstralType(g,digit,new RectangleF(pad,10,w-pad*2,h-30),Color.White,false,true,true,false,DigitScale("countdown"));
            Line(g,A(120,Color.White),1.2f,P(w*.2f,h-9),P(w*.8f,h-9));Star(g,w*.5f,h-9,3,Color.White);return true;
        }
        if(n.StartsWith("text_")){
            float cx=w*.5f,cy=h*.5f;using(var p=Polygon(P(cx-2,3),P(cx+2,3),P(cx+2,cy-2),P(w-3,cy-2),P(w-3,cy+2),P(cx+2,cy+2),P(cx+2,h-3),P(cx-2,h-3),P(cx-2,cy+2),P(3,cy+2),P(3,cy-2),P(cx-2,cy-2))){Fill(g,p,Color.White);if(n.Contains("outline"))Stroke(g,p,Color.White,3);}return true;
        }
        return false;
    }
    static bool HudAsset(Graphics g,string n,int w,int h){
        if(n.StartsWith("btn_")){
            bool disabled=n.Contains("disable");Color c=n.StartsWith("btn_stop")?Gold:disabled?C("#8b93a7"):Mint;
            // "Prism" HUD: a dark glass disc with a gradient rim, a thin accent ring and two gems.
            float cx=w/2f,cy=h/2f,r=40;
            using(var disc=new GraphicsPath()){
                disc.AddEllipse(cx-r,cy-r,r*2,r*2);
                if(!disabled)Bloom(g,disc,c,5,5);
                Gradient(g,disc,A(disabled?130:238,C("#22355a")),A(disabled?130:238,C("#0b1328")));
                var clip=g.Save();g.SetClip(disc);
                using(var b=new SolidBrush(A(disabled?12:30,Color.White)))g.FillEllipse(b,cx-r*1.3f,cy-r*2.05f,r*2.6f,r*2.2f);
                g.Restore(clip);
                Rim(g,disc,3,disabled?110:245,45,RimButton);
            }
            // The inner ring repeats the rim's gradient.
            using(var ring=new GraphicsPath()){ring.AddEllipse(cx-33.5f,cy-33.5f,67,67);Rim(g,ring,1.4f,disabled?70:220,45,RimButton);}
            foreach(float gx in new[]{cx-r,cx+r})Diamond(g,gx,cy,4,7,A(disabled?130:255,Color.White),A(disabled?60:170,Navy));
            Color ink=A(disabled?100:255,Color.White);
            if(n.Contains("skip")){
                using(var p=Polygon(P(29,30),P(48,48),P(29,66)))Fill(g,p,ink);
                using(var p=Polygon(P(48,30),P(67,48),P(48,66)))Fill(g,p,ink);
                Line(g,ink,3,P(70,31),P(70,65));
            }else foreach(int x in new[]{33,53})using(var p=RR(x,30,10,36,3))Fill(g,p,ink);
            return true;
        }
        if(n=="bg_lifeGauge.png"||n=="bg_scoreGauge.png"){
            bool life=n.Contains("life");
            // A dark glass capsule holds the bar; a saturated glass tag (the note skin's material) names it.
            // Tag and capsule ends stay inside the manifest's fixed left/right borders.
            using(var p=RR(2,28,w-4,46,23)){
                Gradient(g,p,A(232,C("#1d2d4d")),A(238,C("#0a1226")));
                var clip=g.Save();g.SetClip(p);
                using(var b=new SolidBrush(A(20,Color.White)))g.FillRectangle(b,0,28,w,17);
                g.Restore(clip);
                Rim(g,p,1.6f,240,0,life?RimLife:RimScore);
            }
            using(var p=RR(4.8f,30.8f,w-9.6f,40.4f,20.2f))Stroke(g,p,A(95,life?Mint:Purple),1);
            Color top=life?Mix(Mint,Color.White,.35f):Mix(Purple,Color.White,.15f),mid=life?Mint:Mix(Purple,Blue,.5f),bot=life?Mix(Mint,C("#3a2f6b"),.14f):Blue;
            PrismGlass(g,8,2,126,31,9,top,mid,bot,1.8f);
            Diamond(g,23,17.5f,5,9,Color.White,A(150,Mix(bot,C("#1b2247"),.55f)));
            if(life)NaturalPlain(g,"LIFE",new RectangleF(38,10,83,16),Navy);
            else Plain(g,"SCORE",new RectangleF(38,10,83,16),Navy,false);
            return true;
        }
        if(n.StartsWith("bar_lifeGauge")||n.StartsWith("gauge_base")){
            if(n.StartsWith("gauge_base")){
                using(var p=RR(.5f,.5f,w-1,h-1,7.5f)){Gradient(g,p,A(245,C("#070c1c")),A(245,C("#16223d")));Stroke(g,p,A(70,Color.White),.8f);}
                return true;
            }
            Color c=n.Contains("rd")?Pink:Mint;
            PrismGlass(g,.5f,.5f,w-1,h-1,7.5f,Mix(c,Color.White,.45f),c,Mix(c,C("#3a2f6b"),.16f),1.2f);return true;
        }
        if(n=="gauge_bar_ingame_freeLive_s.png"){
            using(var p=RR(1,1,w-2,h-2,11)){
                using(var b=new LinearGradientBrush(P(0,0),P(w,0),Mint,Pink)){
                    var blend=new ColorBlend();blend.Positions=new[]{0f,.32f,.66f,1f};blend.Colors=new[]{Mint,Blue,Purple,Pink};b.InterpolationColors=blend;g.FillPath(b,p);
                }
                var clip=g.Save();g.SetClip(p);
                using(var b=new LinearGradientBrush(P(0,2),P(0,12.5f),A(150,Color.White),A(28,Color.White)))g.FillRectangle(b,0,3.2f,w,8.6f);
                using(var b=new SolidBrush(A(48,C("#1b2247"))))g.FillRectangle(b,0,h*.66f,w,h);
                g.Restore(clip);Rim(g,p,1.4f,240,0,Mix(Mint,Color.White,.62f),Mix(Blue,Color.White,.62f),Mix(Purple,Color.White,.62f),Mix(Pink,Color.White,.62f));
            }return true;
        }
        if(n=="icon_heart_gn.png"){Heart(g,new RectangleF(2,3,w-4,h-6),Mint);return true;}
        if(n=="meter_mark.png"){
            // A kite-shaped gem pointing down at its threshold.
            using(var p=Polygon(P(w*.5f,2),P(w-4,9),P(w*.5f,h-2),P(4,9))){Stroke(g,p,A(170,Navy),2);Gradient(g,p,Color.White,Mix(Blue,Color.White,.55f));}
            return true;
        }
        if(n.StartsWith("effect_")){
            // Glow that hugs the gauge capsule.
            Color c=n.Contains("score")?Gold:Mint;float top=h*.28f,bottom=h*.78f;
            using(var p=RR(15,top,w-23,bottom-top,(bottom-top)/2)){Bloom(g,p,c,6,Math.Min(17,h*.11f));Stroke(g,p,A(235,c),1.7f);Stroke(g,p,A(150,Color.White),.7f);}
            return true;
        }
        if(n=="bg_cutin_skill_judge.png"){
            bool score=n.Contains("score"),panel=n.StartsWith("bg_");Color c=score?Gold:panel?Purple:Mint;
            float left=score?76:36,top=h*.28f,bottom=h*.78f;
            using(var p=Polygon(P(15,h*.5f),P(left,top),P(w-22,top),P(w-8,h*.5f),P(w-34,bottom),P(left,bottom))){
                if(panel){Gradient(g,p,A(225,Navy),A(200,C("#362654")));var clip=g.Save();g.SetClip(p);Constellation(g,new RectangleF(10,top,w-20,bottom-top),Mix(c,Color.White,.65f),7,22);g.Restore(clip);}
                else Bloom(g,p,c,6,Math.Min(17,h*.11f));
                Stroke(g,p,A(panel?190:235,c),1.7f);Stroke(g,p,A(140,Color.White),.7f);
            }
            if(panel)using(var p=Bevel(66,5,112,34,7)){Gradient(g,p,A(230,C("#342749")),A(225,Navy));Stroke(g,p,A(180,c),1);}
            for(int x=(int)left+20;x<w-25;x+=32)Line(g,A(130,c),1,P(x,top-4),P(x+3,top));
            Star(g,left-18,h*.52f,9,Mix(c,Color.White,.7f));return true;
        }
        if(n.StartsWith("cardFrame")){
            Color c=n.Contains("_1")?Mint:n.Contains("_2")?Blue:n.Contains("_3")?Gold:n.Contains("_4")?Purple:Pink;
            using(var p=Bevel(3,1,w-6,17,5)){Gradient(g,p,Mix(c,Color.White,.7f),c);Stroke(g,p,A(220,Color.White),.75f);}
            using(var p=Bevel(4,h-10,w-8,8,3)){Gradient(g,p,c,Mix(c,Navy,.25f));Stroke(g,p,A(170,Color.White),.6f);}
            Star(g,17,9,4,Color.White);
            for(int x=40;x<w-12;x+=13)Line(g,A(140,Color.White),.9f,P(x,4),P(x-4,13));
            Line(g,A(170,c),1,P(3,23),P(3,37));Line(g,A(170,c),1,P(w-3,h-35),P(w-3,h-14));return true;
        }
        if(n.StartsWith("icon_skill")){
            if(n.Contains("Support")){using(var p=Polygon(P(w-3,2),P(w*.5f,h*.5f),P(w-3,h-2),P(w*.5f-7,h-2),P(2,h*.5f),P(w*.5f-7,2)))Gradient(g,p,Mix(Blue,Color.White,.6f),Blue);return true;}
            Color c=n.Contains("judge")?Purple:n.Contains("life")?Mint:Blue;
            float cx=w*.5f,cy=h*.5f,r=Math.Min(w,h)*.44f;
            using(var p=new GraphicsPath()){p.AddEllipse(cx-r,cy-r,r*2,r*2);Gradient(g,p,A(220,Navy),A(220,Mix(Navy,c,.2f)));Stroke(g,p,c,1.6f);}
            for(int i=0;i<12;i++){double a=i*Math.PI/6;Line(g,A(170,c),1,P(cx+(float)Math.Cos(a)*r*.83f,cy+(float)Math.Sin(a)*r*.83f),P(cx+(float)Math.Cos(a)*r*.94f,cy+(float)Math.Sin(a)*r*.94f));}
            if(n.Contains("life")){Heart(g,new RectangleF(cx-r*.55f,cy-r*.58f,r*1.1f,r*1.05f),c);Line(g,Color.White,3,P(cx+r*.42f,cy+r*.05f),P(cx+r*.42f,cy+r*.62f));Line(g,Color.White,3,P(cx+r*.14f,cy+r*.34f),P(cx+r*.7f,cy+r*.34f));}
            else if(n.Contains("judge")){Compass(g,cx,cy,r*.6f,c,false);Line(g,Color.White,3,P(cx-10,cy),P(cx+10,cy));Line(g,Color.White,3,P(cx,cy-10),P(cx,cy+10));}
            else using(var p=Polygon(P(cx,cy-r*.61f),P(cx+r*.54f,cy),P(cx+r*.2f,cy),P(cx+r*.2f,cy+r*.53f),P(cx-r*.2f,cy+r*.53f),P(cx-r*.2f,cy),P(cx-r*.54f,cy)))Gradient(g,p,Color.White,c);
            return true;
        }
        return false;
    }
    static void OrbitalPanel(Graphics g,GraphicsPath panel,Color accent,int seed,bool overlay){
        RectangleF r=panel.GetBounds();
        Gradient(g,panel,A(overlay?28:235,Navy),A(overlay?40:245,C("#242440")));
        var st=g.Save();g.SetClip(panel);
        using(var p=new Pen(A(overlay?55:70,accent),.8f)){
            g.DrawEllipse(p,r.X+r.Width*.1f,r.Y+r.Height*.12f,r.Width*.8f,r.Height*.8f);
            g.DrawEllipse(p,r.X+r.Width*.32f,r.Y-r.Height*.2f,r.Width*.36f,r.Height*1.4f);
            g.DrawEllipse(p,r.X-r.Width*.25f,r.Y+r.Height*.5f,r.Width*1.5f,r.Height*.36f);
        }
        Constellation(g,new RectangleF(r.X+8,r.Y+12,r.Width-16,r.Height-24),Mix(accent,Color.White,.7f),seed,(int)(r.Width*r.Height/6500)+9);
        for(float x=r.X+15;x<r.Right-10;x+=20)Line(g,A(65,accent),.7f,P(x,r.Y+8),P(x,r.Y+12));
        g.Restore(st);Stroke(g,panel,A(overlay?135:195,accent),overlay?1:1.6f);
        var inner=g.Save();g.SetClip(panel);Line(g,A(130,Color.White),.8f,P(r.X+14,r.Y+4),P(r.Right-14,r.Y+4));g.Restore(inner);
    }
    static PointF MeshPoint(PointF[] corners,float u,float v){
        return P(corners[0].X*(1-u)*(1-v)+corners[1].X*u*(1-v)+corners[2].X*u*v+corners[3].X*(1-u)*v,
                 corners[0].Y*(1-u)*(1-v)+corners[1].Y*u*(1-v)+corners[2].Y*u*v+corners[3].Y*(1-u)*v);
    }
    static PointF[] JacketPlane(string name){
        // Hand-placed corners in texture pixels, clockwise from the upper left.
        switch(name){
            case "tex_2dmode_jacket_mirror_l.png": return new[] { P(2, 20), P(293, 2), P(306, 174), P(11, 193) };
            case "tex_2dmode_jacket_mirror_main.png": return new[] { P(2, 3), P(471, 3), P(460, 425), P(13, 425) };
            case "tex_2dmode_jacket_mirror_r.png": return new[] { P(10, 2), P(241, 10), P(237, 126), P(1, 118) };
            case "tex_2dmode_jacket_nml_l.png": return new[] { P(2, 15), P(319, 2), P(319, 195), P(7, 208) };
            case "tex_2dmode_jacket_nml_main.png": return new[] { P(2, 4), P(494, 4), P(483, 470), P(13, 470) };
            case "tex_2dmode_jacket_nml_r.png": return new[] { P(2, 2), P(236, 7), P(233, 132), P(2, 127) };
            default: throw new ArgumentException("Unknown jacket mesh: "+name);
        }
    }
    static GraphicsPath CutQuad(PointF[] corners,float cut){
        var points=new List<PointF>();
        for(int i=0;i<4;i++){
            var p=corners[i];var previous=corners[(i+3)%4];var next=corners[(i+1)%4];
            double before=Math.Sqrt((p.X-previous.X)*(p.X-previous.X)+(p.Y-previous.Y)*(p.Y-previous.Y));
            double after=Math.Sqrt((p.X-next.X)*(p.X-next.X)+(p.Y-next.Y)*(p.Y-next.Y));
            float amount=(float)Math.Min(cut,Math.Min(before,after)*.2);
            points.Add(P(p.X+(previous.X-p.X)*amount/(float)before,p.Y+(previous.Y-p.Y)*amount/(float)before));
            points.Add(P(p.X+(next.X-p.X)*amount/(float)after,p.Y+(next.Y-p.Y)*amount/(float)after));
        }
        return Polygon(points.ToArray());
    }
    static GraphicsPath JacketOutline(string meshName,PointF[] corners){
        return CutQuad(corners,meshName.Contains("_main")?15:8);
    }
    static GraphicsPath JacketMaskPath(string maskName){
        string meshName="tex_"+maskName.Substring("mask_".Length);
        var outline=JacketOutline(meshName,JacketPlane(meshName));
        // The sprites share a center pivot. The normal main mask has one less
        // pixel of padding on each side than its texture.
        Size mask=Sizes[maskName],mesh=Sizes[meshName];
        using(var offset=new Matrix()){
            offset.Translate((mask.Width-mesh.Width)*.5f,(mask.Height-mesh.Height)*.5f);
            outline.Transform(offset);
        }
        return outline;
    }
    static PointF[] OutsetQuad(PointF[] corners,float side,float above,float below){
        var normals=new PointF[4];var constants=new float[4];float[] margins={above,side,below,side};
        for(int i=0;i<4;i++){
            var a=corners[i];var b=corners[(i+1)%4];float dx=b.X-a.X,dy=b.Y-a.Y,length=(float)Math.Sqrt(dx*dx+dy*dy);
            normals[i]=P(dy/length,-dx/length);constants[i]=normals[i].X*a.X+normals[i].Y*a.Y+margins[i];
        }
        var result=new PointF[4];
        for(int i=0;i<4;i++){
            int previous=(i+3)%4;var a=normals[previous];var b=normals[i];float denominator=a.X*b.Y-a.Y*b.X;
            result[i]=P((constants[previous]*b.Y-constants[i]*a.Y)/denominator,(a.X*constants[i]-b.X*constants[previous])/denominator);
        }
        return result;
    }
    static void JacketGrid(Graphics g,GraphicsPath panel,bool main,PointF[] corners){
        RectangleF r=panel.GetBounds();Color accent=main?Purple:Blue;
        var state=g.Save();g.SetClip(panel);
        // Invert the line drawing into a gray stencil: old empty cells are filled,
        // while grid lines, orbital tracks and constellation marks become transparent cuts.
        Fill(g,panel,A(main?126:112,C("#a0a0a0")));
        g.CompositingMode=CompositingMode.SourceCopy;
        int cols=Math.Max(12,(int)(r.Width/11)),rows=Math.Max(8,(int)(r.Height/11));
        for(int i=-1;i<=cols+1;i++){
            bool major=i%4==0;float u=i/(float)cols;
            Line(g,Color.Transparent,major?1.0f:.6f,MeshPoint(corners,u,-.06f),MeshPoint(corners,u,1.06f));
        }
        for(int i=-1;i<=rows+1;i++){
            bool major=i%4==0;float v=i/(float)rows;
            Line(g,Color.Transparent,major?1.0f:.6f,MeshPoint(corners,-.06f,v),MeshPoint(corners,1.06f,v));
        }
        for(int y=2;y<rows;y+=4)for(int x=2;x<cols;x+=4){
            PointF p=MeshPoint(corners,x/(float)cols,y/(float)rows);
            using(var marker=Polygon(P(p.X,p.Y-1.7f),P(p.X+1.7f,p.Y),P(p.X,p.Y+1.7f),P(p.X-1.7f,p.Y)))Fill(g,marker,Color.Transparent);
        }
        // Keep the celestial drawing legible as slightly wider engraved cuts through the gray cells.
        using(var orbits=new GraphicsPath()){
            orbits.AddEllipse(r.X+r.Width*.12f,r.Y+r.Height*.12f,r.Width*.76f,r.Height*.76f);
            orbits.AddEllipse(r.X-r.Width*.13f,r.Y+r.Height*.34f,r.Width*1.26f,r.Height*.31f);
            orbits.AddEllipse(r.X+r.Width*.35f,r.Y-r.Height*.19f,r.Width*.30f,r.Height*1.38f);
            using(var rotate=new Matrix()){rotate.RotateAt(-16,P(r.X+r.Width*.5f,r.Y+r.Height*.5f));orbits.Transform(rotate);}
            Stroke(g,orbits,Color.Transparent,1.6f);
        }
        var stars=new[]{MeshPoint(corners,.14f,.34f),MeshPoint(corners,.29f,.20f),MeshPoint(corners,.48f,.41f),MeshPoint(corners,.66f,.18f),MeshPoint(corners,.83f,.46f),MeshPoint(corners,.66f,.73f),MeshPoint(corners,.35f,.81f),MeshPoint(corners,.18f,.65f)};
        int[,] links={{0,1},{1,2},{2,3},{2,4},{4,5},{5,6},{6,7}};
        for(int i=0;i<links.GetLength(0);i++){
            var a=stars[links[i,0]];var b=stars[links[i,1]];
            Line(g,Color.Transparent,1.2f,a,b);
        }
        foreach(var p in stars){
            using(var mark=StarPath(p.X,p.Y,main?3.9f:3.0f))Fill(g,mark,Color.Transparent);
        }
        g.CompositingMode=CompositingMode.SourceOver;Stroke(g,panel,A(150,C("#a0a0a0")),.8f);g.Restore(state);
    }
    public static RectangleF JacketInWindow(int width,int height,int jacketWidth,int jacketHeight,bool mirror){
        // Calibrated from the supplied in-game view and the source artwork's content area.
        // The normal header and mirrored footer are outside the actual jacket plane.
        float coverWidth=jacketWidth*1.03f,coverHeight=jacketHeight*1.015f;
        return new RectangleF((width-coverWidth)*.5f,height*.5f+(mirror?15f:24.5f)-coverHeight*.5f,coverWidth,coverHeight);
    }
    static void JacketWindow(Graphics g,string name,int w,int h){
        bool mirror=name.Contains("mirror");Color accent=mirror?Purple:Blue;
        string jacketName="tex_2dmode_jacket_"+(mirror?"mirror":"nml")+"_main.png";RectangleF cover;
        var corners=JacketPlane(jacketName);
        {
            var jacket=Sizes[jacketName];
            cover=JacketInWindow(w,h,jacket.Width,jacket.Height,mirror);
            for(int i=0;i<4;i++)corners[i]=P(cover.X+corners[i].X*cover.Width/jacket.Width,cover.Y+corners[i].Y*cover.Height/jacket.Height);
        }
        float above=mirror?36:96,below=mirror?96:36;
        Color rim=Mix(C("#b4bac6"),accent,.25f);
        using(var outline=CutQuad(OutsetQuad(corners,36,above,below),16)){
            Gradient(g,outline,A(235,C("#182234")),A(240,C("#171c2d")));
            Bloom(g,outline,accent,1,5);Stroke(g,outline,A(210,rim),1.5f);
        }
        using(var outline=CutQuad(OutsetQuad(corners,33,above-3,below-3),13))Stroke(g,outline,A(65,Color.White),.65f);
        // Reflections carry the wider decorative band below the jacket.
        var a=corners[mirror?2:0];var b=corners[mirror?3:1];float cx=(a.X+b.X)*.5f,cy=(a.Y+b.Y)*.5f+(mirror?48:-48);
        Line(g,A(100,rim),.8f,P(cx-46,cy),P(cx-7,cy));Line(g,A(100,rim),.8f,P(cx+7,cy),P(cx+46,cy));
        Star(g,cx,cy,2.8f,A(190,rim));
    }
    static GraphicsPath LaneOutline(){
        // The 12-lane floor from the horizon (row 8) to the bottom of the sprite rect (row 1171).
        const float top=8,bottom=1171;
        float topHalf=LaneTextureHalf(top),bottomHalf=LaneTextureHalf(bottom);
        return Polygon(P(LaneCx-bottomHalf,bottom),P(LaneCx-topHalf,top),P(LaneCx+topHalf,top),P(LaneCx+bottomHalf,bottom));
    }
    // lane_mask hides notes outside the floor. Its top and bottom fades decide where notes appear, so they
    // are read back from the centre column of the existing mask and kept; only the side edges are redrawn
    // on the lane boundaries.
    static void RenderLaneMask(int w,int h){
        string file=Path.Combine(Target,"lane_mask.png");
        var profile=new Color[h];
        using(var source=new Bitmap(file))for(int y=0;y<h;y++)profile[y]=source.GetPixel(source.Width/2,y);
        using(var image=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            for(int y=0;y<h;y++){
                if(profile[y].A==0)continue;
                float half=LaneTextureHalf(y+.5f);
                for(int x=0;x<w;x++){
                    float coverage=Unit(half+.5f-Math.Abs(x+.5f-LaneCx));
                    int alpha=(int)Math.Round(profile[y].A*coverage);
                    if(alpha>0)image.SetPixel(x,y,Color.FromArgb(alpha,profile[y].R,profile[y].G,profile[y].B));
                }
            }
            image.Save(file,ImageFormat.Png);
        }
    }
    static bool Environment(Graphics g,string n,int w,int h){
        if(n.StartsWith("bg_2dmode")){
            JacketWindow(g,n,w,h);return true;
        }
        if(n.StartsWith("tex_2dmode")){
            var corners=JacketPlane(n);bool main=n.Contains("_main");
            using(var p=JacketOutline(n,corners))JacketGrid(g,p,main,corners);return true;
        }
        if(n.StartsWith("mask_2dmode_jacket")){
            using(var path=JacketMaskPath(n))Fill(g,path,Color.White);return true;
        }
        if(n=="lane_base.png"){
            using(var p=LaneOutline()){
                // Opacity eases out of the horizon (cubic, zero slope where it lands) and levels off at about
                // 67% by 55% of the way down, so the lower half - judge line included - stays nearly flat,
                // drifting only to about 70% at the bottom.
                RectangleF r=p.GetBounds();Color floor=C("#101729");
                const int stops=33;var colors=new Color[stops];var positions=new float[stops];
                for(int i=0;i<stops;i++){
                    float t=i/(float)(stops-1),s=Unit(t/.55f),rise=1-(1-s)*(1-s)*(1-s);
                    float alpha=30+140*rise+8*Smooth((t-.55f)/.45f);
                    colors[i]=A((int)Math.Round(alpha),Mix(Navy,floor,s));positions[i]=t;
                }
                using(var b=new LinearGradientBrush(P(0,r.Top),P(0,r.Bottom+.01f),Color.Black,Color.Black)){
                    b.InterpolationColors=new ColorBlend{Colors=colors,Positions=positions};
                    g.FillPath(b,p);
                }
                var st=g.Save();g.SetClip(p);
                Constellation(g,new RectangleF(w*.18f,h*.48f,w*.64f,h*.5f),A(125,Blue),93,35);
                using(var pen=new Pen(A(25,Blue),1.2f))g.DrawEllipse(pen,w*.12f,h*.5f,w*.76f,h*.46f);
                g.Restore(st);
            }return true;
        }
        if(n=="tex_transition_01.png"){
            // Fit the approved 1024 x 1011 drawing to the corrected complete atlas canvas.
            var transitionState=g.Save();g.ScaleTransform(w/1024f,h/1011f);
            Color[] colors={Pink,Blue,Gold,Mint};
            for(int i=0;i<4;i++){
                float x=i*256;var clip=g.Save();g.SetClip(new RectangleF(x,0,256,260));
                using(var p=Polygon(P(x+91,0),P(x+162,0),P(x+256,207),P(x+256,246),P(x+242,259),P(x+14,259),P(x,246),P(x,207))){
                    Gradient(g,p,Mix(colors[i],Color.White,.7f),colors[i]);var st=g.Save();g.SetClip(p);Constellation(g,new RectangleF(x+12,50,232,194),Color.White,34,15);g.Restore(st);Stroke(g,p,A(210,Color.White),1.5f);
                }g.Restore(clip);
            }
            using(var p=new Pen(Color.White,38))g.DrawEllipse(p,18,277,222,222);
            using(var b=new SolidBrush(Color.White))g.FillEllipse(b,261,263,248,248);
            var strip=g.Save();g.SetClip(new RectangleF(514,260,510,245));
            using(var p=Bevel(514,266,510,238,62))using(var b=new LinearGradientBrush(P(514,380),P(1024,380),A(230,Color.White),A(0,Color.White)))g.FillPath(b,p);
            Constellation(g,new RectangleF(548,306,408,120),A(190,Purple),56,14);g.Restore(strip);
            using(var p=new GraphicsPath()){p.AddEllipse(0,500,512,511);using(var b=new PathGradientBrush(p)){b.CenterColor=Color.White;b.SurroundColors=new[]{A(0,Color.White)};g.FillPath(b,p);}}
            using(var p=new GraphicsPath()){p.AddEllipse(520,500,240,240);Gradient(g,p,Mint,C("#138f86"));Stroke(g,p,A(200,Color.White),2);}
            using(var pen=new Pen(A(150,Color.White),1.5f))g.DrawArc(pen,541,521,198,198,-77,335);
            using(var p=Polygon(P(619,568),P(679,621),P(619,674)))Gradient(g,p,Color.White,Mix(Mint,Color.White,.6f));
            using(var p=new GraphicsPath()){p.AddEllipse(762,500,262,262);using(var b=new PathGradientBrush(p)){b.CenterColor=Color.White;b.SurroundColors=new[]{A(0,Purple)};g.FillPath(b,p);}}g.Restore(transitionState);return true;
        }
        if(n=="label_grd_h40_dbl.png"){
            using(var b=new LinearGradientBrush(P(0,0),P(w,0),A(185,C("#253751")),A(0,Navy)))g.FillRectangle(b,0,0,w,h);
            Line(g,A(80,Blue),.8f,P(1,h-2),P(w*.6f,h-2));return true;
        }
        if(n=="grd_append.png"){
            using(var b=new LinearGradientBrush(P(0,0),P(w,0),Blue,Pink))g.FillRectangle(b,0,0,w,h);return true;
        }
        return false;
    }
    static float Unit(float value){return Math.Max(0,Math.Min(1,value));}
    static float Smooth(float value){
        float t=Unit(value);
        return t*t*(3-2*t);
    }
    static void RenderLaneLines(int w,int h){
        // Every second lane boundary (7 lines for 12 lanes), on the code's perspective lines.
        const int count=7;
        using(var image=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            for(int y=0;y<h;y++){
                float depth=(y+.5f)/h;
                float fade=Smooth(depth/.06f)*Smooth((1-depth)/.10f);
                float halfSpan=LaneTextureHalf(y+.5f);
                for(int i=0;i<count;i++){
                    bool outer=i==0||i==count-1;
                    float u=i/(float)(count-1);
                    float cx=LaneCx+(2*u-1)*halfSpan;
                    float width=outer?3.5f:1.9f;
                    // Half the earlier .7 colour mix, so the lines carry about half the saturation.
                    Color tint=Mix(Color.FromArgb(182,182,182),Mix(Blue,Pink,u),.35f);
                    int left=Math.Max(0,(int)Math.Floor(cx-width));
                    int right=Math.Min(w-1,(int)Math.Ceiling(cx+width));
                    for(int x=left;x<=right;x++){
                        float coverage=Unit(width*.5f+.5f-Math.Abs(x+.5f-cx));
                        int alpha=(int)Math.Round((outer?128:64)*fade*coverage);
                        if(alpha>0)image.SetPixel(x,y,A(alpha,tint));
                    }
                }
            }
            image.Save(Path.Combine(Target,"lane_line.png"),ImageFormat.Png);
        }
    }
    static void RenderFloorFade(int w,int h){
        using(var image=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            for(int y=0;y<h;y++){
                float depth=y/(float)(h-1);
                int alpha=(int)Math.Round(255*Math.Pow(Unit(depth/.5f),2));
                for(int x=0;x<w;x++){
                    float across=x/(float)(w-1);
                    float edge=Smooth(Math.Min(across,1-across)/.04f);
                    Color tint=Mix(Blue,Pink,across);
                    image.SetPixel(x,y,A(alpha,Mix(Color.Black,tint,.62f*edge)));
                }
            }
            image.Save(Path.Combine(Target,"mask_2dmode_floor_grd.png"),ImageFormat.Png);
        }
    }
    // ---- Lane family geometry -------------------------------------------------------------------------
    // Notes are placed by code (LiveConfig): 12 lanes of 1.19 world units judged at y -2.96, each note
    // travelling from SpawnPosition (0, 5.46) and scaling with its progress, so every lane boundary is a
    // straight line from that vanishing point. lane_base, lane_line, lane_mask and judgeLine all derive
    // their edges from it instead of carrying hand-measured numbers.
    const float LaneVanishY=5.46f,LaneJudgeY=-2.96f,LaneHalfAtJudge=6*1.19f;
    static float LaneHalfWidth(float worldY){return LaneHalfAtJudge*(LaneVanishY-worldY)/(LaneVanishY-LaneJudgeY);}
    // lane_base / lane_line / lane_mask: 2048x1176, 101.27473 px per unit, pivot at pixel (1024, 588),
    // placed at world y -0.8 (FrontUIView.prefab LaneRoot / HideArea).
    const float LanePpu=101.27473f,LaneCx=1024,LanePivotRow=588,LaneOriginY=-.8f;
    static float LaneTextureHalf(float row){return LaneHalfWidth(LaneOriginY+(LanePivotRow-row)/LanePpu)*LanePpu;}
    // judgeLine: 108 px per unit, pivot at pixel (804.0, 57) (judgeLine.asset), placed at world y -2.92
    // (LaneRoot -0.8 + JudgeLane -2.12).
    const float JudgePpu=108,JudgeCx=804f,JudgePivotRow=57,JudgeOriginY=-2.92f;
    static float JudgeTextureHalf(float row){return LaneHalfWidth(JudgeOriginY+(JudgePivotRow-row)/JudgePpu)*JudgePpu;}
    // Judge line: the 12-lane slab the notes are judged on. Its edges are the lane boundaries; it is
    // 83 px (0.77 units) tall so tap notes and the taller critical/hold-end notes both sit on it - the
    // sprite rect (rows 15.08..98.92) leaves no room for more. A 4 px rim, 11 dividers at k/12.
    static void RenderJudgeLine(int w,int h){
        const float cx=JudgeCx,top=JudgePivotRow-41.5f,bottom=JudgePivotRow+41.5f,rim=4,divider=2f;
        float topHalf=JudgeTextureHalf(top),bottomHalf=JudgeTextureHalf(bottom);
        float slope=(bottomHalf-topHalf)/(bottom-top),sideRim=rim*(float)Math.Sqrt(1+slope*slope);
        Color rimCentre=Color.FromArgb(176,130,253),rimEnd=Color.FromArgb(208,160,182),fill=Color.FromArgb(45,45,45);
        const int fillAlpha=102,ss=4;
        using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                int rimHits=0,fillHits=0;
                for(int sy=0;sy<ss;sy++)for(int sx=0;sx<ss;sx++){
                    float px=x+(sx+.5f)/ss,py=y+(sy+.5f)/ss;if(py<top||py>=bottom)continue;
                    float dx=Math.Abs(px-cx),half=topHalf+slope*(py-top);if(dx>half)continue;
                    if(py>=top+rim&&py<bottom-rim&&dx<=half-sideRim)fillHits++;else rimHits++;
                }
                if(rimHits+fillHits==0)continue;
                // The rim fades from violet at the centre to a warm rose at both ends.
                float t=Math.Abs(x+.5f-cx)/topHalf,k=Math.Min(1.3f,t*t);
                float rr=rimCentre.R+(rimEnd.R-rimCentre.R)*k,rg=rimCentre.G+(rimEnd.G-rimCentre.G)*k,rb=rimCentre.B+(rimEnd.B-rimCentre.B)*k;
                float wr=rimHits*255f,wf=fillHits*(float)fillAlpha,total=wr+wf;
                int alpha=(int)Math.Round(total/(ss*ss));
                Func<float,float,int> ch=(r,f)=>Math.Max(0,Math.Min(255,(int)Math.Round((r*wr+f*wf)/total)));
                b.SetPixel(x,y,Color.FromArgb(alpha,ch(rr,fill.R),ch(rg,fill.G),ch(rb,fill.B)));
            }
            using(var g=Graphics.FromImage(b)){
                g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.CompositingMode=CompositingMode.SourceOver;
                using(var pen=new Pen(Color.FromArgb(255,246,249,253),divider)){
                    for(int i=1;i<12;i++){
                        float y0=top+rim,y1=bottom-rim;
                        float x0=cx-(topHalf+slope*(y0-top))*(1-i/6f),x1=cx-(topHalf+slope*(y1-top))*(1-i/6f);
                        g.DrawLine(pen,x0,y0,x1,y1);
                    }
                }
                // Four-point sparkles inside both ends of the slab, 19 px in from the outer edge.
                float sparkleInset=JudgeTextureHalf(JudgePivotRow)-19;
                foreach(float sx in new[]{cx-sparkleInset,cx+sparkleInset}){
                    const float sy=JudgePivotRow,rv=9,rh=6f,waist=1.9f;
                    using(var p=Polygon(P(sx,sy-rv),P(sx+waist,sy-waist),P(sx+rh,sy),P(sx+waist,sy+waist),P(sx,sy+rv),P(sx-waist,sy+waist),P(sx-rh,sy),P(sx-waist,sy-waist)))
                        Fill(g,p,Color.FromArgb(250,250,250,255));
                }
            }
            b.Save(Path.Combine(Target,"judgeLine.png"),ImageFormat.Png);
        }
    }
    static bool Direct(string n,int w,int h){
        if(n=="judgeLine.png"){RenderJudgeLine(w,h);return true;}
        if(n=="lane_mask.png"){RenderLaneMask(w,h);return true;}
        if(n=="lane_line.png"||n=="mask_2dmode_floor_grd.png"){
            if(n=="lane_line.png")RenderLaneLines(w,h);
            else RenderFloorFade(w,h);
            return true;
        }
        if(n=="tex_transition_03.png"){
            using(var result=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                    float dx=(x+.5f-w*.5f)/(w*.5f),dy=(y+.5f-h*.5f)/(h*.5f);
                    float r=(float)Math.Sqrt(dx*dx+dy*dy);
                    float swirl=(float)(.5+.5*Math.Cos(2*Math.Atan2(dy,dx)-9*r));
                    float fade=Smooth(r/.14f)*Smooth((1-r)/.32f);
                    Color color=Mix(Blue,Purple,swirl);
                    int alpha=(int)Math.Round(215*fade*(.2+.8*swirl*swirl));
                    result.SetPixel(x,y,A(alpha,color));
                }
                result.Save(Path.Combine(Target,n),ImageFormat.Png);
            }return true;
        }
        if(n=="tex_lifeZero.png"){
            using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                    float dx=(x-w*.5f)/(w*.5f),dy=(y-h*.5f)/(h*.5f),r=(float)Math.Sqrt(dx*dx+dy*dy);
                    float t=Math.Max(0,Math.Min(1,(r-.2f)/.8f));int alpha=(int)(240*t*t*(3-2*t));b.SetPixel(x,y,A(alpha,Color.White));
                }b.Save(Path.Combine(Target,n),ImageFormat.Png);
            }return true;
        }
        return false;
    }
    public static void Run(string root){Run(root,null);}
    public static void Run(string root,string[] only){
        Target=Path.Combine(root,"HUD");Directory.CreateDirectory(Target);
        Bold=new System.Windows.Media.GlyphTypeface(new Uri(Path.GetFullPath(Path.Combine(root,"..","Assets","Game","Resources","Fonts","SourceHanSansSC-Bold.otf"))));
        Medium=new System.Windows.Media.GlyphTypeface(new Uri(Path.GetFullPath(Path.Combine(root,"..","Assets","Game","Resources","Fonts","SourceHanSansSC-Medium.otf"))));
        InitDigitMetrics();
        int count=0,preserved=0;var records=new List<string>{"file\twidth\theight\tstatus"};
        var rows=File.ReadAllLines(Path.Combine(root,"Tools","manifest.csv")).Skip(1).Where(line=>!String.IsNullOrWhiteSpace(line)).Select(line=>line.Split('\t')).ToArray();
        Sizes=rows.ToDictionary(fields=>Path.GetFileName(fields[0]),fields=>new Size(int.Parse(fields[4]),int.Parse(fields[5])),StringComparer.OrdinalIgnoreCase);
        foreach(var fields in rows){
            string n=Path.GetFileName(fields[0]);int w=Sizes[n].Width,h=Sizes[n].Height;
            if(only!=null&&only.Length>0&&Array.IndexOf(only,n)<0)continue;
            {
                if(Keep.Contains(n)){
                    if(!File.Exists(Path.Combine(Target,n)))throw new FileNotFoundException("Missing maintained HUD mask",Path.Combine(Target,n));
                    preserved++;records.Add(n+"\t"+w+"\t"+h+"\tfunctional geometry preserved");
                }else if(Direct(n,w,h))records.Add(n+"\t"+w+"\t"+h+"\tdirect render");
                else {
                    int scale=Math.Max(w,h)>1024?2:4;
                    using(var hi=new Bitmap(w*scale,h*scale,PixelFormat.Format32bppArgb)){
                        using(var g=Setup(hi,scale)){
                            bool handled=TextAsset(g,n,w,h)||HudAsset(g,n,w,h)||Environment(g,n,w,h);
                            if(!handled)throw new Exception("No renderer for "+n);
                        }Save(hi,n,w,h);
                    }
                    records.Add(n+"\t"+w+"\t"+h+"\tcode redrawn");
                }
            }
            count++;Console.WriteLine(count+" "+n);
        }
        string reportPath=Path.Combine(root,"render-report.tsv");
        if(only==null||only.Length==0)File.WriteAllLines(reportPath,records,System.Text.Encoding.UTF8);
        else if(File.Exists(reportPath)){
            var replacements=records.Skip(1).ToDictionary(line=>line.Split('\t')[0],line=>line);
            var previous=File.ReadAllLines(reportPath);
            for(int i=1;i<previous.Length;i++){string key=previous[i].Split('\t')[0];if(replacements.ContainsKey(key))previous[i]=replacements[key];}
            File.WriteAllLines(reportPath,previous,System.Text.Encoding.UTF8);
        }
        Console.WriteLine("Completed "+count+" assets; "+preserved+" functional masks/utility pixels retained.");
    }
}
