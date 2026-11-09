# Replaces the note-skin textures with self-made placeholders, or restores a local pack.
#
# Why: the shipped note skins are original game art. The layouts must stay exactly as they are (sprite
# rects, nine-slice borders and pivots live in the .asset files, the textures are referenced by GUID),
# so this script only repaints the pixels and never touches a .asset/.meta. Every texture keeps its
# size, its transparent padding and its slice borders; only the drawing inside changes.
#
# The placeholder keeps two things from the original, both of which are facts about the game rather
# than about the art: the silhouette family (bar / chevron / diamond) and the hue of each sprite (the
# bar atlas is colour coded per slice - the first three slices are the long-note head, body and tail).
# Colours are sampled from the local pack when it exists, so the placeholder stays readable in place.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/replace-note-skins.ps1            # preview
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/replace-note-skins.ps1 -Apply     # write
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/replace-note-skins.ps1 -Set custom02
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/replace-note-skins.ps1 -Export    # backup
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/replace-note-skins.ps1 -Restore   # back to art
#
# -Export / -Restore exist for one reason: the original art must survive on this machine after the
# placeholders land, without ever entering the repository. LocalPacks/ is git-ignored.
param(
    [ValidateSet('custom01', 'custom02', 'all')] [string]$Set = 'custom01',
    [switch]$Apply,
    [switch]$Export,
    [switch]$Restore,
    [switch]$NoSample,
    # Copy hand-painted art from this folder instead of generating it. The folder must mirror the skin
    # layout (custom01\textures\..., custom01\textures\noteskin\...). Sizes are checked against the
    # existing textures: a sprite rect is a pixel rectangle, so a differently sized file would slice
    # the picture in the wrong place and there is no code path that could repair that at run time.
    [string]$From
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$packRoot = Join-Path $root 'LocalPacks\original-skin'

# Hand-painted import: copy a folder that mirrors the skin layout over the repository textures.
if ($From) {
    $srcRoot = (Resolve-Path $From).Path
    $sets = if ($Set -eq 'all') { @('custom01', 'custom02') } else { @($Set) }
    $copied = 0; $skipped = 0; $bad = 0

    foreach ($s in $sets) {
        $dstRoot = Join-Path $root "Assets\Game\Resources\live\note\$s"
        # -From is either a folder holding one sub-folder per set, or a folder that is itself one set
        # (a hand-made skin arrives as <name>\textures\... without knowing which slot it will fill).
        $srcSet = Join-Path $srcRoot $s
        if (-not (Test-Path $srcSet)) {
            if (Test-Path (Join-Path $srcRoot 'textures')) { $srcSet = $srcRoot }
            else { "skip     $s (no $srcSet)"; continue }
        }

        foreach ($file in (Get-ChildItem $srcSet -Recurse -Filter *.png)) {
            $rel = $file.FullName.Substring($srcSet.Length).TrimStart('\')
            $dst = Join-Path $dstRoot $rel
            if (-not (Test-Path $dst)) { "no such target  $rel"; $skipped++; continue }

            $a = [System.Drawing.Bitmap]::FromFile($file.FullName)
            $b = [System.Drawing.Bitmap]::FromFile($dst)
            $same = ($a.Width -eq $b.Width -and $a.Height -eq $b.Height)
            $sizeText = "$($a.Width)x$($a.Height) vs $($b.Width)x$($b.Height)"
            $a.Dispose(); $b.Dispose()

            if (-not $same) {
                "size mismatch   $rel  ($sizeText) - not copied"
                $bad++
                continue
            }
            Copy-Item $file.FullName $dst -Force
            "copied   $rel"
            $copied++
        }
    }

    "== imported $copied, size-mismatched $bad, skipped $skipped from $srcRoot"
    return
}

if ($Export -or $Restore) {
    $textures = foreach ($s in @('custom01', 'custom02')) {
        Get-ChildItem (Join-Path $root "Assets\Game\Resources\live\note\$s\textures") -Recurse -File -Filter *.png
    }

    if ($Export) {
        foreach ($t in $textures) {
            $setName = if ($t.FullName -match '\\(custom0\d)\\') { $matches[1] } else { 'unknown' }
            $rel = $t.FullName.Substring($t.FullName.IndexOf($setName) + $setName.Length).TrimStart('\')
            $dest = Join-Path (Join-Path $packRoot $setName) $rel
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
            Copy-Item $t.FullName $dest -Force
            "saved    $dest"
        }
        "== exported $($textures.Count) textures to $packRoot"
        return
    }

    $restored = 0
    foreach ($t in $textures) {
        $setName = if ($t.FullName -match '\\(custom0\d)\\') { $matches[1] } else { $null }
        if (-not $setName) { continue }
        $rel = $t.FullName.Substring($t.FullName.IndexOf($setName) + $setName.Length).TrimStart('\')
        $src = Join-Path (Join-Path $packRoot $setName) $rel
        if (-not (Test-Path $src)) { "missing  $src"; continue }
        Copy-Item $src $t.FullName -Force
        "restored $($t.FullName)"
        $restored++
    }
    "== restored $restored textures from $packRoot"
    return
}

$sets = if ($Set -eq 'all') { @('custom01', 'custom02') } else { @($Set) }
$previewDir = Join-Path $root 'Temp\skin-preview'
New-Item -ItemType Directory -Force -Path $previewDir | Out-Null

function New-Canvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    return @($bmp, $g)
}

# Where the original art actually has pixels, inside a sprite rect. The placeholder is drawn into this
# box instead of the whole rect: it is what keeps a key the same size as the one it replaces, and it is
# the only way a friction marker comes out flat the way the original one is.
function Get-OpaqueBounds($bmp, [int]$x, [int]$y, [int]$w, [int]$h, [int]$threshold = 40) {
    if (-not $bmp) { return @([pscustomobject]@{ X = $x; Y = $y; W = $w; H = $h; Found = $false }) }
    $minX = $x; $minY = $y; $maxX = -1; $maxY = -1
    $x2 = [Math]::Min($x + $w, $bmp.Width); $y2 = [Math]::Min($y + $h, $bmp.Height)
    for ($yy = $y; $yy -lt $y2; $yy++) {
        for ($xx = $x; $xx -lt $x2; $xx++) {
            if ($bmp.GetPixel($xx, $yy).A -lt $threshold) { continue }
            if ($xx -lt $minX) { $minX = $xx }
            if ($xx -gt $maxX) { $maxX = $xx }
            if ($yy -lt $minY) { $minY = $yy }
            if ($yy -gt $maxY) { $maxY = $yy }
        }
    }
    if ($maxX -lt 0) { return @([pscustomobject]@{ X = $x; Y = $y; W = $w; H = $h; Found = $false }) }
    return @([pscustomobject]@{ X = $minX; Y = $minY; W = ($maxX - $minX + 1); H = ($maxY - $minY + 1); Found = $true })
}

function New-RoundPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {    $r = [Math]::Min($r, [Math]::Min($w, $h) / 2)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Mix([System.Drawing.Color]$a, [System.Drawing.Color]$b, [double]$t) {
    return [System.Drawing.Color]::FromArgb(
        255,
        [int]($a.R + ($b.R - $a.R) * $t),
        [int]($a.G + ($b.G - $a.G) * $t),
        [int]($a.B + ($b.B - $a.B) * $t))
}

# Average colour of the opaque pixels inside a rect of the ORIGINAL texture. Sampling (rather than
# reading every pixel) keeps this fast enough to run over two full skins.
function Get-Sample($bmp, [int]$x, [int]$y, [int]$w, [int]$h) {
    if ($null -eq $bmp) { return $null }
    $r = 0.0; $g2 = 0.0; $b = 0.0; $n = 0.0
    $step = [Math]::Max(2, [int][Math]::Sqrt([Math]::Max(1, ($w * $h) / 1500)))
    for ($yy = $y; $yy -lt ($y + $h); $yy += $step) {
        if ($yy -lt 0 -or $yy -ge $bmp.Height) { continue }
        for ($xx = $x; $xx -lt ($x + $w); $xx += $step) {
            if ($xx -lt 0 -or $xx -ge $bmp.Width) { continue }
            $c = $bmp.GetPixel($xx, $yy)
            # Translucent art (the trace strip fades from 14% to 88%) still carries its hue: weight the
            # sample by alpha instead of dropping those pixels, or the colour falls back to the default.
            if ($c.A -lt 16) { continue }
            $wgt = $c.A / 255.0
            $r += $c.R * $wgt; $g2 += $c.G * $wgt; $b += $c.B * $wgt; $n += $wgt
        }
    }
    if ($n -lt 4) { return $null }
    return [System.Drawing.Color]::FromArgb(255, [int]($r / $n), [int]($g2 / $n), [int]($b / $n))
}

# A palette derived from one sampled hue: pale interior, saturated border, near-white rim.
function New-Palette($hue) {
    $white = [System.Drawing.Color]::White
    if ($null -eq $hue) { $hue = [System.Drawing.Color]::FromArgb(255, 205, 215, 235) }
    return @{
        Light = Mix $hue $white 0.62
        Mid   = $hue
        Deep  = Mix $hue ([System.Drawing.Color]::Black) 0.18
        Rim   = Mix $hue $white 0.86
    }
}

# A note body: rounded bar, pale interior, saturated border, thin bright rim. Nine-slice safe: the
# rounded ends sit inside the 100 px borders and the middle is one flat band that may be stretched.
function Draw-NoteBody($g, [single]$x, [single]$y, [single]$w, [single]$h, $pal) {
    $radius = [single]([Math]::Min(46, $h / 2 - 2))
    $path = New-RoundPath $x $y $w $h $radius

    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF($x, $y)),
        (New-Object System.Drawing.PointF($x, ($y + $h))),
        $pal.Light, $pal.Mid)
    $g.FillPath($brush, $path)
    $brush.Dispose()

    $pen = New-Object System.Drawing.Pen($pal.Deep, [single]([Math]::Max(3, $h * 0.055)))
    $g.DrawPath($pen, $path)
    $pen.Dispose()

    # Inner bright band along the top, inset so the border stays visible.
    $inset = [single]([Math]::Max(3, $h * 0.06))
    $hlRect = New-Object System.Drawing.RectangleF(($x + $w * 0.05), ($y + $inset), ($w * 0.90), [single]([Math]::Max(2, $h * 0.10)))
    if ($hlRect.Height -gt 0 -and $hlRect.Width -gt 0) {
        $hl = New-Object System.Drawing.SolidBrush((Mix $pal.Light ([System.Drawing.Color]::White) 0.65))
        $g.FillRectangle($hl, $hlRect)
        $hl.Dispose()
    }
    $path.Dispose()
}

# A flick arrow: wide chevron with a notch under the tip. The turned variant is turned to the upper
# left, and its tip is pushed further along the arrow axis - with the plain proportions the far
# shoulder ends up higher than the tip after a turn and the arrow reads as pointing the other way.
function Draw-Chevron($g, [single]$ox, [single]$oy, [single]$w, [single]$h, [bool]$diagonal, $pal) {
    $cx = $ox + $w / 2; $cy = $oy + $h / 2

    $armX = if ($diagonal) { 0.32 } else { 0.34 }
    $shoulderY = 0.16
    $tipY = if ($diagonal) { 0.62 } else { 0.46 }

    $local = @(
        @(0.0, ($tipY * $h)),
        @(($armX * $w), ($shoulderY * $h)),
        @(($armX * $w), (-0.40 * $h)),
        @(0.0, (-0.12 * $h)),
        @((-$armX * $w), (-0.40 * $h)),
        @((-$armX * $w), ($shoulderY * $h))
    )

    $scale = if ($diagonal) { 0.86 } else { 1.0 }
    $angle = if ($diagonal) { [Math]::PI / 4 } else { 0.0 }
    $cos = [Math]::Cos($angle); $sin = [Math]::Sin($angle)

    $turned = foreach ($p in $local) {
        $x = $p[0] * $scale; $y = $p[1] * $scale
        New-Object System.Drawing.PointF(($x * $cos - $y * $sin), ($x * $sin + $y * $cos))
    }
    $xs = $turned | ForEach-Object { $_.X }
    $ys = $turned | ForEach-Object { $_.Y }
    $minX = ($xs | Measure-Object -Minimum).Minimum; $maxX = ($xs | Measure-Object -Maximum).Maximum
    $minY = ($ys | Measure-Object -Minimum).Minimum; $maxY = ($ys | Measure-Object -Maximum).Maximum
    $fit = [Math]::Min(1.0, 0.92 * [Math]::Min($w, $h) / [Math]::Max($maxX - $minX, $maxY - $minY))
    $midX = ($minX + $maxX) / 2; $midY = ($minY + $maxY) / 2

    $pts = foreach ($p in $turned) {
        New-Object System.Drawing.PointF((($cx + ($p.X - $midX) * $fit)), ($cy - ($p.Y - $midY) * $fit))
    }
    $pts = [System.Drawing.PointF[]]$pts

    # The shipped arrow is a thick coloured RING with a light outer edge, not a solid slab: the hole in
    # the middle is what keeps it readable when the sprite is scaled down. Draw it the same way - a
    # wide pen for the ring, a wider pen underneath for the light edge.
    $ring = [single]([Math]::Max(6, [Math]::Min($w, $h) * 0.17))
    $rimWidth = $ring + [single]([Math]::Max(4, $w * 0.030))

    $rimPen = New-Object System.Drawing.Pen($pal.Rim, $rimWidth)
    $rimPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPolygon($rimPen, $pts)
    $rimPen.Dispose()

    $ringPen = New-Object System.Drawing.Pen($pal.Mid, $ring)
    $ringPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPolygon($ringPen, $pts)
    $ringPen.Dispose()
}

# The small markers a bar shows along its body: a diamond, which is the shape the game uses there.
function Draw-Diamond($g, [single]$x, [single]$y, [single]$w, [single]$h, $pal, [bool]$hollow) {
    $cx = $x + $w / 2; $cy = $y + $h / 2
    $rx = $w * 0.5; $ry = $h * 0.5
    $pts = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF($cx, ($cy - $ry))),
        (New-Object System.Drawing.PointF(($cx + $rx), $cy)),
        (New-Object System.Drawing.PointF($cx, ($cy + $ry))),
        (New-Object System.Drawing.PointF(($cx - $rx), $cy))
    )

    if ($hollow) {
        # Thin ring: the friction marker in the shipped art is noticeably finer than a tap key, and a
        # filled diamond reads as one more tap key on the lane.
        $ring = [single]([Math]::Max(2, [Math]::Min($w, $h) * 0.09))
        $rim = New-Object System.Drawing.Pen($pal.Rim, ($ring + [Math]::Max(2, $w * 0.02)))
        $rim.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPolygon($rim, $pts); $rim.Dispose()
        $pen = New-Object System.Drawing.Pen($pal.Mid, $ring)
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPolygon($pen, $pts); $pen.Dispose()
        return
    }

    $fill = New-Object System.Drawing.SolidBrush($pal.Mid)
    $g.FillPolygon($fill, $pts); $fill.Dispose()
    $pen = New-Object System.Drawing.Pen($pal.Rim, [single]([Math]::Max(2, $w * 0.05)))
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPolygon($pen, $pts); $pen.Dispose()
}

