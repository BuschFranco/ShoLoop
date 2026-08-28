# Characters

See [CharacterCatalog.cs](../Scripts/Player/CharacterCatalog.cs). Picked before a run at
[CharacterSelectMenu](../Scenes/UI/CharacterSelectMenu.tscn), applied in exactly one place:
`Player._Ready` reads `GameManager.SelectedCharacter`, looks it up, and applies everything before any
other field is snapshotted.

**The current cast is a joke roster** — portraits of the author's coworkers with gag perks, expected
to be swapped, renamed and retuned often. This document is written for that: the sections below are
ordered by what you're most likely to be here to do.

---

## The cast

| Slug | Name | Perk | Field |
|---|---|---|---|
| `equilibrado` | Equilibrado | — | (the baseline) |
| `centella` | Centella | +15% speed, −10% bullet damage | `MoveSpeedMultiplier` / `BulletDamageMultiplier` |
| `coloso` | Coloso | −10% speed, +20% bullet damage | `MoveSpeedMultiplier` / `BulletDamageMultiplier` |
| `maxi` | Maxi | 2% to hack an enemy for 1s | `EnemyHackChance` |
| `conrado` | Conrado | −50% speed | `MoveSpeedMultiplier` |
| `manu` | Manu | +15% coin drop chance | `CoinDropBonus` |
| `juan` | Juan | — | — |
| `nico_l` | Nico L | 1% for an enemy to die on approach | `EnemySuicideChance` |
| `juli` | Juli | Enemies turn green, −10% HP | `EnemyTint` / `EnemyHpMultiplier` |
| `secreto1`/`2`/`3` | Piloto Secreto I/II/III | — (cosmetic, real perk TBD) | locked — see below |

Plus however many the player has created — see [Custom characters](#custom-characters).

Only the first three vary the ship's own stats. The portrait characters default to 1.0 on everything,
so a portrait with no perk arguments is mechanically identical to Equilibrado.

## Editing a character

Everything about a built-in lives in one entry in `CharacterCatalog.BuiltIns`. Every portrait — the six
amigos plus the three locked "Secreto" slots — goes through the `Portrait(...)` helper, which holds
the defaults so that a dozen near-identical initialisers don't become a dozen places for a multiplier
to get typo'd into being non-cosmetic.

**Rename, or change the text:** edit the strings in that entry. `Description` is the flavour
paragraph; `PerkText` is the one-sentence mechanical claim. They render in two separate boxes, and
each box hides itself when its string is empty — that's how Equilibrado shows only a description and
Centella only a perk.

**Swap the photo:**

```bash
python tools/prep_character_sprite.py foto.jpg Assets/Sprites/Characters/maxi.png --ring 7dfdfe
```

That centre-crops to a square, resizes to 144×144, masks to a circle with a feathered rim, and draws
a neon ring in `--ring`. Any input size or aspect works. Nothing else has to change — the filename is
derived from the slug.

**Add a character:** add a `Portrait(...)` line, drop the PNG in with a matching filename, done. The
select screen builds its list from `CharacterCatalog.All`, so it picks the new one up automatically
(it used to keep its own duplicate list, which meant a new character existed but was unreachable).

**Remove a character:** delete the entry and the PNG. A saved selection pointing at it resolves back
to `BuiltIns[0]` via `Get()`, so nobody ends up stuck on a character that no longer exists.

## Locked characters & Libras

Three slots (`secreto1`/`2`/`3`) are gated behind [Libras](economy.md#libras--gamemanagerlibras),
the game's persistent, cross-run currency — the first thing it's spendable on. The gate is two fields
on `CharacterInfo`, both defaulting to "unlocked" so nothing existing is affected by adding a new one:

```csharp
public bool RequiresUnlock { get; init; }   // false by default — every existing entry is unaffected
public int UnlockCost { get; init; }
```

**`CharacterCatalog` only knows whether a slug *can* be locked, never whether the player has actually
unlocked it** — that's player save state, and it lives on `GameManager` (`UnlockedCharacters`, a
`HashSet<string>`), not in the static catalog. `CharacterCatalog.IsUnlocked(info)` is the one place
that combines the two — always go through it rather than checking `RequiresUnlock` alone.

**Locking a new character:** pass `requiresUnlock: true, unlockCost: N` to `Portrait(...)`. Every
existing call omits both, so nothing already in the cast becomes locked by accident — this is
opt-in per entry, not a default that had to be overridden six times.

**In the carousel** (`CharacterSelectMenu`), a locked character's portrait dims to 55% alpha (the same
"disabled" convention `RewardCard` uses) but its name/description/perk still show in full — seeing
what you're saving up for is the point. `ConfirmButton` and `UnlockButton` are mutually exclusive for
whichever character is framed; pressing Unlock spends via `GameManager.TryUnlockCharacter` and
refreshes in place, without closing the carousel or auto-selecting the newly-unlocked pilot.

