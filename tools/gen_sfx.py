"""Synthesise the game's retro arcade sound effects.

    python tools/gen_sfx.py                      # writes every sound to Assets/Audio/
    python tools/gen_sfx.py --only shoot hit     # regenerate just these two
    python tools/gen_sfx.py --out /tmp/audition  # write somewhere else to audition first

The synthesis primitives live in assetlib/audio.py, shared with the music generator -- both are built
from the same square waves, noise and envelopes, and two copies of the oscillator that defines what
the game sounds like would drift apart.

This script is the sound *design*: the recipes below and the VOLUME table are the whole of it. The
.wav files are build output -- to change how something sounds, change the recipe here and re-run,
don't hand-edit a binary. Same arrangement as prep_character_sprite.py and the character portraits.

ADD NEW RECIPES AT THE END OF build(). noise() draws from a single seeded RNG, consumed in the order
the recipes are evaluated -- which is the order they appear in the dict below. Inserting a recipe that
uses noise() anywhere but the end shifts that stream for every noise-using sound after it, silently
regenerating sounds you never touched. build_assets.py --check catches it, but only if you look.

IMPORTANT -- there is a manual step after this one. Godot assigns each asset a uid:// in a .import
sidecar, and only the editor can do that. So: run this, open Godot once (or `godot --headless
--import`), then commit the .wav *and* the generated .wav.import together.
"""

import argparse
import os
import random

from assetlib.audio import (
    SR, arp, gain, mix, noise, pad, seq, shape, tone, write_wav,
    C4, E4, G4, G3, C5, D5, E5, G5, A5, C6, E6, G6,
)

# Peak level per sound, applied after normalising. This is the mix: it's the only thing standing
# between "arcade" and "wall of noise", so the numbers matter more than the recipes do.
#
# The rule is frequency-of-play, inverted. `hit` fires several times a second in a late round with
# Perforación and Rebote, so it sits far below `player_die`, which fires once per run.
VOLUME = {
    "shoot": 0.34,
    "enemy_shoot": 0.30,
    "hit": 0.24,
    "enemy_die": 0.42,
    "explosion": 0.62,
    "boss_die": 0.90,
    "player_hurt": 0.85,
    "shield": 0.70,
    "dodge": 0.45,
    "player_die": 1.00,
    "pickup": 0.42,
    "level_up": 0.72,
    "countdown": 0.62,
    "round_start": 0.80,
    "boss_alarm": 0.92,
    "round_complete": 0.80,
    "ui_click": 0.42,
    "ui_buy": 0.62,
    "ui_denied": 0.58,
    "modal": 0.34,
}

# --- The sounds -----------------------------------------------------------------------------

