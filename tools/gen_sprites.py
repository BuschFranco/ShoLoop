"""Emit the entity silhouettes (the player ship and the 11 enemies).

    python tools/gen_sprites.py            # writes every silhouette
    python tools/gen_sprites.py --only grunt boss

These were hand-written SVGs, ported from the `Polygon2D` vertex arrays the entities used before
commit e0ee940. Moving the vertices into this table makes them adjustable -- nudge a point, rescale a
whole shape -- without editing XML by hand, and puts them alongside every other generated asset.

docs/visuals.md calls them placeholders, and they are: the shapes carry the *silhouette language*
(triangle = basic, hexagon = tanky, star = demon, dart = fast) and are meant to be replaced by real
art eventually. Until then this is where they live.

CANVAS SIZE IS NOT ARBITRARY. Each shape is authored at roughly 4x its in-arena size, because every
entity scene sets `scale = Vector2(0.25, 0.25)`. That supersample is what keeps the edges from
crawling when the hit-flash punches the sprite to 1.3x. Changing a size here means changing that
scene's scale to match.
"""

import argparse
import os

from assetlib.svg import write_polygon

# name -> (canvas size, output directory, vertices)
#
# Vertices are integers in canvas coordinates, origin top-left. The shape does not have to fill the
# canvas: `grunt` is a 96-tall triangle on a 112 canvas, which is the margin its glow/shadow copies
# expand into without being clipped.
ENEMY_DIR = "Assets/Sprites/Enemies"
CHARACTER_DIR = "Assets/Sprites/Characters"

SPRITES = {
    # --- The player ---------------------------------------------------------------------------
    # A dart, and the only one whose rotation is ever visible: UpdateFacing turns it to face travel,
    # so it needs a clear "front".
    "ship": (144, CHARACTER_DIR, [(144, 72), (24, 116), (24, 28)]),

    # --- Common / Rare ------------------------------------------------------------------------
    "grunt": (112, ENEMY_DIR, [(56, 0), (112, 96), (0, 96)]),
    "rare": (104, ENEMY_DIR, [(52, 0), (104, 52), (52, 104), (0, 52)]),

    # --- Specials -----------------------------------------------------------------------------
    # Hexagons read as armoured, so both tanky enemies use one; the boss is the same shape scaled up.
    "tank": (176, ENEMY_DIR, [(88, 0), (164, 44), (164, 132), (88, 176), (12, 132), (12, 44)]),
    "speedy": (96, ENEMY_DIR, [(96, 48), (24, 12), (44, 48), (24, 84)]),
    "splitter": (128, ENEMY_DIR, [(64, 0), (128, 64), (64, 128), (0, 64)]),
    "shooter": (128, ENEMY_DIR, [(64, 0), (120, 40), (104, 120), (24, 120), (8, 40)]),
    "hidden": (88, ENEMY_DIR, [(44, 0), (56, 32), (88, 44), (56, 56), (44, 88), (32, 56), (0, 44), (32, 32)]),

    # --- Demons -------------------------------------------------------------------------------
    # Ten-point stars: spikier than anything else on screen, which is the read.
    "demon": (144, ENEMY_DIR,
              [(72, 0), (104, 44), (144, 32), (120, 84), (136, 136), (72, 112), (8, 136), (24, 84), (0, 32), (40, 44)]),
    "demon_brute": (224, ENEMY_DIR,
                    [(112, 0), (168, 40), (224, 56), (192, 128), (208, 208), (112, 176), (16, 208), (32, 128), (0, 56), (56, 40)]),
    "demon_stalker": (128, ENEMY_DIR,
                      [(128, 64), (72, 32), (88, 4), (48, 28), (8, 12), (32, 64), (8, 116), (48, 100), (88, 124), (72, 96)]),

    # --- Boss ---------------------------------------------------------------------------------
    # Note: EnemyBoss.tscn currently points at a photo portrait, not at this file. It's kept because
    # it's the fallback silhouette if that photo is ever swapped out.
    "boss": (320, ENEMY_DIR, [(160, 0), (300, 80), (300, 240), (160, 320), (20, 240), (20, 80)]),
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", nargs="*", help="generate just these sprites, by name")
    ap.add_argument("--out", default=None,
                    help="override the output root (default: each sprite's own Assets/ folder)")
    args = ap.parse_args()

    sprites = SPRITES
    if args.only:
        unknown = [n for n in args.only if n not in sprites]
        if unknown:
            raise SystemExit(f"unknown sprite(s): {', '.join(unknown)}\n"
                             f"known: {', '.join(sorted(sprites))}")
        sprites = {n: sprites[n] for n in args.only}

    for name, (size, folder, points) in sprites.items():
        target_dir = args.out if args.out else folder
        path = os.path.join(target_dir, f"{name}.svg")
        write_polygon(path, size, points)
        print(f"{path}  {size}x{size}  {len(points)} pts")

    print(f"\n{len(sprites)} silhouette(s).")


if __name__ == "__main__":
    main()
