"""Build the OpenSekai SE pack described in docs/operations/audio-spec.md.

Usage:  python Tools/audio-gen/make_se_pack.py [output_dir]
Default output: LocalPacks/se-pack/   (custom01, custom02, default,
live_se_common, plus SOURCES.md). Pure standard library; deterministic.

custom01  "Crystal Hat"  - hi-hat sample (cat-fox_alex, CC0) layered with
          glass pings in A major; holds are a levelled, seamless loop of the
          chime-ball rapid shakes (loganzsound, CC0).
custom02  "Glass Marimba" - fully synthetic: marimba-like mallets in D major,
          detuned stereo chords, zips; holds are exactly periodic glass pads
          and airy glints.
default   custom01 at -3 dB (12 cues, no se_live_trace_critical).
live_se_common - countdown/transition/results built from bells, the hat and
          the 36-bar chime sweeps (loganzsound, CC0) with reverb.
"""
import math
import os
import random
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp import *  # noqa: E402,F401,F403

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SE = os.path.join(ROOT, "LocalPacks", "SE")
CHIMES = os.path.join(SE, "46478__loganzsound__chimes-and-bells")
SRC_HAT = os.path.join(SE, "876729__cat-fox_alex__hat-2.wav")
SRC_SHAKE = os.path.join(CHIMES, "873723__loganzsound__chime-ball-pendant-sparkle-shakes-rapid-01.wav")
SRC_SWEEP = os.path.join(CHIMES, "874685__loganzsound__36-bar-chimes-sweep-ascend-01.wav")
SRC_RINGOUT = os.path.join(CHIMES, "874687__loganzsound__36-bar-chimes-sweep-ascend-long-ring-out-01.wav")
SRC_BELLS = os.path.join(CHIMES, "874637__loganzsound__decorative-bell-shakes-long.wav")
SRC_RAIN = os.path.join(SE, "766859__zusisking__ambrain-ext_rainytrainstation_zus_owsfx_loop.wav")
SRC_PHASE = os.path.join(SE, "466098__humanoisemaker__phase-90-loop-w-amp-bypassed.wav")
SRC_BRASS = os.path.join(CHIMES, "874639__loganzsound__homemade-brass-wind-chimes-light-handling-02.wav")
SRC_FRIC = os.path.join(SE, "876185__rasputinero__hh_s_s-alveolar-ejective-fricative.wav")
SRC_SCISSORS = os.path.join(SE, "737698__ibaffette__scissors-one-hit.wav")

PING = ((1, 1.0, 1.0), (2.0, 0.30, 0.6), (3.0, 0.10, 0.4), (4.2, 0.05, 0.3))
BELL = ((1, 1.0, 1.0), (2.0, 0.45, 0.7), (2.76, 0.30, 0.5), (5.4, 0.12, 0.3), (8.93, 0.05, 0.2))
MALLET = ((1, 1.0, 1.0), (3.93, 0.22, 0.3), (9.2, 0.05, 0.12))

N = note


# ================================================================ sources
class Src:
    pass


def load_sources():
    s = Src()
    hat = read_wav(SRC_HAT)
    s.hat = normalize(hat[onset(hat, 0.01):], 0)
    shake = read_wav(SRC_SHAKE)
    s.shake = normalize(shake[onset(shake, 0.01):], 0)
    # strongest moment of the shakes, used as a short sparkle grain
    head = s.shake[: 2 * SR]
    k = max(range(len(head)), key=lambda i: abs(head[i]))
    s.grain_at = max(0, k - int(0.004 * SR))
    sweep = read_wav(SRC_SWEEP, 0, 5.0, taps=10)
    s.sweep = normalize(sweep[onset(sweep, 0.02):], 0)
    ring = read_wav(SRC_RINGOUT, 0, 8.0, taps=10)
    s.ring = normalize(ring[onset(ring, 0.02):], 0)
    bells = read_wav(SRC_BELLS, 0, 15.0, taps=8)
    s.bells = normalize(bells[onset(bells, 0.02):], 0)
    s.rain = read_loop(SRC_RAIN)
    s.phase = read_loop(SRC_PHASE)
    brass = read_wav(SRC_BRASS, 0, 8.0, taps=8)
    s.brass = normalize(brass[onset(brass, 0.02):], 0)
    fric = read_wav(SRC_FRIC)
    s.fric = normalize(fric[onset(fric, 0.02):], 0)
    scis = read_wav(SRC_SCISSORS)
    s.scissors = normalize(scis[onset(scis, 0.02):], 0)
    return s


