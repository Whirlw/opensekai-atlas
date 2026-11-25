# Checks the V2 art against what the game cuts out of it. Run in Windows PowerShell (5.1):
#   powershell -NoProfile -ExecutionPolicy Bypass -File ArtSource/Tools/verify.ps1
# Checks the generated file names and dimensions, the geometry in Assets and the visual specifications.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$data = Join-Path $PSScriptRoot 'data'
. (Join-Path $PSScriptRoot 'load-renderer.ps1')
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

public sealed class Pic
{
    public readonly int W, H;
    public readonly bool Rgba;
    private readonly byte[] _p;

    public Pic(string file)
    {
        using (var b = new Bitmap(file))
        {
            W = b.Width; H = b.Height; Rgba = b.PixelFormat == PixelFormat.Format32bppArgb;
            _p = new byte[W * H * 4];
            BitmapData d = b.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(d.Scan0, _p, 0, _p.Length);
            b.UnlockBits(d);
        }
    }

    public int A(int x, int y) { return _p[(y * W + x) * 4 + 3]; }
    private int C(int x, int y, int k) { return _p[(y * W + x) * 4 + k]; }

    /// <summary>The strongest alpha in a rectangle.</summary>
    public int MaxAlpha(int x, int y, int w, int h)
    {
        int best = 0;
        for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) best = Math.Max(best, A(i, j));
        return best;
    }

    public int MinAlpha(int x, int y, int w, int h)
    {
        int best = 255;
        for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) best = Math.Min(best, A(i, j));
        return best;
    }

    /// <summary>How much any row changes between two columns: 0 when a stretch between them cannot show.</summary>
    public int ColumnSpread(int x0, int x1, int y0, int y1)
    {
        int worst = 0;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                for (int k = 0; k < 4; k++)
                {
                    if (k < 3 && (A(x, y) == 0 || A(x0, y) == 0)) continue;
                    worst = Math.Max(worst, Math.Abs(C(x, y, k) - C(x0, y, k)));
                }
        return worst;
    }

    /// <summary>Largest alpha difference from another picture of the same size.</summary>
    public int AlphaDifference(Pic o, int top, int height)
    {
        int worst = 0;
        for (int i = top * W * 4 + 3; i < (top + height) * W * 4; i += 4) worst = Math.Max(worst, Math.Abs(_p[i] - o._p[i]));
        return worst;
    }

    /// <summary>Overlap of the two solid areas, 0..1.</summary>
    public double Overlap(Pic o)
    {
        long both = 0, any = 0;
        for (int i = 3; i < _p.Length; i += 4)
        {
            bool a = _p[i] > 127, b = o._p[i] > 127;
            if (a && b) both++;
            if (a || b) any++;
        }
        return any == 0 ? 1 : both / (double)any;
    }

    /// <summary>The average relative luminance of a rectangle laid over another luminance, 0..1.</summary>
    public double Luminance(int x, int y, int w, int h, double under)
    {
        double sum = 0;
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
            {
                double own = 0.2126 * Linear(C(i, j, 2)) + 0.7152 * Linear(C(i, j, 1)) + 0.0722 * Linear(C(i, j, 0));
                double a = A(i, j) / 255.0;
                sum += own * a + under * (1 - a);
            }
        return sum / (w * h);
    }

    /// <summary>How far a pixel is from grey, 0..1.</summary>
    public double Saturation(int x, int y)
    {
        int max = Math.Max(C(x, y, 0), Math.Max(C(x, y, 1), C(x, y, 2))), min = Math.Min(C(x, y, 0), Math.Min(C(x, y, 1), C(x, y, 2)));
        return max == 0 ? 0 : (max - min) / (double)max;
    }

    private static double Linear(int v)
    {
        double s = v / 255.0;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    /// <summary>Bounds of everything at least this opaque, as left, top, right, bottom (inclusive).</summary>
    public int[] Bounds(int alpha, int x, int y, int w, int h)
    {
        int l = W, t = H, r = -1, b = -1;
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                if (A(i, j) >= alpha) { l = Math.Min(l, i); r = Math.Max(r, i); t = Math.Min(t, j); b = Math.Max(b, j); }
        return new[] { l, t, r, b };
    }
}

