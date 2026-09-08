"""Emit the entity silhouettes (the player ship and the 11 enemies).

    python tools/gen_sprites.py            # writes every silhouette
    python tools/gen_sprites.py --only grunt boss

These were hand-written SVGs, ported from the `Polygon2D` vertex arrays the entities used before
commit e0ee940. Moving the vertices into this table makes them adjustable -- nudge a point, rescale a
whole shape -- without editing XML by hand, and puts them alongside every other generated asset.

docs/visuals.md calls them placeholders, and they are: the shapes carry the *silhouette language*
(triangle = basic, hexagon = tanky, star = demon, dart = fast) and are meant to be replaced by real
art eventually. Until then this is where they live.

CANVAS SIZE IS NOT ARBITRARY, and its meaning inverted in the pixel-art pass. Each shape used to be
authored at ~4x its arena size and drawn at `scale = Vector2(0.25, 0.25)` -- a supersample, for smooth
edges. It is now authored at **half** its arena size and drawn at `scale = Vector2(2, 2)`, so one texel
in the file is exactly one 2px block on screen. The canvas here IS the sprite's pixel grid.

Two world pixels per block rather than the project's usual four (`Juice.PixelSize`): these silhouettes
are small -- `hidden` is 22px in the arena -- and on a 4px grid it would be five blocks across, which
erases the shape language (triangle = basic, hexagon = tanky, star = demon, dart = fast) that is the
entire point of them. See docs/visuals.md.

Changing a size here means changing that scene's `scale` to match, and the arena footprint with it.
"""

import argparse
import os

from assetlib.raster import write_polygon

# name -> (canvas size in texels, output directory, vertices)
#
# Vertices are integers in texel coordinates, origin top-left. The shape does not have to fill the
# canvas: `grunt` is a 12-tall triangle on a 15 canvas, which is the margin its glow/shadow copies
# expand into without being clipped.
#
# A vertex at coordinate N needs a canvas of at least N+1: N is the boundary *after* pixel N-1, so a
# canvas of exactly N clips it. That cost several shapes their bottom tip when these were shrunk to
# texel scale, where one row is a large fraction of the sprite.
ENEMY_DIR = "Assets/Sprites/Enemies"
CHARACTER_DIR = "Assets/Sprites/Characters"

SPRITES = {
    # --- The player ---------------------------------------------------------------------------
    # A dart, and the only one whose rotation is ever visible: UpdateFacing turns it to face travel,
    # so it needs a clear "front".
    "ship": (19, CHARACTER_DIR, [(18, 9), (3, 15), (3, 3)]),

    # --- Common / Rare ------------------------------------------------------------------------
    "grunt": (15, ENEMY_DIR, [(7, 0), (14, 12), (0, 12)]),
    "rare": (15, ENEMY_DIR, [(7, 0), (14, 7), (7, 14), (0, 7)]),

    # --- Specials -----------------------------------------------------------------------------
    # Hexagons read as armoured, so both tanky enemies use one; the boss is the same shape scaled up.
    "tank": (23, ENEMY_DIR, [(11, 0), (20, 5), (20, 17), (11, 22), (2, 17), (2, 5)]),
    "speedy": (13, ENEMY_DIR, [(12, 6), (3, 2), (6, 6), (3, 10)]),
    "splitter": (17, ENEMY_DIR, [(8, 0), (16, 8), (8, 16), (0, 8)]),
    "shooter": (16, ENEMY_DIR, [(8, 0), (15, 5), (13, 15), (3, 15), (1, 5)]),
    "hidden": (13, ENEMY_DIR, [(6, 0), (8, 4), (12, 6), (8, 8), (6, 12), (4, 8), (0, 6), (4, 4)]),

    # --- Demons -------------------------------------------------------------------------------
    # Ten-point stars: spikier than anything else on screen, which is the read.
    "demon": (19, ENEMY_DIR,
              [(9, 0), (13, 6), (18, 4), (15, 11), (17, 17), (9, 14), (1, 17), (3, 11), (0, 4), (5, 6)]),
    "demon_brute": (29, ENEMY_DIR,
                    [(14, 0), (21, 5), (28, 7), (24, 16), (26, 26), (14, 22), (2, 26), (4, 16), (0, 7), (7, 5)]),
    "demon_stalker": (17, ENEMY_DIR,
                      [(16, 8), (9, 4), (11, 1), (6, 4), (1, 2), (4, 8), (1, 15), (6, 13), (11, 16), (9, 12)]),

    # --- Boss ---------------------------------------------------------------------------------
    # Note: EnemyBoss.tscn currently points at a photo portrait, not at this file. It's kept because
    # it's the fallback silhouette if that photo is ever swapped out.
    "boss": (41, ENEMY_DIR, [(20, 0), (37, 10), (37, 30), (20, 40), (3, 30), (3, 10)]),
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
        path = os.path.join(target_dir, f"{name}.png")
        write_polygon(path, size, points)
        print(f"{path}  {size}x{size}  {len(points)} pts")

    print(f"\n{len(sprites)} silhouette(s).")


if __name__ == "__main__":
    main()
