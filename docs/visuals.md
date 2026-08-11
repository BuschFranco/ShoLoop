# Visuals: Palette, Glow & Animation

The game has no art assets — every visual is a `Polygon2D`, `Line2D`, or `Label` built either in a
`.tscn` or procedurally in code. The look comes entirely from colour, glow, and tweens.

## Where colour lives (two places, on purpose)

| Source | Covers | File |
|---|---|---|
| `Palette.cs` | everything drawn procedurally — rings, beams, blasts, floating labels, obstacles, arena edge, health bars | [Scripts/Util/Palette.cs](../Scripts/Util/Palette.cs) |
| `.tscn` `color =` | visuals authored as scene nodes — ship, enemy bodies, projectile bodies, UI dims | the individual scene files |

They're **not** unified, and that's deliberate: `Enemy` reads its own `Visual.Color` back in `_Ready`
as `_visualBaseColor`, the restore target for the [hit flash](enemies.md#hit-reaction). Moving enemy
colours into `Palette` would give the same value two owners and let them silently drift apart. The
two sides are kept in step by hand.

### The palette

Pink/magenta primary, cyan for the player, violet/purple for variety, deep purple-black backdrops.

| Role | Colour | Notes |
|---|---|---|
| Player ship | `#7dfdfe` electric cyan | The one cold colour. The player must stay findable in a pink crowd — a pink ship wouldn't be. |
| Player bullets | `#ff4fd8` hot pink | |
| Crit bullets | `#ffe066` gold | Deliberately *outside* the pink family so a crit reads as different. |
| Orbit blades | `#f9c2ff` pink lilac | |
| Laser | `#ff2fb9` magenta | |
| Missile / blast | `#ff8ae2` / `#ff6ec7` | |
| Grunt | `#ff2e88` hot pink | |
| Rare | `#b14aed` violet | |
| Tank | `#ff5c8a` coral pink | |
| Speedy | `#ff71ce` bright pink | |
| Splitter | `#c77dff` lavender | |
| Shooter | `#e040fb` purple magenta | |
| Hidden | `#fff0f7` near-white | Reads as the rare/valuable one. |
| Boss | `#a60057` deep crimson magenta | |
| Enemy bullets | `#ff3860` red pink | |
| Obstacles | `#1a0f2b` fill, `#ff4fd8` outline | |
| Burn tint | `#ff8a5c` | Applied via `Modulate`, see [enemies.md](enemies.md#burn-incendiario). |

Enemy hues stay **varied within the neon family** rather than all going pink. Telling a Tank from a
Speedy at a glance is gameplay information, and shape alone isn't enough to carry it.

**Reward tier colours are exempt** ([RewardTierRoller.cs](../Scripts/Upgrades/RewardTierRoller.cs)).
They were brightened into the neon range but keep four clearly distinct hues (mint / sky / violet /
gold), because tier is information the player reads while deciding what to buy — four shades of pink
would cost more in legibility than it gains in cohesion.

## Glow (the part that makes it "neon")

`Arena.tscn` holds a `WorldEnvironment` whose `Environment` enables glow. Without it the palette is
just pink; with it, bright pixels bleed light and the whole thing reads as neon signage.

Two settings matter and are easy to get wrong:

- **`background_mode = 3` (Canvas).** Glow in a 2D game requires the canvas background mode. The
  default (`Clear Color`) produces no bloom at all.
- **`glow_hdr_threshold = 0.85`.** Only pixels brighter than this bloom, which is why `Palette.cs`
  keeps most channels at or near `1.0`. Dim colours simply won't glow.

Glow is supported by Godot's **Mobile** renderer, which this project uses
(`renderer/rendering_method = "mobile"`) — but it is not free. If framerate suffers on device, the
cheapest wins are lowering `glow_strength`/`glow_bloom`, or disabling `adjustment_enabled`.

## Animation

All of it is `Tween`-driven; there are no `AnimationPlayer`s.

| Effect | Where | Notes |
|---|---|---|
| Enemy spawn pop-in | `Enemy.PlaySpawnAnimation` | Scale 0→1 with a `Back` ease plus a small unwind rotation, so enemies arrive rather than blink in. |
| Enemy hit flash | `Enemy.PlayHitFeedback` | White flash + scale punch. See [enemies.md](enemies.md#hit-reaction). |
| Player idle breathe | `Player.StartIdleAnimations` | Looping ±6% scale, so the ship is never perfectly static. |
| Range-ring drift | `Player.StartIdleAnimations` | 24s full rotation — a static circle reads as UI, a drifting one as a field. |
| Level-up nova / ultimates | `Player` | Expanding, fading rings. |
| Missile blast | `Missile.SpawnBlastVisual` | Parented to the Bullets container, since the missile frees itself the same frame. |
| Score / damage popups | `Enemy` | Drift up and fade. |
| Boss banner | `BossBanner` | Hold then fade. |

**Animations that touch a `Visual` child never touch the parent node's own `Scale`.** On enemies that
property carries the Splitter's per-generation shrink (`SplitVisualScale`); on the player it would
fight the arena clamp. Every scale tween in the project targets the child for that reason.
