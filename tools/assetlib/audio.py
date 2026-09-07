"""Chiptune synthesis primitives for the audio generators in tools/.

Stdlib only -- `wave` is enough to write PCM, so nothing here needs installing.

These started life inside gen_sfx.py and moved out when a music generator arrived needing the exact
same building blocks. That music was later cut for sounding bad and its generator deleted, so
gen_sfx.py is currently the only caller. This module stays anyway: it's the readable home for the
oscillators either way, and the next thing that generates audio will want precisely these.

The low sample rate is deliberate and is *part of the sound*, not a compromise: it rolls the high end
off the square waves the way the hardware being imitated did.
"""

import math
import random
import struct
import wave

SR = 22050

# A pentatonic set, so anything arpeggiated lands in tune with everything else in the game.
C3, D3, E3, G3, A3 = 131, 147, 165, 196, 220
C4, D4, E4, G4, A4 = 262, 294, 330, 392, 440
C5, D5, E5, G5, A5 = 523, 587, 659, 784, 880
C6, E6, G6 = 1047, 1319, 1568


def tone(f0, f1, dur, duty=0.5, wave_kind="square"):
    """A tone gliding from f0 to f1. Phase is accumulated rather than computed from t, because
    sampling sin(2*pi*f*t) with a changing f steps the phase and puts a click in every sweep."""
    n = max(1, int(SR * dur))
    out = []
    phase = 0.0
    for i in range(n):
        f = f0 + (f1 - f0) * (i / n)
        phase = (phase + f / SR) % 1.0
        if wave_kind == "square":
            out.append(1.0 if phase < duty else -1.0)
        elif wave_kind == "saw":
            out.append(2.0 * phase - 1.0)
        elif wave_kind == "tri":
            out.append(4.0 * abs(phase - 0.5) - 1.0)
        else:
            out.append(math.sin(2.0 * math.pi * phase))
    return out


def noise(dur):
    return [random.uniform(-1.0, 1.0) for _ in range(max(1, int(SR * dur)))]


def shape(samples, attack=0.004, hold=0.0, curve=2.5):
    """Fast fade in, hold, then decay to silence across whatever is left.

    The attack is never zero: starting a square wave at full amplitude puts a click at the front of
    the sound. `curve` is the whole personality -- 4+ is a percussive tick, ~1 is a held note."""
    n = len(samples)
    a = max(1, int(SR * attack))
    h = int(SR * hold)
    tail = max(1, n - a - h)
    out = []
    for i, s in enumerate(samples):
        if i < a:
            g = i / a
        elif i < a + h:
            g = 1.0
        else:
            g = (1.0 - (i - a - h) / tail) ** curve
        out.append(s * g)
    return out


def mix(*tracks):
    out = [0.0] * max(len(t) for t in tracks)
    for track in tracks:
        for i, s in enumerate(track):
            out[i] += s
    return out


def seq(*tracks):
    out = []
    for track in tracks:
        out.extend(track)
    return out


def gain(samples, g):
    return [s * g for s in samples]


def silence(dur):
    return [0.0] * max(1, int(SR * dur))


def arp(freqs, note_dur, duty=0.5, curve=1.6):
    """One note per frequency, in order. Rising reads as reward, falling as loss -- that direction
    is doing more communicative work than the specific notes are."""
    return seq(*[shape(tone(f, f, note_dur, duty), attack=0.003, curve=curve) for f in freqs])


def pad(samples, dur):
    """Extend (or truncate) to exactly `dur` seconds. Layering a bassline against a drum pattern only
    lines up if every layer is the same length to the sample."""
    n = max(1, int(SR * dur))
    if len(samples) >= n:
        return samples[:n]
    return samples + [0.0] * (n - len(samples))


def normalize(samples, peak_target):
    """Scale so the loudest sample sits at peak_target. Done before any per-asset volume so a recipe
    change can't silently make one sound twice as loud as the rest of the mix."""
    peak = max(1e-9, max(abs(s) for s in samples))
    scale = peak_target / peak
    return [s * scale for s in samples]


def write_wav(path, samples, volume):
    """Normalise to the loudest sample, then scale to this asset's slot in the mix."""
    scaled = normalize(samples, volume)
    frames = bytearray()
    for s in scaled:
        frames += struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767))

    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(bytes(frames))
