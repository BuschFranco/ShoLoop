---
name: pixel-art-image-prep
description: Turns a raw image the user hands you (a logo export, a screenshot, an arbitrary PNG/JPEG) into assets this project can actually use — a clean background-removed cutout with real alpha transparency, and/or a pixel-art-pixelated version matching Infinitix's procedural, nearest-neighbor, zero-antialiasing visual style (see docs/visuals.md). Use this whenever the user pastes or attaches an image and asks to "recortar el fondo", remove/cut a background, make something transparent, pixelate an image, or wants a logo/asset added to the game menu or the web site — even if they don't use those exact words. Especially relevant when the source looks like a checkerboard-background "transparent PNG" preview that was actually saved as a JPEG (very common when someone screenshots or re-exports a transparency preview) — that checkerboard is baked into real pixels, not real alpha, and needs the specific removal technique this skill documents, not a naive color-key.
---

# Preparing images for Infinitix (game + web)

This captures what actually worked (and what didn't) removing the background from and pixelating
the INFINITIX logo, done live in a session with the user watching the result at every step. The
reusable implementation is `tools/pixelate_logo.py` — read it before touching pixels yourself, it
already has all of this built in and documented in its own module docstring.

## When to reach for this

- The user gives you an image and wants it in the game and/or on the web (`web/`).
- The image needs its background removed (transparent PNG requested, or a checkerboard is visible).
- The image needs to look pixel-art (game menus, HUD, anything inside the actual game view) as
  opposed to left glossy/photographic (the web site — it already uses gradients and rounded corners,
  it has no pixel-art constraint).

Don't reach for a generic "remove background" approach without reading the section below first — the
naive version (chroma-key one exact background color) silently fails on a screenshot-of-a-transparent-
preview source, which is the single most common way these images arrive.

## Step 0: get the actual file

A pasted-in-chat image is not a file on disk. Ask the user to save it into the repo (suggest
`Assets/Branding/<name>_source.png` or wherever makes sense) and give you the path — there is no tool
that pulls a chat attachment onto the filesystem for you. Check what format they actually saved: if
it's a `.jpg`/`.jpeg`, assume it has no real alpha channel at all (JPEG can't hold one) even if it
*looks* like it has a transparent checkerboard background — that checkerboard was rendered into flat
pixels by whatever tool showed the "transparent" preview, then flattened into the JPEG. This is the
normal case, not an edge case; plan for it by default rather than assuming real alpha until proven
otherwise (`Image.open(path).mode == "RGBA"` with genuinely varying alpha values is the tell that it's
real).

## Step 1: run `tools/pixelate_logo.py`

```
python tools/pixelate_logo.py <source> <cutout_out.png> <pixel_out.png> [--block N]
```

It does two jobs — background cutout, then pixelation — and both are non-obvious enough that it's
worth understanding *why* before you change any thresholds:

**Cutout** auto-detects which of two situations it's dealing with by sampling a strip along the top of
the source, then picks the matching test — the caller doesn't need to know in advance which kind of
background a given source has:

- **Checkerboard** (the sampled strip has two distinct luminance tones, ~50+ apart — typically a JPEG
  screenshot of a transparency preview): combines saturation ratio `(max-min channel)/max` below a
  threshold, AND luminance within the checker's own auto-detected range. Neither test alone is enough:
  - A **raw channel spread** test (not saturation ratio) sounds equivalent but isn't — a dark, richly
    saturated outline color (e.g. deep navy) has a *small absolute spread* just because its channel
    values are all small, and gets wrongly caught. The *ratio* is what actually captures "how colorful
    is this," independent of brightness.
  - **Luminance as "close to one of two known tones"** (rather than a continuous range) misses the
    seams between checker squares — JPEG blending smears a thin line of pixels to roughly the
    *midpoint* luminance between the two tones, which is far from both, so a "close to tone A or tone
    B" distance test leaves a visible ghost grid after pixelation. A continuous range catches the
    seams too.
  - The two checker tones are **clustered by luminance**, not by counting the most common exact RGB
    tuples. JPEG noise scatters one true tone across dozens of near-duplicate tuples, so "most common
    tuple" can pick two noisy variants of the *same* tone and never notice the other one exists — this
    was a real bug caught by looking at the actual output, not something to assume can't happen.
  - If the source has a soft glow/bloom around it, expect a **residual faint fringe** near the edges no
    matter what — the glow got flattened into "checker tinted by translucent color" with no real alpha
    info left to recover, and a plain per-pixel test isn't real alpha matting. Loosening the saturation
    ceiling further trades this fringe against eating into genuinely colorful thin details (sparkle
    rays, thin strokes). If the fringe actually matters for a specific use, the honest fix is asking
    the user to re-export a real PNG with true alpha, not chasing the threshold further.