**The three `secreto` slots are deliberately unfinished** — cosmetic-only placeholders with a "?"
portrait, meant to be filled in exactly like the six amigos originally were: swap the PNG with
`tools/prep_character_sprite.py`, then add a real `Description`/`PerkText` to the catalog entry.
Nothing about the unlock plumbing needs to change when that happens.

## Per-pilot leaderboard & level

Each pilot slug gets its own top-10 score list, kept in `user://records.cfg` alongside the overall
leaderboard but under its own ConfigFile section (`records_<slug>`, see
`GameManager.LoadCharacterRecords`/`AppendCharacterRecord`) — answering "how am I doing with THIS
pilot" separately from the main menu's "what's my best run ever". `RegisterFinalScore` appends to both
on every run, keyed by whichever pilot `GameManager.SelectedCharacter` names — same slug either way, so
custom characters need no special-casing.

`CharacterSelectMenu` shows the top 3 (`RefreshRecords`, `MaxRecordsShown`) inside each pilot's own box
in the carousel — a fixed-height `RecordsBox` (46px, `clip_contents = true`), same pattern as the
Reward card fix for text that shouldn't be able to grow its container. `MaxDescriptionHeight` was
trimmed from 190 to 144 to make room for it inside the panel's fixed landscape budget.

This is a third per-pilot number alongside `GetCharacterLevel`(see below) — level tracks permanent
XP-style progression, the records list tracks actual runs. Both are keyed by slug and shown together
in the carousel, but they're unrelated: leveling up doesn't touch the leaderboard, and a great run
doesn't grant XP beyond the Libras it already earns (see [economy.md](economy.md)).

## Perks: how they reach the game

Every perk follows the same path, and the shape matters more than any individual perk:

```
CharacterInfo field  →  Player._Ready re-exposes it as a public field  →  Enemy / EnemySpawner
                                                                          read it off the player node
```

`Enemy` and `EnemySpawner` never reference `CharacterCatalog`. They read the value off the player they
already hold. That's what keeps "what this character does to a run" applied in one place.

| Field | Read by | What it does |
|---|---|---|
| `MoveSpeedMultiplier`, `BulletDamageMultiplier` | `Player._Ready` | multiplies the ship's own stats |
| `CoinDropBonus` | `Enemy.TryDropPickup` | added to the coin pickup's drop chance |
| `EnemyHpMultiplier` | `EnemySpawner.SpawnOne` | scales every spawned enemy's `MaxHp` |
| `EnemyTint` | `Enemy._Ready` | overrides every enemy's identity colour |
| `EnemyHackChance` | `Enemy.TickProximityPerks` → `Enemy.ApplyHack` | freezes the enemy for 1s |
| `EnemySuicideChance` | `Enemy.TickProximityPerks` | kills the enemy via `TakeDamage` |
| `Color` | `Player._Ready`, HUD, carousel | tints the sprite (see [visuals.md](visuals.md)) |

### Adding a new kind of perk

1. Add the field to `CharacterInfo`.
2. Add an optional parameter to `Portrait(...)` and assign it.
3. Re-expose it as a public field on `Player`, assigned in `_Ready` alongside the others.
4. Read it from wherever it applies, off the player node.

**Give it a sane zero.** `CharacterInfo` is a `readonly struct`, so a field an entry doesn't set
arrives as `0` / `null`, not as the default you had in mind. A multiplier is the dangerous case:
`EnemyHpMultiplier` unset would mean "every enemy has 0 HP", which is why `Player._Ready` guards it
with `> 0f ? value : 1f`. Chances and bonuses are safe, because 0 already means "off".

## Perk gotchas worth knowing before you retune

- **`CoinDropBonus` is absolute probability, not a multiplier**, and that's the scale that makes the
  number honest: from round 5 on, a coin pickup is worth the same `CoinsReward` the kill already pays
  out directly, so taking a 2% drop chance to 17% raises total coin income by roughly that same 15%. A
  relative +15% would have moved 2% to 2.3% and been invisible. It's added, not multiplied, so it's
  worth the same in every round instead of being swallowed by the rounds 1–2 drop boost.

- **The proximity perks roll once per enemy**, the first time it comes within `EffectiveFireRange` —
  not on a schedule. A repeating roll would scale with how long enemies loiter *and* with how many are
  alive: at a late-round cap of ~50, "2% every half second" fires several times a second. One roll per
  enemy ties the rate to enemies *spawned*, which is what the percentage reads as.

  The cost of that choice is that these perks are **subtle**: expect a handful of triggers per
  60-second round, and they fire out at the edge of fire range rather than next to you. If a perk
  needs to be felt, raising the chance is the knob — and `PerkText` has to be updated to match.

- **Juli's tint is applied before the colour snapshot** in `Enemy._Ready`. That ordering is load
  bearing: the hit flash and the boss/stalker dash telegraphs all restore `VisualBaseColor`, so a tint
  applied *after* the snapshot would be reverted by the first hit the enemy took.

