# New skin: redraws ONLY notes.png, traceLine_eff.png and longNoteLine.png in ArtSource/Notes.
# All other textures stay as they are. The previous skin lives in ArtSource/NotesV2 (scripts in Tools/NotesV2).
# Usage: powershell -ExecutionPolicy Bypass -File draw_skin.ps1 [-Only notes|trace|long]   (default: all three)
# Output: ArtSource/Notes/<name>.png, plus skin-preview.png next to this script.
param([ValidateSet('all','notes','trace','long')][string]$Only='all')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class SkinV2 {
    const int S = 4; // supersampling for the notes atlas
    static Color C(string s) { return ColorTranslator.FromHtml(s); }
    static Color A(int a, Color c) { return Color.FromArgb(a, c); }
    static Color Mix(Color a, Color b, float t) { return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t)); }
    static PointF P(float x, float y) { return new PointF(x, y); }

    // ---------------------------------------------------------------- notes.png
    // 354 x 1316 atlas = 7 cells of 354 x 188, top to bottom:
    //   0 gold tap   1 cyan tap   2 blue(purple-cyan) tap   3 pink flick
    //   4 cyan slim  5 gold slim  6 pink slim          (rows 4..6 are the "slim" 40px bodies)
    // Geometry follows docs/operations/notes-atlas-spec.md: body ends at x = 55 / 299 (inside the 100px
    // 9-slice borders), wide face rows 50..128 + 5px thickness to 133, slim face 72..112 + 4px to 116.
    //
    // "Prism" skin: a saturated glass bar with a crisp white frame, a gloss band on the upper half and a
    // darker lower third. Each end cap carries a white gem (a diamond; a four-point star on critical notes)
    // and a divider, all inside the fixed 55..100 / 254..299 zones; the stretched middle is uniform in x.
    public static readonly string[] CellAccents = {"#ffcc46","#2bf2c5","#848fff","#ff73b3","#2bf2c5","#ffcc46","#ff73b3"};
    static GraphicsPath RR(float x, float y, float w, float h, float r) {
        var p = new GraphicsPath();
        p.AddArc(x, y, r*2, r*2, 180, 90); p.AddArc(x+w-r*2, y, r*2, r*2, 270, 90);
        p.AddArc(x+w-r*2, y+h-r*2, r*2, r*2, 0, 90); p.AddArc(x, y+h-r*2, r*2, r*2, 90, 90);
        p.CloseFigure(); return p;
    }
    static void DrawCell(Graphics g, int row, Color accent, bool slim) {
        float y = slim ? 72 : 50, h = slim ? 40 : 78, depth = slim ? 4 : 5, r = slim ? 9 : 13;
        const float L = 55, R = 354 - L; float w = R - L, cy = y + h/2;
        bool blue = row == 2, crit = row == 0 || row == 5;
        Color top  = blue ? C("#ae81ff") : Mix(accent, Color.White, .30f);
        Color mid  = blue ? Mix(C("#ae81ff"), C("#65caff"), .5f) : accent;
        Color bot  = blue ? C("#65caff") : Mix(accent, C("#3a2f6b"), .14f);
        Color deep = Mix(bot, C("#1b2247"), .55f);

        // Soft halo around the whole silhouette (face + thickness).
        using (var path = RR(L, y, w, h + depth, r))
            for (int width = 46; width >= 2; width -= 2) {
                float d = width/2f; int a = (int)((slim ? 11 : 16)*Math.Exp(-d*d/(2*9f*9f)));
                if (a == 0) continue;
                using (var br = new LinearGradientBrush(P(0, y - 30), P(0, y + h + 30), A(a, top), A(a, bot)))
                using (var p = new Pen(br, width)) { p.LineJoin = LineJoin.Round; g.DrawPath(p, path); }
            }
        // Thickness: the same silhouette dropped by `depth`, in the deep shade.
        using (var path = RR(L, y + depth, w, h, r)) using (var b = new SolidBrush(deep)) g.FillPath(b, path);
        // Face.
        using (var face = RR(L, y, w, h, r)) {
            using (var b = new LinearGradientBrush(P(0, y - 1), P(0, y + h + 1), top, bot)) {
                var blend = new ColorBlend(); blend.Positions = new[]{0f, .5f, 1f}; blend.Colors = new[]{top, mid, bot};
                b.InterpolationColors = blend; g.FillPath(b, face);
            }
            var clip = g.Save(); g.SetClip(face);
            float step = y + h*.64f;   // darker lower third, with a hairline on the step
            using (var b = new SolidBrush(A(54, deep))) g.FillRectangle(b, L, step, w, y + h - step);
            using (var p = new Pen(A(120, Color.White), 1f)) g.DrawLine(p, L, step, R, step);
            g.Restore(clip);
        }
        // Gloss band on the upper half.
        float inset = slim ? 4.5f : 6.5f;
        using (var gloss = RR(L + inset, y + inset, w - inset*2, h*.40f, Math.Max(2, r - inset)))
        using (var b = new LinearGradientBrush(P(0, y + inset - 1), P(0, y + inset + h*.40f + 1), A(140, Color.White), A(26, Color.White)))
            g.FillPath(b, gloss);
        // White frame.
        float fw = slim ? 2.4f : 3f;
        using (var frame = RR(L + fw/2, y + fw/2, w - fw, h - fw, r - fw/2)) using (var p = new Pen(A(250, Color.White), fw)) g.DrawPath(p, frame);
        // End caps: gem + divider, mirrored.
        foreach (int side in new[]{0, 1}) {
            var st = g.Save(); if (side == 1) { g.TranslateTransform(354, 0); g.ScaleTransform(-1, 1); }
            float cx = L + (slim ? 21 : 25), hw = slim ? 6.5f : 8.5f, hh = slim ? 9 : 16;
            PointF[] gem;
            if (crit) { float a = hw + (slim ? 3 : 5), q = slim ? 2.4f : 3.4f;
                gem = new[]{P(cx, cy-hh), P(cx+q, cy-q), P(cx+a, cy), P(cx+q, cy+q), P(cx, cy+hh), P(cx-q, cy+q), P(cx-a, cy), P(cx-q, cy-q)}; }
            else gem = new[]{P(cx, cy-hh), P(cx+hw, cy), P(cx, cy+hh), P(cx-hw, cy)};
            using (var p = new Pen(A(150, deep), slim ? 2.4f : 3.2f)) { p.LineJoin = LineJoin.Round; g.DrawPolygon(p, gem); }
            using (var b = new SolidBrush(A(252, Color.White))) g.FillPolygon(b, gem);
            float dx = L + (slim ? 38 : 43), m = slim ? 8 : 12;
            using (var p = new Pen(A(110, deep), 3f)) g.DrawLine(p, dx, y + m, dx, y + h - m);
            using (var p = new Pen(A(215, Color.White), 1.4f)) g.DrawLine(p, dx, y + m, dx, y + h - m);
            g.Restore(st);
        }
    }
    public static void Notes(string output) {
        using (var hi = new Bitmap(354*S, 1316*S, PixelFormat.Format32bppArgb)) {
            using (var g = Graphics.FromImage(hi)) {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform(S, S);
                for (int i = 0; i < 7; i++) {
                    var st = g.Save(); g.TranslateTransform(0, i*188);
                    DrawCell(g, i, C(CellAccents[i]), i >= 4);
                    g.Restore(st);
                }
            }
            using (var bmp = new Bitmap(354, 1316, PixelFormat.Format32bppArgb)) {
                using (var g = Graphics.FromImage(bmp)) {
                    g.CompositingMode = CompositingMode.SourceCopy; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(hi, new Rectangle(0,0,354,1316), 0,0,hi.Width,hi.Height, GraphicsUnit.Pixel);
                }
                bmp.Save(output, ImageFormat.Png);
            }
        }
    }

    // ------------------------------------------------------ shared line cross-section
    // Both line textures are sampled by noteline.shader (NoteLineView): u 0..0.125 (x 0..38.25) is a fixed
    // 0.4-unit strip outside each edge of the line, u 0.125..0.875 (x 38.25..267.75) is the line itself
    // (stretched to its width). Cross-section: soft outer glow, a bright rail on the edge, a calmer core.
    // Transparent pixels keep their colour so bilinear filtering does not pull in black.
    static Color LinePixel(int x, int w, Color accent, float bright, float bodyA, float railA, float glowA) {
        float d = Math.Min(x + .5f, w - x - .5f), edge = w*.125f, railW = 5.25f;
        Color rail = Mix(accent, Color.White, .50f + .40f*bright);
        Color body = Mix(accent, Color.White, .30f + .42f*bright);
        Color halo = Mix(accent, Color.White, .10f + .30f*bright);
        if (d < edge - railW) { float k = Math.Max(0, Math.Min(1, (d - 8)/(edge - railW - 8))); return A((int)(glowA*k*k), halo); }
        if (d < edge) return A((int)railA, rail);
        float e = Math.Min(1, (d - edge)/34f); e = e*e*(3 - 2*e);
        return A((int)(railA*.85f + (bodyA - railA*.85f)*e), Mix(rail, body, .25f + .75f*e));
    }

    // ----------------------------------------------------------- traceLine_eff.png
    // 4 frames of h/4 rows, top to bottom: normal pressed, normal idle, critical pressed, critical idle.
    // Inside a frame the shader walks upwards along the guide (bottom = start), so alpha ramps up downwards
    // and the guide fades out towards its end. Only rgb is taken from the pressed frame.
    public static void Trace(string output, int w, int h) {
        int fh = h/4;
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb)) {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
                int frame = y/fh; float t = (y%fh)/(fh - 1f); bool crit = frame >= 2, pressed = frame%2 == 0;
                float floor = crit ? 47 : 36, alpha = floor + (252 - floor)*(float)Math.Pow(t, 1.48);
                bmp.SetPixel(x, y, LinePixel(x, w, C(crit ? "#ffcc46" : "#2bf2c5"), (pressed ? .55f : .05f) + .15f*t, alpha*.62f, alpha, alpha*.22f));
            }
            bmp.Save(output, ImageFormat.Png);
        }
    }

    // ------------------------------------------------------------ longNoteLine.png
    // 4 bands of h/4 rows, top to bottom: normal pressed, normal idle, critical pressed, critical idle
    // (same sampling as above; bottom row of a band = the hold's head).
    public static void Long(string output, int w, int h) {
        int bh = h/4;
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb)) {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
                int band = y/bh; float t = (y%bh)/(bh - 1f); bool crit = band >= 2, pressed = band%2 == 0;
                bmp.SetPixel(x, y, LinePixel(x, w, C(crit ? "#ffcc46" : "#2bf2c5"), (pressed ? .62f : .04f) + .10f*t, 160 + 18*t, 250, 78));
            }
            bmp.Save(output, ImageFormat.Png);
        }
    }

    // --------------------------------------------------------------------- preview
    public static void Preview(string dir, string output) {
        using (var card = new Bitmap(1160, 960, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(card)) {
            g.Clear(C("#080e1d"));
            using (var notes = new Bitmap(Path.Combine(dir, "notes.png")))
                for (int i = 0; i < 7; i++) g.DrawImage(notes, new Rectangle(32+(i%3)*376, 20+(i/3)*200, 354, 188), new Rectangle(0, i*188, 354, 188), GraphicsUnit.Pixel);
            using (var t = new Bitmap(Path.Combine(dir, "traceLine_eff.png"))) g.DrawImage(t, 32, 640, t.Width, t.Height);
            using (var l = new Bitmap(Path.Combine(dir, "longNoteLine.png"))) { g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(l, 408, 640, l.Width, l.Height*8); }
            card.Save(output, ImageFormat.Png);
        }
    }
    public static void Run(string reference, string target, string preview, string only) {
        Directory.CreateDirectory(target);
        int tw, th, lw, lh;
        using (var i = new Bitmap(Path.Combine(reference, "traceLine_eff.png"))) { tw = i.Width; th = i.Height; }
        using (var i = new Bitmap(Path.Combine(reference, "longNoteLine.png"))) { lw = i.Width; lh = i.Height; }
        if (only == "all" || only == "notes") { Notes(Path.Combine(target, "notes.png")); Console.WriteLine("notes.png 354x1316"); }
        if (only == "all" || only == "trace") { Trace(Path.Combine(target, "traceLine_eff.png"), tw, th); Console.WriteLine("traceLine_eff.png " + tw + "x" + th); }
        if (only == "all" || only == "long")  { Long(Path.Combine(target, "longNoteLine.png"), lw, lh); Console.WriteLine("longNoteLine.png " + lw + "x" + lh); }
        Preview(target, preview);
    }
}
'@
Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
$art = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
[SkinV2]::Run((Join-Path $art 'Notes'), (Join-Path $art 'Notes'), (Join-Path $PSScriptRoot 'skin-preview.png'), $Only)
