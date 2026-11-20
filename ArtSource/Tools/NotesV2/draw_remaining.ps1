param([switch]$NodesOnly, [switch]$DamageOnly)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$code = @'
using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class AstralRemaining {
    static Color C(string s) { return ColorTranslator.FromHtml(s); }
    static Color A(int a,Color c) { return Color.FromArgb(a,c); }
    static Color Mix(Color a,Color b,float t) { return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t)); }
    static PointF P(float x,float y) { return new PointF(x,y); }
    static void Line(Graphics g,Color c,float w,params PointF[] pts) { using(var p=new Pen(c,w)) { p.LineJoin=LineJoin.Round; g.DrawLines(p,pts); } }
    static void Star(Graphics g,float x,float y,float r,Color c) {
        using(var b=new SolidBrush(c)) g.FillPolygon(b,new[]{P(x,y-r),P(x+r*.23f,y-r*.23f),P(x+r*.75f,y),P(x+r*.23f,y+r*.23f),P(x,y+r),P(x-r*.23f,y+r*.23f),P(x-r*.75f,y),P(x-r*.23f,y-r*.23f)});
    }
    static GraphicsPath Poly(PointF[] pts) { var p=new GraphicsPath(); p.AddPolygon(pts); return p; }
    static void Save(Bitmap hi,string path,int w,int h,double brightness=1) {
        using(var bmp=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {
            using(var g=Graphics.FromImage(bmp)) {
                g.CompositingMode=CompositingMode.SourceCopy;
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                g.DrawImage(hi,new Rectangle(0,0,w,h),0,0,hi.Width,hi.Height,GraphicsUnit.Pixel);
            }
            if(brightness!=1)for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                Color pixel=bmp.GetPixel(x,y);if(pixel.A==0)continue;
                bmp.SetPixel(x,y,Color.FromArgb(pixel.A,(int)Math.Round(pixel.R*brightness),(int)Math.Round(pixel.G*brightness),(int)Math.Round(pixel.B*brightness)));
            }
            bmp.Save(path,ImageFormat.Png);
        }
    }
    static Graphics Setup(Bitmap b) { var g=Graphics.FromImage(b);g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(4,4);return g; }
    static void Glow(Graphics g,GraphicsPath face,Color accent,bool arrow) {
        // Stronger than notes.png gold halo (peak 24, sigma 10, maximum width 48).
        float border=0;
        for(int i=48;i>=(arrow?10:4);i-=2) {
            float distance=i/2f;
            float sigma=11.5f;
            int alpha=(int)((arrow?34:3)*Math.Exp(-distance*distance/(2*sigma*sigma)));
            if(alpha==0)continue;
            using(var p=new Pen(A(alpha,accent),border+i)) {p.LineJoin=LineJoin.Round;g.DrawPath(p,face);}
        }
    }
    static float TailOpacity(float y,float start,int h) {
        float t=Math.Max(0,Math.Min(1,(y-start)/(h-start)));
        if(t<.30f)return (225+(190-225)*t/.30f)/255f;
        if(t<.75f)return (190+(68-190)*(t-.30f)/.45f)/255f;
        return 68*(1-t)/(.25f*255f);
    }
    static void TailLink(Graphics g,PointF a,PointF b,float start,int h,Color ink) {
        int steps=Math.Max(1,(int)(Math.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Y-a.Y)*(b.Y-a.Y))/2));
        for(int i=0;i<steps;i++) {
            float t=i/(float)steps,u=(i+1)/(float)steps;
            PointF p=P(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t),q=P(a.X+(b.X-a.X)*u,a.Y+(b.Y-a.Y)*u);
            int alpha=(int)(140*TailOpacity((p.Y+q.Y)*.5f,start,h));
            if(alpha>0)Line(g,A(alpha,ink),.7f,p,q);
        }
    }
    static void Trail(Graphics g,GraphicsPath face,PointF[] pts,bool[,] mask,int w,int h,bool diagonal,Color ink) {
        float dx=diagonal?h*.58f:0,dy=h;
        // Sweep the measured silhouette downwards; diagonal arrows keep their angled tail.
        using(var sweep=new Region(face)) {
            for(int i=0;i<pts.Length;i++) {
                PointF a=pts[i],b=pts[(i+1)%pts.Length];
                using(var quad=Poly(new[]{a,b,P(b.X+dx,b.Y+dy),P(a.X+dx,a.Y+dy)}))sweep.Union(quad);
            }
            var st=g.Save();g.SetClip(sweep,CombineMode.Replace);
            float start=h*.30f;
            using(var brush=new LinearGradientBrush(P(0,start),P(0,h),Color.White,Color.Transparent)) {
                var blend=new ColorBlend();blend.Positions=new[]{0f,.30f,.75f,1f};
                blend.Colors=new[]{A(225,Color.White),A(190,Color.White),A(68,Color.White),A(0,Color.White)};
                brush.InterpolationColors=blend;g.FillRectangle(brush,0,start,w,h-start);
            }
            // Distribute an irregular star field over the complete visible swept tail.
            // Stars and each short segment use the exact same fade curve as the white trail.
            g.SetClip(face,CombineMode.Exclude);
            var stars=new List<PointF>();var random=new Random(w*111+h);
            float spacing=Math.Max(23,w/13f);int row=0;
            for(float y=start+7;y<h-2;y+=18,row++) {
                for(float x=6+(row%2)*spacing*.5f;x<w-4;x+=spacing) {
                    PointF p=P(x+(float)(random.NextDouble()-.5)*spacing*.42f,y+(float)(random.NextDouble()-.5)*9);
                    if(p.X<4||p.X>w-4||p.Y>h-2)continue;
                    if(sweep.IsVisible(p)&&!face.IsVisible(p)&&sweep.IsVisible(p.X-3,p.Y)&&sweep.IsVisible(p.X+3,p.Y))stars.Add(p);
                }
            }
            var degree=new int[stars.Count];
            for(int i=0;i<stars.Count;i++) {
                int closest=-1;float best=spacing*spacing*2.4f;
                for(int j=0;j<i;j++) {
                    float sx=stars[j].X-stars[i].X,sy=stars[j].Y-stars[i].Y,d=sx*sx+sy*sy;
                    if(degree[j]<3&&d<best){best=d;closest=j;}
                }
                if(closest>=0){TailLink(g,stars[closest],stars[i],start,h,ink);degree[closest]++;degree[i]++;}
            }
            for(int i=0;i<stars.Count;i++) {
                PointF p=stars[i];float fade=TailOpacity(p.Y,start,h);
                if(i%3==0) {
                    Star(g,p.X,p.Y,4.3f,A((int)(245*fade),Color.White));
                    Star(g,p.X,p.Y,2.6f,A((int)(205*fade),ink));
                } else {
                    using(var b=new SolidBrush(A((int)(215*fade),ink)))g.FillEllipse(b,p.X-1,p.Y-1,2,2);
                    using(var b=new SolidBrush(A((int)(230*fade),Color.White)))g.FillEllipse(b,p.X-.45f,p.Y-.45f,.9f,.9f);
                }
            }
            g.Restore(st);
        }
    }

    // Measure only the arrow's foreground geometry. No reference pixels are copied.
    static bool[,] ArrowMask(Bitmap src) {
        var mask=new bool[src.Width,src.Height];
        for(int y=0;y<src.Height;y++) for(int x=0;x<src.Width;x++) {
            Color c=src.GetPixel(x,y);
            mask[x,y]=c.A>220 && c.R>=222 && c.R<=246 && c.G>=100 && c.G<=160 && c.B>=150 && c.B<=210;
        }
        // The source contains detached pink shadow contours: retain only the solid face.
        int w=src.Width,h=src.Height;var visited=new bool[w,h];var largest=new List<int>();
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(mask[x,y]&&!visited[x,y]){
            var component=new List<int>();var queue=new Queue<int>();queue.Enqueue(y*w+x);visited[x,y]=true;
            while(queue.Count>0){int v=queue.Dequeue(),vx=v%w,vy=v/w;component.Add(v);
                foreach(int delta in new[]{-1,1,-w,w}){int n=v+delta,nx=n%w,ny=n/w;
                    if(n>=0&&n<w*h&&Math.Abs(nx-vx)+Math.Abs(ny-vy)==1&&mask[nx,ny]&&!visited[nx,ny]){visited[nx,ny]=true;queue.Enqueue(n);}
                }
            }
            if(component.Count>largest.Count)largest=component;
        }
        mask=new bool[w,h];foreach(int v in largest)mask[v%w,v/w]=true;
        return mask;
    }
    static float Distance(PointF p,PointF a,PointF b) {
        float dx=b.X-a.X,dy=b.Y-a.Y;
        float t=(dx*dx+dy*dy)==0?0:Math.Max(0,Math.Min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(dx*dx+dy*dy)));
        dx=p.X-a.X-t*dx;dy=p.Y-a.Y-t*dy;return (float)Math.Sqrt(dx*dx+dy*dy);
    }
    static void Simplify(List<PointF> pts,int lo,int hi,List<PointF> output) {
        float far=0;int index=-1;
        for(int i=lo+1;i<hi;i++) { float d=Distance(pts[i],pts[lo],pts[hi]);if(d>far){far=d;index=i;} }
        if(far>2.8f) { Simplify(pts,lo,index,output);Simplify(pts,index,hi,output); }
        else output.Add(pts[lo]);
    }
    static PointF[] Outline(bool[,] mask,int w,int h) {
        int stride=w+1;var edges=new Dictionary<int,int>();
        for(int y=0;y<h;y++) for(int x=0;x<w;x++) if(mask[x,y]) {
            if(y==0||!mask[x,y-1]) edges[y*stride+x]=y*stride+x+1;
            if(x==w-1||!mask[x+1,y]) edges[y*stride+x+1]=(y+1)*stride+x+1;
            if(y==h-1||!mask[x,y+1]) edges[(y+1)*stride+x+1]=(y+1)*stride+x;
            if(x==0||!mask[x-1,y]) edges[(y+1)*stride+x]=y*stride+x;
        }
        var largest=new List<PointF>();
        while(edges.Count>0) {
            int start=0;foreach(var key in edges.Keys){start=key;break;}
            int cursor=start;var loop=new List<PointF>();
            while(edges.ContainsKey(cursor)) {
                loop.Add(P(cursor%stride,cursor/stride));int next=edges[cursor];edges.Remove(cursor);cursor=next;if(cursor==start)break;
            }
            if(loop.Count>largest.Count)largest=loop;
        }
        if(largest.Count<10)throw new Exception("Could not measure arrow silhouette");
        int split=0;float max=0;
        for(int i=1;i<largest.Count;i++){float dx=largest[i].X-largest[0].X,dy=largest[i].Y-largest[0].Y;float d=dx*dx+dy*dy;if(d>max){max=d;split=i;}}
        largest.Add(largest[0]);var result=new List<PointF>();Simplify(largest,0,split,result);Simplify(largest,split,largest.Count-1,result);
        return result.ToArray();
    }
    static void Arrow(string srcPath,string output,int w,int h,bool gold,int size) {
        bool[,] mask;PointF[] pts;
        using(var src=new Bitmap(srcPath)){mask=ArrowMask(src);pts=Outline(mask,w,h);}
        Color accent=C(gold?"#ffcc46":"#ff73b3");Color ink=Mix(accent,C("#704875"),.48f);
        using(var hi=new Bitmap(w*4,h*4,PixelFormat.Format32bppArgb)) {
            using(var g=Setup(hi)) using(var face=Poly(pts)) {
                Trail(g,face,pts,mask,w,h,srcPath.Contains("diagonal"),ink);
                Glow(g,face,accent,true);
                // The darker extrusion supports a colored face and pale outer rim.
                var st=g.Save();g.TranslateTransform(0,4);
                using(var b=new SolidBrush(Mix(accent,C("#30405f"),.62f)))g.FillPath(b,face);
                using(var p=new Pen(Mix(accent,C("#30405f"),.62f),9)){p.LineJoin=LineJoin.Bevel;g.DrawPath(p,face);}g.Restore(st);
                using(var b=new LinearGradientBrush(P(0,20),P(0,h-20),Mix(accent,Color.White,.12f),accent))g.FillPath(b,face);
                using(var p=new Pen(Mix(accent,Color.White,.84f),8)){p.LineJoin=LineJoin.Bevel;g.DrawPath(p,face);}
                using(var p=new Pen(Mix(accent,Color.White,.96f),2)){p.LineJoin=LineJoin.Bevel;g.DrawPath(p,face);}
                // The star chain follows the measured arrow arms and cannot change its direction.
                int minX=w,maxX=0;for(int x=0;x<w;x++)for(int y=0;y<h;y++)if(mask[x,y]){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);}
                int count=Math.Min(7,size+2);var stars=new List<PointF>();
                for(int i=0;i<count;i++){
                    int x=(int)(minX+(maxX-minX)*(i+1f)/(count+1f));var ys=new List<int>();
                    for(int y=0;y<h;y++)if(mask[x,y])ys.Add(y);
                    if(ys.Count>8)stars.Add(P(x,ys[ys.Count/2]));
                }
                var clip=g.Save();g.SetClip(face);
                if(stars.Count>1)Line(g,A(185,Color.White),.85f,stars.ToArray());
                foreach(var p in stars){Star(g,p.X,p.Y,4.8f,Color.White);}
                // Fine orbital etching stays inside the bright arrow face.
                using(var p=new Pen(A(75,Color.White),.7f))g.DrawEllipse(p,w*.28f,h*.22f,w*.44f,h*.6f);
                g.Restore(clip);
            }
            Save(hi,output,w,h);
        }
    }
    static PointF[] Stellar(float cx,float cy,float radius) {
        var pts=new PointF[12];
        for(int i=0;i<12;i++){double a=-Math.PI/2+i*Math.PI/6;float r=i%2==0?radius:radius*.63f;pts[i]=P(cx+(float)Math.Cos(a)*r,cy+(float)Math.Sin(a)*r);}
        return pts;
    }
    struct V3 {
        public double X,Y,Z;
        public V3(double x,double y,double z){X=x;Y=y;Z=z;}
        public static V3 operator -(V3 a,V3 b){return new V3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
    }
    static V3 Cross(V3 a,V3 b){return new V3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}
    static double Dot(V3 a,V3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
    static V3 Rotate(V3 v){
        double ax=.68,ay=-.52,az=.6235987756;
        double x=v.X,y=v.Y*Math.Cos(ax)-v.Z*Math.Sin(ax),z=v.Y*Math.Sin(ax)+v.Z*Math.Cos(ax);
        double xx=x*Math.Cos(ay)+z*Math.Sin(ay),zz=-x*Math.Sin(ay)+z*Math.Cos(ay);
        return new V3(xx*Math.Cos(az)-y*Math.Sin(az),xx*Math.Sin(az)+y*Math.Cos(az),zz);
    }
    static Color FacetColor(Color accent,double diffuse,double position){
        double light=Math.Max(0,Math.Min(1,diffuse+position*.065));
        if(light>.43)return Mix(accent,Color.White,(float)Math.Min(.95,(light-.43)/.52));
        Color shade=Color.FromArgb((int)(accent.R*.50),(int)(accent.G*.38),(int)(accent.B*.30));
        return Mix(accent,shade,(float)Math.Min(.82,(.43-light)*1.8));
    }
    static double[] BlurEmission(double[] input,int w,int h,double sigma){
        int radius=(int)Math.Ceiling(sigma*3);var kernel=new double[radius*2+1];double total=0;
        for(int i=-radius;i<=radius;i++){double value=Math.Exp(-i*i/(2*sigma*sigma));kernel[i+radius]=value;total+=value;}
        for(int i=0;i<kernel.Length;i++)kernel[i]/=total;
        var temp=new double[w*h];var output=new double[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            double value=0;for(int k=-radius;k<=radius;k++){int xx=x+k;if(xx>=0&&xx<w)value+=input[y*w+xx]*kernel[k+radius];}temp[y*w+x]=value;
        }
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            double value=0;for(int k=-radius;k<=radius;k++){int yy=y+k;if(yy>=0&&yy<h)value+=temp[yy*w+x]*kernel[k+radius];}output[y*w+x]=value;
        }
        return output;
    }
    static Bitmap LuminanceGlow(int[] surface,int w,int h,Color accent){
        var emission=new double[w*h];int sw=w*4;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            double value=0;
            for(int j=0;j<4;j++)for(int i=0;i<4;i++){
                int pixel=surface[(y*4+j)*sw+x*4+i];
                if(pixel==0)continue;
                double luminance=(.2126*((pixel>>16)&255)+.7152*((pixel>>8)&255)+.0722*(pixel&255))/255;
                value+=Math.Pow(luminance,2.2)*((uint)pixel>>24)/255.0;
            }
            emission[y*w+x]=value/16;
        }
        // Bright facets emit more light. Two blur radii create a soft near halo and faint falloff.
        var near=BlurEmission(emission,w,h,5.5);var far=BlurEmission(emission,w,h,10);
        var glow=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            int alpha=(int)Math.Round(300*(near[y*w+x]*.78+far[y*w+x]*.22));
            glow.SetPixel(x,y,alpha==0?Color.Transparent:A(alpha,accent));
        }
        return glow;
    }
    static Color NodeShade(Color accent){return Color.FromArgb((int)(accent.R*.24),(int)(accent.G*.24),(int)(accent.B*.24));}
    static Bitmap NodeShadow(int[] surface,int w,int h,Color accent,bool small){
        var coverage=new double[w*h];int sw=w*4;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            double value=0;
            for(int j=0;j<4;j++)for(int i=0;i<4;i++)value+=((uint)surface[(y*4+j)*sw+x*4+i]>>24)/255.0;
            coverage[y*w+x]=value/16;
        }
        // Expand the emission footprint, then blend broad Gaussian lobes beneath the bright bloom.
        // Extending the seed keeps a substantial dark band instead of thinning it by blur alone.
        int spread=small?6:9;var expanded=new double[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            double value=0;
            for(int j=-spread;j<=spread;j++)for(int i=-spread;i<=spread;i++){
                if(i*i+j*j>spread*spread)continue;int xx=x+i,yy=y+j;
                if(xx>=0&&xx<w&&yy>=0&&yy<h)value=Math.Max(value,coverage[yy*w+xx]);
            }
            expanded[y*w+x]=value;
        }
        var near=BlurEmission(expanded,w,h,small?10:14);
        var far=BlurEmission(expanded,w,h,small?19:27);
        var shadow=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        // Keep the wider footprint, with the original lighter tint and shadow gain.
        Color tint=NodeShade(accent);
        int offsetY=small?2:3;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            int yy=y-offsetY;if(yy<0)continue;
            double edge=Math.Min(Math.Min(x,w-1-x),Math.Min(y,h-1-y));
            double fade=Math.Max(0,Math.Min(1,edge/(small?5.0:7.0)));fade=fade*fade*(3-2*fade);
            // Round off the expanded field before the fixed texture boundary, avoiding a square patch.
            double dx=(x-(w-1)*.5)/((w-1)*.5),dy=(y-(h-1)*.5)/((h-1)*.5);
            double radial=Math.Max(0,Math.Min(1,(1-Math.Sqrt(dx*dx+dy*dy))/.36));
            fade*=radial*radial*(3-2*radial);
            int alpha=Math.Max(0,Math.Min(255,(int)Math.Round(.5625*(115*near[yy*w+x]+190*far[yy*w+x])*fade)));
            if(alpha>0)shadow.SetPixel(x,y,A(alpha,tint));
        }
        return shadow;
    }
    static void NodeWaterline(Bitmap image,Color accent,double[] distances,double level){
        int w=image.Width,h=image.Height;
        var data=image.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        var rgba=new int[w*h];System.Runtime.InteropServices.Marshal.Copy(data.Scan0,rgba,0,rgba.Length);
        Color water=Mix(accent,Color.White,.82f);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            int index=y*w+x;Color pixel=Color.FromArgb(rgba[index]);if(pixel.A==0)continue;
            double distance=distances[index];bool onCrystal=!double.IsNaN(distance);
            bool submerged=onCrystal?distance>=0:y+.5>=level*4;
            int alpha=submerged?(int)Math.Round(pixel.A*.5):pixel.A;
            Color color=pixel;
            // The visible surface/plane intersection is rasterized per triangle with the same depth buffer.
            // Its screen-space distance keeps a fine meniscus consistent across differently angled facets.
            if(onCrystal){
                double weight=.88*Math.Exp(-distance*distance/(2*1.8*1.8));
                if(weight>.002)color=Mix(color,water,(float)weight);
            }
            rgba[index]=A(alpha,color).ToArgb();
        }
        System.Runtime.InteropServices.Marshal.Copy(rgba,0,data.Scan0,rgba.Length);image.UnlockBits(data);
    }
    static void Node(string output,int w,int h,Color accent,bool small) {
        // Stella octangula: eight triangular pyramids on the faces of an octahedron.
        // Its 24 exterior triangular faces are the union of two interpenetrating tetrahedra.
        var faces=new List<V3[]>();
        foreach(int sx in new[]{-1,1})foreach(int sy in new[]{-1,1})foreach(int sz in new[]{-1,1}){
            V3 apex=new V3(sx,sy,sz),a=new V3(sx,0,0),b=new V3(0,sy,0),c=new V3(0,0,sz);
            faces.Add(new[]{Rotate(apex),Rotate(a),Rotate(b)});
            faces.Add(new[]{Rotate(apex),Rotate(b),Rotate(c)});
            faces.Add(new[]{Rotate(apex),Rotate(c),Rotate(a)});
        }
        double minX=999,minY=999,maxX=-999,maxY=-999;
        foreach(var face in faces)foreach(var v in face){double k=7/(7-v.Z*.35),px=v.X*k,py=-v.Y*k;minX=Math.Min(minX,px);maxX=Math.Max(maxX,px);minY=Math.Min(minY,py);maxY=Math.Max(maxY,py);}
        double radius=small?32:55,scale=Math.Min(radius*2/(maxX-minX),radius*2/(maxY-minY));
        double cx=w*.5,cy=small?56:80;
        int sw=w*4,sh=h*4;
        var zbuffer=new double[sw*sh];for(int i=0;i<zbuffer.Length;i++)zbuffer[i]=double.NegativeInfinity;
        var pixels=new int[sw*sh];
        var waterDistances=new double[sw*sh];for(int i=0;i<waterDistances.Length;i++)waterDistances[i]=double.NaN;
        foreach(var face in faces){
            V3 n=Cross(face[1]-face[0],face[2]-face[0]);V3 center=new V3((face[0].X+face[1].X+face[2].X)/3,(face[0].Y+face[1].Y+face[2].Y)/3,(face[0].Z+face[1].Z+face[2].Z)/3);
            if(Dot(n,center)<0)n=new V3(-n.X,-n.Y,-n.Z);
            if(n.Z<=0)continue;
            double len=Math.Sqrt(Dot(n,n));n=new V3(n.X/len,n.Y/len,n.Z/len);
            double diffuse=Math.Max(0,Dot(n,new V3(-.46,.72,.52)));
            var p=new PointF[3];
            for(int j=0;j<3;j++){double k=7/(7-face[j].Z*.35);p[j]=P((float)((cx+(face[j].X*k-(minX+maxX)/2)*scale)*4),(float)((cy+(-face[j].Y*k-(minY+maxY)/2)*scale)*4));}
            double denominator=(p[1].Y-p[2].Y)*(p[0].X-p[2].X)+(p[2].X-p[1].X)*(p[0].Y-p[2].Y);
            if(Math.Abs(denominator)<.001)continue;
            // From above, the near side of the water plane projects lower: Y + .30 Z = 0.
            // Its visible intersection bends downward over the forward-facing facets.
            var depths=new double[3];for(int j=0;j<3;j++)depths[j]=(-face[j].Y-.30*face[j].Z)*scale*4;
            double gx=((depths[0]-depths[2])*(p[1].Y-p[2].Y)-(depths[1]-depths[2])*(p[0].Y-p[2].Y))/denominator;
            double gy=((p[0].X-p[2].X)*(depths[1]-depths[2])-(p[1].X-p[2].X)*(depths[0]-depths[2]))/denominator;
            double waterGradient=Math.Max(.00001,Math.Sqrt(gx*gx+gy*gy));
            int left=Math.Max(0,(int)Math.Floor(Math.Min(p[0].X,Math.Min(p[1].X,p[2].X)))),right=Math.Min(sw-1,(int)Math.Ceiling(Math.Max(p[0].X,Math.Max(p[1].X,p[2].X))));
            int top=Math.Max(0,(int)Math.Floor(Math.Min(p[0].Y,Math.Min(p[1].Y,p[2].Y)))),bottom=Math.Min(sh-1,(int)Math.Ceiling(Math.Max(p[0].Y,Math.Max(p[1].Y,p[2].Y))));
            double[] heights=new double[3];
            for(int j=0;j<3;j++){PointF a=p[(j+1)%3],b=p[(j+2)%3];heights[j]=Math.Abs(denominator)/Math.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Y-a.Y)*(b.Y-a.Y));}
            for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++){
                double xx=x+.5,yy=y+.5;
                double a=((p[1].Y-p[2].Y)*(xx-p[2].X)+(p[2].X-p[1].X)*(yy-p[2].Y))/denominator;
                double b=((p[2].Y-p[0].Y)*(xx-p[2].X)+(p[0].X-p[2].X)*(yy-p[2].Y))/denominator,c=1-a-b;
                if(a<0||b<0||c<0)continue;
                double z=a*face[0].Z+b*face[1].Z+c*face[2].Z;int index=y*sw+x;if(z<=zbuffer[index])continue;
                zbuffer[index]=z;
                waterDistances[index]=(a*depths[0]+b*depths[1]+c*depths[2])/waterGradient;
                double edge=Math.Min(a*heights[0],Math.Min(b*heights[1],c*heights[2]));
                Color color=FacetColor(accent,diffuse,(a*.7+b*.15+c*.15)-.4);
                // Fine reflective ridges replace the old flat white perimeter.
                if(edge<1.6)color=Mix(color,Mix(accent,Color.White,.7f),(float)((1-edge/1.6)*.37));
                pixels[index]=color.ToArgb();
            }
        }
        using(var surface=new Bitmap(sw,sh,PixelFormat.Format32bppArgb)){
            var data=surface.LockBits(new Rectangle(0,0,sw,sh),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(pixels,0,data.Scan0,pixels.Length);surface.UnlockBits(data);
            var mask=new bool[w,h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)mask[x,y]=pixels[(y*4+2)*sw+x*4+2]!=0;
            PointF[] outline=Outline(mask,w,h);
            using(var backing=NodeShadow(pixels,w,h,accent,small))
            using(var glow=LuminanceGlow(pixels,w,h,accent))
            using(var hi=new Bitmap(sw,sh,PixelFormat.Format32bppArgb)){
                using(var g=Setup(hi))using(var silhouette=Poly(outline)){
                    g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    g.DrawImage(backing,new Rectangle(0,0,w,h),0,0,w,h,GraphicsUnit.Pixel);
                    g.DrawImage(glow,new Rectangle(0,0,w,h),0,0,w,h,GraphicsUnit.Pixel);
                    var shadow=g.Save();g.TranslateTransform(1,small?3:5);
                    using(var b=new SolidBrush(A(65,NodeShade(accent))))g.FillPath(b,silhouette);
                    for(int width=6;width>=2;width-=2)using(var pen=new Pen(A(6,NodeShade(accent)),width)){pen.LineJoin=LineJoin.Round;g.DrawPath(pen,silhouette);}
                    g.Restore(shadow);
                    g.DrawImage(surface,new Rectangle(0,0,w,h),0,0,sw,sh,GraphicsUnit.Pixel);
                }
                NodeWaterline(hi,accent,waterDistances,cy-(minY+maxY)*.5*scale);
                Save(hi,output,w,h,.92);
            }
        }
    }
    static void TraceTexture(Bitmap bmp) {
        int w=bmp.Width,h=bmp.Height;
        using(var hi=new Bitmap(w*4,h*4,PixelFormat.Format32bppArgb)) {
            using(var g=Setup(hi)) {
                // Identical star-map geometry in every 56px atlas strip prevents visual jumps.
                for(int frame=0;frame<4;frame++) {
                    var st=g.Save();g.TranslateTransform(0,frame*56);g.SetClip(new RectangleF(0,0,w,56));
                    Color etch=C(frame<2?"#267d85":"#9f762b");
                    var stars=new[]{P(19,15),P(51,36),P(85,24),P(118,43),P(151,17),P(184,30),P(217,12),P(250,40),P(283,24)};
                    Line(g,Color.White,1.8f,stars[0],stars[1],stars[2],stars[3]);
                    Line(g,Color.White,1.8f,stars[4],stars[5],stars[6]);
                    Line(g,Color.White,1.8f,stars[6],stars[7],stars[8]);
                    for(int i=0;i<stars.Length;i++) {
                        PointF p=stars[i];
                        if(i%3==0)Star(g,p.X,p.Y,5.2f,Color.White);
                        else using(var b=new SolidBrush(Color.White))g.FillEllipse(b,p.X-1.9f,p.Y-1.9f,3.8f,3.8f);
                    }
                    using(var p=new Pen(A(48,Color.White),.65f)) {
                        for(int offset=-w;offset<=w;offset+=w) {
                            g.DrawEllipse(p,offset-35,-11,188,70);
                            g.DrawEllipse(p,offset+142,14,190,66);
                        }
                    }
                    for(int x=8;x<w;x+=19) {
                        float y=7+(x*13%39);
                        using(var b=new SolidBrush(A(230,Color.White)))g.FillEllipse(b,x-.2f,y-.2f,1.7f,1.7f);
                    }
                    // Fine streaming rails and instrument ticks remain secondary to the star map.
                    Line(g,A(48,Color.White),.65f,P(0,51),P(w,51));
                    for(int x=7;x<w;x+=12)Line(g,A(45,etch),.6f,P(x,52),P(x,54));
                    g.Restore(st);
                }
            }
            using(var pattern=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {
                using(var g=Graphics.FromImage(pattern)) {
                    g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    g.DrawImage(hi,new Rectangle(0,0,w,h),0,0,hi.Width,hi.Height,GraphicsUnit.Pixel);
                }
                for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                    Color basis=bmp.GetPixel(x,y),detail=pattern.GetPixel(x,y);
                    // Subtract coverage from alpha: stars and links reveal the background.
                    // Outside the cutouts, the four original color and opacity ramps remain intact.
                    int alpha=(int)Math.Round(basis.A*(1-.25f*detail.A/255f));
                    bmp.SetPixel(x,y,alpha==0?Color.Transparent:A(alpha,basis));
                }
            }
        }
    }
    static void Lines(string name,string output,int w,int h) {
        // Repeatable connection textures keep their original strip layout and alpha ramps.
        using(var bmp=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                Color color;
                if(name=="traceLine_eff.png") {
                    int frame=y/56;float t=(y%56)/55f;
                    Color a=C(frame<2?"#2bf2c5":"#ffcc46");
                    int alpha=(int)((frame<2?36:47)+(252-(frame<2?36:47))*Math.Pow(t,1.48));
                    Color c=Mix(a,Color.White,.06f+.20f*t+(frame%2)*.08f);
                    color=A(alpha,c);
                } else if(name=="longNoteLine.png") {
                    Color a=C(y<8?"#2bf2c5":"#ffcc46");float t=(y%8)/7f;
                    float edge=Math.Min(1,Math.Abs(x-152.5f)/115.5f);
                    Color centre=Mix(a,Color.White,.35f+.43f*t);
                    Color shaded=Mix(centre,Color.White,.34f*edge*edge);
                    // custom01 uses 33px clear margins and a 4px half-alpha lip at each end.
                    int alpha=x<33||x>=273?0:x<37||x>=269?102:204;
                    color=alpha==0?Color.Transparent:A(alpha,shaded);
                } else {
                    float d=Math.Abs(y-19.5f);int alpha=(int)(242*Math.Exp(-d*d/15f));
                    color=A(alpha,Mix(C("#cbd6ff"),Color.White,.7f));
                }
                bmp.SetPixel(x,y,color);
            }
            // traceLine_eff now uses only its four smooth color/opacity ramps, without cutout texture.
            bmp.Save(output,ImageFormat.Png);
        }
    }
    public static void RenderDamageNode(string reference,string target) {
        using(var im=new Bitmap(reference)) {
            Node(target,im.Width,im.Height,C("#b263ff"),true);
        }
    }
    public static void Run(string reference,string target,bool nodesOnly) {
        Directory.CreateDirectory(target);int count=0;
        foreach(string path in Directory.GetFiles(reference,"*.png",SearchOption.AllDirectories)) {
            string name=Path.GetFileName(path);if(name=="notes.png")continue;
            if(nodesOnly&&!(name.StartsWith("notes_friction")||name.StartsWith("notes_long")))continue;
            string rel=path.Substring(reference.Length).TrimStart(Path.DirectorySeparatorChar);
            string output=Path.Combine(target,Path.GetFileName(path));int w,h;
            using(var im=new Bitmap(path)){w=im.Width;h=im.Height;}
            if(name.StartsWith("notes_flick_arrow")) {
                string geometry=Path.Combine(Path.GetDirectoryName(path),name.Replace("_crtcl",""));
                string n=name.Replace("notes_flick_arrow_","").Replace("crtcl_","").Substring(0,2);
                Arrow(geometry,output,w,h,name.Contains("crtcl"),int.Parse(n));
            } else if(name.StartsWith("notes_friction")||name.StartsWith("notes_long")) {
                Color accent=C(name.Contains("crtcl")?"#ffcc46":name.Contains("flick")?"#ff73b3":"#2bf2c5");
                Node(output,w,h,accent,name.StartsWith("notes_friction"));
            } else Lines(name,output,w,h);
            count++;Console.WriteLine(rel+" "+w+"x"+h);
        }
        Console.WriteLine("Rendered "+count+" textures. Approved notes.png was not modified.");
    }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
$root=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'NotesV2'
$reference=Join-Path (Split-Path $root -Parent) '..\Assets\Game\Resources\live\note\custom01\textures'
if(-not(Test-Path -LiteralPath $reference)){$reference=Join-Path (Split-Path (Split-Path $root -Parent) -Parent) 'custom01\textures'}
if($DamageOnly) {
    [AstralRemaining]::RenderDamageNode((Join-Path $root 'notes_friction_among_long.png'), (Join-Path $root 'notes_damage_friction.png'))
    & (Join-Path $PSScriptRoot 'draw_notes.ps1') -DamageOnly
} else {
    [AstralRemaining]::Run($reference,(Join-Path $root '.'),$NodesOnly.IsPresent)
}