- **Bosses are exempt from the proximity perks, for two reasons.** `Boss` overrides `_PhysicsProcess`
  without calling base, so it never runs the roll — a 1% chance to instantly delete a boss would undo
  the whole encounter. But the same override means a boss would never tick a hack timer down either,
  so a hacked boss would be frozen and unable to fire for the rest of the fight. `Enemy.ApplyHack`
  refuses bosses outright, and the timer is decremented in `FinishMovement` (the seam every subclass
  including `Boss` funnels through) rather than in `_PhysicsProcess`, so neither guard is the only
  thing standing between a future change and an unwinnable encounter.

- **A slower character does not become easier to catch.** `Enemy.FinishMovement` caps enemy chase
  speed at 90% of the player's *current* `MoveSpeed`, so halving the player halves the cap with it and
  the relative gap is unchanged. Conrado's −50% lands entirely on the things that *don't* scale with
  the player: enemy bullets and missiles (fixed speed), crossing the arena, and the timed round
  events. Pinning the cap to a fixed baseline instead would make enemies literally uncatchable-by-you
  at half speed, which is the exact bug the cap exists to prevent — see [enemies.md](enemies.md).

- **A hacked enemy is still shoveable.** `FinishMovement` is called with a zero move direction rather
  than skipped, so separation and knockback still apply: a frozen enemy is a harmless obstacle that
  the Vendaval can still push, not a rigid pillar the crowd stacks on. It also keeps `MoveAndSlide()`
  running, without which it would stop resolving collisions with obstacles.

- **`ComputeMoveDirection` is still called while hacked**, and its result thrown away. `DemonStalker`
  decrements its lunge timers *inside* that method, so skipping the call would freeze them and it
  would finish the remainder of a lunge in a stale direction when the hack ended — a visible
  teleport-slide. The price is one telegraphed lunge that never happens.

## Identity and persistence

A character is identified by a **string slug**, not an enum, because player-created characters have no
compile-time identity to enumerate. For portrait characters the slug doubles as the sprite's filename,
so there's one name to keep straight instead of two.

`GameManager` persists it as `selected_character_slug` in `user://settings.cfg`. The selection used to
be an index into the built-in list, so `LoadSettings` falls back to the legacy `selected_character`
integer when the slug key is absent — an existing install keeps whoever it had picked instead of
silently resetting.

## Custom characters

[CharacterCreator](../Scenes/UI/CharacterCreator.tscn) lets the player add their own: pick an image,
type a name and a description, and [CustomCharacterStore](../Scripts/Player/CustomCharacterStore.cs)
writes two things under `user://` — local only, never bundled, never uploaded:

| Path | Contents |
|---|---|
| `user://characters.cfg` | one section per character (slug `custom_N`) with `name` and `description` |
| `user://characters/<slug>.png` | the processed 144×144 portrait |

- **They are always perk-free, and the multipliers are hardcoded on load** rather than read from the
  file. `characters.cfg` is plain text the player can edit, so reading stats out of it would be
  shipping a cheat menu. `PerkText` is never set either, so their perk box stays hidden.
- **The portrait is processed on save, not on load** — centre-crop, resize, then a feathered circular
  mask and ring, mirroring `tools/prep_character_sprite.py` so a bundled portrait and a player-made
  one are the same kind of asset. Doing it per load would redo that work on every carousel step.
- **`user://` textures can't be `GD.Load`ed** — nothing there goes through the import pipeline. They
  have to be decoded into an `ImageTexture` at runtime, which is why every consumer (the ship, the HUD
  portrait, the carousel) goes through `CharacterCatalog.Texture(info)` rather than loading
  `SpritePath` itself. That method caches, since all three ask for the same texture.
- **Slugs reuse the lowest free number**, so a character created after a deletion lands in the middle
  of the list rather than at the end — which is why `Create` returns the new slug instead of letting
  the caller assume it's the last entry.

## The select screen's two boxes

`Description` and `PerkText` render in separate boxes so the mechanical claim isn't buried at the end
of a paragraph of flavour. Two constraints shaped that layout:

- **The panel is a fixed 600px wide** (set on `Panel`, not `Box` — `CenterContainer` sizes to the
  combined minimum, so putting it on `Box` would add the stylebox margins on top and give 636 in a
  648-wide viewport). At 564 usable px the longest description wraps to 7–8 lines, so it fits without
  scrolling.
- **The description keeps its `ScrollContainer` anyway**, as a ceiling rather than a scrollbar. Two
  cases would otherwise strand the buttons off-screen with no way to close the menu: the game in
  **Landscape**, where the logical viewport is only 648 *tall* and this panel nearly fills it; and a
  custom character whose description is 600 characters of pressed Enter, which the creator's
  multi-line `TextEdit` happily accepts.
