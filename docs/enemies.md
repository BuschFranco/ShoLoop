# Enemies

Base behavior lives in [Enemy.cs](../Scripts/Enemy/Enemy.cs) — every enemy type (including specials) is the *same* script with different exported stats and a different scene, not separate classes.

## Common behavior (all enemy types)

- `CharacterBody2D`, group `enemies`, seeks the player every physics frame, steering around obstacles (see below) and pushed apart from any other enemy that's too close (see further below). No full pathfinding — it's reactive steering, not a route plan.
- Collision: `layer = 2` (Enemies), `mask = 8` (Obstacles) — enemies don't physically collide with the player or each other (that would need the physics engine to resolve it; separation is instead a plain script-side velocity nudge, see below), but they *do* collide with arena obstacles. Damage to the player only happens through the player's `Hurtbox` `Area2D` detecting overlap (see [player.md](player.md)); this is why enemies never "shove" the player around.

### Obstacle avoidance

`ComputeAvoidanceDirection` raycasts `AvoidanceProbeDistance` ahead (default 70px; 90 on the Tank, 120 on the Boss, since a wider body needs more warning) against **layer 8 only**. Clear path → drive straight at the player. Blocked → try the detour angles in `AvoidanceAngles` (20°, 40°, … 125°) in order, nearest-to-straight first, and take the first one that isn't blocked — so an enemy deviates as little as it can get away with and re-converges once past the corner.

Two details that matter:

- **The chosen side is committed to** (`_avoidanceSide`, held across frames until the path is clear again). Re-deciding every frame makes enemies jitter left-right in place at a wall instead of actually going around it. The initial side is picked by probing 90° each way and taking whichever is clear, coin-flipping when both are.
- **If every detour on the committed side is blocked** — an inside corner, or wedged between two obstacles — the commitment flips so the next frame explores the other way out, and meanwhile the enemy slides straight along the wall rather than pressing into it.

This layer is why obstacles work at all: relying on `MoveAndSlide` alone wasn't enough, because it only slides when the velocity has a tangential component. An enemy meeting a wall face head-on resolves to ~zero slide and just grinds against it forever. Physics collision is still there underneath as the safety net.

### Collision layer map

| Bit | Layer | Used by | Masks |
|---|---|---|---|
| 1 | Player | `Player.tscn` body | 8 (obstacles) |
| 2 | Enemies | every enemy body | 8 (obstacles) |
| 4 | Player bullets | `Bullet.tscn` | 2 + 8 (enemies, obstacles) |
| 8 | Obstacles | `Obstacle` static bodies | — (static) |

Areas sit on layer 0 and only mask: the player's `Hurtbox` masks 2, `EnemyBullet` masks 1, `OrbitBlade` masks 2.

**Player bullets are stopped by obstacles** — `Bullet.OnBodyEntered` frees the bullet on *any* body its mask lets through, damaging it first only if it's an `Enemy`. This covers Companion drone shots too, since the drone fires the same `Bullet.tscn`.

**Enemy bullets are not** — `EnemyBullet` still masks only layer 1, so it flies through walls. That asymmetry is deliberate for now but worth knowing: obstacles currently cost the player shots without giving them cover. Making it symmetric is a one-line mask change (`1` → `9`) plus the same free-on-any-body tweak in `EnemyBullet.OnBodyEntered`.

Non-projectile damage is unaffected by obstacles either way: the Laser, the level-up nova, and the Pulso Nova ultimate are all plain distance checks with no line-of-sight test, so they still reach through walls.
- On death (`CurrentHp <= 0` in `TakeDamage`): registers a kill with `GameManager.RegisterKill(XpToGive, CoinsReward, Category)`, then `QueueFree()`.
- Exported base stats: `MoveSpeed`, `MaxHp`, `XpReward`, `CoinsReward`, `ContactDamage`, `Category`.

## Enemy categories

Every enemy has an `EnemyCategory` (`Enemy.cs`): `Common`, `Rare`, `Special`, `Hidden`, `Boss`. Score/XP per kill is each enemy's own `XpReward` (round-scaled like everything else — see [difficulty-scaling.md](difficulty-scaling.md)), not a fixed table keyed by category — see [economy.md](economy.md#score-gamemanagerscore) for why Score and XP are synced 1:1. `Category` still matters for spawn-roll weighting (below), for triggering `SpecialEnemiesKilled` stat tracking on anything non-Common, and for ending a boss round on a `Boss` kill.

