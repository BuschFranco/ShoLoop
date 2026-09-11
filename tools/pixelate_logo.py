"""Turn the raw INFINITIX logo export into game- and web-ready assets.

    python tools/pixelate_logo.py Assets/logo.png Assets/Branding/infinitix_logo_cutout.png Assets/Branding/infinitix_logo_pixel.png
    python tools/pixelate_logo.py Assets/logo.png Assets/Branding/infinitix_logo_cutout.png Assets/Branding/infinitix_logo_pixel.png --block 6

Two jobs, because the source isn't what it looks like:

1. CUTOUT. The source usually isn't a real transparent image -- either a JPEG screenshot of a
   "transparent PNG" preview (the checkerboard baked into real pixels, since JPEG can't hold an alpha
   channel at all) or a PNG saved with its preview backdrop flattened in instead of true alpha (no
   checkerboard, just one solid colour). `cutout()` samples a strip along the top of the source
   (guaranteed pure background, above the wordmark) and picks whichever test fits what it finds
   there, so the same tool handles both without the caller having to know which one a given source is:

   - **Checkerboard** (the sampled strip has two distinct luminance tones, ~50+ apart): every pixel
     that's both fairly desaturated AND within the checker's luminance range gets zeroed. A luminance
     *range* rather than "close to one of the two known tones" on purpose: at every seam between a
     light and a dark checker square, JPEG blending leaves a thin line of pixels at roughly the
     *midpoint* luminance between the two tones -- outside a "close to either tone" distance test
     entirely, close to neither. And a generous saturation ceiling (not a strict "basically grey" one)
     because the source can also render a soft ambient glow around the wordmark, which a flattened
     JPEG shows as checker-tinted-by-translucent-colour rather than clean alpha -- moderately
     saturated (0.3-0.4), same as some of the checker's own JPEG ringing, and nowhere near the
     wordmark's own built-in outline colours (0.75+) or its palest highlights (luminance 230+, so
     protected by the range regardless of saturation). Recovering the glow's true colour/alpha would
     need real alpha matting, which this per-pixel test isn't, so it's simply removed along with the
     rest of the background.
   - **Solid backdrop** (the sampled strip is all one colour): plain RGB colour-distance from the
     sampled mean, same as a conventional single-colour chroma key. The saturation test above doesn't
     apply here -- a solid backdrop can be strongly saturated itself (a dark purple background, not a
     neutral grey, in the source this branch was written against), so refusing to touch anything
     saturated would refuse to touch the background at all.

   Either way this is a plain per-pixel test, not a real matting algorithm -- a handful of edge pixels
   right at the letter/background boundary can be left faintly tinted. Good enough for a wordmark; if
   it shows up, raise SATURATION_MAX or SOLID_COLOR_DIST for a harder cut (whichever branch applies),
   or lower it for fewer false positives inside the letters.

2. PIXELATE. A second pass over the cutout, for the game copy only -- the game's whole UI is
   procedural pixel-art with nearest-neighbour filtering and zero antialiasing (see docs/visuals.md),
   and this glossy/gradiented logo is the opposite of that by construction. Downscale-then-upscale
   with NEAREST is the same trick tools/gen_font.py and tools/gen_sprites.py already rely on for hard
   edges: every --block source pixels collapse into one flat-coloured block, gradients included, and
   the alpha edge from step 1 gets the same stepped treatment instead of staying smooth.

The web site keeps the CUTOUT output as-is (unpixelated) -- it already uses gradients and rounded
corners, so the glossy original fits there with no further processing.
"""

import argparse

from PIL import Image, ImageChops, ImageFilter