public static class SpriteMesh
{
    private static float[] Numbers(string text, string pattern)
    {
        Match m = Regex.Match(text, pattern);
        var v = new float[m.Groups.Count - 1];
        for (int i = 0; i < v.Length; i++) v[i] = float.Parse(m.Groups[i + 1].Value, CultureInfo.InvariantCulture);
        return v;
    }

    private static byte[] Hex(string s)
    {
        var b = new byte[s.Length / 2];
        for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
        return b;
    }

    /// <summary>The solid pixels of a picture that its sprite's mesh leaves out, in the sprite's cell or in all of it.</summary>
    public static int Outside(string assetFile, Pic picture, bool whole)
    {
        string t = File.ReadAllText(assetFile);
        float[] r = Numbers(t, @"m_Rect:\s+serializedVersion: 2\s+x: ([-\d.e]+)\s+y: ([-\d.e]+)\s+width: ([-\d.e]+)\s+height: ([-\d.e]+)");
        float[] p = Numbers(t, @"m_Pivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}");
        float ppu = Numbers(t, @"m_PixelsToUnits: ([-\d.e]+)")[0];
        string rd = t.Substring(t.IndexOf("m_RD:", StringComparison.Ordinal));
        byte[] ib = Hex(Regex.Match(rd, "m_IndexBuffer: ([0-9a-f]*)").Groups[1].Value);
        byte[] vb = Hex(Regex.Match(rd, "_typelessdata: ([0-9a-f]*)").Groups[1].Value);
        int x0 = Math.Max(0, (int)r[0]), x1 = Math.Min(picture.W, (int)Math.Ceiling(r[0] + r[2]));
        int y0 = Math.Max(0, (int)r[1]), y1 = Math.Min(picture.H, (int)Math.Ceiling(r[1] + r[3]));
        if (whole) { x0 = y0 = 0; x1 = picture.W; y1 = picture.H; }
        var on = new bool[(x1 - x0) * (y1 - y0)];
        for (int k = 0; k + 5 < ib.Length; k += 6)
        {
            var q = new double[6];
            for (int j = 0; j < 3; j++)
            {
                int v = BitConverter.ToUInt16(ib, k + j * 2) * 12;
                q[j * 2] = r[0] + p[0] * r[2] + BitConverter.ToSingle(vb, v) * ppu;
                q[j * 2 + 1] = r[1] + p[1] * r[3] + BitConverter.ToSingle(vb, v + 4) * ppu;
            }
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    double X = x + 0.5, Y = y + 0.5;
                    double a = (q[2] - q[0]) * (Y - q[1]) - (q[3] - q[1]) * (X - q[0]);
                    double b = (q[4] - q[2]) * (Y - q[3]) - (q[5] - q[3]) * (X - q[2]);
                    double c = (q[0] - q[4]) * (Y - q[5]) - (q[1] - q[5]) * (X - q[4]);
                    if ((a >= 0 && b >= 0 && c >= 0) || (a <= 0 && b <= 0 && c <= 0)) on[(y - y0) * (x1 - x0) + x - x0] = true;
                }
        }
        int count = 0;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (!on[(y - y0) * (x1 - x0) + x - x0] && picture.A(x, picture.H - 1 - y) > 100) count++;
        return count;
    }
}
'@

$script:failed = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if (-not $ok) { $script:failed++ }
    Write-Host ("{0}  {1}{2}" -f $(if ($ok) { 'ok  ' } else { 'FAIL' }), $name, $(if ($detail) { "  ($detail)" } else { '' }))
}
function Pic([string]$group, [string]$name) { New-Object Pic (Join-Path $root "$group\$name") }