- **Solid backdrop** (the sampled strip is all one color — a PNG saved with its own preview background
  flattened in rather than a checkerboard): plain RGB color-distance from the sampled mean, a
  conventional single-color chroma key. Don't reach for the saturation test here — a solid backdrop
  found in practice was a strongly saturated dark purple, not a neutral grey, so a "must be
  desaturated" rule would refuse to key it out at all. Color distance alone works because there's only
  one background tone to compare against, and it separated cleanly from the logo's own dark bevel
  shadows (which differ in *luminance*, not just hue, from a dark-but-saturated backdrop).

Trust the auto-detection but verify it picked the right branch on a source you haven't seen before —
print or inspect `max(lums) - min(lums)` from the sampled strip if the result looks wrong; a source
with an unusually busy/gradient backdrop could confuse the two-tones-vs-one-tone heuristic.

**Pixelate** (game-only; skip for web assets) downscales with Lanczos and upscales back with nearest-
neighbor — same trick `tools/gen_font.py`/`tools/gen_sprites.py` use for hard pixel edges. The one
easy-to-miss detail: **premultiply alpha before the downscale, un-premultiply after**. Pillow resizes
RGBA channels independently, so a fully-transparent pixel's leftover (but invisible) RGB color — still
whatever the original background was — bleeds into neighboring blocks during a normal resize and
reappears as a faint ghost pattern once nearest-upscaled. This is exactly the kind of bug that looks
fine in your head and wrong on screen; if you're ever unsure whether a resize step needs this, render
the result and look rather than reasoning it through.

Both cutout and pixelate finish with cheap cleanup worth keeping: a morphological opening (erode then
dilate by the same small radius, `PIL.ImageFilter.MinFilter`/`MaxFilter`) clears stray noise flecks in
the alpha mask without shrinking real content (letterforms are much thicker than noise), and cropping
to the *alpha channel's own* bounding box (not the whole image's, which is fooled by transparent
pixels' stale RGB) trims wasted canvas.

## Step 2: actually look at the output

Composite the result over a couple of representative backgrounds before calling it done — the game's
dark background and, for the pixelated version, at the roughly-real size it'll render at in the menu.
A quick way that doesn't need Godot running:

```python
from PIL import Image
im = Image.open("path/to/output.png")
bg = Image.new("RGBA", im.size, (11, 6, 20, 255))  # the game's own backdrop colour
Image.alpha_composite(bg, im).convert("RGB").save("/tmp/check.png")
```

Then read that file back as an image and look at it. Don't trust a "no errors" run — the checker-ghost
and color-bleed bugs above both produced clean exit codes and wrong pictures. If something looks off,
form a specific hypothesis about *why* (sample a few suspicious pixels with `Image.getpixel` and
compare their saturation/luminance against known-good and known-bad regions) rather than randomly
nudging constants.

## Step 3: ask before assuming, but only where it matters

- **`--block` (pixelation strength)**: bigger blocks read as "more pixel-art" but can turn fine detail
  (thin strokes, small sparkle/flare shapes) to mush. Default 8 is a reasonable starting guess for a
  logo-sized image; there's no formula that picks this correctly without looking at the specific
  source, so try one value, look at it, adjust.
- **Which output(s) are actually needed**: the pixelated version is for anything rendered inside the
  game's own UI (menus, HUD); the plain cutout (unpixelated) is for the web site and anywhere else
  glossy/photographic art is already the norm. Don't pixelate something bound for `web/` — check
  `docs/visuals.md` if unsure why the game and the web site have different rules.
- **Where the file goes**: game-facing pixel art belongs under `Assets/` (this project already has an
  `Assets/Branding/` folder for hand-authored brand art); after adding a new PNG there, Godot needs to
  actually import it once (open the editor, or `godot --headless --import`) before a scene referencing
  it will work — a `.tscn` can reference the path immediately, but nothing renders until that import
  step has produced the `.godot/imported/...` cache. Web-facing images go under `web/public/`, and
  should be sized/compressed for their actual display size (`Image.resize` + `optimize=True` when
  saving) rather than shipped at full source resolution.

## If the source already has real alpha

Skip the whole checkerboard-detection dance — `Image.open(path).convert("RGBA")` already has correct
transparency, so cutout() would be a no-op (and could even be actively wrong, since a real translucent
edge might have moderate saturation the checker-removal thresholds would misjudge). Go straight to the
pixelation step if the game needs it, or use the file as-is for the web.
