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
| `Assets/Sprites/Enemies/*.svg`, `ship.svg` | 12 | `gen_sprites.py` | One white polygon each; vertices live in a table |
| `Assets/Sprites/Characters/*.png` | 9 | `prep_character_sprite.py` | **Not** in `build_assets.py` — see below |
| `Assets/Splash/BootSplash.*`, `icon.svg` | 3 | — | Hand-authored, changed once a project |
| `Assets/Fonts/RetroFont.tres` | 1 | — | Not a font file: a `SystemFont` naming Consolas/DejaVu |

**Why portraits aren't in `build_assets.py`:** `prep_character_sprite.py` turns *a photograph* into a
portrait, and the photographs aren't in the repo. It's a one-shot tool run by hand when a character
is added, not something regenerable from scratch:

```bash
python tools/prep_character_sprite.py foto.jpg Assets/Sprites/Characters/maxi.png --ring 7dfdfe
```

## The manual step

Godot assigns every asset a `uid://` in a `.import` sidecar, and **only the editor can write it** — a
Python script can't. So the full loop is:

1. `python tools/build_assets.py`
2. **Open Godot once** (or `godot --headless --import`)
3. Commit the asset **and** its `.import` together

Between 1 and 2 the paths don't resolve. `AudioManager` guards every load with `ResourceLoader.Exists`
so the game runs *silently rather than crashing* in that window — that guard is load-bearing.

## Structure

```
tools/
  build_assets.py      one entry point; regenerates everything and reports the diff
  requirements.txt     Pillow, needed only by the portrait tool
  assetlib/
    audio.py           oscillators, envelopes, mixing, WAV writing
    palette.py         the colours that get baked into files
    svg.py             polygon -> silhouette SVG
  gen_sfx.py           19 sound effects
  gen_sprites.py       12 entity silhouettes
  prep_character_sprite.py   photo -> circular portrait
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
`make_portrait()`**, so a portrait a player creates in-game matches a bundled one. These have
**already drifted**: the C# version feathers the rim over 1.5px and lerps the ring into the photo;
the Python version uses a 4× supersampled hard mask and composites over the backdrop, and it trims
transparent padding first while the C# version doesn't. Worth unifying or at least re-checking if
either side is touched.

## Animation

**There is nothing to generate.** This project has no animation assets at all — zero
`AnimationPlayer`, zero `AnimatedSprite2D`, zero `SpriteFrames`, no keyframes on disk anywhere. All 41
of its animations are `CreateTween()` calls in C#, most of them going through the shared helpers in
`Scripts/Util/Juice.cs`, and the place to edit one is the C# constant — a duration, an ease, a scale
factor. See [visuals.md](visuals.md) for that vocabulary.

This is a fact about the project rather than a gap in the tooling. If file-based animation ever
arrives, it gets a `gen_*.py` here and a row in the table above.
