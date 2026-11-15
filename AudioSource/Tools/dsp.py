"""Small pure-Python DSP toolkit for building the OpenSekai SE pack.

No third-party dependencies: signals are plain lists of floats (mono) or
tuples (left, right) of such lists (stereo). Everything runs at SR.
"""
import math
import random
import struct

SR = 48000


# ---------------------------------------------------------------- WAV I/O
def read_wav(path, start_s=0.0, dur_s=None, taps=16):
    """Read a WAV (PCM 16/24/32 or float32) and return mono float list at SR.

    Only the requested window is decoded, then summed to mono and
    resampled to SR when needed.
    """
    with open(path, "rb") as f:
        data = f.read()
    if data[:4] != b"RIFF" or data[8:12] != b"WAVE":
        raise ValueError(f"{path}: not a RIFF/WAVE file")
    i = 12
    fmt = None
    pcm = None
    while i + 8 <= len(data):
        cid = data[i:i + 4]
        size = struct.unpack("<I", data[i + 4:i + 8])[0]
        body = data[i + 8:i + 8 + size]
        if cid == b"fmt ":
            tag, ch, sr, _, block, bits = struct.unpack("<HHIIHH", body[:16])
            if tag == 0xFFFE:
                tag = struct.unpack("<H", body[24:26])[0]
            fmt = (tag, ch, sr, block, bits)
        elif cid == b"data":
            pcm = body
        i += 8 + size + (size & 1)
    tag, ch, sr, block, bits = fmt
    frames = len(pcm) // block
    f0 = int(start_s * sr)
    f1 = frames if dur_s is None else min(frames, f0 + int(dur_s * sr) + 64)
    width = bits // 8
    out = []
    for n in range(f0, f1):
        acc = 0.0
        base = n * block
        for c in range(ch):
            o = base + c * width
            if tag == 3 and bits == 32:
                v = struct.unpack_from("<f", pcm, o)[0]
            elif bits == 16:
                v = struct.unpack_from("<h", pcm, o)[0] / 32768.0
            elif bits == 24:
                b = pcm[o:o + 3]
                iv = b[0] | (b[1] << 8) | (b[2] << 16)
                if iv & 0x800000:
                    iv -= 0x1000000
                v = iv / 8388608.0
            elif bits == 32:
                v = struct.unpack_from("<i", pcm, o)[0] / 2147483648.0
            else:
                raise ValueError(f"{path}: unsupported {bits}-bit format {tag}")
            acc += v
        out.append(acc / ch)
    if sr != SR:
        out = resample(out, sr, SR, taps)
    return out


def read_loop(path):
    """Read a whole WAV that is itself a seamless loop, keeping its channels.

    Resampling treats the file as circular (wraps a few hundred samples onto
    each end first), so the seam survives the rate change. Returns a tuple of
    channel lists at SR (mono files come back as (x, x)).
    """
    with open(path, "rb") as f:
        data = f.read()
    i = 12
    while i + 8 <= len(data):
        cid = data[i:i + 4]
        size = struct.unpack("<I", data[i + 4:i + 8])[0]
        body = data[i + 8:i + 8 + size]
        if cid == b"fmt ":
            tag, ch, sr, _, block, bits = struct.unpack("<HHIIHH", body[:16])
            if tag == 0xFFFE:
                tag = struct.unpack("<H", body[24:26])[0]
        elif cid == b"data":
            pcm = body
        i += 8 + size + (size & 1)
    width = bits // 8
    frames = len(pcm) // block
    chans = [[0.0] * frames for _ in range(ch)]
    for n in range(frames):
        for c in range(ch):
            o = n * block + c * width
            if tag == 3 and bits == 32:
                v = struct.unpack_from("<f", pcm, o)[0]
            elif bits == 16:
                v = struct.unpack_from("<h", pcm, o)[0] / 32768.0
            elif bits == 24:
                b = pcm[o:o + 3]
                iv = b[0] | (b[1] << 8) | (b[2] << 16)
                v = (iv - 0x1000000 if iv & 0x800000 else iv) / 8388608.0
            else:
                v = struct.unpack_from("<i", pcm, o)[0] / 2147483648.0
            chans[c][n] = v
    if sr != SR:
        pad = 256
        n_out = round(frames * SR / sr)
        off = round(pad * SR / sr)
        chans = [resample(c[-pad:] + c + c[:pad], sr, SR)[off:off + n_out] for c in chans]
    if len(chans) == 1:
        chans = [chans[0], list(chans[0])]
    return tuple(chans[:2])