# Check the published PNGs against the renderer's inputs without redrawing them.
$total = 0
foreach ($group in 'Notes', 'NotesV2', 'HUD', 'Effects', 'Backgrounds') {
    $want = @([ArtV2.Render]::Group($group, $data))
    $have = @(Get-ChildItem (Join-Path $root $group) -File -Filter *.png)
    $missing = @($want.Name | Where-Object { $_ -notin $have.Name })
    $extra = @($have.Name | Where-Object { $_ -notin $want.Name })
    $bad = foreach ($asset in $want) {
        if ($asset.Name -in $missing) { continue }
        $pic = Pic $group $asset.Name
        if ($pic.W -ne $asset.Width -or $pic.H -ne $asset.Height -or -not $pic.Rgba) { $asset.Name }
    }
    $total += $have.Count
    $errors = @($missing + $extra + @($bad) | Where-Object { $_ })
    Check "$group inventory, dimensions and RGBA format" ($errors.Count -eq 0) ($errors -join ' ')
}

# ---- notes.png
foreach ($skin in 'Notes', 'NotesV2') {
  $p = Pic $skin 'notes.png'
  foreach ($k in 0..6) {
    $y = $k * 188
    $slim = $k -ge 4; $top = if ($slim) { 72 } else { 50 }; $bottom = if ($slim) { 116 } else { 133 }
    $b = $p.Bounds(250, 0, $y, 354, 188)
    $clear = [Math]::Max($p.MaxAlpha(0, $y, 354, 1), $p.MaxAlpha(0, $y + 187, 354, 1))
    $same = $p.ColumnSpread(101, 254, $y, $y + 188)
    Check "$skin/notes.png cell $k" ($b[0] -eq 55 -and $b[2] -eq 298 -and $b[1] -eq $y + $top -and $b[3] -eq $y + $bottom - 1 -and $clear -eq 0 -and $same -eq 0) `
        "body x $($b[0])..$($b[2] + 1) rows $($b[1] - $y)..$($b[3] - $y + 1), isolation rows $clear, stretch $same"
  }
}

# ---- the bodies are at least as light as the first set's second skin
$lane = 0.006
$minimumLuminance = 0.70169285440119777, 0.6983611267703137, 0.5500670708434765, 0.61883079547062925, 0.48144521466785251, 0.47745512775855592, 0.35015717442229066
foreach ($skin in 'Notes', 'NotesV2') {
    $p = Pic $skin 'notes.png'
    foreach ($k in 0..6) {
        $top = if ($k -ge 4) { 72 } else { 50 }; $height = if ($k -ge 4) { 44 } else { 83 }
        $own = $p.Luminance(55, $k * 188 + $top, 244, $height, $lane); $least = $minimumLuminance[$k]
        Check "$skin/notes.png cell $k meets the luminance minimum" ($own -ge $least) ('{0:0.000}, at least {1:0.000}' -f $own, $least)
    }
}

# ---- common arrows, nodes and lines
$apart = @(Get-ChildItem (Join-Path $root 'Notes') -Filter *.png | Where-Object { $_.Name -notin 'notes.png','notes_damage.png' -and (Get-FileHash $_.FullName).Hash -ne (Get-FileHash (Join-Path $root "NotesV2\$($_.Name)")).Hash })
Check 'NotesV2 has the arrows, nodes and lines of Notes' ($apart.Count -eq 0) ($apart.Name -join ' ')

# Damage uses the Friction body slice and small node, with its own palette.
foreach ($skin in 'Notes', 'NotesV2') {
    $damage = Pic $skin 'notes_damage.png'
    $friction = Pic $skin 'notes.png'
    $top = 4 * 188
    Check "$skin Damage body matches the Friction silhouette" ($damage.AlphaDifference($friction, $top, 188) -eq 0)
    Check "$skin Damage atlas is empty outside the Friction cell" ($damage.MaxAlpha(0, 0, $damage.W, $top) -eq 0 -and $damage.MaxAlpha(0, $top + 188, $damage.W, $damage.H - $top - 188) -eq 0)
    $damage = Pic $skin 'notes_damage_friction.png'
    Check "$skin Damage node matches the Friction node silhouette" ($damage.AlphaDifference((Pic $skin 'notes_friction_among_long.png'), 0, $damage.H) -eq 0)
}

# ---- nine-slice middles
$p = Pic 'HUD' 'bg_lifeGauge.png'; Check 'bg_lifeGauge stretch columns 139..147' ($p.ColumnSpread(139, 147, 0, 80) -eq 0) "$($p.ColumnSpread(139, 147, 0, 80))"
$p = Pic 'HUD' 'bg_scoreGauge.png'; Check 'bg_scoreGauge stretch columns 138..139' ($p.ColumnSpread(138, 140, 0, 80) -eq 0)
Check 'SCORE lettering and shadow stay outside the stretch region' ($p.MaxAlpha(137, 0, 4, 20) -eq 0)
foreach ($n in 'bg_lifeGauge', 'bg_scoreGauge') { $p = Pic 'HUD' "$n.png"; $v = $p.MaxAlpha(0, $p.H - 1, $p.W, 1); Check "$n is clear in its last row" ($v -eq 0) "strongest alpha $v" }
foreach ($n in 'bar_lifeGauge_gn', 'bar_lifeGauge_rd') { $p = Pic 'HUD' "$n.png"; $v = $p.ColumnSpread(13, 251, 0, 24); Check "$n stretch columns" ($v -le 2) "$v" }

# ---- masks and stencils keep their shape
$mask = Pic 'HUD' 'lane_mask.png'
$d = $mask.AlphaDifference((New-Object Pic (Join-Path $data 'mask-reference\lane_mask.png')), 0, $mask.H)
Check 'lane_mask alpha' ($d -le 2) "largest difference $d"
foreach ($n in 'mask_2dmode_jacket_nml_main', 'mask_2dmode_jacket_mirror_main') {
    $o = (Pic 'HUD' "$n.png").Overlap((New-Object Pic (Join-Path $data "mask-reference\$n.png"))); Check "$n covers the same plane" ($o -gt 0.985) ('{0:P1}' -f $o)
}
foreach ($n in '16_9_white', 'bg_base_wh', 'bg_base_ingame_wh', 'white6x6', 'mask_line') {
    Check "$n matches the retained source" ((Get-FileHash (Join-Path $root "HUD\$n.png")).Hash -eq (Get-FileHash (Join-Path $data "kept\$n.png")).Hash)
}
foreach ($n in 'live_result_default_tex_common_Square_02', 'live_result_default_tex_common_white_02') {
    Check "$n matches the retained source" ((Get-FileHash (Join-Path $root "Effects\$n.png")).Hash -eq (Get-FileHash (Join-Path $data "kept\$n.png")).Hash)
}

# ---- countdown digits: cells of one texture, so nothing may be cut at a border
foreach ($n in 1..3) {
    $p = Pic 'HUD' "tex_live_countdown_$n.png"
    $v = [Math]::Max([Math]::Max($p.MaxAlpha(0, 0, $p.W, 1), $p.MaxAlpha(0, $p.H - 1, $p.W, 1)), [Math]::Max($p.MaxAlpha(0, 0, 1, $p.H), $p.MaxAlpha($p.W - 1, 0, 1, $p.H)))
    Check "tex_live_countdown_$n fades before its edge" ($v -le 3) "$v"
}

# ---- judge line: inside its sprite rectangle, ends on the lane edges
$p = Pic 'HUD' 'judgeLine.png'; $b = $p.Bounds(8, 0, 0, $p.W, $p.H)
Check 'judgeLine stays in rows 15..98' ($b[1] -ge 15 -and $b[3] -le 98) "rows $($b[1])..$($b[3])"
$half = { param($row) 6 * 1.19 * (5.46 - (-2.92 + (57 - $row) / 108)) / (5.46 + 2.96) * 108 }
$m = $p.Bounds(128, 0, 57, $p.W, 1); $want = & $half 57.5
Check 'judgeLine ends on the lane edges' ([Math]::Abs(($m[2] + 1 - $m[0]) / 2 - $want) -le 1.5) ('half width {0:N1}, lane {1:N1}' -f (($m[2] + 1 - $m[0]) / 2), $want)

# ---- banners: no letter leaves its slice
$rows = Import-Csv (Join-Path $PSScriptRoot 'data\result-letter-slices.csv')
foreach ($texture in 'ALL-PERFECT', 'FULL-COMBO', 'LIVE-CLEAR', 'LIVE-FINISH') {
    $p = Pic 'Effects' "live_result_default_$texture.png"
    $cells = @($rows | Where-Object { $_.texture -eq "$texture.png" -and [int]$_.width -lt $p.W } | Sort-Object { [int]$_.rectX })
    $worst = 0
    for ($i = 0; $i -le $cells.Count; $i++) {
        $x0 = if ($i -eq 0) { 0 } else { [int]$cells[$i - 1].rectX + [int]$cells[$i - 1].width }
        $x1 = if ($i -eq $cells.Count) { $p.W } else { [int]$cells[$i].rectX }
        if ($x1 -gt $x0) { $worst = [Math]::Max($worst, $p.MaxAlpha($x0, 0, $x1 - $x0, $p.H)) }
        if ($i -lt $cells.Count) { $x = [int]$cells[$i].rectX; foreach ($edge in $x, ($x + [int]$cells[$i].width - 1)) { $worst = [Math]::Max($worst, $p.MaxAlpha($edge, 0, 1, $p.H)) } }
    }
    Check "$texture letters stay in their slices" ($worst -le 24) "strongest alpha on a slice border $worst"
}

# ---- banners: every letter stays on its sprite's mesh, which follows an older letter and has holes

$result = Join-Path (Split-Path $root -Parent) 'Assets\Game\AssetBundles\resources\tutorial\effect_asset\live\result\default'
$prefabs = (Get-ChildItem $result -Filter *.prefab | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join ''
$textures = @{}
foreach ($texture in 'ALL-PERFECT', 'FULL-COMBO', 'LIVE-CLEAR', 'LIVE-FINISH', 'FAILED') {
    $textures[([regex]::Match([IO.File]::ReadAllText((Join-Path $result "textures\$texture.png.meta")), 'guid: ([0-9a-f]{32})').Groups[1].Value)] = $texture
}
$used = 0; $cut = @()
foreach ($file in Get-ChildItem (Join-Path $result 'textures') -Filter *.asset) {
    $guid = [regex]::Match([IO.File]::ReadAllText("$($file.FullName).meta"), 'guid: ([0-9a-f]{32})').Groups[1].Value
    if (-not $prefabs.Contains($guid)) { continue }
    $texture = $textures[[regex]::Match([IO.File]::ReadAllText($file.FullName), 'texture: \{fileID: \d+, guid: ([0-9a-f]{32})').Groups[1].Value]
    if (-not $texture) { continue }
    $used++
    $n = [SpriteMesh]::Outside($file.FullName, (Pic 'Effects' "live_result_default_$texture.png"), $false)
    if ($n -gt 12) { $cut += "$texture/$($file.BaseName) $n px" }
}
Check "$used banner sprites keep their letters" ($used -gt 0 -and $cut.Count -eq 0) ($cut -join ', ')
foreach ($n in 'bg_2dmode_window_nml', 'bg_2dmode_window_mirror') {
    $v = [SpriteMesh]::Outside((Join-Path (Split-Path $root -Parent) "Assets\Graphics\Sprites\$n.asset"), (Pic 'HUD' "$n.png"), $true)
    Check "$n keeps its whole card" ($v -eq 0) "$v px outside the mesh"
}

# ---- particles: nothing runs into a neighbouring slot or off a texture that is shown whole
$p = Pic 'Effects' 'live_tap_effect_0_tex_note_common_all.png'
$worst = 0
foreach ($cell in @(0,0,64),@(64,0,64),@(192,0,64),@(64,64,64),@(128,64,64),@(0,128,64),@(192,128,64),@(384,128,128),@(0,384,128),@(128,256,128),@(384,256,128),@(128,384,128),@(384,384,128),@(0,512,128),@(128,512,128),@(0,640,128),@(128,640,128),@(256,512,128),@(384,512,128),@(512,0,128),@(640,0,128),@(512,128,128),@(640,128,128),@(512,512,128),@(768,512,128),@(896,0,128),@(896,256,128)) {
    $x, $y, $s = $cell
    foreach ($v in $p.MaxAlpha($x, $y, $s, 1), $p.MaxAlpha($x, $y + $s - 1, $s, 1), $p.MaxAlpha($x, $y, 1, $s), $p.MaxAlpha($x + $s - 1, $y, 1, $s)) { $worst = [Math]::Max($worst, $v) }
}
Check 'hit effect slots are clear at their borders' ($worst -le 6) "strongest alpha $worst"
foreach ($n in 'live_result_default_tex_common_Circle_03', 'live_result_default_tex_common_flare_02', 'live_result_default_tex_flash_v2_01', 'live_result_default_tex_allperfect_flare', 'live_result_default_tex_fullcombo_flare', 'live_result_default_tex_clear_flare', 'live_result_default_tex_finish_flare') {
    $p = Pic 'Effects' "$n.png"
    $v = [Math]::Max([Math]::Max($p.MaxAlpha(0, 0, $p.W, 1), $p.MaxAlpha(0, $p.H - 1, $p.W, 1)), [Math]::Max($p.MaxAlpha(0, 0, 1, $p.H), $p.MaxAlpha($p.W - 1, 0, 1, $p.H)))
    Check "$n fades before its edge" ($v -le 3) "$v"
}
# The ribbons scroll along u: the last column has to continue into the first.
foreach ($n in 'Line_01', 'Line_02', 'Line_03') {
    $p = Pic 'Effects' "live_result_default_tex_common_$n.png"; $worst = 0
    for ($y = 0; $y -lt $p.H; $y++) { $worst = [Math]::Max($worst, [Math]::Abs($p.A(0, $y) - $p.A($p.W - 1, $y))) }
    Check "$n repeats without a seam" ($worst -le 6) "$worst"
}
$p = Pic 'Backgrounds' 'default.png'; Check 'default.png is opaque' ($p.MinAlpha(0, 0, $p.W, $p.H) -eq 255)
# The HUD over that stage, where the panels are. Their faces are as deep as it, so the edges have to carry them, and
# the faces have to keep their colour: grey ones (saturation 0.39) were tried and looked like dust.
function Contrast([double]$a, [double]$b) { ([Math]::Max($a, $b) + 0.05) / ([Math]::Min($a, $b) + 0.05) }
$stage = [Math]::Max($p.Luminance(20, 560, 560, 130, 0), $p.Luminance(1560, 560, 470, 130, 0))
foreach ($n in @('bg_scoreGauge', 138, 34, 28, 72), @('bg_lifeGauge', 139, 34, 28, 72), @('btn_stop', 47, 20, 8, 86), @('label_autolive', 189, 16, 9, 63), @('ui_scorerank_plate', 250, 130, 55, 651), @('bg_2dmode_window_nml', 300, 174, 160, 693)) {
    $q = Pic $(if ($n[0] -eq 'ui_scorerank_plate') { 'Backgrounds' } else { 'HUD' }) "$($n[0]).png"
    $edge = [Math]::Min((Contrast ($q.Luminance($n[1], $n[3], 2, 2, $stage)) $stage), (Contrast ($q.Luminance($n[1], $n[4], 2, 2, $stage)) $stage))
    $colour = $q.Saturation($n[1], $n[2])
    Check "$($n[0]) stands clear of the stage" ($edge -ge 4 -and $colour -ge 0.5) ('edge {0:N2}, saturation {1:N2}' -f $edge, $colour)
}
# The rank card and the score panel each have their own silhouette.
$p = Pic 'Backgrounds' 'ui_scorerank_plate.png'; $q = Pic 'HUD' 'bg_scoreGauge.png'
Check 'ui_scorerank_plate touches the top of the screen' ($p.MaxAlpha(0, 0, 500, 51) -eq 0 -and $p.MinAlpha(250, 52, 1, 600) -eq 255)
Check 'ui_scorerank_plate has a clear right margin at every height' ($p.MaxAlpha(476, 0, 24, $p.H) -eq 0)
Check 'bg_scoreGauge owns its rounded left end' ($q.A(2, 29) -lt 16 -and $q.A(4, 51) -eq 255 -and $q.A(26, 29) -eq 255)
$p = Pic 'Backgrounds' 'bg_sekai.png'; Check 'bg_sekai.png is opaque' ($p.MinAlpha(0, 0, $p.W, $p.H) -eq 255)

Write-Host ''
if ($script:failed) { Write-Host "$($script:failed) checks failed, $total files."; exit 1 }
Write-Host "All checks passed, $total files."
