"""Mowa ogników: krótkie sylaby bez znaczenia (jak „simlish”), syntezowane formantami w numpy, zapis OGG przez gen_audio.save.

Źródło: ciąg impulsów krtaniowych z lekkim drżeniem i oddechem (szept, upiorny ton), filtrowany trzema rezonatorami
(formanty samogłosek podniesione o 1/4, bo ogniki są małe). Spółgłoski: wybuch szumu (t, k), szum syczący (s, sz, f),
nosowe mruczenie (m, n), płynne przejście formantów (l, r, w, j). Na koniec krótki pogłos z szumu.
Wysokość głosu każdy ognik zmienia sam (AudioSource.pitch), więc te same sylaby brzmią u każdego inaczej.

Uruchom z katalogu głównego repo:  python3 tools/audio/gen_babble.py
Wynik: Assets/_Project/Resources/Audio/sfx_babble_*.ogg (12 sylab dla run i 4 reakcje: q, ex, hum, laugh).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen_audio import SR, save  # noqa: E402

rng = np.random.default_rng(11)
FORMANT_SCALE = 1.25

VOWELS = {
    "a": (800, 1200, 2500),
    "e": (450, 1900, 2550),
    "i": (300, 2250, 3000),
    "o": (480, 850, 2500),
    "u": (330, 750, 2350),
}


def resonate(x, freqs, bws, gains):
    """Trzy równoległe rezonatory dwubiegunowe; formanty mogą się zmieniać w czasie (tablice długości x)."""
    out = np.zeros_like(x)
    for f, bw, g in zip(freqs, bws, gains):
        f = np.broadcast_to(np.asarray(f, dtype=float), x.shape)
        r = np.exp(-np.pi * bw / SR)
        a1 = 2 * r * np.cos(2 * np.pi * f / SR)
        a2 = -r * r
        y1 = y2 = 0.0
        y = np.empty_like(x)
        for n in range(len(x)):
            v = (1 - r) * x[n] + a1[n] * y1 + a2 * y2
            y2, y1 = y1, v
            y[n] = v
        out += y * g
    return out


def glottal(f0):
    """Impulsy krtaniowe (piła wygładzona) dla zmiennej wysokości f0 (tablica Hz)."""
    phase = np.cumsum(f0) / SR
    frac = phase % 1.0
    pulse = np.where(frac < 0.6, np.sin(np.pi * frac / 0.6) ** 2, 0.0)
    return np.diff(pulse, prepend=0.0) * 8.0


def contour(n, f_start, f_end, vibrato=0.025):
    t = np.arange(n) / SR
    base = np.linspace(f_start, f_end, n)
    return base * (1 + vibrato * np.sin(2 * np.pi * 6.5 * t))


def env_ad(n, attack, release):
    e = np.ones(n)
    a = max(1, int(attack * SR))
    r = max(1, int(release * SR))
    e[:a] = np.linspace(0, 1, a)
    e[-r:] *= np.linspace(1, 0, r)
    return e


def band_noise(n, lo, hi):
    x = rng.standard_normal(n)
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(n, 1 / SR)
    spec *= (1 / (1 + (lo / np.maximum(f, 1)) ** 4)) * (1 / (1 + (f / hi) ** 4))
    y = np.fft.irfft(spec, n)
    return y / (np.max(np.abs(y)) or 1)


def vowel(v, dur, f0a, f0b, start_formants=None):
    """Samogłoska z opcjonalnym dojazdem formantów (spółgłoski płynne)."""
    n = int(dur * SR)
    target = np.array(VOWELS[v]) * FORMANT_SCALE
    if start_formants is not None:
        start = np.array(start_formants) * FORMANT_SCALE
        glide = np.clip(np.arange(n) / (0.06 * SR), 0, 1)
        freqs = [start[k] + (target[k] - start[k]) * glide for k in range(3)]
    else:
        freqs = list(target)
    src = glottal(contour(n, f0a, f0b))
    src += rng.standard_normal(n) * 0.1    # oddech: szeptany, upiorny
    y = resonate(src, freqs, (90, 120, 180), (1.0, 0.7, 0.35))
    return y * env_ad(n, 0.012, 0.05)


def consonant(c, f0):
    """Początek sylaby. Zwraca (dźwięk, formanty startowe dla płynnych albo None)."""
    if c in ("t", "k"):
        n = int(0.025 * SR)
        lo, hi = (3000, 7000) if c == "t" else (1500, 3500)
        return band_noise(n, lo, hi) * np.exp(-np.arange(n) / (0.006 * SR)) * 0.6, None
    if c in ("s", "sh", "f", "z"):
        n = int(0.06 * SR)
        lo, hi = {"s": (4500, 9000), "sh": (2200, 5000), "f": (1200, 7000), "z": (3500, 8000)}[c]
        y = band_noise(n, lo, hi) * env_ad(n, 0.015, 0.02) * (0.35 if c != "f" else 0.2)
        if c == "z":
            y += resonate(glottal(contour(n, f0, f0)), [250 * FORMANT_SCALE], [80], [1.0]) * 0.3
        return y, None
    if c in ("m", "n"):
        n = int(0.05 * SR)
        hum = resonate(glottal(contour(n, f0, f0)), [270 * FORMANT_SCALE, (1000 if c == "m" else 1700) * FORMANT_SCALE],
                       [60, 200], [1.0, 0.15])
        return hum * env_ad(n, 0.01, 0.005) * 0.6, None
    if c == "b":
        n = int(0.015 * SR)
        return resonate(glottal(contour(n, f0, f0)), [200], [80], [1.0]) * 0.5, None
    glides = {"l": (350, 1100, 2600), "r": (400, 1100, 1600), "w": (300, 650, 2200), "y": (280, 2300, 3000)}
    return np.zeros(0), glides.get(c)


def hum(dur, f0a, f0b):
    """Mruczenie z zamkniętymi ustami („mmm”)."""
    n = int(dur * SR)
    y = resonate(glottal(contour(n, f0a, f0b)), [270 * FORMANT_SCALE, 1000 * FORMANT_SCALE], [60, 200], [1.0, 0.15])
    return y * env_ad(n, 0.03, 0.12)


def syllable(c, v, f0a, f0b, dur=0.13):
    head, glide = consonant(c, f0a) if c else (np.zeros(0), None)
    body = vowel(v, dur, f0a, f0b, glide)
    return np.concatenate([head, body])


def reverb(x, tail=0.14, mix=0.22):
    n = int(tail * SR)
    ir = rng.standard_normal(n) * np.exp(-np.arange(n) / (0.035 * SR))
    dry = np.concatenate([x, np.zeros(n)])
    wet = np.zeros(len(dry))
    conv = np.convolve(x, ir)[: len(dry)]
    wet[: len(conv)] = conv
    return dry + wet / (np.max(np.abs(wet)) or 1) * mix * np.max(np.abs(x))


def chain(*parts, gap=0.02):
    g = np.zeros(int(gap * SR))
    out = []
    for p in parts:
        out += [p, g]
    return np.concatenate(out[:-1])


F0 = 330.0   # podstawa; ognik i tak przestraja (0.85-1.25)

SYLLABLES = [
    ("m", "i", 1.00, 1.12),
    ("l", "o", 1.05, 0.92),
    ("b", "a", 1.00, 1.00),
    ("n", "u", 0.95, 1.02),
    ("sh", "i", 1.08, 1.00),
    ("k", "a", 1.02, 0.95),
    ("r", "e", 0.98, 1.08),
    ("w", "o", 1.00, 0.90),
    ("t", "e", 1.06, 1.10),
    ("f", "a", 0.96, 0.92),
    ("y", "u", 1.00, 1.14),
    ("z", "o", 0.94, 0.98),
]

if __name__ == "__main__":
    for i, (c, v, a, b) in enumerate(SYLLABLES):
        save(f"sfx_babble_{i}", reverb(syllable(c, v, F0 * a, F0 * b)), 0.5)
    # reakcje: pytające „hm?” w górę, „oh!” z nagłym skokiem, mruczenie w dół, chichot „he-he-he”
    save("sfx_babble_q", reverb(chain(syllable("m", "u", F0 * 0.95, F0 * 1.0, 0.07), syllable("", "e", F0 * 1.05, F0 * 1.45, 0.16), gap=0.0)), 0.5)
    save("sfx_babble_ex", reverb(syllable("", "o", F0 * 1.35, F0 * 1.05, 0.18)), 0.55)
    save("sfx_babble_hum", reverb(hum(0.32, F0 * 0.95, F0 * 0.78)), 0.4)
    save("sfx_babble_laugh", reverb(chain(*[syllable("sh" if k == 0 else "", "e", F0 * (1.2 - 0.05 * k), F0 * (1.12 - 0.05 * k), 0.06)
                                            for k in range(3)], gap=0.035)), 0.45)