def steadiest(x, sec, win=0.01):
    """Start (s) of the `sec`-long stretch of x whose short-term level varies least."""
    hop = int(win * SR)
    env = [math.sqrt(sum(v * v for v in x[i:i + hop]) / hop) + 1e-6 for i in range(0, len(x) - hop, hop)]
    span = int(sec / win)
    best, best_k = None, 0
    for k in range(0, len(env) - span):
        seg = env[k:k + span]
        m = sum(seg) / span
        if m < 0.2 * max(env):  # skip near-silent stretches
            continue
        cv = math.sqrt(sum((e - m) ** 2 for e in seg) / span) / m
        if best is None or cv < best:
            best, best_k = cv, k
    return best_k * win


def sparkle(S, sec, hp=5000, seed_offset=0.0):
    a = S.grain_at + int(seed_offset * SR)
    g = biquad(S.shake[a:a + int(sec * SR)], "hp", hp)
    return fade(g, 0.002, sec * 0.6)


def periodic_tone(freq, L, harmonics=((1, 1.0),), lfo_cycles=0, depth=0.0, phase=0.0):
    """A tone that is exactly periodic over L seconds (for seamless loops)."""
    n = int(L * SR)
    f = round(freq * L) / L
    out = [0.0] * n
    for h, amp in harmonics:
        fh = f * h
        if fh > SR * 0.45:
            continue
        w = 2 * math.pi * fh / SR
        for i in range(n):
            out[i] += amp * math.sin(w * i + phase * h)
    if lfo_cycles:
        wl = 2 * math.pi * lfo_cycles / n
        out = [v * (1 - depth * 0.5 * (1 + math.cos(wl * i + phase))) for i, v in enumerate(out)]
    return out


def circular_add(dst, src, at):
    n = len(dst)
    for i, v in enumerate(src):
        dst[(at + i) % n] += v


def finish(sig, dur, peak_db, fout=None):
    sig = trim(sig, dur)
    sig = fade(sig, 0.0, fout if fout is not None else min(0.04, dur * 0.3))
    return normalize(sig, peak_db)


