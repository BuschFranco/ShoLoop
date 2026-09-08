#!/usr/bin/env python3
"""Build Assets/Fonts/PixelFont.ttf -- a TrueType font whose glyphs are literal squares.

WHY A TTF AND NOT A BITMAP FONT
-------------------------------
The UI uses fifteen distinct font sizes (12 13 14 15 16 17 18 20 22 26 30 32 36 40 72). A bitmap
atlas font is crisp at its native size and integer multiples of it, and resampled-soft everywhere
else -- which would be most of those fifteen. A TTF made of axis-aligned squares, imported with
antialiasing off, has no native size: the rasterizer fills whole pixels at any scale, so it stays
hard-edged at 12 and at 72 alike. That also means it survives the project's non-integer window
scaling, which we explicitly decided not to change.

THE GRID
--------
Glyphs are 5 wide by 7 tall, the last row sitting on the baseline. One grid pixel is 125 font units
against an em of 1000, i.e. **8 grid pixels to the em**. That ratio is chosen so the game's most-used
size lands exactly: at font_size 16 one grid pixel is exactly 2.0 screen pixels, and 20/24/32/40/72
come out whole too. Sizes like 13 land on a fraction and the rasterizer rounds them -- squares end up
1 or 2 px wide rather than uniformly 1.6 -- which reads as pixel-art rather than as blur.

Accents and descenders deliberately overflow the em box (up to 1125 units up, 125 down). That is
legal TrueType; the hhea/OS/2 ascender and descender below are set to cover them so lines never clip.

Run:  python tools/gen_font.py
      python tools/gen_font.py --out /tmp/try   # write somewhere else first
"""

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from assetlib.glyphs import DESCENDERS, build_glyph_table

try:
    from fontTools.fontBuilder import FontBuilder
    from fontTools.pens.ttGlyphPen import TTGlyphPen
except ImportError:
    sys.exit(
        "gen_font.py needs fontTools:\n\n    pip install -r tools/requirements.txt\n"
    )

# --- Metrics ---------------------------------------------------------------------------------

UNITS_PER_EM = 1000
PIXEL = 125                    # 8 grid pixels to the em; see the module docstring for why
GLYPH_COLS = 5
BASELINE_ROW = 6               # the last row of the 7-row block rests on the baseline
ADVANCE = (GLYPH_COLS + 1) * PIXEL   # one blank column of letter-spacing

# Row -2 is the top of an uppercase accent; row 7 is the bottom of a descender.
ASCENT = (BASELINE_ROW - (-2) + 1) * PIXEL   # 1125
DESCENT = -(7 - BASELINE_ROW) * PIXEL        # -125
CAP_HEIGHT = 7 * PIXEL                       # 875
X_HEIGHT = 5 * PIXEL                         # 625, lowercase starts at row 2

FAMILY = "Infinitix Pixel"
VERSION = "1.000"

FONT_NAME = "PixelFont.ttf"

# Drawn rather than left as an empty box: if a codepoint ever slips through uncovered, a visible
# hollow rectangle on screen is far easier to notice than a blank space.
NOTDEF = ["#####", "#...#", "#...#", "#...#", "#...#", "#...#", "#####"]


def horizontal_runs(row):
    """Consecutive '#' spans in one row, as (start_col, end_col_exclusive).

    Merging a row into runs instead of emitting one square per pixel matters: adjacent squares share
    an edge, and some rasterizers leave a hairline seam there. A run is a single rectangle, so a
    stretch like "#####" has no interior edges at all.
    """
    runs = []
    col = 0
    while col < len(row):
        if row[col] == "#":
            start = col
            while col < len(row) and row[col] == "#":
                col += 1
            runs.append((start, col))
        else:
            col += 1
    return runs


def draw_glyph(rows_by_index, shift=0):
    """Turn {row_index: "#.##."} into a TrueType glyph.

    Contours are wound clockwise in the Y-up font coordinate system, which is what TrueType wants for
    filled outlines.
    """
    pen = TTGlyphPen(None)
    for row_index in sorted(rows_by_index):
        row = rows_by_index[row_index]
        y0 = (BASELINE_ROW - (row_index + shift)) * PIXEL
        y1 = y0 + PIXEL
        for start, end in horizontal_runs(row):
            x0, x1 = start * PIXEL, end * PIXEL
            pen.moveTo((x0, y0))
            pen.lineTo((x0, y1))
            pen.lineTo((x1, y1))
            pen.lineTo((x1, y0))
            pen.closePath()
    return pen.glyph()


def glyph_name(char):
    return "uni%04X" % ord(char)


def build(out_path):
    table = build_glyph_table()

    glyph_order = [".notdef"]
    glyphs = {".notdef": draw_glyph({i: r for i, r in enumerate(NOTDEF)})}
    metrics = {".notdef": (ADVANCE, 0)}
    cmap = {}

    # sorted() keeps the output byte-identical run to run, which is what lets build_assets.py --check
    # detect a hand-edited font the same way it does for the .wav files.
    for char in sorted(table, key=ord):
        name = glyph_name(char)
        shift = 1 if char in DESCENDERS else 0
        glyph_order.append(name)
        glyphs[name] = draw_glyph(table[char], shift)
        metrics[name] = (ADVANCE, 0)
        cmap[ord(char)] = name

    fb = FontBuilder(UNITS_PER_EM, isTTF=True)
    fb.setupGlyphOrder(glyph_order)
    fb.setupCharacterMap(cmap)
    fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=ASCENT, descent=DESCENT, lineGap=0)
    fb.setupNameTable({
        "familyName": FAMILY,
        "styleName": "Regular",
        "uniqueFontIdentifier": "%s %s" % (FAMILY, VERSION),
        "fullName": "%s Regular" % FAMILY,
        "psName": "InfinitixPixel-Regular",
        "version": "Version " + VERSION,
    })
    fb.setupOS2(
        sTypoAscender=ASCENT, sTypoDescender=DESCENT, sTypoLineGap=0,
        usWinAscent=ASCENT, usWinDescent=-DESCENT,
        sxHeight=X_HEIGHT, sCapHeight=CAP_HEIGHT,
        achVendID="INFX",
    )
    fb.setupPost()

    # FontBuilder stamps head.created/modified with the wall clock. Pinning them to the TrueType
    # epoch is what makes two runs produce identical bytes.
    fb.font["head"].created = 0
    fb.font["head"].modified = 0

    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
    fb.save(out_path)
    return len(cmap)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="Assets/Fonts", help="output directory")
    args = ap.parse_args()

    out_path = os.path.join(args.out, FONT_NAME)
    count = build(out_path)
    print("%s  %d glyphs  %.1f KB" % (FONT_NAME, count, os.path.getsize(out_path) / 1024.0))
    print("  -> " + out_path)


if __name__ == "__main__":
    main()