$rectRegex = 'm_Rect:\s*\r?\n\s*serializedVersion: 2\s*\r?\n\s*x: (-?\d+)\s*\r?\n\s*y: (-?\d+)\s*\r?\n\s*width: (\d+)\s*\r?\n\s*height: (\d+)'

function Get-SpriteRects([string]$skinRoot) {
    $map = @{}
    Get-ChildItem $skinRoot -Recurse -Filter *.png.meta | ForEach-Object {
        $g = (Select-String -Path $_.FullName -Pattern '^guid: (\w+)' | Select-Object -First 1).Matches[0].Groups[1].Value
        if ($g) { $map[$g] = ($_.FullName -replace '\.meta$', '') }
    }
    $rects = @{}
    Get-ChildItem $skinRoot -Recurse -Filter *.asset | ForEach-Object {
        $t = Get-Content $_.FullName -Raw
        if ($t -notmatch 'Sprite:') { return }
        $name = [regex]::Match($t, 'm_Name: (.+)').Groups[1].Value.Trim()
        $guid = [regex]::Match($t, 'texture: \{fileID: 2800000, guid: (\w+)').Groups[1].Value
        $m = [regex]::Match($t, $rectRegex)
        if (-not $m.Success -or -not $map.ContainsKey($guid)) { return }
        $png = $map[$guid]
        if (-not $rects.ContainsKey($png)) { $rects[$png] = @() }
        $rects[$png] += [pscustomobject]@{
            Name = $name; X = [int]$m.Groups[1].Value; Y = [int]$m.Groups[2].Value
            W = [int]$m.Groups[3].Value; H = [int]$m.Groups[4].Value
        }
    }
    return $rects
}

