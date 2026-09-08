"""Writing the flat silhouette PNGs the game uses for entity bodies.

Every entity sprite in this game is one white polygon on a square canvas. Colour never lives in the
file: `Sprite2D.modulate` tints the white silhouette at runtime, which is why one shape can serve an
enemy, its elite aura and its drop shadow (see docs/visuals.md).

WHY PNG AND NOT SVG
-------------------
These were SVGs, authored at ~4x their arena size and drawn at `scale = Vector2(0.25, 0.25)`. That
supersample existed to keep edges clean through the 1.25-1.3x punch tweens, back when the project
filtered textures linearly.

The pixel-art pass made that backwards. With nearest filtering a 4:1 downscale is point sampling --
it throws away fifteen of every sixteen texels and aliases hard. Worse, an SVG can't produce a hard
edge at small sizes anyway: Godot rasterises it with the SVG renderer's own antialiasing, so the
softness would be baked into the texels where no filter setting can reach it.

So the shapes are now rasterised here, at their final texel size, with a **hard** polygon fill --
PIL's ImageDraw.polygon covers whole pixels and does no antialiasing at all. One texel in the file is
one block on screen, and the scene's `scale` says how big a block is.
"""

import os

from PIL import Image, ImageDraw


def write_polygon(path, size, points):
    """One white polygon, hard-edged, on a transparent `size` x `size` RGBA canvas.

    `points` is a flat sequence of (x, y) pairs in texel coordinates, origin top-left.
    """
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(image).polygon([tuple(p) for p in points], fill=(255, 255, 255, 255))
    # optimize=True keeps these deterministic and tiny; they are a few hundred bytes each.
    image.save(path, "PNG", optimize=True)