# ============================================================ custom01
def set1(S):
    hat = S.hat
    out = {}

    def ping(f, tau, sec=0.3, amp=1.0):
        return gain(tone(f, sec, tau, PING, attack=0.0008), amp)

    out["se_live_tap"] = finish(fade(biquad(hat, "hp", 2500), 0, 0.03), 0.07, -10)

    # perfect and critical are both the plain hit: the hat on its own
    out["se_live_perfect"] = finish(biquad(hat, "hp", 500), 0.20, -1)
    out["se_live_critical"] = list(out["se_live_perfect"])

    out["se_live_great"] = finish(biquad(hat, "lp", 9000), 0.17, -2)

    out["se_live_good"] = finish(biquad(pitch(hat, -3), "lp", 4500), 0.15, -4)

    # the other critical cues = their normal version plus a little room
    def verb(sig, dur):
        return finish(reverb(sig, 0.72, 0.4, 0.22, 0.35), dur, -1, fout=0.12)

    def loop_verb(lp):
        """Reverb that wraps around the loop: run three periods, keep the middle."""
        L = length(lp)
        r = reverb(tuple(c * 3 for c in lp), 0.72, 0.4, 0.22, 0.0)
        return tuple(c[L:2 * L] for c in r)

    # connect and trace: the "ts" fricative layered with the scissors snip
    fric = biquad(S.fric, "hp", 1500)
    scis = biquad(S.scissors, "hp", 700)
    out["se_live_connect"] = finish(widen(mix(
        (fade(scis, 0, 0.05)[: int(0.1 * SR)], 0, 0.8),
        (fade(fric, 0, 0.04)[: int(0.08 * SR)], 0, 0.6),
    ), 0.5, 0.2), 0.12, -5, fout=0.03)
    out["se_live_connect_critical"] = verb(out["se_live_connect"], 0.40)

    out["se_live_trace"] = finish(widen(mix(
        (fade(scis, 0, 0.12)[: int(0.25 * SR)], 0, 0.8),
        (fric, 0, 0.75),
    ), 0.6, 0.25), 0.26, -2, fout=0.08)
    out["se_live_trace_critical"] = verb(out["se_live_trace"], 0.55)

    swoosh = noise(0.18, 11)
    swoosh = sweep_bp(swoosh, 1500, 9000, 1.5)
    n = len(swoosh)
    swoosh = [v * math.sin(math.pi * i / n) ** 1.5 for i, v in enumerate(swoosh)]
    out["se_live_flick"] = finish(mix(
        (out["se_live_perfect"], 0, 1.0),
        (out["se_live_great"], 0, 0.8),
        (normalize(mix(
            (pan_sweep(swoosh, -0.6, 0.6), 0, 0.9),
            (biquad(pitch(hat, 5), "hp", 800), 0, 0.6)), -2), 0, 0.35),
    ), 0.22, -1)
    out["se_live_flick_critical"] = verb(out["se_live_flick"], 0.50)

    # one short, steady stretch of the shakes, looped on itself and tiled to
    # fill the file (the WAV path plays it once today; it also loops cleanly)
    XF = 0.15

    def steady_cell(src, hp, cell_n):
        cell = (cell_n + 0.5) / SR
        x = biquad(src, "hp", hp)
        x = biquad(x, "hshelf", 9000, 0.7, -3)
        st = steadiest(x, cell + XF + 0.05)
        x = level_ride(x[int(st * SR):int((st + cell + XF + 0.1) * SR)], 0.012, 0.9)
        c = make_loop(x, 0.02, cell, XF)
        return (c, circular_delay(c, 0.011))

    def tiles_for(n):
        """Split an n-sample loop into k equal cells of about 0.6 s."""
        k = min((k for k in range(2, 40) if n % k == 0), key=lambda k: abs(n / k - 0.6 * SR))
        return k, n // k

    def at_rms(sig, target):
        return gain(sig, db(target) / rms(sig))

    def hold(body, layer):
        """Main loop + the previous hold sound 6 dB under it, same period."""
        return mix((at_rms(body, -20), 0, 1.0), (at_rms(layer, -26), 0, 1.0))

    # normal hold body: the rainy train station ambience, cut to a 7.2 s seamless loop
    rain = biquad_st(level_ride_st(S.rain, 0.1, 0.5), "hp", 120)
    rain = make_loop(rain, 0.8, (7.2 * SR + 0.5) / SR, 0.8)
    k, cn = tiles_for(length(rain))
    bc = steady_cell(S.bells, 1500, cn)
    # the bell shakes are discrete hits: layer the cell with uneven circular copies
    bc = tuple([a + b + c for a, b, c in zip(ch, circular_delay(ch, 0.213), circular_delay(ch, 0.437))] for ch in bc)
    out["se_live_long"] = normalize(fade(hold(rain, tuple(ch * k for ch in bc)), 0.003, 0), -9)

    # critical hold body: the phase-90 loop as it is (already seamless)
    ph = biquad_st(S.phase, "hp", 80)
    k, cn = tiles_for(length(ph))
    sc = loop_verb(steady_cell(S.shake, 2000, cn))
    out["se_live_long_critical"] = normalize(fade(hold(ph, tuple(ch * k for ch in sc)), 0.003, 0), -9)
    return out