# Near-neutral: (max-min channel) / max below this counts as "essentially grey" -- a RATIO, not a
# raw channel spread, because the logo's dark navy outline is dark enough (low absolute values) that
# its raw spread overlaps the halo's, while its saturation (0.75+) sits nowhere near the halo's
# (0.3-0.4). Wide enough to catch the soft ambient glow the source renders around the wordmark, which
# got flattened into "checker tinted by translucent colour" rather than clean alpha the same way the
# rest of the checkerboard did -- recovering true colour/alpha for that would need real alpha
# matting (this is a per-pixel colour test, not that), so it's simply keyed out with everything else;
# the wordmark keeps its own built-in neon outline glow regardless, just not the extra soft bloom
# further out.
SATURATION_MAX = 0.42
# Padding added on both ends of the detected checker luminance range, below. What actually protects
# real content here: the palest chrome highlights sample at luminance 230+, and the dark navy outline
# at 20-35 -- both well outside this range regardless of saturation, so raising SATURATION_MAX to
# reach the glow halo doesn't risk either one.
LUMINANCE_MARGIN = 12


def sample_background(img, sample_rows=100):
    """Raw background samples from a strip along the top of the source -- guaranteed pure
    background, since the wordmark sits well below it in every source this has seen so far."""
    return [
        img.getpixel((x, y))[:3]
        for y in range(0, min(sample_rows, img.height), 2)
        for x in range(0, img.width, 2)
    ]


# Above this, the sampled strip has two distinct tones (a checkerboard) rather than one flat colour
# -- a real PNG export with a solid backdrop samples within a few units of itself; a checkerboard
# screenshot samples ~50+ apart (see the two module-level constants above the checkerboard branch,
# tuned against an actual example of each).
CHECKERBOARD_LUM_SPREAD = 30

# For a SOLID background: plain RGB distance from the sampled mean colour (squared, to skip the
# sqrt in the hot loop). Unlike the checkerboard case, this background can be strongly saturated
# (a flat dark purple backdrop measured here, not a neutral grey) -- SATURATION_MAX would refuse to
# touch it at all, so a solid background is keyed on colour distance instead, the same way a
# conventional single-colour chroma key works.
SOLID_COLOR_DIST = 35
SOLID_COLOR_DIST_SQ = SOLID_COLOR_DIST * SOLID_COLOR_DIST


def cutout(img):
    """Keys out the background, replacing it with real alpha transparency. Two different sources
    need two different tests (picked automatically, see CHECKERBOARD_LUM_SPREAD): a checkerboard
    screenshot has no single background colour to key against, while a solid backdrop isn't
    reliably desaturated the way a checkerboard's grey squares are."""
    img = img.convert("RGBA")
    samples = sample_background(img)
    lums = [sum(c) / 3 for c in samples]
    pixels = img.load()

    if max(lums) - min(lums) > CHECKERBOARD_LUM_SPREAD:
        lum_lo, lum_hi = min(lums) - LUMINANCE_MARGIN, max(lums) + LUMINANCE_MARGIN
        for y in range(img.height):
            for x in range(img.width):
                r, g, b, a = pixels[x, y]
                peak = max(r, g, b)
                saturation = (peak - min(r, g, b)) / peak if peak else 0.0
                luminance = (r + g + b) / 3
                if saturation < SATURATION_MAX and lum_lo <= luminance <= lum_hi:
                    pixels[x, y] = (r, g, b, 0)
    else:
        n = len(samples)
        mean = tuple(sum(c[i] for c in samples) / n for i in range(3))
        for y in range(img.height):
            for x in range(img.width):
                r, g, b, a = pixels[x, y]
                dist_sq = (r - mean[0]) ** 2 + (g - mean[1]) ** 2 + (b - mean[2]) ** 2
                if dist_sq < SOLID_COLOR_DIST_SQ:
                    pixels[x, y] = (r, g, b, 0)

    r, g, b, a = img.split()

    # A few isolated JPEG-noise flecks elsewhere in the checker field can still slip through above --
    # rare, but each one drags trim()'s bounding box out to include a patch of otherwise-empty canvas
    # around it. An opening (erode then dilate back by the same amount) clears them: a fleck only a
    # few pixels wide is erased outright by the erosion and has nothing left to regrow from, while the
    # letterforms -- tens of pixels thick -- lose and then regain their edge with no visible change.
    a = a.filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(5))

    # The spread/luminance test is a per-pixel guess, not real matting -- right at the letter/checker
    # boundary it leaves a thin, noisy ring of pixels just outside the thresholds above. Eroding the
    # alpha by a couple more pixels trims that ring away, and a small blur turns the jagged
    # pixel-by-pixel on/off edge from the hard cutoff above into one soft antialiased line instead.
    a = a.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(1.2))
    return Image.merge("RGBA", (r, g, b, a))