def write_wav(path, sig, rng=None):
    """Write 16-bit PCM at SR with TPDF dither. sig is mono list or (L, R)."""
    rng = rng or random.Random(0x5E)
    stereo = isinstance(sig, tuple)
    chans = list(sig) if stereo else [sig]
    n = len(chans[0])
    frames = bytearray()
    for i in range(n):
        for c in chans:
            v = c[i] * 32767.0 + (rng.random() - rng.random())
            v = int(round(v))
            v = 32767 if v > 32767 else (-32768 if v < -32768 else v)
            frames += struct.pack("<h", v)
    nch = len(chans)
    header = b"RIFF" + struct.pack("<I", 36 + len(frames)) + b"WAVE"
    header += b"fmt " + struct.pack("<IHHIIHH", 16, 1, nch, SR, SR * nch * 2, nch * 2, 16)
    header += b"data" + struct.pack("<I", len(frames))
    with open(path, "wb") as f:
        f.write(header + frames)


# ---------------------------------------------------------- resampling etc.
def resample(x, sr_in, sr_out, taps=16):
    """Windowed-sinc resampler (Blackman window, +/- taps zero crossings)."""
    ratio = sr_in / sr_out
    cutoff = min(1.0, 1.0 / ratio)
    n_out = int(len(x) / ratio)
    out = [0.0] * n_out
    half = int(taps / cutoff)
    nx = len(x)
    for n in range(n_out):
        t = n * ratio
        k0 = int(t)
        acc = 0.0
        for k in range(k0 - half + 1, k0 + half + 1):
            if 0 <= k < nx:
                d = t - k
                a = d * cutoff
                s = 1.0 if a == 0 else math.sin(math.pi * a) / (math.pi * a)
                w = 0.42 + 0.5 * math.cos(math.pi * d / half) + 0.08 * math.cos(2 * math.pi * d / half)
                acc += x[k] * s * w
        out[n] = acc * cutoff
    return out


def pitch(x, semis):
    """Varispeed pitch shift (changes duration) with cubic interpolation."""
    r = 2.0 ** (semis / 12.0)
    n_out = int((len(x) - 3) / r)
    out = [0.0] * n_out
    for n in range(n_out):
        t = n * r
        i = int(t)
        f = t - i
        y0 = x[i - 1] if i > 0 else x[0]
        y1, y2, y3 = x[i], x[i + 1], x[i + 2]
        c1 = 0.5 * (y2 - y0)
        c2 = y0 - 2.5 * y1 + 2 * y2 - 0.5 * y3
        c3 = 0.5 * (y3 - y0) + 1.5 * (y1 - y2)
        out[n] = ((c3 * f + c2) * f + c1) * f + y1
    return out