# ============================================================ custom02
def set2(S):
    out = {}

    def click(seed, amp=1.0):
        x = biquad(noise(0.006, seed), "bp", 3500, 1.0)
        return gain(mul(x, env_exp(0.006, 0.0008, 0.0001)), amp * 4)

    def mallet(f, tau, sec=0.35, detune_cents=0.0):
        return tone(f * 2 ** (detune_cents / 1200), sec, tau, MALLET, attack=0.0006)

    def st_mallet(f, tau, cents=5.0, sec=0.35):
        return (mallet(f, tau, sec, -cents), mallet(f, tau, sec, cents))

    out["se_live_tap"] = finish(mix(
        (click(1), 0, 0.8), (tone(1600, 0.03, 0.008), 0, 0.6)), 0.04, -10, fout=0.01)

    out["se_live_perfect"] = finish(mix(
        (mallet(N("D6"), 0.09), 0, 1.0),
        (tone(N("D7"), 0.1, 0.05), 0, 0.25),
        (click(2), 0, 0.5)), 0.22, -1)

    out["se_live_great"] = finish(mix(
        (mallet(N("A5"), 0.07), 0, 1.0), (click(3), 0, 0.4)), 0.19, -2)

    out["se_live_good"] = finish(biquad(mix(
        (mallet(N("D5"), 0.045), 0, 1.0), (click(4), 0, 0.2)), "lp", 3000), 0.15, -4)

    chord = mix(*[(st_mallet(N(nm), 0.11 - 0.01 * k), 0.004 * k, g)
                  for k, (nm, g) in enumerate((("D6", 0.6), ("F#6", 0.45), ("A6", 0.45)))])
    out["se_live_critical"] = finish(mix(
        (chord, 0, 1.0),
        (widen(tone(N("D7"), 0.25, 0.06, ((1, 1, 1), (3.0, 0.4, 0.5), (5.0, 0.15, 0.3)))), 0.0, 0.25),
        (click(5), 0, 0.5)), 0.34, -1, fout=0.08)

    out["se_live_connect"] = finish(mix(
        (mallet(N("A6"), 0.03), 0, 1.0), (click(6), 0, 0.3)), 0.12, -5)

    out["se_live_connect_critical"] = finish(mix(
        (st_mallet(N("A6"), 0.04), 0, 0.7),
        (st_mallet(N("D7"), 0.035, 7), 0.003, 0.5),
        (click(7), 0, 0.3)), 0.15, -3)

    zip_ = tone(700, 0.09, 0.05, ((1, 1, 1), (2, 0.3, 0.6)), glide=3000)
    nz = sweep_bp(noise(0.12, 21), 1000, 7000, 1.4)
    n = len(nz)
    nz = [v * math.sin(math.pi * i / n) for i, v in enumerate(nz)]
    base_flick = mix((pan_sweep(zip_, -0.5, 0.5), 0, 0.5), (pan_sweep(nz, -0.6, 0.6), 0, 0.7))
    out["se_live_flick"] = finish(mix(
        (out["se_live_perfect"], 0, 1.0),
        (normalize(mix((base_flick, 0, 1.0), (mallet(N("D7"), 0.04), 0.07, 0.5)), -2), 0, 0.8)),
        0.22, -1)

    arp = mix(*[(pan(mallet(N(nm), 0.06), -0.5 + 0.33 * k), 0.018 * k, 0.5)
                for k, nm in enumerate(("D6", "A6", "D7", "F#7"))])
    out["se_live_flick_critical"] = finish(mix(
        (out["se_live_critical"], 0, 1.0),
        (normalize(mix((base_flick, 0, 0.9), (arp, 0.03, 1.0)), -1), 0, 0.8)),
        0.34, -1, fout=0.08)

    # ---- hold loops: exactly periodic pads + seamless noise layers
    def pad(L, notes, lfo):
        Lc, Rc = [0.0] * int(L * SR), [0.0] * int(L * SR)
        for k, f in enumerate(notes):
            harm = ((1, 1.0), (2, 0.25), (3, 0.08))
            a = periodic_tone(f, L, harm, lfo, 0.5, phase=k * 0.9)
            b = periodic_tone(f + 0.75, L, harm, lfo, 0.5, phase=k * 0.9 + 1.7)
            Lc = [x + y * (0.6 / (k + 1)) for x, y in zip(Lc, a)]
            Rc = [x + y * (0.6 / (k + 1)) for x, y in zip(Rc, b)]
        return (Lc, Rc)

    def air(L, f, seed, amp):
        x = biquad(noise(L + 0.6, seed), "bp", f, 0.9)
        x = make_loop(x, 0.1, L, 0.4)
        return gain((x, circular_delay(x, 0.013)), amp)

    def glints(L, notes, count, seed, amp):
        r = random.Random(seed)
        Lc, Rc = [0.0] * int(L * SR), [0.0] * int(L * SR)
        for _ in range(count):
            f = N(r.choice(notes))
            t = tone(f, 0.3, 0.06, ((1, 1, 1), (2.0, 0.2, 0.5)), attack=0.002)
            at = r.randrange(len(Lc))
            p = r.uniform(-0.7, 0.7)
            a = (p + 1) * math.pi / 4
            circular_add(Lc, [v * amp * math.cos(a) for v in t], at)
            circular_add(Rc, [v * amp * math.sin(a) for v in t], at)
        return (Lc, Rc)

    lo = mix((pad(4.0, [N("D5"), N("A5"), N("D6")], 2), 0, 1.0), (air(4.0, 5000, 31, 0.06), 0, 1.0))
    out["se_live_long"] = normalize(fade(lo, 0.003, 0), -10)

    lc = mix((pad(4.0, [N("D5"), N("A5"), N("D6"), N("F#6"), N("A6")], 2), 0, 1.0),
             (pad(4.0, [N("D7")], 4), 0, 0.35),
             (air(4.0, 6500, 32, 0.08), 0, 1.0))
    out["se_live_long_critical"] = normalize(fade(lc, 0.003, 0), -9)

    penta = ["D6", "E6", "F#6", "A6", "B6", "D7"]
    tr = mix((air(4.0, 7000, 33, 0.25), 0, 1.0), (glints(4.0, penta, 24, 41, 0.35), 0, 1.0),
             (mallet(N("D7"), 0.04, 0.2), 0, 0.2))
    out["se_live_trace"] = normalize(fade(tr, 0.002, 0), -8)

    tc = mix((air(4.0, 8000, 34, 0.25), 0, 1.0),
             (glints(4.0, ["A6", "B6", "D7", "E7", "F#7", "A7"], 40, 42, 0.35), 0, 1.0),
             (pad(4.0, [N("D7"), N("A7")], 3), 0, 0.12),
             (st_mallet(N("A7"), 0.04, 6, 0.2), 0, 0.17),
             (st_mallet(N("D7"), 0.05, 6, 0.2), 0, 0.17))
    out["se_live_trace_critical"] = normalize(fade(tc, 0.002, 0), -7)
    return out


