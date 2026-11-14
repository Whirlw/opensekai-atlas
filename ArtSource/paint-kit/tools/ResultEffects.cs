using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class ResultEffects {
    static string Target;
    static readonly System.Collections.Generic.Dictionary<string, Size> Sizes = new System.Collections.Generic.Dictionary<string, Size> {
        { "live_result_default_ALL-PERFECT.png", new Size(1444, 182) },
        { "live_result_default_FAILED.png", new Size(1368, 182) },
        { "live_result_default_FULL-COMBO.png", new Size(1382, 182) },
        { "live_result_default_LIVE-CLEAR.png", new Size(1236, 182) },
        { "live_result_default_LIVE-FINISH.png", new Size(1236, 182) },
        { "live_result_default_tex_allperfect_flare.png", new Size(1800, 512) },
        { "live_result_default_tex_clear_flare.png", new Size(1400, 320) },
        { "live_result_default_tex_common_Circle_03.png", new Size(128, 128) },
        { "live_result_default_tex_common_Line_01.png", new Size(512, 128) },
        { "live_result_default_tex_common_Line_02.png", new Size(512, 128) },
        { "live_result_default_tex_common_Line_03.png", new Size(512, 128) },
        { "live_result_default_tex_common_flare_02.png", new Size(512, 256) },
        { "live_result_default_tex_finish_flare.png", new Size(1400, 320) },
        { "live_result_default_tex_flash_v2_01.png", new Size(960, 540) },
        { "live_result_default_tex_fullcombo_flare.png", new Size(1600, 384) },
        { "live_result_default_tex_kira_v2_01.png", new Size(16, 16) },
        { "live_result_default_txt_allperfect_line_dgr.png", new Size(1444, 182) },
        { "live_result_default_txt_fullcombo_line_dgr.png", new Size(1382, 182) },
        { "live_result_default_txt_liveclear_line_dgr.png", new Size(1220, 166) },
        { "live_result_default_txt_livefailed_line_dgr.png", new Size(1352, 166) },
        { "live_result_default_txt_livefinish_line_dgr.png", new Size(1220, 166) },
    };

    static System.Windows.Media.GlyphTypeface Font;
    sealed class LetterSlice { public string Name; public RectangleF Box; }
    static Dictionary<string,List<LetterSlice>> LetterSlices=new Dictionary<string,List<LetterSlice>>();
    static void LoadSlices(string root){
        LetterSlices.Clear();
        string table=Path.Combine(root,"tools","data","result-letter-slices.csv");
        foreach(string line in File.ReadAllLines(table)){
            string[] cells=line.Split(',');if(cells.Length<10||cells[0]=="sprite")continue;
            string texture=Path.GetFileNameWithoutExtension(cells[2]);
            if(texture!="ALL-PERFECT"&&texture!="FULL-COMBO"&&texture!="FAILED"&&texture!="LIVE-CLEAR"&&texture!="LIVE-FINISH")continue;
            float x=float.Parse(cells[3],System.Globalization.CultureInfo.InvariantCulture),y=float.Parse(cells[4],System.Globalization.CultureInfo.InvariantCulture);
            float w=float.Parse(cells[5],System.Globalization.CultureInfo.InvariantCulture),h=float.Parse(cells[6],System.Globalization.CultureInfo.InvariantCulture);
            string kind=Kind(texture);if(w>=MainWidth(kind))continue;
            if(!LetterSlices.ContainsKey(kind))LetterSlices[kind]=new List<LetterSlice>();
            // Native sprite rectangles use the bottom-left origin. The drawing CSV has incorrect negative tops.
            LetterSlices[kind].Add(new LetterSlice{Name=cells[0],Box=new RectangleF(x,182-y-h,w,h)});
        }
        foreach(var pair in LetterSlices)pair.Value.Sort((a,b)=>a.Box.Left.CompareTo(b.Box.Left));
    }
    static Color Gold=C("#ffcc46"),Mint=C("#2bf2c5"),Purple=C("#ae81ff"),Blue=C("#65caff"),Pink=C("#ff73b3");
    static Color C(string s){return ColorTranslator.FromHtml(s);}
    static Color A(int a,Color c){return Color.FromArgb(Math.Max(0,Math.Min(255,a)),c);}
    static Color Mix(Color a,Color b,float t){return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
    static PointF P(float x,float y){return new PointF(x,y);}
    static int Clamp(double v){return Math.Max(0,Math.Min(255,(int)Math.Round(v)));}
    static void Stroke(Graphics g,GraphicsPath path,Color c,float width){using(var p=new Pen(c,width)){p.LineJoin=LineJoin.Bevel;g.DrawPath(p,path);}}
    static void FineTypeGlow(Graphics g,GraphicsPath path,Color c,float gain,float radius){for(int i=(int)(radius*2);i>=2;i-=2){int alpha=(int)(gain*Math.Exp(-i*i/(2*radius*radius)));if(alpha>0)Stroke(g,path,A(alpha,c),i);}}
    static float[] Gaussian(float[] source,int w,int h,float sigma){
        int radius=(int)Math.Ceiling(sigma*3);var kernel=new float[radius*2+1];double total=0;
        for(int i=-radius;i<=radius;i++){kernel[i+radius]=(float)Math.Exp(-i*i/(2*sigma*sigma));total+=kernel[i+radius];}
        for(int i=0;i<kernel.Length;i++)kernel[i]/=(float)total;
        var temp=new float[w*h];var result=new float[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            float value=0;int lo=Math.Max(-radius,-x),hi=Math.Min(radius,w-1-x),row=y*w;
            for(int k=lo;k<=hi;k++)value+=source[row+x+k]*kernel[k+radius];temp[row+x]=value;
        }
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            float value=0;int lo=Math.Max(-radius,-y),hi=Math.Min(radius,h-1-y);
            for(int k=lo;k<=hi;k++)value+=temp[(y+k)*w+x]*kernel[k+radius];result[y*w+x]=value;
        }return result;
    }
    static void Bloom(Graphics g,GraphicsPath path,Color c,float gain,float radius){
        // Rasterize the filled glyph alpha, then convolve it with a separable Gaussian.
        // Corners and counters emit light continuously rather than stacking wide polygon strokes.
        RectangleF bounds=path.GetBounds();int pad=(int)Math.Ceiling(radius*1.3f)+8;
        int left=(int)Math.Floor(bounds.Left)-pad,top=(int)Math.Floor(bounds.Top)-pad;
        int w=(int)Math.Ceiling(bounds.Right)-left+pad,h=(int)Math.Ceiling(bounds.Bottom)-top+pad;
        var emission=new float[w*h];
        using(var hi=new Bitmap(w*2,h*2,PixelFormat.Format32bppArgb)){
            using(var mask=Graphics.FromImage(hi)){mask.Clear(Color.Transparent);mask.SmoothingMode=SmoothingMode.AntiAlias;mask.ScaleTransform(2,2);mask.TranslateTransform(-left,-top);using(var white=new SolidBrush(Color.White))mask.FillPath(white,path);}
            using(var low=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
                using(var down=Graphics.FromImage(low)){down.CompositingMode=CompositingMode.SourceCopy;down.InterpolationMode=InterpolationMode.HighQualityBicubic;down.PixelOffsetMode=PixelOffsetMode.HighQuality;down.DrawImage(hi,new Rectangle(0,0,w,h),0,0,hi.Width,hi.Height,GraphicsUnit.Pixel);}
                var pixels=new int[w*h];var data=low.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);Marshal.Copy(data.Scan0,pixels,0,pixels.Length);low.UnlockBits(data);
                for(int i=0;i<pixels.Length;i++)emission[i]=((uint)pixels[i]>>24)/255f;
            }
        }
        var blurred=Gaussian(emission,w,h,Math.Max(1,radius*.4f));
        using(var glow=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            var pixels=new int[w*h];int rgb=c.ToArgb()&0x00ffffff;
            for(int i=0;i<pixels.Length;i++){int alpha=Clamp(blurred[i]*gain*13);pixels[i]=alpha==0?0:(alpha<<24)|rgb;}
            var data=glow.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);Marshal.Copy(pixels,0,data.Scan0,pixels.Length);glow.UnlockBits(data);
            g.DrawImage(glow,new Rectangle(left,top,w,h),0,0,w,h,GraphicsUnit.Pixel);
        }
    }
    static void Line(Graphics g,Color c,float width,params PointF[] points){using(var p=new Pen(c,width))g.DrawLines(p,points);}
    static void Star(Graphics g,float x,float y,float r,Color c){
        using(var path=new GraphicsPath()){
            path.AddPolygon(new[]{P(x,y-r),P(x+r*.19f,y-r*.19f),P(x+r*.82f,y),P(x+r*.19f,y+r*.19f),P(x,y+r),P(x-r*.19f,y+r*.19f),P(x-r*.82f,y),P(x-r*.19f,y-r*.19f)});
            using(var b=new SolidBrush(c))g.FillPath(b,path);
        }
    }
    static void Shine(Graphics g,float x,float y,float r,float spread=1.6f){
        using(var p=new GraphicsPath()){
            p.AddEllipse(x-r*spread,y-r*spread,r*spread*2,r*spread*2);
            using(var b=new PathGradientBrush(p)){b.CenterColor=A(190,Color.White);b.SurroundColors=new[]{A(0,Color.White)};g.FillPath(b,p);}
        }
        Star(g,x,y,r,Color.White);
    }
    static GraphicsPath TextPath(string text,RectangleF box){
        var path=new GraphicsPath(FillMode.Winding);float cursor=0;
        foreach(char ch in text){
            ushort index;if(!Font.CharacterToGlyphMap.TryGetValue(ch,out index))throw new Exception("Missing glyph: "+ch);
            var outline=Font.GetGlyphOutline(index,100,100).GetFlattenedPathGeometry(.2,System.Windows.Media.ToleranceType.Absolute);
            foreach(var f in outline.Figures){var points=new List<PointF>{P(cursor+(float)f.StartPoint.X,(float)f.StartPoint.Y)};
                foreach(var s in f.Segments){var poly=s as System.Windows.Media.PolyLineSegment;if(poly!=null)foreach(var p in poly.Points)points.Add(P(cursor+(float)p.X,(float)p.Y));else{var line=s as System.Windows.Media.LineSegment;if(line!=null)points.Add(P(cursor+(float)line.Point.X,(float)line.Point.Y));}}
                if(points.Count>2){path.StartFigure();path.AddLines(points.ToArray());if(f.IsClosed)path.CloseFigure();}
            }
            cursor+=(float)Font.AdvanceWidths[index]*100+3.5f;
        }
        RectangleF r=path.GetBounds();using(var m=new Matrix(box.Width/r.Width,0,0,box.Height/r.Height,box.X-r.X*box.Width/r.Width,box.Y-r.Y*box.Height/r.Height))path.Transform(m);
        return path;
    }
    static GraphicsPath ExclamationPath(RectangleF box,RectangleF slice){
        using(var original=TextPath("!",box))
        using(var iterator=new GraphicsPathIterator(original))
        using(var figure=new GraphicsPath()){
            var result=new GraphicsPath(FillMode.Winding);bool closed;
            while(iterator.NextSubpath(figure,out closed)>0){
                RectangleF bounds=figure.GetBounds();
                if(bounds.Top>box.Top+box.Height*.65f){
                    // The dot is slightly narrower than the stem's upper edge, with breathing room in the slice.
                    float diameter=slice.Width*.73f;
                    float centreY=Math.Min(bounds.Top+bounds.Height*.5f,slice.Bottom-6-diameter*.5f);
                    result.AddEllipse(slice.Left+(slice.Width-diameter)*.5f,centreY-diameter*.5f,diameter,diameter);
                }else{
                    float stemWidth=slice.Width*.84f;
                    float scaleX=stemWidth/bounds.Width,scaleY=1.06f;
                    using(var transform=new Matrix(scaleX,0,0,scaleY,slice.Left+(slice.Width-stemWidth)*.5f-bounds.Left*scaleX,bounds.Top-bounds.Top*scaleY))figure.Transform(transform);
                    result.AddPath(figure,false);
                }
                figure.Reset();
            }
            return result;
        }
    }
    static GraphicsPath CaptionPath(string kind,float offsetX,float offsetY){
        if(kind=="fail")return TextPath(Words(kind),new RectangleF(offsetX+12,offsetY+14.5f,MainWidth(kind)-24,153));
        string letters=Words(kind).Replace(" ","").Replace(".","");
        List<LetterSlice> slices=LetterSlices[kind];
        if(letters.Length!=slices.Count)throw new Exception("Letter/slice mismatch: "+kind);
        float top=0,bottom=182;
        foreach(var slice in slices){top=Math.Max(top,slice.Box.Top);bottom=Math.Min(bottom,slice.Box.Bottom);}
        // Every animated letter keeps the same cap height and baseline within its slice.
        float capHeight=bottom-top-20,capTop=(top+bottom-capHeight)*.5f;
        var result=new GraphicsPath(FillMode.Winding);
        for(int i=0;i<slices.Count;i++){
            RectangleF cell=slices[i].Box;
            var box=new RectangleF(offsetX+cell.X+8,offsetY+capTop,cell.Width-16,capHeight);
            var screenSlice=new RectangleF(offsetX+cell.X,offsetY+cell.Y,cell.Width,cell.Height);
            using(var glyph=letters[i]=='!'?ExclamationPath(box,screenSlice):TextPath(letters[i].ToString(),box))result.AddPath(glyph,false);
        }
        return result;
    }
    static void CaptionOutline(Graphics g,GraphicsPath path,int width,int height){
        // Trace the actual foreground silhouette, including the narrow lower face.
        // Both the caption and its line_dgr now share the same upright layout and visible boundary.
        const int scale=2;int w=width*scale,h=height*scale;
        using(var mask=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            using(var drawing=Graphics.FromImage(mask)){
                drawing.SmoothingMode=SmoothingMode.AntiAlias;drawing.ScaleTransform(scale,scale);
                var state=drawing.Save();drawing.TranslateTransform(0,3.3f);
                using(var side=new SolidBrush(Color.White))drawing.FillPath(side,path);
                Stroke(drawing,path,Color.White,3.5f);drawing.Restore(state);
                using(var brush=new SolidBrush(Color.White))drawing.FillPath(brush,path);
                Stroke(drawing,path,Color.White,1.1f);
            }
            var data=mask.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            var pixels=new int[w*h];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);mask.UnlockBits(data);
            var alpha=new byte[pixels.Length];for(int i=0;i<alpha.Length;i++)alpha[i]=(byte)((uint)pixels[i]>>24);
            var offsets=new List<Point>();
            for(int dy=-3;dy<=3;dy++)for(int dx=-3;dx<=3;dx++)if(dx*dx+dy*dy<=5)offsets.Add(new Point(dx,dy));
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                int low=255,high=0;
                foreach(var delta in offsets){
                    int xx=x+delta.X,yy=y+delta.Y;
                    int value=xx<0||xx>=w||yy<0||yy>=h?0:alpha[yy*w+xx];
                    low=Math.Min(low,value);high=Math.Max(high,value);
                }
                int opacity=Clamp((high-low)*(235-60.0*y/(h-1))/255.0);
                pixels[y*w+x]=opacity==0?0:(opacity<<24)|0x00ffffff;
            }
            data=mask.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            Marshal.Copy(pixels,0,data.Scan0,pixels.Length);mask.UnlockBits(data);
            g.DrawImage(mask,new Rectangle(0,0,width,height),0,0,w,h,GraphicsUnit.Pixel);
        }
    }
    static string Kind(string name){if(name.Contains("allperfect")||name.Contains("ALL-PERFECT"))return "ap";if(name.Contains("fullcombo")||name.Contains("FULL-COMBO"))return "fc";if(name.Contains("livefailed")||name.Contains("FAILED"))return "fail";if(name.Contains("finish")||name.Contains("FINISH"))return "finish";return "clear";}
    static string Words(string kind){return kind=="ap"?"ALL PERFECT!!":kind=="fc"?"FULL COMBO!":kind=="fail"?"LIVE FAILED...":kind=="finish"?"LIVE FINISH":"LIVE CLEAR!";}
    static int MainWidth(string kind){return kind=="ap"?1444:kind=="fc"?1382:kind=="fail"?1368:1236;}
    static Color Accent(string kind){return kind=="ap"?Purple:kind=="fc"?Pink:kind=="fail"?C("#a5b1c8"):kind=="finish"?Blue:Gold;}
    static LinearGradientBrush TextGradient(string kind,RectangleF box,int alpha){
        var brush=new LinearGradientBrush(P(box.Left,0),P(box.Right,0),Color.White,Color.White);
        var blend=new ColorBlend();
        if(kind=="ap"){
            blend.Positions=new[]{0f,.24f,.50f,.76f,1f};blend.Colors=new[]{A(alpha,Mix(Gold,Color.White,.48f)),A(alpha,Mix(Pink,Color.White,.53f)),A(alpha,Mix(Purple,Color.White,.48f)),A(alpha,Mix(Blue,Color.White,.43f)),A(alpha,Mix(Mint,Color.White,.48f))};
        }else{
            Color a=Accent(kind),b=kind=="fc"?Purple:kind=="finish"?Purple:kind=="clear"?C("#ffcaa0"):C("#d5ddeb");
            blend.Positions=new[]{0f,.5f,1f};blend.Colors=new[]{A(alpha,Mix(a,Color.White,.65f)),A(alpha,Mix(a,Color.White,.85f)),A(alpha,Mix(b,Color.White,.48f))};
        }
        brush.InterpolationColors=blend;return brush;
    }
    static void Caption(Graphics g,string name,int w,int h){
        string kind=Kind(name);bool outline=name.Contains("_line_dgr"),flare=name.Contains("_flare");
        float textW=MainWidth(kind)-20,textH=158;
        var box=new RectangleF((w-textW)*.5f,(h-textH)*.5f,textW,textH);
        Color accent=Accent(kind);
        using(var path=CaptionPath(kind,(w-MainWidth(kind))*.5f,(h-182)*.5f)){
            if(outline){
                CaptionOutline(g,path,w,h);
                return;
            }
            if(flare){
                // The same glyph contour and pixel size as the foreground caption, with broad bloom.
                bool premium=kind=="ap"||kind=="fc";
                Bloom(g,path,accent,premium?38:7,premium?58:32);
                Bloom(g,path,Color.White,premium?28:5,premium?27:15);
                using(var brush=TextGradient(kind,box,255))g.FillPath(brush,path);
                using(var white=new SolidBrush(A(premium?130:65,Color.White)))g.FillPath(white,path);
                Stroke(g,path,Color.White,premium?2.5f:1.2f);
                if(premium)for(int i=0;i<4;i++){
                    float x=box.Left+box.Width*(.12f+i*.245f);
                    bool found=false;
                    for(float yy=box.Top+8;yy<box.Top+box.Height*.48f&&!found;yy+=2)
                    for(float offset=0;offset<=18&&!found;offset+=3){
                        if(path.IsVisible(x+offset,yy)){Shine(g,x+offset,yy,kind=="ap"?12:10);found=true;}
                    }
                }
                return;
            }
            var st=g.Save();g.TranslateTransform(0,3.3f);Stroke(g,path,Mix(accent,C("#19243d"),.65f),3.5f);g.Restore(st);
            FineTypeGlow(g,path,accent,2.4f,7);
            using(var brush=TextGradient(kind,box,255))g.FillPath(brush,path);
            Stroke(g,path,Mix(accent,Color.White,.4f),1.1f);
            // Upper highlights and low-key faceted engraving stay clipped inside the glyphs.
            var clip=g.Save();g.SetClip(path);
            using(var brush=new LinearGradientBrush(P(0,box.Top),P(0,box.Bottom),A(85,Color.White),A(0,Color.White)))g.FillRectangle(brush,box);
            for(float x=box.Left+24;x<box.Right;x+=92)Star(g,x,box.Top+textH*.78f,3.1f,A(110,Color.White));
            g.Restore(clip);
        }
    }
    static Color Spectral(float t){
        Color[] colors={Gold,Pink,Purple,Blue,Mint};float position=Math.Max(0,Math.Min(.99999f,t))*4;int i=(int)position;
        return Mix(colors[i],colors[i+1],position-i);
    }
    static Bitmap Field(int w,int h,int mode){
        var output=new Bitmap(w,h,PixelFormat.Format32bppArgb);var pixels=new int[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            double u=(x+.5)/w,v=(y+.5)/h,alpha;Color color;
            if(mode==0||mode==1){
                double value=0,soft=0;
                for(int i=0;i<5;i++){
                    double centre=.5+Math.Sin(u*Math.PI*2+i*1.31)*(.065+i*.011),width=.012+i*.005;
                    double d=(v-centre)/width;
                    value+=Math.Exp(-d*d)*(.22+i*.035)*(.35+.65*Math.Pow(Math.Cos(u*Math.PI*2+i),2));
                    soft+=Math.Exp(-d*d/8)*(.13+i*.018)*(.45+.55*Math.Pow(Math.Cos(u*Math.PI*2+i),2));
                }
                alpha=Math.Min(1,value*2.25+soft*.85);color=mode==0?Color.White:Mix(Spectral((float)u),Color.White,(float)(Math.Min(1,value*1.7)*.75));
            }else if(mode==2){alpha=1;color=Mix(Spectral((float)u),Color.White,.38f);}
            else{
                double dx=(u-.5)*2,dy=(v-.5)*2;
                double core=Math.Exp(-dx*dx/.009-dy*dy/.024);
                double ray=Math.Exp(-dx*dx/.56-dy*dy/.003),halo=Math.Exp(-dx*dx/.28-dy*dy/.095);
                if(mode==3){core=Math.Exp(-(dx*dx+dy*dy)/.020);ray=0;halo=Math.Exp(-(dx*dx+dy*dy)/.24);}
                alpha=Math.Min(1,core*1.2+ray*1.15+halo*.65);
                alpha*=Math.Pow(Math.Max(0,1-Math.Abs(dx)),.3)*Math.Pow(Math.Max(0,1-Math.Abs(dy)),.2);
                Color tint=mode==4?Purple:Blue;color=Mix(tint,Color.White,(float)Math.Min(1,Math.Sqrt(core)*.98+ray*.38));
            }
            pixels[y*w+x]=A(Clamp(alpha*255),color).ToArgb();
        }
        var data=output.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);Marshal.Copy(pixels,0,data.Scan0,pixels.Length);output.UnlockBits(data);return output;
    }
    static void Save(Bitmap hi,string name,int w,int h){using(var bmp=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
        using(var g=Graphics.FromImage(bmp)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(hi,new Rectangle(0,0,w,h),0,0,hi.Width,hi.Height,GraphicsUnit.Pixel);}
        bmp.Save(Path.Combine(Target,name),ImageFormat.Png);
    }}
    public static void Run(string root){Run(root,null,false);}
    public static void Run(string root,string target,bool mainOnly){
        Target=String.IsNullOrEmpty(target)?Path.Combine(root,"new_effects"):target;Directory.CreateDirectory(Target);
        Font=new System.Windows.Media.GlyphTypeface(new Uri(Path.GetFullPath(Path.Combine(root,"..","..","Assets","Game","Resources","Fonts","SourceHanSansSC-Bold.otf"))));
        LoadSlices(root);
        int count=0;
        foreach(string name in Sizes.Keys){
            if(mainOnly&&(name.Contains("_tex_")||name.Contains("_txt_")))continue;
            int w=Sizes[name].Width,h=Sizes[name].Height;
            if(name.Contains("_tex_common_Line_")){
                int mode=name.Contains("Line_01")?0:name.Contains("Line_02")?1:2;
                using(var image=Field(w,h,mode))image.Save(Path.Combine(Target,name),ImageFormat.Png);
            }else{
                int scale=w>1000?2:4;
                using(var hi=new Bitmap(w*scale,h*scale,PixelFormat.Format32bppArgb)){
                    using(var g=Graphics.FromImage(hi)){
                        g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.ScaleTransform(scale,scale);
                        if(name.Contains("_tex_common_Circle_")||name.Contains("_tex_common_flare_")||name.Contains("_tex_flash_")){
                            int mode=name.Contains("Circle_")?3:name.Contains("common_flare_")?4:5;
                            using(var field=Field(w,h,mode))g.DrawImage(field,new Rectangle(0,0,w,h));
                            Shine(g,w*.5f,h*.5f,Math.Min(w,h)*.075f);
                            if(mode!=3){
                                using(var pen=new Pen(A(70,mode==4?Pink:Blue),.7f)){
                                    g.DrawBezier(pen,P(w*.1f,h*.52f),P(w*.35f,h*.45f),P(w*.67f,h*.57f),P(w*.9f,h*.49f));
                                    g.DrawBezier(pen,P(w*.1f,h*.49f),P(w*.35f,h*.56f),P(w*.67f,h*.44f),P(w*.9f,h*.51f));
                                }
                            }
                        }else if(name.Contains("_tex_kira_"))Shine(g,w*.5f,h*.5f,6.7f,1.16f);
                        else Caption(g,name,w,h);
                    }Save(hi,name,w,h);
                }
            }
            count++;Console.WriteLine(name+" "+w+"x"+h);
        }
        Console.WriteLine("Completed "+count+" additional result textures; two utility squares excluded.");
    }
}
