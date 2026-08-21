"""Turn any image into a game-ready circular character portrait.

    python tools/prep_character_sprite.py foto.jpg Assets/Sprites/Characters/amigo1.png
    python tools/prep_character_sprite.py foto.png Assets/Sprites/Characters/amigo2.png --ring 3dff8f

Every character comes out as the same thing: a solid disc with a neon ring, on a square RGBA
canvas. Uniform on purpose -- the whole cast then renders at one size and reads as one set.

Accepts photos and cut-outs alike. A transparent input gets its empty padding trimmed first (so
the subject fills the disc rather than sitting small inside it) and whatever is still transparent
inside the circle is filled with the game's backdrop colour, so the result is never see-through.

The output is NOT tinted: these characters must carry Color = White in CharacterCatalog, because
that colour is applied as the sprite's Modulate and would multiply over the photo.
"""

import argparse
import os
from PIL import Image, ImageDraw

# 4x the ~36px the ship occupies in world space -- the same supersample the SVG silhouettes use,
# which keeps the edge clean through the breathe/punch tweens that scale the sprite up.
DEFAULT_SIZE = 144
RING_WIDTH_RATIO = 0.055
BACKDROP = (11, 6, 20, 255)      # Palette.Backdrop
SS = 4                            # mask supersample, so the circle edge isn't jagged


def circle_mask(size):
    mask = Image.new("L", (size * SS, size * SS), 0)
    ImageDraw.Draw(mask).ellipse((0, 0, size * SS - 1, size * SS - 1), fill=255)
    return mask.resize((size, size), Image.LANCZOS)


def make_portrait(img, size, ring_rgb):
    img = img.convert("RGBA")

    # Trim transparent padding first: without this a cut-out with a wide empty margin would end up
    # as a small subject floating in the middle of the disc.
    bbox = img.getchannel("A").getbbox()
    if bbox and bbox != (0, 0, img.width, img.height):
        img = img.crop(bbox)

    # Centre-crop to a square so the circle never distorts the aspect ratio.
    side = min(img.width, img.height)
    left, top = (img.width - side) // 2, (img.height - side) // 2
    img = img.crop((left, top, left + side, top + side)).resize((size, size), Image.LANCZOS)

    # Flatten onto the backdrop so transparent pixels inside the disc don't show the arena through.
    disc = Image.alpha_composite(Image.new("RGBA", (size, size), BACKDROP), img)
    disc.putalpha(circle_mask(size))

    ring = max(2, round(size * RING_WIDTH_RATIO))
    inset = ring * SS // 2
    overlay = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
    ImageDraw.Draw(overlay).ellipse(
        (inset, inset, size * SS - 1 - inset, size * SS - 1 - inset),
        outline=ring_rgb + (255,), width=ring * SS,
    )
    return Image.alpha_composite(disc, overlay.resize((size, size), Image.LANCZOS))


def parse_hex(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("source")
    ap.add_argument("dest")
    ap.add_argument("--size", type=int, default=DEFAULT_SIZE)
    ap.add_argument("--ring", default="ff4fd8", help="hex neon ring colour")
    args = ap.parse_args()

    out = make_portrait(Image.open(args.source), args.size, parse_hex(args.ring))
    os.makedirs(os.path.dirname(args.dest) or ".", exist_ok=True)
    out.save(args.dest, "PNG")
    print(f"{args.source} -> {args.dest}  {out.width}x{out.height}  ring #{args.ring.lstrip('#')}")


if __name__ == "__main__":
    main()