def biquad_st(sig, *a):
    return tuple(biquad(c, *a) for c in sig)


def level_ride_st(sig, *a):
    return tuple(level_ride(c, *a) for c in sig)


# ====================================================== live_se_common
def common(S):
    out = {}

    def bell(f, tau, sec):
        return tone(f, sec, tau, BELL, attack=0.0015, phase_seed=int(f))

    out["SE_LIVE_COUNT_DOWN"] = finish(mix(
        (biquad(S.hat, "hp", 600), 0, 0.5),
        (tone(N("A5"), 0.25, 0.08, ((1, 1, 1), (2, 0.4, 0.5), (3, 0.15, 0.3))), 0, 0.8),
        (tone(N("A6"), 0.2, 0.05), 0, 0.3)), 0.22, -2, fout=0.05)

    def arpeggio(names, step, tau, spread=0.8):
        parts = []
        for k, nm in enumerate(names):
            p = -spread / 2 + spread * k / max(1, len(names) - 1)
            parts.append((pan(bell(N(nm), tau, tau * 5), p), step * k, 0.5))
            parts.append((biquad(S.hat, "hp", 1500), step * k, 0.15))
        return mix(*parts)

    def chord(names, at, tau, amp):
        return [(widen(bell(N(nm), tau, tau * 5), 0.7, 0.3), at + 0.006 * k, amp)
                for k, nm in enumerate(names)]

    def sample_bed(x, dur, hp, fin=0.05):
        return widen(fade(biquad(x[: int(dur * SR)], "hp", hp), fin, dur * 0.5), 0.8, 0.4)

    dry = mix((arpeggio(["A5", "C#6", "E6", "A6"], 0.09, 0.45), 0, 1.0),
              *chord(["A6", "E7"], 0.36, 0.7, 0.35),
              (sample_bed(S.sweep, 1.8, 1500), 0.1, 0.3))
    out["se_live_clear"] = finish(reverb(dry, 0.8, 0.35, 0.25, 1.2), 2.6, -1, fout=0.6)

    dry = mix((arpeggio(["A5", "C#6", "E6", "A6", "C#7", "E7"], 0.075, 0.5), 0, 1.0),
              *chord(["A6", "C#7", "E7", "A7"], 0.5, 0.9, 0.3),
              (sample_bed(S.sweep, 2.5, 1200), 0.05, 0.35))
    out["se_live_full_combo"] = finish(reverb(dry, 0.83, 0.3, 0.3, 1.5), 3.4, -1, fout=0.8)

    shimmer = mix(*[(widen(tone(N(nm), 3.2, 1.2, ((1, 1, 1), (2, 0.2, 0.6)), attack=0.25)), 0, 0.12)
                    for nm in ("A6", "E7", "A7")])
    dry = mix((arpeggio(["A4", "E5", "A5", "C#6", "E6", "A6", "C#7", "E7", "A7"], 0.06, 0.4, 1.2), 0, 1.0),
              *chord(["A5", "C#6", "E6", "A6", "C#7", "E7"], 0.6, 1.1, 0.28),
              (shimmer, 0.6, 1.0),
              (widen(sparkle(S, 0.6, 3000)), 0.6, 0.35),
              (sample_bed(S.ring, 4.0, 900, 0.3), 0.15, 0.4))
    out["se_live_all_perfect"] = finish(reverb(dry, 0.86, 0.3, 0.35, 1.8), 4.6, -1, fout=1.0)

    nz = sweep_bp(noise(0.8, 51), 300, 6000, 1.2)
    n = len(nz)
    pk = 0.45 * n
    nz = [v * ((i / pk) ** 2 if i < pk else math.exp(-(i - pk) / (0.08 * SR))) for i, v in enumerate(nz)]
    # soft: darker whoosh, no bell; the bar-chime glissando and a stretch of
    # brass wind chimes carry the shimmer, all low-passed and in a wetter room
    nz = biquad(nz, "lp", 5000)
    # longer excerpts with long exponential fades so the shimmer rings out
    def ring_out(x, tau):
        return [v * math.exp(-max(0.0, i / SR - 0.3) / tau) for i, v in enumerate(x)]
    gliss = biquad(biquad(S.sweep[: int(1.8 * SR)], "hp", 600), "lp", 8000)
    gliss = fade(ring_out(gliss, 0.45), 0.12, 0.3)
    b_at = steadiest(S.brass, 1.6)
    brass = biquad(biquad(S.brass[int(b_at * SR):int((b_at + 1.6) * SR)], "hp", 400), "lp", 7000)
    brass = fade(ring_out(brass, 0.4), 0.1, 0.3)
    sp = biquad(sparkle(S, 0.4, 2500, 0.5), "lp", 8000)
    dry = mix((pan_sweep(nz, -0.4, 0.4), 0, 0.55),
              (widen(gliss, 0.8, 0.4), 0.05, 0.5),
              (widen(brass, 1.1, 0.4), 0.2, 0.45),
              (widen(sp), 0.33, 0.15))
    out["SE_AREA_TRANSITION_SEKAI"] = finish(reverb(dry, 0.8, 0.5, 0.32, 1.2), 2.2, -4, fout=0.5)
    return out


