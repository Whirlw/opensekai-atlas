"""Render the five shared live sounds from CC0 samples and synthesized tones.

Run python AudioSource/Tools/make_se_pack.py [output_dir] to write WAVs
under live_se_common in the output directory (AudioSource by default).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp import (
    SR, biquad, fade, mix, noise, normalize, note, onset, pan, pan_sweep,
    read_wav, reverb, sweep_bp, tone, trim, widen, write_wav,
)

HERE = os.path.dirname(os.path.abspath(__file__))
AUDIO_SOURCE = os.path.dirname(HERE)
SE = os.path.join(HERE, "samples")
SRC_HAT = os.path.join(SE, "876729__cat-fox_alex__hat-2.wav")
SRC_SHAKE = os.path.join(SE, "873723__loganzsound__chime-ball-pendant-sparkle-shakes-rapid-01.wav")
SRC_SWEEP = os.path.join(SE, "874685__loganzsound__36-bar-chimes-sweep-ascend-01.wav")
SRC_RINGOUT = os.path.join(SE, "874687__loganzsound__36-bar-chimes-sweep-ascend-long-ring-out-01.wav")
SRC_BRASS = os.path.join(SE, "874639__loganzsound__homemade-brass-wind-chimes-light-handling-02.wav")

BELL = ((1, 1.0, 1.0), (2.0, 0.45, 0.7), (2.76, 0.30, 0.5), (5.4, 0.12, 0.3), (8.93, 0.05, 0.2))

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
    brass = read_wav(SRC_BRASS, 0, 8.0, taps=8)
    s.brass = normalize(brass[onset(brass, 0.02):], 0)
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


def finish(sig, dur, peak_db, fout=None):
    sig = trim(sig, dur)
    sig = fade(sig, 0.0, fout if fout is not None else min(0.04, dur * 0.3))
    return normalize(sig, peak_db)


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


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else AUDIO_SOURCE
    output = os.path.join(root, "live_se_common")
    cues = common(load_sources())
    os.makedirs(output, exist_ok=True)
    for name, signal in cues.items():
        write_wav(os.path.join(output, name + ".wav"), signal)
    print(f"Wrote {len(cues)} shared sounds to {output}")


if __name__ == "__main__":
    main()