def build():
    """Every effect, by the name the game loads it as (AudioManager's Sfx enum)."""
    return {
        # Fires up to ~5x a second all run long, so it's short and quiet enough to sit under
        # everything else. The downward sweep is the classic arcade "pew".
        "shoot": shape(tone(900, 280, 0.085, duty=0.35), attack=0.002, curve=2.6),

        # Deliberately duller and lower than the player's: you should be able to tell who fired
        # without looking.
        "enemy_shoot": shape(tone(420, 150, 0.11, duty=0.5), attack=0.003, curve=2.0),

        # The most-repeated sound in the game by a wide margin. Kept to a dry tick -- anything with
        # a tail turns into mush the moment a piercing shot crosses three enemies.
        "hit": mix(
            shape(noise(0.045), attack=0.001, curve=4.0),
            gain(shape(tone(700, 340, 0.045), attack=0.001, curve=3.5), 0.5),
        ),

        "enemy_die": mix(
            shape(noise(0.17), attack=0.001, curve=2.2),
            gain(shape(tone(320, 70, 0.17, duty=0.3), attack=0.002, curve=1.8), 0.65),
        ),

        "explosion": mix(
            shape(noise(0.38), attack=0.002, curve=1.6),
            gain(shape(tone(180, 40, 0.38, duty=0.3), attack=0.004, curve=1.4), 0.8),
        ),

        "boss_die": seq(
            mix(shape(noise(0.3), attack=0.002, curve=1.5),
                gain(shape(tone(200, 50, 0.3, duty=0.3), curve=1.3), 0.9)),
            arp([C6, G5, C6, E6], 0.12, duty=0.4, curve=1.2),
        ),

        # Harsh and low, and the one sound allowed to be genuinely unpleasant -- losing a life is
        # the single most important thing the game has to tell you.
        "player_hurt": mix(
            shape(tone(260, 90, 0.26, duty=0.22), attack=0.002, curve=1.5),
            gain(shape(noise(0.26), attack=0.002, curve=2.2), 0.55),
        ),

        # Must be unmistakably NOT player_hurt: this says "you're fine, the shield ate it". Clean,
        # bright, metallic -- no noise layer at all, which is what separates them.
        "shield": mix(
            shape(tone(1180, 1180, 0.2, wave_kind="tri"), attack=0.002, curve=2.4),
            gain(shape(tone(1770, 1770, 0.14, wave_kind="sine"), attack=0.002, curve=3.0), 0.5),
        ),

        # A dodge currently has no feedback of any kind in the game -- no visual, nothing. This is
        # the only thing telling you the reward you bought just did something.
        "dodge": gain(shape(mix(noise(0.16), gain(tone(500, 1500, 0.16, wave_kind="tri"), 0.5)),
                            attack=0.02, curve=2.0), 0.8),

        "player_die": arp([A5, G5, E5, C5, G4, E4, C4, G3], 0.13, duty=0.3, curve=0.9),

        "pickup": arp([G5, C6], 0.05, duty=0.4, curve=2.0),

        "level_up": arp([C5, E5, G5, C6], 0.075, duty=0.45, curve=1.3),

        "countdown": shape(tone(660, 660, 0.1, duty=0.5), attack=0.003, curve=2.2),

        # The "GO". Same family as the countdown beep but an octave up and doubled, so it lands as
        # the end of that sequence rather than as an unrelated sound.
        "round_start": arp([C6, G6], 0.09, duty=0.45, curve=1.6),

        "round_complete": arp([C5, E5, G5, C6, E6], 0.085, duty=0.45, curve=1.3),

        "ui_click": shape(tone(1400, 1100, 0.028, duty=0.4), attack=0.001, curve=3.5),

        "ui_buy": arp([E5, A5, C6], 0.055, duty=0.45, curve=1.7),

        # Falling and buzzy -- the inverse of ui_buy, which is what makes it read as refusal.
        "ui_denied": seq(
            shape(tone(200, 200, 0.09, duty=0.2), attack=0.002, curve=1.8),
            shape(tone(150, 150, 0.13, duty=0.2), attack=0.002, curve=1.6),
        ),

        # Quiet enough to be felt more than heard; it plays on every menu transition.
        "modal": gain(shape(mix(noise(0.13), gain(tone(300, 900, 0.13, wave_kind="tri"), 0.4)),
                            attack=0.015, curve=2.5), 0.7),

        # Boss-round klaxon: a two-tone alarm, three cycles of it, over a low rumble.
        #
        # The alternation is what makes it read as an *alarm* rather than as a long beep -- a single
        # sustained tone at this length just sounds like a UI error. Duty 0.35 rather than a clean 0.5
        # square adds the reedy buzz a klaxon has. The rumble underneath is padded to the full length
        # of the two-tone pattern so it holds the whole thing together instead of ducking between
        # beeps, and it's the one part that isn't pitched: it's felt more than heard.
        "boss_alarm": mix(
            seq(*[
                part
                for _ in range(3)
                for part in (
                    shape(tone(392, 392, 0.17, duty=0.35), attack=0.008, hold=0.10, curve=1.2),
                    shape(tone(294, 294, 0.17, duty=0.35), attack=0.008, hold=0.10, curve=1.2),
                )
            ]),
            gain(pad(shape(noise(1.02), attack=0.05, hold=0.8, curve=1.1), 1.02), 0.22),
        ),
    }

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="Assets/Audio", help="output directory")
    ap.add_argument("--only", nargs="*", help="generate just these sounds, by name")
    ap.add_argument("--seed", type=int, default=7,
                    help="noise seed; fixed so re-running produces identical files")
    args = ap.parse_args()

    # Seeded so a regeneration that changes nothing produces byte-identical files, and git stays
    # quiet instead of showing every .wav as modified on every run.
    random.seed(args.seed)

    sounds = build()
    if args.only:
        unknown = [n for n in args.only if n not in sounds]
        if unknown:
            raise SystemExit(f"unknown sound(s): {', '.join(unknown)}\nknown: {', '.join(sorted(sounds))}")
        sounds = {n: sounds[n] for n in args.only}

    os.makedirs(args.out, exist_ok=True)
    for name, samples in sounds.items():
        path = os.path.join(args.out, f"{name}.wav")
        write_wav(path, samples, VOLUME[name])
        print(f"{path}  {len(samples) / SR * 1000:5.0f} ms  vol {VOLUME[name]:.2f}")

    print(f"\n{len(sounds)} file(s). Now open Godot once so it writes the .wav.import sidecars, "
          f"then commit the .wav and .import files together.")


if __name__ == "__main__":
    main()