# ================================================================= main
SOURCES = """# OpenSekai SE pack — sources and licences

Generated by `Tools/audio-gen/make_se_pack.py` (deterministic; standard-library Python).
All files: 48000 Hz, 16-bit PCM WAV, TPDF-dithered.

## Third-party samples used (all CC0 1.0)

| Sample | Author | URL | Used in | Modifications |
| --- | --- | --- | --- | --- |
| hat-2 (876729) | cat-fox_alex | https://freesound.org/s/876729/ | custom01 one-shots except connect/trace, default, SE_LIVE_COUNT_DOWN, results | Summed to mono, resampled 44.1→48 kHz, trimmed, filtered, pitch-shifted, layered with synthesized pings |
| Chime Ball Pendant Sparkle Shakes Rapid 01 (873723) | loganzsound | https://freesound.org/s/873723/ | custom01 long_critical loop, sparkle grains, default, all_perfect, transition | High-passed, shelved, loudness-levelled, pitch-shifted, cut into crossfaded seamless loops, layered with synthesized tones |
| hh_s_s alveolar ejective fricative (876185) | rasputinero | https://freesound.org/s/876185/ | custom01 + default connect / trace | Resampled 44.1→48 kHz, high-passed, trimmed, layered with the scissors snip; critical adds reverb |
| scissors one hit (737698) | ibaffette | https://freesound.org/s/737698/ | custom01 + default connect / trace | Summed to mono, resampled 44.1→48 kHz, high-passed, trimmed, widened; critical adds reverb |
| Decorative Bell Shakes Long (874637) | loganzsound | https://freesound.org/s/874637/ | custom01 + default se_live_long | Resampled 96→48 kHz, steadiest 0.75 s found and levelled, cut into a 0.6 s seamless loop, tiled |
| AmbRain_EXT_RainyTrainStation loop (766859) | zusisking | https://freesound.org/s/766859/ | custom01 + default se_live_long (main layer) | Levelled, high-passed 120 Hz, cut to a 7.2 s seamless crossfaded loop; bell-shake loop layered 6 dB under |
| Phase 90 loop w amp bypassed (466098) | humanoisemaker | https://freesound.org/s/466098/ | custom01 se_live_long_critical (main layer) | Circular resample 44.1→48 kHz, high-passed 80 Hz, used whole as a 4.15 s loop; reverbed chime-ball loop layered 6 dB under |
| Homemade Brass Wind Chimes Light Handling 02 (874639) | loganzsound | https://freesound.org/s/874639/ | SE_AREA_TRANSITION_SEKAI | Resampled 96→48 kHz, steadiest 1.6 s excerpt, band-limited 400 Hz–7 kHz, exponential ring-out, widened, reverb |
| 36 Bar Chimes Sweep Ascend 01 (874685) | loganzsound | https://freesound.org/s/874685/ | se_live_clear, se_live_full_combo, SE_AREA_TRANSITION_SEKAI | Resampled 96→48 kHz, excerpted, high-passed, widened, faded, reverb |
| 36 Bar Chimes Sweep Ascend Long Ring Out 01 (874687) | loganzsound | https://freesound.org/s/874687/ | se_live_all_perfect | Resampled 96→48 kHz, excerpted, high-passed, widened, faded, reverb |

Licence: Creative Commons 0 — http://creativecommons.org/publicdomain/zero/1.0/
(loganzsound pack readme: https://freesound.org/people/loganzsound/packs/46478/).
All samples confirmed CC0 by the project owner (2026-11-15).

## Per group

- **custom01 — Crystal Hat** (A major), hit sounds +3 dB over set 2.
  perfect = critical = the plain hat; flick = perfect + great + a quiet swoosh; connect / trace = fricative + scissors snip;
  other critical cues = their normal version + light reverb. long = rainy train station loop + the decorative bell-shake loop 6 dB under;
  long_critical = phase-90 loop + the reverbed chime-ball loop 6 dB under; connect/trace cues are 6 dB under the rest (plays steadily once today, and loops cleanly when looping lands).
- **custom02 — Glass Marimba** (D major). 100% synthesized (自制). Hold cues are exactly periodic pads/glints plus crossfaded noise loops.
- **default** — custom01 at −3 dB, 12 cues (no se_live_trace_critical).
- **live_se_common** — synthesized bells (original arpeggios, no melodic quotation) + hat-2 + chime sweeps, Freeverb-style reverb.

Note: the current WAV playback path does not loop yet (audio-spec.md); loops are 4–6 s so holds keep sounding for that long.
"""


