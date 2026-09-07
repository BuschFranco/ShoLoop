"""The game's colours, for the tools that bake colour into a file.

This is a deliberate, documented duplicate of Scripts/Util/Palette.cs. Python can't read the C# and
there is no build step that could generate one from the other, so the honest options were "duplicate
it in one obvious place" or "duplicate it in every script that needs a colour" -- which is what was
happening before, with prep_character_sprite.py carrying its own private copy of the backdrop.

**If you change a colour here, change it in Palette.cs too.** Only the values that actually get baked
into a generated file live here; anything drawn at runtime reads Palette.cs and must not be added.
"""

# Palette.Backdrop -- the deep purple-black behind everything. Baked into character portraits so a
# transparent pixel inside the disc shows this rather than the arena.
BACKDROP_RGBA = (11, 6, 20, 255)

# Palette.ObstacleOutline -- the default neon ring around a character portrait.
RING_DEFAULT = "ff4fd8"

# The ring colours cycled for player-made characters, mirroring CustomCharacterStore.RingColors so a
# portrait made in-game and one made with prep_character_sprite.py look like the same kind of object.
RING_CYCLE = (
    "ff4fd8",  # ObstacleOutline  (magenta)
    "7dfdfe",  # Accent           (cyan)
    "3dff8f",  # PlayerBullet     (green)
    "ffe066",  # CritBullet       (gold)
    "c65bff",  # OndaBlast        (violet)
    "ff2e88",  # HeartPickup      (hot pink)
)
