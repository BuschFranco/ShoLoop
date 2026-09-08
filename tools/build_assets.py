"""Regenerate every generated asset, and report what actually changed.

    python tools/build_assets.py              # regenerate everything
    python tools/build_assets.py --audition   # ...and write a page to review it all
    python tools/build_assets.py --check      # change nothing; fail if anything is stale

WHY A REPORT AND NOT JUST "DONE"
--------------------------------
Every generator here is deterministic: same inputs, byte-identical outputs. So the normal result of
running this is *nothing changed*, and that is exactly what makes the report worth having -- when a
file does change, it's because a recipe changed, and you want to see which one before committing.

`--check` is the same idea for CI or a pre-commit hook: it regenerates into a temp directory and
fails if the committed assets don't match, catching "edited the .wav by hand" and "changed the recipe
and forgot to regenerate" alike.

WHAT IS *NOT* HERE
------------------
Animation. This project has no animation assets to generate -- no AnimationPlayer, no SpriteFrames,
no keyframes on disk anywhere. All 41 of its animations are `CreateTween()` calls in C#, and the
place to edit one is the C# constant, not a file. If file-based animation ever arrives, it gets a
gen_*.py here and a row in the table below; the absence is a fact about the project, not an omission.
"""

import argparse
import hashlib
import os
import shutil
import subprocess
import sys
import tempfile

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(TOOLS_DIR)

# Each generator, and the assets it owns. The `out_flag` is how to redirect it elsewhere, which
# --check needs; a generator without one can't be checked in isolation and is regenerated in place.
GENERATORS = [
    {
        "script": "gen_sfx.py",
        "label": "sound effects",
        "outputs": ["Assets/Audio"],
        "pattern": ".wav",
        # Still excludes music_*, even though there is no music generator right now: the playback
        # plumbing is intact, so a real music track dropped into Assets/Audio must not be mistaken
        # for a sound effect this script owns and reported as unexpectedly appearing.
        "exclude_prefix": "music_",
        "out_flag": "--out",
    },
    {
        "script": "gen_sprites.py",
        "label": "entity silhouettes",
        "outputs": ["Assets/Sprites/Enemies", "Assets/Sprites/Characters"],
        "pattern": ".png",
        # An explicit list, not just the extension. The silhouettes became PNGs in the pixel-art pass
        # (see assetlib/raster.py), and ship.png shares Assets/Sprites/Characters with the nine
        # portraits -- which are also .png but belong to prep_character_sprite.py. Without this the
        # check would report every portrait as an asset gen_sprites.py failed to produce.
        "names": [
            "boss.png", "demon.png", "demon_brute.png", "demon_stalker.png", "grunt.png",
            "hidden.png", "rare.png", "shooter.png", "ship.png", "speedy.png", "splitter.png",
            "tank.png",
        ],
        "out_flag": "--out",
    },
    {
        "script": "gen_font.py",
        "label": "pixel font",
        "outputs": ["Assets/Fonts"],
        "pattern": ".ttf",
        "out_flag": "--out",
    },
]

# prep_character_sprite.py is deliberately absent: it takes a photo nobody has in the repo and turns
# it into one named portrait. It's a one-shot tool run by hand when a character is added, not
# something that can be re-run from scratch. docs/assets.md says so where a reader will look.


def file_hashes(spec):
    """Hash every asset a generator owns, keyed by path relative to the repo root."""
    found = {}
    for folder in spec["outputs"]:
        abs_folder = os.path.join(REPO_ROOT, folder)
        if not os.path.isdir(abs_folder):
            continue
        for name in sorted(os.listdir(abs_folder)):
            if not name.endswith(spec["pattern"]):
                continue
            if "include_prefix" in spec and not name.startswith(spec["include_prefix"]):
                continue
            if "exclude_prefix" in spec and name.startswith(spec["exclude_prefix"]):
                continue
            if "names" in spec and name not in spec["names"]:
                continue
            path = os.path.join(abs_folder, name)
            with open(path, "rb") as f:
                found[f"{folder}/{name}"] = hashlib.md5(f.read()).hexdigest()
    return found


def run(script, extra_args=()):
    result = subprocess.run(
        [sys.executable, os.path.join(TOOLS_DIR, script), *extra_args],
        cwd=REPO_ROOT, capture_output=True, text=True,
    )
    if result.returncode != 0:
        sys.stderr.write(result.stdout + result.stderr)
        raise SystemExit(f"{script} failed")
    return result.stdout


def build(audition):
    changed_total = 0
    for spec in GENERATORS:
        before = file_hashes(spec)
        run(spec["script"])
        after = file_hashes(spec)

        added = sorted(set(after) - set(before))
        removed = sorted(set(before) - set(after))
        modified = sorted(p for p in set(before) & set(after) if before[p] != after[p])
        changed_total += len(added) + len(removed) + len(modified)

        print(f"\n{spec['label']:22} {len(after)} file(s)")
        for path in added:
            print(f"  + {path}")
        for path in modified:
            print(f"  ~ {path}")
        for path in removed:
            print(f"  - {path}")
        if not (added or modified or removed):
            print("  (unchanged)")

    print()
    if changed_total:
        print(f"{changed_total} file(s) changed.")
        print("Open Godot once so it writes the .import sidecars, then commit assets and .import "
              "files together.")
    else:
        # ASCII only in console output: the Windows console defaults to cp1252 and turns an em-dash
        # into a replacement character, which looks like the script is broken.
        print("Nothing changed - every generated asset already matches its recipe.")

    if audition:
        write_audition()

    return changed_total