# Loudness targets shared by both note sets so switching sets in settings
# keeps the same balance. One-shots: RMS of the first 80 ms (the part that
# reads as the hit). Holds: RMS of the whole loop, transients soft-limited.
ONESHOT_RMS = {
    "se_live_tap": -25.0, "se_live_perfect": -15.0, "se_live_great": -16.5,
    "se_live_good": -19.0, "se_live_critical": -14.3, "se_live_connect": -21.0,
    "se_live_connect_critical": -18.5, "se_live_flick": -14.5, "se_live_flick_critical": -13.8,
}
HOLD_RMS = {
    # long holds: offsets from the set's balanced perfect (its 80 ms hit RMS),
    # i.e. the whole loop sits 15 dB (critical 13.5 dB) under the perfect hit
    "se_live_long": -15.0, "se_live_long_critical": -13.5,
    # set 2's trace loops follow the same rule (set 1's trace cues are one-shots)
    "se_live_trace": -10.0, "se_live_trace_critical": -8.5,
}
# set 1 trace cues are one-shot snips, not loops
SET1_ONESHOT_EXTRA = {"se_live_trace": -17.0, "se_live_trace_critical": -15.5}


CONNECT_TRACE_DB = -6.0  # connect / trace cues sit at half the volume of the rest


def balance(cues, oneshot_boost_db=0.0, extra_oneshots=None, long_extra_db=0.0):
    """oneshot_boost_db > 0 lifts the hit sounds and soft-limits the peaks."""
    oneshots = dict(ONESHOT_RMS, **(extra_oneshots or {}))
    out = {}
    order = sorted(cues, key=lambda n: n not in oneshots)  # one-shots (perfect) first
    for name in order:
        sig = cues[name]
        trim_db = CONNECT_TRACE_DB if name.startswith(("se_live_connect", "se_live_trace")) else 0.0
        if name in oneshots:
            out[name] = level(sig, oneshots[name] + oneshot_boost_db + trim_db, 0.08, -1.0,
                              limit=oneshot_boost_db > 0)
        else:
            ref = 20 * math.log10(rms(out["se_live_perfect"], 0, 0.08))
            extra = long_extra_db if name.startswith("se_live_long") else 0.0
            out[name] = level(sig, HOLD_RMS[name] + ref + extra, None, -4.0, limit=True)
    return {n: out[n] for n in cues}


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "LocalPacks", "se-pack")
    t0 = time.time()
    S = load_sources()
    print(f"sources loaded ({time.time() - t0:.1f}s)")
    groups = {}
    groups["custom01"] = balance(set1(S), oneshot_boost_db=3.0, extra_oneshots=SET1_ONESHOT_EXTRA)
    print(f"custom01 ({time.time() - t0:.1f}s)")
    groups["custom02"] = balance(set2(S))
    print(f"custom02 ({time.time() - t0:.1f}s)")
    groups["default"] = {k: gain(v, db(-3)) for k, v in groups["custom01"].items()
                         if k != "se_live_trace_critical"}
    groups["live_se_common"] = common(S)
    print(f"live_se_common ({time.time() - t0:.1f}s)")
    for g, cues in groups.items():
        d = os.path.join(out_dir, g)
        os.makedirs(d, exist_ok=True)
        for name, sig in cues.items():
            write_wav(os.path.join(d, name + ".wav"), sig)
    with open(os.path.join(out_dir, "SOURCES.md"), "w", encoding="utf-8") as f:
        f.write(SOURCES)
    total = sum(len(c) for c in groups.values())
    print(f"wrote {total} files to {out_dir} ({time.time() - t0:.1f}s)")


if __name__ == "__main__":
    main()
