"""Dźwięki-placeholdery Pockland (Etap 6c): syntezowane w numpy, zapisywane jako OGG (ffmpeg, libvorbis).

Mroczne i ciche: tło dnia (fale, wiatr, rzadkie ptaki), tło nocy (świerszcze, niski pomruk, fale), deszcz,
krótkie efekty (sadzenie, zbiór, moneta, kopanie, ścinanie, stawianie, klik, Księga, powiadomienie, grzmot, magia, kafelek, bąbel).
Docelowe dźwięki nagra albo dobierze się ręcznie - nazwy plików zostają.

Uruchom z katalogu głównego repo:  python3 tools/audio/gen_audio.py
Wynik: Assets/_Project/Resources/Audio/*.ogg (Resources.Load w AudioManager).
"""
import os
import subprocess
import tempfile
import wave

import numpy as np

SR = 22050
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Audio")
rng = np.random.default_rng(7)


def t_axis(sec):
    return np.arange(int(SR * sec)) / SR


def lowpass(x, cutoff):
    """Prosty filtr jednobiegunowy (wielokrotnie, żeby był łagodniejszy)."""
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    for _ in range(2):
        acc = 0.0
        for i in range(len(x)):
            acc = (1 - a) * x[i] + a * acc
            y[i] = acc
        x = y.copy()
    return y


def lowpass_fast(x, cutoff):
    """Filtr dolnoprzepustowy w dziedzinie częstotliwości (dla długich sygnałów)."""
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    spec *= 1 / (1 + (f / cutoff) ** 4)
    return np.fft.irfft(spec, len(x))


def bandpass_fast(x, lo, hi):
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    spec *= (1 / (1 + (lo / np.maximum(f, 1)) ** 4)) * (1 / (1 + (f / hi) ** 4))
    return np.fft.irfft(spec, len(x))


def env(n, attack, decay):
    t = np.arange(n) / SR
    e = np.minimum(1, t / max(attack, 1e-4)) * np.exp(-t / max(decay, 1e-4))
    return e


def loopable(x, fade=0.5):
    """Pętla bez trzasku: koniec przenikany w początek."""
    n = int(SR * fade)
    head, tail = x[:n].copy(), x[-n:].copy()
    w = np.linspace(0, 1, n)
    x = x[:-n].copy()
    x[:n] = head * w + tail * (1 - w)
    return x


def normalize(x, peak):
    m = np.max(np.abs(x)) or 1
    return x / m * peak


def save(name, x, peak=0.6):
    os.makedirs(OUT, exist_ok=True)
    x = normalize(x, peak)
    pcm = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        path = tmp.name
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    out = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", path, "-c:a", "libvorbis", "-q:a", "3", out], check=True)
    os.remove(path)
    print("ok", name, f"{len(x) / SR:.2f}s")


# ---------------------------------------------------------------- tła (pętle)

def waves(sec, strength=1.0):
    t = t_axis(sec)
    noise = rng.standard_normal(len(t))
    surf = lowpass_fast(noise, 500)
    # fale: powolne narastanie i opadanie (okres dzieli długość pętli)
    swell = 0.55 + 0.45 * np.sin(2 * np.pi * t / (sec / 3)) ** 2
    return surf * swell * strength


def wind(sec):
    t = t_axis(sec)
    w = bandpass_fast(rng.standard_normal(len(t)), 200, 900)
    return w * (0.4 + 0.3 * np.sin(2 * np.pi * t / sec * 2 + 1.3))


def chirp(f0, f1, dur):
    t = t_axis(dur)
    f = np.linspace(f0, f1, len(t))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * env(len(t), 0.01, dur / 3)


def amb_day():
    sec = 12.0
    x = waves(sec) * 0.7 + wind(sec) * 0.25
    for at in (2.3, 2.5, 7.9):    # rzadkie, dalekie ptaki
        c = chirp(2600, 3400, 0.12) * 0.05
        i = int(at * SR)
        x[i:i + len(c)] += c
    return loopable(x)


def amb_night():
    sec = 12.0
    t = t_axis(sec)
    x = waves(sec, 0.5) * 0.5
    # świerszcze: wysoki ton cięty w krótkie ćwierknięcia, dwa „owady” w innym rytmie
    for f, rate, phase, amp in ((4300, 3.0, 0.0, 0.05), (4700, 2.2, 1.1, 0.035)):
        gate = (np.sin(2 * np.pi * rate * t + phase) > 0.6).astype(float)
        trill = (np.sin(2 * np.pi * 45 * t) > 0).astype(float)
        x += np.sin(2 * np.pi * f * t) * gate * trill * amp
    x += np.sin(2 * np.pi * 55 * t) * 0.04 * (0.6 + 0.4 * np.sin(2 * np.pi * t / sec))   # niski pomruk głębin
    return loopable(x)