# ----------------------------------------------------------------- filters
def biquad(x, kind, freq, q=0.707, gain_db=0.0):
    """RBJ cookbook biquad: lp, hp, bp, peak, hshelf, lshelf."""
    w = 2 * math.pi * freq / SR
    cw, sw = math.cos(w), math.sin(w)
    alpha = sw / (2 * q)
    A = 10 ** (gain_db / 40)
    if kind == "lp":
        b0, b1, b2 = (1 - cw) / 2, 1 - cw, (1 - cw) / 2
        a0, a1, a2 = 1 + alpha, -2 * cw, 1 - alpha
    elif kind == "hp":
        b0, b1, b2 = (1 + cw) / 2, -(1 + cw), (1 + cw) / 2
        a0, a1, a2 = 1 + alpha, -2 * cw, 1 - alpha
    elif kind == "bp":
        b0, b1, b2 = alpha, 0.0, -alpha
        a0, a1, a2 = 1 + alpha, -2 * cw, 1 - alpha
    elif kind == "peak":
        b0, b1, b2 = 1 + alpha * A, -2 * cw, 1 - alpha * A
        a0, a1, a2 = 1 + alpha / A, -2 * cw, 1 - alpha / A
    elif kind in ("hshelf", "lshelf"):
        sq = 2 * math.sqrt(A) * alpha
        if kind == "hshelf":
            b0 = A * ((A + 1) + (A - 1) * cw + sq)
            b1 = -2 * A * ((A - 1) + (A + 1) * cw)
            b2 = A * ((A + 1) + (A - 1) * cw - sq)
            a0 = (A + 1) - (A - 1) * cw + sq
            a1 = 2 * ((A - 1) - (A + 1) * cw)
            a2 = (A + 1) - (A - 1) * cw - sq
        else:
            b0 = A * ((A + 1) - (A - 1) * cw + sq)
            b1 = 2 * A * ((A - 1) - (A + 1) * cw)
            b2 = A * ((A + 1) - (A - 1) * cw - sq)
            a0 = (A + 1) + (A - 1) * cw + sq
            a1 = -2 * ((A - 1) + (A + 1) * cw)
            a2 = (A + 1) + (A - 1) * cw - sq
    else:
        raise ValueError(kind)
    b0, b1, b2, a1, a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0
    out = [0.0] * len(x)
    x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        y = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, v, y1, y
        out[i] = y
    return out


def sweep_bp(x, f0, f1, q=2.0):
    """Band-pass whose centre glides exponentially from f0 to f1 (SVF)."""
    n = len(x)
    out = [0.0] * n
    low = band = 0.0
    damp = 1.0 / q
    for i, v in enumerate(x):
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        g = 2 * math.sin(math.pi * min(f, SR / 6) / SR)
        high = v - low - damp * band
        band += g * high
        low += g * band
        out[i] = band
    return out


# --------------------------------------------------------------- utilities
def silence(sec):
    return [0.0] * int(sec * SR)


def length(sig):
    return len(sig[0]) if isinstance(sig, tuple) else len(sig)


def gain(x, g):
    if isinstance(x, tuple):
        return tuple(gain(c, g) for c in x)
    return [v * g for v in x]


def db(d):
    return 10 ** (d / 20)


def fit(x, n):
    return x[:n] + [0.0] * max(0, n - len(x))


def mix(*parts, total=None):
    """mix((signal, offset_sec, gain), ...) -> mono or stereo sum."""
    stereo = any(isinstance(p[0], tuple) for p in parts)
    end = 0
    for sig, off, _ in parts:
        end = max(end, int(off * SR) + length(sig))
    if total is not None:
        end = int(total * SR)
    if stereo:
        L, R = [0.0] * end, [0.0] * end
        for sig, off, g in parts:
            o = int(off * SR)
            l, r = sig if isinstance(sig, tuple) else (sig, sig)
            for i in range(min(len(l), end - o)):
                L[o + i] += l[i] * g
                R[o + i] += r[i] * g
        return (L, R)
    out = [0.0] * end
    for sig, off, g in parts:
        o = int(off * SR)
        for i in range(min(len(sig), end - o)):
            out[o + i] += sig[i] * g
    return out


def pan(x, p):
    """Constant-power pan, p in [-1, 1]."""
    a = (p + 1) * math.pi / 4
    return ([v * math.cos(a) for v in x], [v * math.sin(a) for v in x])


def pan_sweep(x, p0, p1):
    n = len(x)
    L, R = [0.0] * n, [0.0] * n
    for i, v in enumerate(x):
        a = (p0 + (p1 - p0) * i / max(1, n - 1) + 1) * math.pi / 4
        L[i], R[i] = v * math.cos(a), v * math.sin(a)
    return (L, R)