def trim(img, padding=12):
    """Crops to the opaque content's bounding box, plus a little breathing room -- the source canvas
    is a big square with the wordmark occupying a comparatively thin strip across the middle, and
    every consumer of these assets (a TextureRect in-game, an <img> on the web) wants that strip, not
    the mostly-empty square it came in."""
    # img.getbbox() alone would look at all 4 bands and treat a transparent pixel's stale leftover
    # RGB (still checker-grey; cutout() never clears it, only zeroes alpha) as "content" -- so this
    # asks only the alpha band, which is the one channel that actually says what's visible.
    bbox = img.split()[3].getbbox()
    if bbox is None:
        return img
    left, top, right, bottom = bbox
    left = max(0, left - padding)
    top = max(0, top - padding)
    right = min(img.width, right + padding)
    bottom = min(img.height, bottom + padding)
    return img.crop((left, top, right, bottom))


def pixelate(img, block):
    """Downscale then upscale with NEAREST so every `block` source pixels become one flat block.

    Alpha is premultiplied before the LANCZOS downscale and undone right after -- Pillow resizes
    every RGBA channel independently, so a fully-transparent pixel's leftover RGB (still the
    checker-grey it started as; cutout() only zeroed its alpha, never its colour) would otherwise get
    blended into the edge blocks next to it and reappear as a faint grey checker pattern once
    nearest-upscaled back to full size. Premultiplying makes a transparent pixel's contribution to
    that blend (0, 0, 0) instead of (grey, grey, grey), so only real, visible colour survives into
    each block.
    """
    small_size = (max(1, img.width // block), max(1, img.height // block))

    r, g, b, a = img.split()
    premultiplied = Image.merge("RGBA", (
        ImageChops.multiply(r, a), ImageChops.multiply(g, a), ImageChops.multiply(b, a), a,
    ))
    small = premultiplied.resize(small_size, Image.LANCZOS)

    # Undo the premultiply now that filtering is done -- too slow as a per-pixel Python loop at full
    # resolution, but `small` is tiny by construction (source size / block).
    pixels = small.load()
    for y in range(small.height):
        for x in range(small.width):
            pr, pg, pb, pa = pixels[x, y]
            if pa > 0:
                pixels[x, y] = (min(255, pr * 255 // pa), min(255, pg * 255 // pa), min(255, pb * 255 // pa), pa)

    return small.resize(img.size, Image.NEAREST)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("source", help="the raw logo export (e.g. Assets/logo.jpg)")
    ap.add_argument("cutout_out", help="where to write the background-removed, unpixelated PNG (web)")
    ap.add_argument("pixel_out", help="where to write the pixelated PNG (game)")
    ap.add_argument("--block", type=int, default=8, help="source pixels per pixelated block (default 8)")
    args = ap.parse_args()

    source = Image.open(args.source)
    cut = trim(cutout(source))
    cut.save(args.cutout_out)
    print(f"{args.cutout_out}  {cut.width}x{cut.height}")

    pixel = pixelate(cut, args.block)
    pixel.save(args.pixel_out)
    print(f"{args.pixel_out}  {pixel.width}x{pixel.height}  block={args.block}")


if __name__ == "__main__":
    main()