def amb_rain():
    sec = 8.0
    t = t_axis(sec)
    x = lowpass_fast(rng.standard_normal(len(t)), 2500) * 0.5
    for _ in range(140):    # pojedyncze krople
        i = rng.integers(0, len(t) - 400)
        x[i:i + 300] += rng.standard_normal(300) * env(300, 0.0005, 0.004) * 0.6
    return loopable(x)


# ---------------------------------------------------------------- efekty

def thud(freq, dur, noise=0.5):
    n = int(SR * dur)
    t = np.arange(n) / SR
    body = np.sin(2 * np.pi * freq * t * (1 - 0.3 * t / dur)) * env(n, 0.002, dur / 4)
    hit = lowpass_fast(rng.standard_normal(n), 800) * env(n, 0.001, dur / 8) * noise
    return body + hit


def pluck(freq, dur):
    """Karplus-Strong: szarpnięta struna."""
    n = int(SR * dur)
    period = int(SR / freq)
    buf = rng.uniform(-1, 1, period)
    out = np.empty(n)
    for i in range(n):
        out[i] = buf[i % period]
        buf[i % period] = 0.5 * (buf[i % period] + buf[(i + 1) % period]) * 0.996
    return out


def bell(freqs, dur, decay):
    n = int(SR * dur)
    t = np.arange(n) / SR
    x = sum(np.sin(2 * np.pi * f * t) * (0.6 ** k) for k, f in enumerate(freqs))
    return x * env(n, 0.003, decay)


def noise_burst(dur, lo, hi, decay):
    n = int(SR * dur)
    return bandpass_fast(rng.standard_normal(n), lo, hi) * env(n, 0.003, decay)


def sfx_thunder():
    n = int(SR * 3.0)
    crack = noise_burst(0.15, 800, 5000, 0.04)
    rumble = lowpass_fast(rng.standard_normal(n), 140) * env(n, 0.05, 0.9)
    rumble[:len(crack)] += crack * 0.5
    return rumble


def sfx_magic():
    out = np.zeros(int(SR * 0.9))
    for k, f in enumerate((523, 659, 784, 1047)):   # łagodne arpeggio
        b = bell((f, f * 2.01), 0.6, 0.18) * 0.5
        i = int(k * 0.09 * SR)
        out[i:i + len(b)] += b[:len(out) - i]
    return out


def sfx_bubble():
    out = np.zeros(int(SR * 0.5))
    for k in range(3):
        n = int(SR * 0.08)
        t = np.arange(n) / SR
        f = 300 + 500 * t / 0.08 + k * 80
        b = np.sin(2 * np.pi * np.cumsum(f) / SR) * env(n, 0.002, 0.02)
        i = int(k * 0.13 * SR)
        out[i:i + n] += b
    return out


if __name__ == "__main__":
    save("amb_day", amb_day(), 0.5)
    save("amb_night", amb_night(), 0.5)
    save("amb_rain", amb_rain(), 0.5)
    save("sfx_plant", thud(110, 0.25, 0.7), 0.5)
    save("sfx_harvest", pluck(330, 0.45) + bell((1320,), 0.45, 0.12) * 0.3, 0.55)
    save("sfx_coin", bell((1800, 2700), 0.3, 0.07), 0.35)
    save("sfx_dig", noise_burst(0.3, 200, 2500, 0.07) + thud(90, 0.3, 0.3) * 0.5, 0.5)
    save("sfx_chop", thud(190, 0.25, 1.0), 0.6)
    save("sfx_place", thud(140, 0.2, 0.4), 0.5)
    save("sfx_click", noise_burst(0.05, 1500, 6000, 0.008), 0.3)
    save("sfx_book", noise_burst(0.4, 1500, 7000, 0.09), 0.35)
    save("sfx_toast", bell((880, 1320), 0.7, 0.2), 0.3)
    save("sfx_thunder", sfx_thunder(), 0.7)
    save("sfx_magic", sfx_magic(), 0.45)
    save("sfx_tile", thud(70, 0.6, 0.8) + bell((660,), 0.6, 0.2) * 0.3, 0.55)
    save("sfx_bubble", sfx_bubble(), 0.4)