def widen(x, ms=0.6, amt=0.35):
    """Mono -> stereo via a short mid/side delay (Haas-style side signal)."""
    d = int(ms * SR / 1000)
    side = [(x[i - d] if i >= d else 0.0) - x[i] for i in range(len(x))]
    return ([v + amt * s for v, s in zip(x, side)], [v - amt * s for v, s in zip(x, side)])


def fade(x, fin=0.0, fout=0.0):
    if isinstance(x, tuple):
        return tuple(fade(c, fin, fout) for c in x)
    x = list(x)
    n = len(x)
    a, b = int(fin * SR), int(fout * SR)
    for i in range(min(a, n)):
        x[i] *= math.sin(0.5 * math.pi * i / a)
    for i in range(min(b, n)):
        x[n - 1 - i] *= math.sin(0.5 * math.pi * i / b)
    return x


def trim(x, sec):
    n = int(sec * SR)
    if isinstance(x, tuple):
        return tuple(c[:n] for c in x)
    return x[:n]


def peak(sig):
    chans = sig if isinstance(sig, tuple) else (sig,)
    return max(max(abs(v) for v in c) for c in chans)


def normalize(sig, target_db):
    p = peak(sig)
    return gain(sig, db(target_db) / p) if p > 0 else sig


def level(sig, target_db, window=None, ceiling_db=-1.0, limit=False):
    """Gain so RMS (over the first `window` s, or whole file) hits target_db.

    Peaks stay under ceiling_db: either the gain is reduced (limit=False) or
    the excess is soft-limited with a tanh curve (limit=True, for textures
    whose sparse transients would otherwise keep the body too quiet).
    """
    g = db(target_db) / max(1e-9, rms(sig, 0.0, window))
    c = db(ceiling_db)
    if not limit:
        return gain(sig, min(g, c / peak(sig)))
    sig = gain(sig, g)
    if peak(sig) <= c:
        return sig
    if isinstance(sig, tuple):
        return tuple([c * math.tanh(v / c) for v in ch] for ch in sig)
    return [c * math.tanh(v / c) for v in sig]


def rms(sig, start=0.0, dur=None):
    chans = sig if isinstance(sig, tuple) else (sig,)
    a = int(start * SR)
    tot = cnt = 0
    for c in chans:
        seg = c[a:] if dur is None else c[a:a + int(dur * SR)]
        tot += sum(v * v for v in seg)
        cnt += len(seg)
    return math.sqrt(tot / max(1, cnt))


def onset(x, thresh=0.02):
    """Index of first sample whose magnitude exceeds thresh * peak."""
    p = max(abs(v) for v in x)
    for i, v in enumerate(x):
        if abs(v) > thresh * p:
            return i
    return 0


def soft_clip(sig, drive=1.0):
    if isinstance(sig, tuple):
        return tuple(soft_clip(c, drive) for c in sig)
    return [math.tanh(v * drive) / math.tanh(drive) for v in sig]


