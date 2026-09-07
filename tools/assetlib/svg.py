"""Writing the flat silhouette SVGs the game uses for entity bodies.

Every entity sprite in this game is one white polygon on a square canvas. Colour never lives in the
file: `Sprite2D.modulate` tints the white silhouette at runtime, which is why one shape can serve an
enemy, its elite aura and its drop shadow (see docs/visuals.md).

The canvas is deliberately ~4x the size the sprite occupies in the arena — every entity scene sets
`scale = Vector2(0.25, 0.25)`. That supersample is what keeps the edges clean through the 1.25-1.3x
punch and telegraph tweens that scale these sprites up.
"""

import os

TEMPLATE = (
    '<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" '
    'viewBox="0 0 {size} {size}">\n'
    '  <polygon points="{points}" fill="#ffffff"/>\n'
    "</svg>\n"
)


def polygon_svg(size, points):
    """`points` is a flat sequence of (x, y) pairs in canvas coordinates."""
    joined = " ".join(f"{x},{y}" for x, y in points)
    return TEMPLATE.format(size=size, points=joined)


def write_polygon(path, size, points):
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    # newline="" so this writes LF on every platform. Godot doesn't care, but the repo's
    # .gitattributes normalises to LF and a CRLF file would show as modified on every regeneration.
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(polygon_svg(size, points))