foreach ($s in $sets) {
    $skinRoot = "Assets\Game\Resources\live\note\$s"
    if (-not (Test-Path $skinRoot)) { throw "skin not found: $skinRoot" }

    $rects = Get-SpriteRects $skinRoot
    $touched = 0

    foreach ($png in ($rects.Keys | Sort-Object)) {
        $src = [System.Drawing.Bitmap]::FromFile((Resolve-Path $png))
        $w = $src.Width; $h = $src.Height
        $src.Dispose()

        # The same texture in the local pack, used only to read the intended hue of each sprite.
        $rel = $png.Substring((Resolve-Path $skinRoot).Path.Length).TrimStart('\')
        $origPath = Join-Path (Join-Path $packRoot $s) $rel
        $orig = $null
        if (-not $NoSample -and (Test-Path $origPath)) { $orig = [System.Drawing.Bitmap]::FromFile($origPath) }

        $canvas = New-Canvas $w $h
        $bmp = $canvas[0]; $g = $canvas[1]

        foreach ($r in $rects[$png]) {
            # Unity sprite rects use a bottom-left origin; GDI+ is top-left.
            $top = $h - ($r.Y + $r.H)
            $pal = New-Palette (Get-Sample $orig $r.X $top $r.W $r.H)

            # Draw into the box the original art occupies, not into the whole rect: same footprint,
            # and shapes that are flat in the original (the friction markers) stay flat.
            $box = (Get-OpaqueBounds $orig $r.X $top $r.W $r.H)[0]
            $dx = [single]$box.X; $dy = [single]$box.Y; $dw = [single]$box.W; $dh = [single]$box.H

            switch -Regex ($r.Name) {
                '^notes_\d+$' {
                    Draw-NoteBody $g $dx $dy $dw $dh $pal
                    break
                }
                '^longNoteLine$' {
                    $p = New-RoundPath $dx $dy $dw $dh ([single]($dh / 2))
                    $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
                        (New-Object System.Drawing.PointF($dx, $dy)),
                        (New-Object System.Drawing.PointF($dx, ($dy + $dh))), $pal.Light, $pal.Mid)
                    $g.FillPath($br, $p); $br.Dispose(); $p.Dispose()
                    break
                }
                '^simultaneousLine$' {
                    $p = New-RoundPath $dx $dy $dw $dh ([single]($dw / 2))
                    $g.FillPath((New-Object System.Drawing.SolidBrush($pal.Light)), $p); $p.Dispose()
                    break
                }
                '^notes_flick_arrow' {
                    Draw-Chevron $g $dx $dy $dw $dh ($r.Name -match 'diagonal') $pal
                    break
                }
                '^notes_friction' {
                    # A friction key is a thin ring in the shipped art, visibly finer than a tap key.
                    Draw-Diamond $g $dx $dy $dw $dh $pal $false
                    break
                }
                '^notes_long_among' {
                    Draw-Diamond $g $dx $dy $dw $dh $pal $false
                    break
                }
                default {
                    $p = New-RoundPath $dx $dy $dw $dh ([single]12)
                    $g.FillPath((New-Object System.Drawing.SolidBrush($pal.Mid)), $p); $p.Dispose()
                    break
                }
            }
        }

        $g.Dispose()
        if ($orig) { $orig.Dispose() }

        $target = if ($Apply) { $png } else { Join-Path $previewDir ("$s-" + (Split-Path $png -Leaf)) }
        $bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $touched++
        if (-not $Apply) { "preview  $target" }
    }

    # Textures with no sprite in front of them (material textures such as traceLine_eff) never show up in
    # the sprite rects above, so repaint them whole. Missing them left an original asset in the build.
    $spritePngs = @{}
    foreach ($k in $rects.Keys) { $spritePngs[$k] = $true }
    foreach ($pngFile in (Get-ChildItem $skinRoot -Recurse -Filter *.png)) {
        $png = $pngFile.FullName
        if ($spritePngs.ContainsKey($png)) { continue }

        $rel = $png.Substring((Resolve-Path $skinRoot).Path.Length).TrimStart('\')
        $origPath = Join-Path (Join-Path $packRoot $s) $rel
        $orig = $null
        if (-not $NoSample -and (Test-Path $origPath)) { $orig = [System.Drawing.Bitmap]::FromFile($origPath) }

        $src = [System.Drawing.Bitmap]::FromFile($png)
        $w = $src.Width; $h = $src.Height
        $src.Dispose()

        $canvas = New-Canvas $w $h
        $bmp = $canvas[0]; $g = $canvas[1]

        # A stripe texture is drawn as stripes, everything else as one rounded plate. The band
        # boundaries are read from the original rather than assumed: the shipped trace strip has four
        # bands with transparent gaps, and dividing the canvas evenly drifts off them.
        $isStripes = $png -match 'traceLine|trace_line'
        $alpha = if ($isStripes) { 120 } else { 255 }

        # The shipped strip is four equal bands, each fading from near-transparent at its top to almost
        # opaque at its bottom. Four bands, and that vertical alpha ramp, are what makes the decoration
        # sit behind the lane instead of over it.
        # A marker that happens to have no sprite still has to look like a marker, not a plate.
        if (-not $isStripes -and ($png -match 'friction|long_among')) {
            $pal = New-Palette (Get-Sample $orig 0 0 $w $h)
            $box = (Get-OpaqueBounds $orig 0 0 $w $h)[0]
            Draw-Diamond $g $box.X $box.Y $box.W $box.H $pal $false
            $g.Dispose()
            if ($orig) { $orig.Dispose() }
            $target = if ($Apply) { $png } else { Join-Path $previewDir ("$s-" + $pngFile.Name) }
            $bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
            $bmp.Dispose()
            $touched++
            if (-not $Apply) { "preview  $target" }
            continue
        }

        $bandCount = if ($isStripes) { 4 } else { 1 }
        $bandH = [single]($h / $bandCount)
        for ($b = 0; $b -lt $bandCount; $b++) {
            $top = [single]($b * $bandH)
            $pal = New-Palette (Get-Sample $orig 0 ([int]($top + $bandH * 0.45)) $w ([int][Math]::Max(2, $bandH * 0.2)))
            # Row-by-row rather than a gradient brush: the brush overloads are ambiguous for a
            # PointF/Color/Color call, and this keeps the alpha ramp explicit.
            $fromA = if ($isStripes) { 20 } else { $alpha }
            $toA = if ($isStripes) { 225 } else { $alpha }
            $clip = New-RoundPath ([single]0) $top ([single]$w) $bandH ([single]([Math]::Min($bandH / 2, 8)))
            $g.SetClip($clip)
            $rows = [int][Math]::Max(1, $bandH)
            for ($r = 0; $r -lt $rows; $r++) {
                $t = if ($rows -le 1) { 0.0 } else { $r / ($rows - 1.0) }
                $a = [int]($fromA + ($toA - $fromA) * $t)
                $col = [System.Drawing.Color]::FromArgb($a, $pal.Mid)
                $pen = New-Object System.Drawing.Pen($col, [single]1)
                $g.DrawLine($pen, [single]0, [single]($top + $r), [single]($w - 1), [single]($top + $r))
                $pen.Dispose()
            }
            $g.ResetClip(); $clip.Dispose()
        }

        $g.Dispose()
        if ($orig) { $orig.Dispose() }

        $target = if ($Apply) { $png } else { Join-Path $previewDir ("$s-" + $pngFile.Name) }
        $bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $touched++
        if (-not $Apply) { "preview  $target" }
    }

    if ($Apply) { "== $s : $touched textures" } else { "== $s : $touched previews" }
}








