# Assets: where every file comes from

Almost nothing in this game is hand-authored art. Most visuals are C# that draws itself, and the
files that *do* exist are mostly **build output** — produced by a script in `tools/`, which is the
real source of truth.

The one rule that matters: **to change a generated asset, change its recipe and re-run the
generator.** Hand-editing the binary works until the next regeneration silently overwrites it.

## One command

```bash
python tools/build_assets.py              # regenerate everything, report what changed
python tools/build_assets.py --audition   # ...and open a page to hear/see it all at once
python tools/build_assets.py --check      # change nothing; fail if a committed asset is stale
```

Every generator is deterministic — fixed noise seeds, no timestamps — so the normal outcome is
"nothing changed". That's what makes the report useful: when something *does* change, it's because a
recipe changed, and you see which file before you commit it.

`--check` is the CI/pre-commit form: it regenerates into a temp directory and fails if the committed
assets don't match. It catches both "edited the .wav by hand" and "changed the recipe and forgot to
re-run".

Only `prep_character_sprite.py` needs an install (`pip install -r tools/requirements.txt`, for
Pillow). Everything else is stdlib-only, so a fresh clone can rebuild every sound and silhouette with
nothing but Python.

## The inventory

| Asset | Count | Generator | Notes |
|---|---|---|---|
| `Assets/Audio/*.wav` (effects) | 19 | `gen_sfx.py` | Recipes + the `VOLUME` mix table are the sound design |
| `Assets/Sprites/Enemies/*.png`, `ship.png` | 12 | `gen_sprites.py` | One white polygon each, hard-filled at texel size; vertices live in a table |
| `Assets/Sprites/Characters/*.png` | 9 | `prep_character_sprite.py` | **Not** in `build_assets.py` — see below |
| `Assets/Splash/BootSplash.*`, `icon.svg` | 3 | — | Hand-authored, changed once a project |
| `Assets/Fonts/PixelFont.ttf` | 1 | `gen_font.py` | 121 glyphs, drawn as literal squares — see below |

**Why portraits aren't in `build_assets.py`:** `prep_character_sprite.py` turns *a photograph* into a
portrait, and the photographs aren't in the repo. It's a one-shot tool run by hand when a character
is added, not something regenerable from scratch:

```bash
python tools/prep_character_sprite.py foto.jpg Assets/Sprites/Characters/maxi.png --ring 7dfdfe
```

## The font, and why it's generated rather than downloaded

`gen_font.py` builds a TrueType font whose glyphs are literal axis-aligned squares, from the shape
table in `assetlib/glyphs.py`. Two reasons it isn't a downloaded pixel font:

**Fifteen font sizes.** The UI uses 12, 13, 14, 15, 16, 17, 18, 20, 22, 26, 30, 32, 36, 40 and 72.
That rules out a bitmap atlas font, which is crisp at its native size and integer multiples and
resampled-soft at everything else. A TTF of squares imported with `antialiasing=0` has no native
size: the rasteriser fills whole pixels at any scale.

**Coverage.** The UI needs 25 non-ASCII codepoints. Thirteen are ordinary Spanish (`áéíóú ñ ÁÉÍÓÚ ¡¿`)
but nine are symbols almost no free pixel font carries — `─` for the pause menu's section rules, `★`
on reward cards, `◀ ▶` for the pilot carousel, `✓ ✗` in builds and loadout, `→` in requirement rows,
and `· × − —` around the HUD. Owning the glyph table makes coverage true by construction instead of
something you discover as an empty box on screen. `allow_system_fallback=false` in the `.import` keeps
it honest: a gap shows as `.notdef`'s hollow rectangle rather than being quietly papered over with a
smooth system font.

The grid is 5×7 in an em of 8 blocks, chosen so the most-used size lands exactly — at `font_size`
16 one block is exactly 2.0 screen pixels, and 20/24/32/40/72 come out whole too.

## The manual step

Godot assigns every asset a `uid://` in a `.import` sidecar. So the full loop is:

1. `python tools/build_assets.py`
2. **Open Godot once** (or `godot --headless --import`)
3. Commit the asset **and** its `.import` together

Between 1 and 2 the paths don't resolve. `AudioManager` guards every load with `ResourceLoader.Exists`
so the game runs *silently rather than crashing* in that window — that guard is load-bearing.

**A sidecar can be written by hand if you have to**, which is how `PixelFont.ttf.import` got its
import settings (`antialiasing=0`, `subpixel_positioning=0`, `allow_system_fallback=false`) before the
editor had ever seen the file. Two details make it work:

- `path` is `res://.godot/imported/<basename>-<md5 of the res:// path>.<ext>` — plain hex MD5 of the
  path string, verified against an existing `.wav` sidecar.
- `uid://` is base-34: repeatedly `% 34`, digits `0-9` then letters `a-x`, most significant first.

Godot rewrites the file on its next import either way, so a wrong *value* is self-correcting but also
silently reverts to the importer default. If the font ever comes out soft, check `antialiasing` first.

## Structure

```
tools/
  build_assets.py      one entry point; regenerates everything and reports the diff
  requirements.txt     Pillow (portraits) and fontTools (the font); nothing else needs either
  assetlib/
    audio.py           oscillators, envelopes, mixing, WAV writing
    palette.py         the colours that get baked into files
    raster.py          polygon -> hard-edged silhouette PNG
    glyphs.py          the 5x7 shape of every character the game renders
  gen_sfx.py           19 sound effects
  gen_sprites.py       12 entity silhouettes
  gen_font.py          PixelFont.ttf, 121 glyphs drawn as squares
  prep_character_sprite.py   photo -> square framed portrait
```

`assetlib/audio.py` was extracted when a music generator arrived and needed the same oscillators the
sound effects were built from. **That music was later cut for sounding bad** (see
[audio.md](audio.md)), so today only `gen_sfx.py` imports it — the shared module outlived the second
caller that justified it. It's staying: it's the readable home for the synthesis primitives either
way, and anything that generates audio next will want exactly these.

## Two duplications you should know about

**`assetlib/palette.py` duplicates `Scripts/Util/Palette.cs`.** Python can't read C# and there's no
build step that could generate one from the other, so the choice was "duplicate once, obviously" or
"duplicate in every script that needs a colour" — which is what was happening before. Only values
that get *baked into a file* live there. **Change one, change the other.**

**`CustomCharacterStore.MakePortrait()` in C# reimplements `prep_character_sprite.py`'s
`make_portrait()`**, so a portrait a player creates in-game matches a bundled one. These have drifted
once before, when both masked to a circle: the C# side feathered the rim over 1.5px while the Python
side used a 4× supersampled hard mask. The pixel-art pass removed the circle from both — the frame is
now a square border `max(2, round(size * 0.055))` px thick, which is exact on both sides and has no
antialiasing to disagree about. One difference remains by design: the Python side composites over the
backdrop and trims transparent padding first, because it accepts arbitrary cut-outs.

Both sides must still change **in the same commit**. A bundled portrait sitting next to a
player-made one is the only place the mismatch shows, and it shows immediately.

## Animation

**There is nothing to generate.** This project has no animation assets at all — zero
`AnimationPlayer`, zero `AnimatedSprite2D`, zero `SpriteFrames`, no keyframes on disk anywhere. All 41
of its animations are `CreateTween()` calls in C#, most of them going through the shared helpers in
`Scripts/Util/Juice.cs`, and the place to edit one is the C# constant — a duration, an ease, a scale
factor. See [visuals.md](visuals.md) for that vocabulary.

This is a fact about the project rather than a gap in the tooling. If file-based animation ever
arrives, it gets a `gen_*.py` here and a row in the table above.
