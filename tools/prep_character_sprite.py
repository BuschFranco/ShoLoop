"""Turn any image into a game-ready character portrait.

    python tools/prep_character_sprite.py foto.jpg Assets/Sprites/Characters/amigo1.png
    python tools/prep_character_sprite.py foto.png Assets/Sprites/Characters/amigo2.png --ring 3dff8f
    python tools/prep_character_sprite.py old.png old.png --from-round   # migrate a pre-pixel portrait

Every character comes out as the same thing: a full-bleed square photo inside a hard neon frame.
Uniform on purpose -- the whole cast then renders at one size and reads as one set.

Square, not a disc: the game is pixel-art, and a circle mask has no way to end except in a soft
antialiased edge. --from-round exists because the nine portraits already in the repo were built as
discs and their source photographs were never committed; see the flag's own comment.

Accepts photos and cut-outs alike. A transparent input gets its empty padding trimmed first (so
the subject fills the disc rather than sitting small inside it) and whatever is still transparent
inside the circle is filled with the game's backdrop colour, so the result is never see-through.

The output is NOT tinted: these characters must carry Color = White in CharacterCatalog, because
that colour is applied as the sprite's Modulate and would multiply over the photo.
"""

import argparse
import os
from PIL import Image, ImageDraw

from assetlib.palette import BACKDROP_RGBA, RING_DEFAULT

# 4x the ~36px the ship occupies in world space -- the same supersample the SVG silhouettes use,
# which keeps the edge clean through the breathe/punch tweens that scale the sprite up.
DEFAULT_SIZE = 144
RING_WIDTH_RATIO = 0.055
BACKDROP = BACKDROP_RGBA          # was a private copy of Palette.Backdrop; now shared

# There used to be an SS = 4 supersample here, purely so the circular mask's edge wouldn't come out
# jagged. A square needs no such thing: its edges are axis-aligned, so they are exact at any size.


def unround(img):
    """Recover a square photo from a portrait that was already masted into a disc.

    The original photographs for the bundled cast are not in the repo, so there is nothing to re-crop
    from -- but the largest square that fits inside the old circle is still real photo data. Taking
    that inscribed square (side = diameter / sqrt(2), so ~71% of the width, centred) gives a genuine
    full-bleed square. The alternative, filling the transparent corners with backdrop, would leave a
    round photo sitting in a square frame -- visibly different from a portrait a player makes in
    CharacterCreator, which is exactly the drift this is meant to avoid.
    """
    img = img.convert("RGBA")
    side = int(min(img.width, img.height) / (2 ** 0.5))
    left, top = (img.width - side) // 2, (img.height - side) // 2
    return img.crop((left, top, left + side, top + side))


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

    # Flatten onto the backdrop so transparent pixels don't show the arena through.
    portrait = Image.alpha_composite(Image.new("RGBA", (size, size), BACKDROP), img)

    # A hard-edged square frame, drawn straight at final size -- no supersample, no antialiasing.
    # This must stay in step with CustomCharacterStore.MakePortrait, which does the same thing for
    # portraits players create in-game. The two have drifted apart once already.
    ring = max(2, round(size * RING_WIDTH_RATIO))
    ImageDraw.Draw(portrait).rectangle(
        (0, 0, size - 1, size - 1), outline=ring_rgb + (255,), width=ring)
    return portrait


def parse_hex(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("source")
    ap.add_argument("dest")
    ap.add_argument("--size", type=int, default=DEFAULT_SIZE)
    ap.add_argument("--ring", default=RING_DEFAULT, help="hex neon frame colour")
    ap.add_argument("--from-round", action="store_true",
                    help="source is an old disc-masked portrait; recover its inscribed square first")
    args = ap.parse_args()

    source = Image.open(args.source)
    if args.from_round:
        source = unround(source)
    out = make_portrait(source, args.size, parse_hex(args.ring))
    os.makedirs(os.path.dirname(args.dest) or ".", exist_ok=True)
    out.save(args.dest, "PNG")
    print(f"{args.source} -> {args.dest}  {out.width}x{out.height}  frame #{args.ring.lstrip('#')}")


if __name__ == "__main__":
    main()