def level_ride(x, win=0.05, strength=0.7):
    """Even out a texture's loudness so a hold loop sounds steady."""
    hop = int(win * SR)
    env = []
    for s in range(0, len(x), hop):
        seg = x[s:s + hop]
        env.append(math.sqrt(sum(v * v for v in seg) / max(1, len(seg))) + 1e-6)
    # smooth with a 5-tap moving average
    sm = [sum(env[max(0, k - 2):k + 3]) / len(env[max(0, k - 2):k + 3]) for k in range(len(env))]
    ref = sorted(sm)[len(sm) // 2]
    out = [0.0] * len(x)
    for i, v in enumerate(x):
        pos = i / hop - 0.5
        k = max(0, min(len(sm) - 2, int(pos)))
        f = min(1.0, max(0.0, pos - k))
        e = sm[k] * (1 - f) + sm[k + 1] * f
        out[i] = v * (ref / e) ** strength
    return out


def make_loop(x, start, dur, xfade):
    """Seamless loop: the file's last sample flows into its first.

    The first `xfade` seconds are an equal-power blend of the material at
    `start` and the material right after the loop end, so restarting the
    file at sample 0 continues the texture instead of clicking.
    """
    if isinstance(x, tuple):
        return tuple(make_loop(c, start, dur, xfade) for c in x)
    s, L, X = int(start * SR), int(dur * SR), int(xfade * SR)
    if s + L + X > len(x):
        raise ValueError("not enough material for loop")
    out = x[s:s + L]
    for i in range(X):
        t = i / X
        out[i] = x[s + i] * math.sin(0.5 * math.pi * t) + x[s + L + i] * math.cos(0.5 * math.pi * t)
    return out


def circular_delay(x, sec):
    d = int(sec * SR) % len(x)
    return x[-d:] + x[:-d] if d else list(x)


# ------------------------------------------------------------- generators
def noise(sec, seed):
    r = random.Random(seed)
    return [r.uniform(-1, 1) for _ in range(int(sec * SR))]


def env_exp(sec, tau, attack=0.001):
    n = int(sec * SR)
    a = max(1, int(attack * SR))
    return [(i / a if i < a else 1.0) * math.exp(-(i / SR) / tau) for i in range(n)]


def mul(x, e):
    return [a * b for a, b in zip(x, e)]


def tone(freq, sec, tau, partials=((1, 1.0, 1.0),), attack=0.001, glide=None, phase_seed=None):
    """Additive tone. partials = (ratio, amp, tau_scale). glide = end freq."""
    n = int(sec * SR)
    out = [0.0] * n
    a = max(1, int(attack * SR))
    r = random.Random(phase_seed) if phase_seed is not None else None
    for ratio, amp, tscale in partials:
        if freq * ratio >= SR * 0.45:
            continue
        ph = r.random() * 2 * math.pi if r else 0.0
        t_p = tau * tscale
        for i in range(n):
            f = freq if glide is None else freq * (glide / freq) ** (i / n)
            ph += 2 * math.pi * f * ratio / SR
            att = i / a if i < a else 1.0
            out[i] += amp * att * math.exp(-(i / SR) / t_p) * math.sin(ph)
    return out


def note(name):
    """'A6' / 'C#7' / 'Bb5' -> Hz (A4 = 440)."""
    names = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}
    k = names[name[0]]
    rest = name[1:]
    if rest[0] == "#":
        k += 1
        rest = rest[1:]
    elif rest[0] == "b":
        k -= 1
        rest = rest[1:]
    return 440.0 * 2 ** ((k + 12 * (int(rest) - 4)) / 12)


# ------------------------------------------------------------------ reverb
def reverb(x, size=0.8, damp=0.35, wet=0.3, tail=1.5, seed=3):
    """Compact Freeverb-style stereo reverb. x mono or stereo."""
    if isinstance(x, tuple):
        src = [(a + b) * 0.5 for a, b in zip(*x)]
        dry = x
    else:
        src = x
        dry = (x, x)
    n = len(src) + int(tail * SR)
    src = fit(src, n)
    combs = [1116, 1188, 1277, 1356, 1422, 1491]
    aps = [556, 441, 341]
    outs = []
    for spread in (0, 23):
        acc = [0.0] * n
        for c in combs:
            d = int((c + spread) * SR / 44100)
            buf = [0.0] * d
            idx = 0
            store = 0.0
            for i in range(n):
                o = buf[idx]
                store = o * (1 - damp) + store * damp
                buf[idx] = src[i] * 0.015 + store * size
                idx = idx + 1 if idx + 1 < d else 0
                acc[i] += o
        for a in aps:
            d = int((a + spread) * SR / 44100)
            buf = [0.0] * d
            idx = 0
            for i in range(n):
                b = buf[idx]
                buf[idx] = acc[i] + b * 0.5
                acc[i] = b - acc[i]
                idx = idx + 1 if idx + 1 < d else 0
        outs.append(acc)
    L = [d + w * wet for d, w in zip(fit(dry[0], n), outs[0])]
    R = [d + w * wet for d, w in zip(fit(dry[1], n), outs[1])]
    return (L, R)
