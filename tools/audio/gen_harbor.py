"""Dźwięki Przystani we mgle: fanfara wygranej przy losowaniu daru i wystrzał fajerwerku.

Fanfara z dzwonków (rozłożony akord w górę, potem wybrzmiewający akord z migotaniem), w tonacji molowej z jasnym
zakończeniem, żeby pasowała do mrocznej muzyki tła. Duża fanfara (dar epicki i legendarny) jest dłuższa, o oktawę
szersza i ma niski gong. Fajerwerk: stłumiony wystrzał i trzaskający ogon iskier.

Uruchom z katalogu głównego repo:  python3 tools/audio/gen_harbor.py
Wynik: Assets/_Project/Resources/Audio/sfx_fanfare.ogg, sfx_fanfare_big.ogg, sfx_firework.ogg
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen_audio import SR, bell, env, lowpass_fast, noise_burst, save, thud  # noqa: E402

rng = np.random.default_rng(23)


def note(n):
    """Półtony od A4 (440 Hz)."""
    return 440.0 * 2 ** (n / 12)


def mix(total, parts):
    out = np.zeros(int(total * SR))
    for at, x in parts:
        i = int(at * SR)
        out[i:i + len(x)] += x[: len(out) - i]
    return out


def chime(f, dur, decay, gain=1.0):
    """Dzwonek: ton z lekko rozstrojonymi alikwotami (2, 3, 4.2) i drugim, cichym tonem obok (bicie)."""
    return (bell((f, f * 2.0, f * 3.0, f * 4.2), dur, decay) + bell((f * 1.003,), dur, decay) * 0.4) * gain


def shimmer(dur, f0):
    """Migotanie: szybkie, ciche dzwonki w górze, coraz rzadsze."""
    parts = []
    t = 0.0
    k = 0
    while t < dur:
        f = f0 * (2 ** (rng.integers(0, 12) / 12))
        parts.append((t, chime(f, 0.4, 0.08, 0.18)))
        t += 0.05 + 0.02 * k
        k += 1
    return mix(dur + 0.5, parts)


def fanfare(big=False):
    # d-moll w górę (D F A), na koniec jasne D-dur (Fis): „wygrana we mgle”
    steps = [-7, -4, 0, 5] if not big else [-19, -14, -7, -4, 0, 5, 8]
    gap = 0.09
    parts = [(i * gap, chime(note(s), 1.2, 0.35, 0.8 + 0.05 * i)) for i, s in enumerate(steps)]
    end = len(steps) * gap + 0.04
    chord = [5, 9, 12] if not big else [-7, 5, 9, 12, 17]
    for s in chord:
        parts.append((end, chime(note(s), 2.4 if big else 1.8, 0.9 if big else 0.6, 0.55)))
    parts.append((end + 0.05, shimmer(1.2 if big else 0.7, note(17))))
    if big:
        n = int(2.5 * SR)
        t = np.arange(n) / SR
        gong = (np.sin(2 * np.pi * 73.4 * t) + 0.5 * np.sin(2 * np.pi * 110.1 * t)) * env(n, 0.01, 0.9)
        parts.append((0.0, lowpass_fast(gong, 600) * 0.9))
    return mix(end + (3.2 if big else 2.3), parts)


def firework():
    parts = [(0.0, thud(95, 0.25, 0.9)), (0.0, noise_burst(0.08, 600, 4000, 0.02) * 0.6)]   # wystrzał
    # trzaski iskier: krótkie kliknięcia coraz cichsze
    for k in range(18):
        at = 0.12 + rng.uniform(0.0, 0.6) * (1 + k / 18)
        parts.append((at, noise_burst(0.02, 2500, 9000, 0.004) * (0.5 - 0.02 * k)))
    return mix(1.3, parts)


if __name__ == "__main__":
    save("sfx_fanfare", fanfare(False), 0.55)
    save("sfx_fanfare_big", fanfare(True), 0.6)
    save("sfx_firework", firework(), 0.4)