def check():
    """Regenerate into a temp dir and compare, without touching the working tree."""
    stale = []
    for spec in GENERATORS:
        if "out_flag" not in spec:
            continue
        committed = file_hashes(spec)
        with tempfile.TemporaryDirectory() as tmp:
            run(spec["script"], (spec["out_flag"], tmp))
            for rel, digest in committed.items():
                name = os.path.basename(rel)
                candidate = os.path.join(tmp, name)
                if not os.path.exists(candidate):
                    continue
                with open(candidate, "rb") as f:
                    if hashlib.md5(f.read()).hexdigest() != digest:
                        stale.append(rel)

    if stale:
        print("These committed assets do not match what their generator produces:")
        for path in sorted(stale):
            print(f"  ~ {path}")
        print("\nRun: python tools/build_assets.py")
        return 1

    print("All generated assets match their recipes.")
    return 0


AUDITION_DIR = os.path.join(TOOLS_DIR, "audition")


def write_audition():
    """A single page that plays every sound and shows every silhouette.

    This is the closest thing to an 'editor' that makes sense here: the value isn't in nudging a
    waveform with a mouse, it's in hearing all 21 audio assets next to each other and noticing that
    one is twice as loud as the rest before it ships.
    """
    os.makedirs(AUDITION_DIR, exist_ok=True)

    audio_dir = os.path.join(REPO_ROOT, "Assets/Audio")
    sounds, music = [], []
    if os.path.isdir(audio_dir):
        for name in sorted(os.listdir(audio_dir)):
            if not name.endswith(".wav"):
                continue
            shutil.copy2(os.path.join(audio_dir, name), os.path.join(AUDITION_DIR, name))
            (music if name.startswith("music_") else sounds).append(name)

    svgs = []
    for folder in ("Assets/Sprites/Enemies", "Assets/Sprites/Characters"):
        abs_folder = os.path.join(REPO_ROOT, folder)
        if not os.path.isdir(abs_folder):
            continue
        for name in sorted(os.listdir(abs_folder)):
            if name.endswith(".svg"):
                shutil.copy2(os.path.join(abs_folder, name), os.path.join(AUDITION_DIR, name))
                svgs.append(name)

    def audio_rows(names):
        return "\n".join(
            f'    <div class="row"><span>{n[:-4]}</span>'
            f'<audio controls preload="none" src="{n}"></audio></div>'
            for n in names
        )

    # The silhouettes are white on white, so they get the game's own backdrop behind them.
    sprite_cells = "\n".join(
        f'    <figure><img src="{n}" alt="{n}"><figcaption>{n[:-4]}</figcaption></figure>'
        for n in svgs
    )

    html = f"""<!doctype html>
<meta charset="utf-8">
<title>Infinitix — asset audition</title>
<style>
  body {{ background:#0b0614; color:#cfd8e8; font:14px/1.5 Consolas,monospace; margin:0; padding:24px; }}
  h1 {{ color:#7dfdfe; font-size:20px; }}
  h2 {{ color:#ff4fd8; font-size:16px; margin-top:32px; }}
  .row {{ display:flex; align-items:center; gap:12px; padding:3px 0; }}
  .row span {{ width:170px; color:#9aa8ba; }}
  audio {{ height:32px; }}
  .sheet {{ display:flex; flex-wrap:wrap; gap:16px; }}
  figure {{ margin:0; text-align:center; }}
  figure img {{ width:96px; height:96px; background:#1a0f2b; border:1px solid #6b3fa0; }}
  figcaption {{ color:#9aa8ba; font-size:12px; margin-top:4px; }}
  p.note {{ color:#9aa8ba; }}
</style>
<h1>Infinitix — asset audition</h1>
<p class="note">Generated by <code>tools/build_assets.py --audition</code>. Not committed; regenerate
any time. Listen for one sound noticeably louder than its neighbours — that's the mix bug this page
exists to catch.</p>

<h2>Sound effects ({len(sounds)})</h2>
{audio_rows(sounds)}

<h2>Music ({len(music)})</h2>
{audio_rows(music)}

<h2>Silhouettes ({len(svgs)})</h2>
<div class="sheet">
{sprite_cells}
</div>
"""
    index = os.path.join(AUDITION_DIR, "index.html")
    with open(index, "w", encoding="utf-8", newline="") as f:
        f.write(html)
    print(f"\nAudition page: {os.path.relpath(index, REPO_ROOT)}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--audition", action="store_true",
                    help="also write tools/audition/index.html to review everything")
    ap.add_argument("--check", action="store_true",
                    help="change nothing; exit non-zero if any committed asset is stale")
    args = ap.parse_args()

    if args.check:
        raise SystemExit(check())

    build(args.audition)


if __name__ == "__main__":
    main()