## Enemy types

| Scene | Role | Category | Base MaxHp | Base ContactDamage | Base MoveSpeed | Base XP / Coins | Visual |
|---|---|---|---|---|---|---|---|
| [EnemyGrunt.tscn](../Scenes/Enemy/EnemyGrunt.tscn) | filler | Common | 30 | 10 | 90 | 1 / 1 | pink triangle |
| [EnemyRare.tscn](../Scenes/Enemy/EnemyRare.tscn) | mid-tier filler | Rare | 50 | 12 | 110 | 3 / 2 | blue diamond |
| [EnemyTank.tscn](../Scenes/Enemy/EnemyTank.tscn) | slow bruiser | Special | 120 | 20 | 70 | 6 / 6 | bigger, orange |
| [EnemySpeedy.tscn](../Scenes/Enemy/EnemySpeedy.tscn) | fast glass cannon | Special | 15 | 14 | 190 | 2 / 3 | small, green |
| [EnemySplitter.tscn](../Scenes/Enemy/EnemySplitter.tscn) | splits on death | Special | 45 | 10 | 100 | 7 / 4 | teal, see below |
| [EnemyShooter.tscn](../Scenes/Enemy/EnemyShooter.tscn) | slow ranged artillery | Special | 120 | 14 | 85 | 6 / 6 | purple pentagon, shoots (see below) |
| [EnemyHidden.tscn](../Scenes/Enemy/EnemyHidden.tscn) | rare and valuable | Hidden | 25 | 16 | 140 | 6 / 6 | pale silver star, flat 5% spawn chance, shoots |
| [EnemyBoss.tscn](../Scenes/Enemy/EnemyBoss.tscn) | boss-round encounter | Boss | 2400 | 35 | 55 | 150 / 40 | large crimson hexagon, see [rounds.md](rounds.md#boss-rounds) |

All of these numbers are the **round-1 baseline**. The spawner multiplies them at spawn time — see [difficulty-scaling.md](difficulty-scaling.md). Boss's `XpReward` was bumped from an earlier 50 to 150 specifically to keep its Score/XP payout feeling like a real spike now that Score is synced to XP instead of the old flat +1000 category value (round 5 ≈ ×2 mult → ~300 Score/XP in one hit).

Grunts are always available. `EnemySpawner.ChooseEnemyScene()` rolls sequentially, rarest first: Hidden (flat 5%), else Special (uniformly among whichever of `TankScene` / `SpeedyScene` / `SplitterScene` are assigned, per the existing round-scaled chance), else Rare (round-scaled chance), else Grunt. These are independent rolls, not one normalized distribution, so Hidden's rate stays exactly 5% regardless of how the other chances are tuned.

## Health bars (Boss/Special only)

`Enemy._Ready()` only builds a health bar (`CreateHealthBar`) when `Category == Special || Category == Boss` — Common/Rare/Hidden enemies die fast enough that a bar would just be noise. Two stacked `Polygon2D` rectangles (dark background + red fill, both plain procedural geometry, no texture) sit above the enemy at `HealthBarOffset` ([Export], tuned per-scene to clear each enemy's own visual radius — e.g. `-18` for the small Speedy, `-52` for the big Boss); `UpdateHealthBar()` resizes the fill's polygon width proportionally to `CurrentHp / MaxHp` on every `TakeDamage` call. A Splitter's children inherit their parent's `HealthBarOffset` via `Split()` (along with `Category`) so they keep a health bar too.

Every hit on a health-bar enemy also spawns a floating red `-10`-style number (`SpawnDamageNumber`, same drift-up-and-fade pattern as the score popup) showing the actual HP lost — clamped to whatever HP the enemy actually had left, so an overkill hit shows the real remaining HP rather than the raw (possibly enormous) damage number.

## Hit reaction

`PlayHitFeedback()` gives every enemy — not just the health-bar ones — a short white flash and a scale punch (1.3× easing back over 0.14s) whenever a hit lands. Three details:

- It's **skipped on the killing blow**, since the enemy is `QueueFree`d on that same frame and there'd be nothing left to animate.
- It tweens the **`Visual` child**, not the enemy node. The enemy's own `Scale` carries the Splitter's per-generation shrink (`SplitVisualScale`) and must not be clobbered.
- Any in-flight tween is `Kill()`ed before starting a new one. Rapid hits (a laser volley, a blade sweep) would otherwise stack tweens fighting over the same two properties and could leave an enemy stuck mid-flash or mid-punch.

The flash restores each enemy's own base colour, captured from the `Visual` node in `_Ready()` rather than assumed, since every enemy scene picks its own.

## Keeping the pack dispersed

Two independent mechanisms, because a herded crowd was collapsing into a single ball *and* trailing the player in one straight column. They fix different halves of that.

### Separation (enemies don't stack)

`ComputeSeparation()` scans the `enemies` group and, for every other enemy within `SeparationRadius` ([Export], default **46px**), pushes this one directly away from that enemy's center — stronger the closer they are (`(minDist - dist) / minDist`, scaled by a flat `SeparationStrength = 170`). This is a plain script-side velocity nudge, not physics-engine collision (enemies only mask obstacles, see above) — cheap, fully predictable, and immune to Godot's kinematic-body collision quirks.

Both constants were roughly doubled from an earlier 24px / 80: 24px is barely more than touching for the larger enemies, and a strength of 80 against move speeds of 90–190 simply lost to the chase force, so a cornered crowd still balled up.

The summed push is `LimitLength(1f)`-clamped **before** scaling. It accumulates one term per crowding neighbour, so deep inside a pack the raw sum gets large enough to fling an enemy across the arena; clamping caps the shove at exactly `SeparationStrength`, comparable to move speed — firm, but never launching.

### Chase-angle offset (enemies don't single-file)

Separation alone doesn't stop the pack travelling as one column, because every enemy computes the *identical* chase vector toward the player. Each enemy therefore gets `_chaseAngleOffset`, a persistent heading offset randomised once in `_Ready()` within ±`MaxChaseAngleOffset` ([Export], default 14°), applied to its chase direction.

It's persistent rather than per-frame so it reads as a different approach *line*, not as jitter. And it's kept small deliberately: a constant angular offset makes the path a slow inward spiral rather than an orbit, so enemies still converge — they just arrive along different arcs. The Boss overrides it to `0` for a direct, unwavering approach (and uses a wider 70px separation radius to match its size).

## Enemy speed multiplier

`Enemy._PhysicsProcess` multiplies its velocity by `GameManager.Instance.EnemySpeedMultiplier` (default `1f`) on top of its own `MoveSpeed`. This is the hook the **Zona Lenta** Ultimate uses (see [rewards.md](rewards.md#ultimates)) — centralizing the multiplier on `GameManager` rather than mutating each enemy's own `MoveSpeed` means it automatically applies to enemies spawned mid-effect too, not just the ones that existed the moment it was triggered.

## Contact damage no longer matters for the player

`ContactDamage` still exists and is still scaled up per round (see [difficulty-scaling.md](difficulty-scaling.md)), but since the lives system replaced HP (see [player.md](player.md)), **every hit costs the player exactly one life (or one shield charge) regardless of `ContactDamage`'s value.** The stat is kept because it's still meaningful lore/data (and cheap to keep scaling), but it's currently vestigial for player-damage purposes — it doesn't make a hit "worse." If you want tougher enemies to actually hurt more, that's the one place the current design doesn't follow through, and would need a deliberate change (e.g. some enemies costing 2 lives per hit).

## Splitting (EnemySplitter)

Split fields on `Enemy.cs`: `CanSplit`, `SplitCount` (children per split, default 2), `SplitGeneration` (0 = original), `MaxSplitGenerations` (default 3), `SplitHpScale` (0.5 — each child has half its parent's HP), `SplitVisualScale` (0.75 — each generation is visually smaller).

On death, if `CanSplit && SplitGeneration < MaxSplitGenerations`, `Enemy.Split()` spawns `SplitCount` children evenly spaced around a single random starting angle (`baseAngle + angleStep * i`, `angleStep = Tau / SplitCount`) at 22px from the death position — evenly spacing them (rather than each child independently rolling its own random angle) guarantees they never land close together by bad luck, so the split visibly reads as separate pieces rather than a pile at the parent's feet. Each child gets:
- `SplitGeneration + 1`
- `MaxHp = parent.MaxHp × SplitHpScale`
- `ContactDamage`/`MoveSpeed` unchanged from parent
- `XpReward`/`CoinsReward` halved
- `Scale` shrunk by `SplitVisualScale`

Generation math: 1 original → splits into 2 (gen 1) → each splits into 2 more (gen 2, 4 total) → each splits again (gen 3, 8 total) → gen-3 children die for good with no further split. Killing an entire lineage end-to-end is 15 kills from one spawn.

**XP only pays out on a kill that doesn't itself trigger another split.** `Enemy.TakeDamage` computes `willSplit` (the same condition that gates `Split()`) and passes `0` XP to `RegisterKill` whenever `willSplit` is true — so killing the original or any gen-1/gen-2 body (which just produces more enemies) grants no XP at all; only the terminal gen-3 kills do. Since Score is synced 1:1 with XP (see [economy.md](economy.md)), Score is withheld the same way — only the terminal kills show a score popup or add to the total. Coins are unaffected by any of this and still pay out on every kill along the way, including the ones that split further.

`SelfScene` (a plain, non-`[Export]` field) is how a splitter knows what to instantiate for its children, **without the scene referencing itself** — `EnemySpawner.SpawnOne()` sets `enemy.SelfScene = scene` right after instantiating any enemy, and `Split()` copies it onto each child so the lineage can keep splitting. This avoids any risk from a `.tscn` file holding a resource reference to its own path.

## Special-enemy roll

`EnemySpawner._specialChance` (a `RoundCurve`, see [difficulty-scaling.md](difficulty-scaling.md)) is rolled independently for every spawn attempt. On success, one of the assigned special scenes is picked uniformly at random; otherwise a plain Grunt spawns (after also failing the Hidden and Rare rolls — see "Enemy categories and score" above).

## Shooting enemies (`ShooterEnemy`)

[`ShooterEnemy.cs`](../Scripts/Enemy/ShooterEnemy.cs) extends `Enemy` — inheriting chase movement, HP, health bar, and death/scoring unchanged — and adds a `Timer` at `1f / FireRate` that instantiates an `EnemyBullet` aimed at wherever the player is *at the moment of firing* (deliberately not predictive, so shots are dodgeable). [`EnemyBullet.cs`](../Scripts/Enemy/EnemyBullet.cs) (`Area2D`, `collision_mask = 1` so it only detects the Player body) travels in a straight line and calls `Player.TakeHit(GlobalPosition)` on contact — the same shield-then-life logic, 2s invulnerability, and knockback a melee hit uses (see [player.md](player.md)).

A shooting variant of anything is therefore just a `.tscn` pointing at this script with `EnemyBulletScene` assigned. Three exist:

| Scene | `FireRate` | Cadence |
|---|---|---|
| `EnemyShooter.tscn` (Special) | 0.4 | ~1 shot / 2.5s |
| `EnemyHidden.tscn` (Hidden) | 0.3 | ~1 shot / 3.3s |
| `EnemyBoss.tscn` (Boss) | 0.25 | ~1 shot / 4s |

The **Shooter** is a deliberately slow artillery piece (`MoveSpeed = 85`) with Tank-equivalent HP — it contrasts with the Tank (fast-closing bruiser), Speedy (rusher), and Splitter. The **Hidden** enemy keeps its fast/fragile profile and just gains the lowest-cadence gun of the three, so its 5% rarity stays a spike in threat rather than a wall.

## The Boss

`Boss.cs` is now just `public partial class Boss : ShooterEnemy { }` — all its old firing logic moved to `ShooterEnemy` when the Hidden/Shooter enemies needed the same behavior. The distinct type is kept so `EnemyBoss.tscn` has a boss-specific script to hang future boss-only mechanics on, and so `is Boss` checks remain possible; its exported names (`EnemyBulletScene`, `FireRate`) are inherited unchanged, so the scene file needed no edits. Its melee contact damage works exactly like any other enemy's, just at a much higher `ContactDamage`, and at 2400 base HP it's roughly 4× as tanky as every other enemy combined. It only ever appears in boss rounds — see [rounds.md](rounds.md#boss-rounds).
