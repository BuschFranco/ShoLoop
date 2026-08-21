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

Every enemy has an `EnemyCategory` (`Enemy.cs`): `Common`, `Rare`, `Special`, `Hidden`, `Boss`, `Demon`. Score/XP per kill is each enemy's own `XpReward` (round-scaled like everything else — see [difficulty-scaling.md](difficulty-scaling.md)), not a fixed table keyed by category — see [economy.md](economy.md#score-gamemanagerscore) for why Score and XP are synced 1:1. `Category` still matters for spawn-roll weighting (below), for triggering `SpecialEnemiesKilled` stat tracking on anything non-Common, and for ending a boss round on a `Boss` kill.

## Enemy types

| Scene | Role | Category | Base MaxHp | Base ContactDamage | Base MoveSpeed | Base XP / Coins | Visual |
|---|---|---|---|---|---|---|---|
| [EnemyGrunt.tscn](../Scenes/Enemy/EnemyGrunt.tscn) | filler | Common | 30 | 10 | 90 | 1 / 1 | pink triangle |
| [EnemyRare.tscn](../Scenes/Enemy/EnemyRare.tscn) | mid-tier filler | Rare | 50 | 12 | 110 | 3 / 2 | blue diamond |
| [EnemyTank.tscn](../Scenes/Enemy/EnemyTank.tscn) | slow bruiser | Special | 170 | 26 | 70 | 6 / 6 | bigger, orange |
| [EnemySpeedy.tscn](../Scenes/Enemy/EnemySpeedy.tscn) | fast glass cannon | Special | 24 | 18 | 205 | 2 / 3 | small, green |
| [EnemySplitter.tscn](../Scenes/Enemy/EnemySplitter.tscn) | splits on death | Special | 65 | 14 | 100 | 7 / 4 | teal, see below |
| [EnemyShooter.tscn](../Scenes/Enemy/EnemyShooter.tscn) | slow ranged artillery | Special | 160 | 18 | 85 | 6 / 6 | purple pentagon, shoots (see below) |
| [EnemyHidden.tscn](../Scenes/Enemy/EnemyHidden.tscn) | rare and valuable | Hidden | 25 | 16 | 140 | 6 / 6 | pale silver star, flat 5% spawn chance, shoots |
| [EnemyBoss.tscn](../Scenes/Enemy/EnemyBoss.tscn) | boss-round encounter | Boss | 2400 | 35 | 55 | 600 / 1500 | large crimson hexagon, see [rounds.md](rounds.md#boss-rounds) |
| [EnemyDemon.tscn](../Scenes/Enemy/EnemyDemon.tscn) | late-game core | Demon | 200 | 30 | 150 | 9 / 8 | jagged crimson star |
| [EnemyDemonBrute.tscn](../Scenes/Enemy/EnemyDemonBrute.tscn) | late-game heavy | Demon | 400 | 45 | 95 | 14 / 12 | large dark-crimson star |
| [EnemyDemonStalker.tscn](../Scenes/Enemy/EnemyDemonStalker.tscn) | late-game ambusher | Demon | 130 | 28 | 210 | 10 / 9 | sharp pink shard, spawns ahead of you and lunges |

All of these numbers are the **round-1 baseline**. The spawner multiplies them at spawn time — see [difficulty-scaling.md](difficulty-scaling.md). The Boss's payout is deliberately the largest single number in the game: at the round multiplier it lands around 1,440 XP / 3,600 coins on the first boss round and climbs to ~7,200 / 18,000 once the curve caps. It has to be worth a whole boss fight, and it's the one kill the round-end economy is built around.

`EnemySpawner.ChooseEnemyScene()` rolls sequentially, rarest first: Hidden (flat 5%), else Special (per the round-scaled chance, picked from the weighted pool below), else Rare (round-scaled chance), else Grunt. These are independent rolls, not one normalized distribution, so Hidden's rate stays exactly 5% regardless of how the other chances are tuned.

Grunts are always available **through round 17 only** — from round 18 on `CommonSuppressionCurve` promotes every would-be Grunt to a Special instead, so the Common tier stops appearing entirely. See [difficulty-scaling.md](difficulty-scaling.md#late-game-composition-shift-and-crowd-taper-rounds-1018) for that ramp and the crowd taper it's paired with.

### Special enemy weights

The Special branch is **weighted**, not a uniform pick among the four assigned scenes:

| Scene | Weight | Share of the Special branch |
|---|---|---|
| `TankScene` | 40 | 40% |
| `SpeedyScene` | 25 | 25% |
| `ShooterScene` | 20 | 20% |
| `SplitterScene` | 15 | 15% |

It used to be uniform (25% each). Weighting became necessary once `CommonSuppressionCurve` pushed Specials from ~57% to ~82% of every spawn: at uniform odds that made Shooter ~20% of the *entire field*, and a screen full of ranged artillery reads as far more overwhelming than the same number of melee bodies. Splitter got the lowest weight for a different reason — each one becomes 15 bodies across its generations (see [Splitting](#splitting-enemysplitter)), re-inflating exactly the crowd count the paired taper is bringing down. Tank absorbs most of the shifted share: at 120 HP it's what keeps threat-per-body high while total count falls.

`ChooseSpecialScene()` skips any unassigned scene when summing weights, so removing a scene from `Arena.tscn` redistributes its share proportionally rather than breaking the roll.

## Health bars (Special only; Boss has its own fixed HUD bar — see below)

`Enemy._Ready()` only builds a health bar (`CreateHealthBar`) when `Category == Special` — Common/Rare/Hidden enemies die fast enough that a bar would just be noise, and Boss gets a fixed bar on the HUD instead (see "Fixed boss health bar" below) rather than also carrying this floating one, so the two don't end up looking like duplicates of each other. Two stacked `Polygon2D` rectangles (dark background + red fill, both plain procedural geometry, no texture) sit above the enemy at `HealthBarOffset` ([Export], tuned per-scene to clear each enemy's own visual radius — e.g. `-18` for the small Speedy, `-52` for the big Boss); `UpdateHealthBar()` resizes the fill's polygon width proportionally to `CurrentHp / MaxHp` on every `TakeDamage` call. A Splitter's children inherit their parent's `HealthBarOffset` via `Split()` (along with `Category`) so they keep a health bar too.

Every hit on **every** enemy — not just the ones with a health bar — spawns a floating `-10`-style number (`SpawnDamageNumber`, same drift-up-and-fade pattern as the score popup) showing the actual HP lost, clamped to whatever HP the enemy actually had left so an overkill hit shows the real remaining HP rather than the raw (possibly enormous) damage number. This used to be gated on the enemy having a health bar (i.e. Special/Boss only), which quietly meant a Common/Rare kill gave no per-hit feedback at all — worth calling out since it also used to get mistaken for a completely different popup, see the colour note below.

**Colour is deliberately outside the pink family**: `Palette.DamageNumber` is a hot orange (`#ff7a1a`), and `Palette.ScorePopup` (the `+N` that shows on a kill) was moved onto `Player`'s own cyan. They used to both be bright pinks (`ff6b8a` vs `ffa8f0`) — different hues on paper, but once the arena's bloom washes out saturation, a `-5` over an enemy and a `+5` reward popup read as the same kind of event, and specifically as *the player* losing something rather than the enemy. See [visuals.md](visuals.md) for the rest of the palette.

## World pickups (Heart / Shield / XP / Coin drops)

`Enemy.TryDropPickup()` rolls two independent chances on **every** kill, split generations included (same rule Coins already follows) — see [`HeartPickup.cs`](../Scripts/Pickup/HeartPickup.cs) / [`ShieldPickup.cs`](../Scripts/Pickup/ShieldPickup.cs):

- **1% (`HeartDropChance`)**: spawns a `HeartPickup` — walking into it calls `Player.AddLife(1)`, healing 1 life capped at `MaxLives`. Distinct from the Corazón Legendario reward, which raises `MaxLives` itself; this is a mid-run top-up only.
- **1% (`ShieldDropChance`)**: spawns a `ShieldPickup`, but only rolled at all when `Player.MaxShieldCharges > 0` (i.e. the player has actually bought a Barrier) — otherwise it'd drop into the world with nothing to do. Walking into it calls `Player.AddShieldCharge()`, the same method the passive Regeneración timer uses.
- **From round 11 on (`LateDropRound`), both chances drop to 0.15% (`LateDropChance`)** — by then the player has usually stacked enough Barrier/lives sources that the early rate would flood the field with pickups.
- Both scenes are loaded once via `GD.Load<PackedScene>` (static fields on `Enemy`) rather than `[Export]`, so every enemy scene picks them up automatically without hand-wiring the reference into all eight enemy `.tscn` files.

Two more drops roll on the same per-kill pass, both normally worth whatever that specific enemy's own reward was:

- **2% (`XpPickupDropChance`)**: an `XpPickup` worth the enemy's `XpReward`, granting XP *and* Score (Score stays synced 1:1 with XP everywhere else, so a picked-up gem follows the same rule).
- **2% (`CoinPickupDropChance`)**: a `CoinPickup` worth the enemy's `CoinsReward`.
- **Rounds 1–2 boost both drop chances to 7% (`EarlyXpCoinDropChance`, gated by `EarlyDropRound = 3`).** Per-kill payout is at its floor there — `RewardMultCurve` is still ~1× — so at the flat rate the player reached the first shop with almost nothing to spend. This mirrors the late-round taper above, in the opposite direction. Heart/Shield are unaffected.
- **Separately, rounds 1–4 give every Coin gem a flat value of 7 (`EarlyCoinPickupValue`, gated by `EarlyCoinValueRound = 5`)** instead of `CoinsReward`, which is still ~1 that early even with the chance boost above — a wider window than the drop-chance boost, and a value change rather than a chance change. Only Coin gems get this; XP gems still scale off `XpReward` throughout.

All four pulse gently, magnetise toward the player inside `MagnetRadius`, and auto-expire after 10s (fading out over the last second) if never collected. They share `PickupBase` and the `pickups` group. **Unlike enemies and their bullets, they are *not* cleared at round end** — see [rounds.md](rounds.md) for why, and for the `RefreshLifetime()` call that gives a carried-over drop its full window back.

## Burn (Incendiario)

Damage-over-time applied by the player's bullets (see [player.md](player.md#burn-incendiario) for the reward side). `ApplyBurn(dps, duration)` sets the state; `TickBurn(delta)` runs it from `_PhysicsProcess`.

- **Refreshes rather than stacks** — a second hit takes `Max` of both the DPS and the remaining duration. Stacking separate instances would make sustained fire scale burn quadratically with fire rate, dwarfing every other damage source almost immediately.
- Ticks every `BurnTickInterval` (0.25s), dealing `dps × accumulated` so the total is correct regardless of framerate.
- Damage goes through `TakeDamage(damage, showFlash: false)` — only the flash+scale-punch is suppressed, not the floating damage number. At 4 ticks/second the flash would strobe rather than read as a hit landing, but the number is exactly what tells you Incendiario is doing anything, so it stays on. (`TakeDamage`'s two flags, `showFlash` and `showNumber`, used to be a single `showFeedback` bool; burn passing that as `false` silently meant burn ticks showed no number at all, which read as burn not working.) Everything else calls the 3-arg form's defaults and keeps both.
- Tinted on the **enemy root's `Modulate`, not the `Visual`'s** — the [hit reaction](#hit-reaction) below owns the `Visual`'s `Modulate` (and the `Visual`'s value is the enemy's identity colour besides), so the two would fight over one property. Tinting the parent multiplies on top of the child, so a burning enemy still flashes white when shot.
- `TickBurn` is called **before** movement in `_PhysicsProcess`, with an early `return` if it killed the enemy, since `TakeDamage` can `QueueFree` from inside it.

## Hit reaction

`PlayHitFeedback()` gives every enemy — not just the health-bar ones — a short white flash and a scale punch (1.3× easing back over 0.14s) whenever a hit lands. Three details:

- It's **skipped on the killing blow**, since the enemy is `QueueFree`d on that same frame and there'd be nothing left to animate.
- It tweens the **`Visual` child**, not the enemy node. The enemy's own `Scale` carries the Splitter's per-generation shrink (`SplitVisualScale`) and must not be clobbered.
- Any in-flight tween is `Kill()`ed before starting a new one. Rapid hits (a laser volley, a blade sweep) would otherwise stack tweens fighting over the same two properties and could leave an enemy stuck mid-flash or mid-punch.

The flash restores each enemy's own base colour **and its base scale**, both captured from the `Visual` node in `_Ready()` rather than assumed, since every enemy scene picks its own colour and the `Visual`'s `scale` is what carries its on-screen size (see [visuals.md](visuals.md)). Assuming `Colors.White` and `Vector2.One` here is precisely what broke rendering in `e0ee940`; the snapshots are exposed to subclasses as `VisualBaseColor`/`VisualBaseScale` so the boss and stalker dash telegraphs restore the same values.

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

`EnemySpawner._specialChance` (a `RoundCurve`, see [difficulty-scaling.md](difficulty-scaling.md)) is rolled independently for every spawn attempt. On success, `ChooseSpecialScene()` picks from the [weighted special pool](#special-enemy-weights); otherwise a plain Grunt spawns (after also failing the Hidden and Rare rolls, and — from round 10 on — the common-suppression roll that promotes Grunts to Specials, see "Enemy categories and score" above).

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

`Boss : ShooterEnemy` keeps its ranged firing exactly as inherited (`EnemyBulletScene`/`FireRate` untouched, still ticking on its own `Timer` regardless of movement pattern below). Its melee contact damage works exactly like any other enemy's, just at a much higher `ContactDamage`, and at 2400 base HP it's roughly 4× as tanky as every other enemy combined. It only ever appears in boss rounds — see [rounds.md](rounds.md#boss-rounds).

### Movement patterns (rolled once per boss, per run)

`Boss._Ready()` rolls one `Pattern` — `Chase`, `Charge`, or `Lunge` — via its own `Random`, once, and commits to it for the boss's whole life. This is what makes two different boss rounds feel like different encounters instead of the same straight-line chaser every time:

- **Chase** — the plain baseline: straight pursuit with obstacle avoidance, unchanged from every other enemy. Kept as a real possible outcome (not just a fallback) so Charge/Lunge read as *a* pattern rather than *the* pattern.
- **Charge** — a telegraphed dash from range. `Idle` (normal chase, 1.5s) → `Windup` (freezes in place, tints its `Visual.Modulate` to `Palette.Warning` and pulses its scale for 0.6s — the tell) → `Execute` (locks onto the player's position at windup-end and lunges at 5× `MoveSpeed` for 0.5s, bypassing obstacle avoidance since a committed dash doesn't politely detour) → `Cooldown` (1.5s, back to plain chase) → loops. If the straight line ahead is blocked at windup-end, the dash is skipped entirely (drops straight to Cooldown) rather than clipping through a wall.
- **Lunge** — a short, sharp burst that only fires once the boss has actually closed to near-melee range, rather than on a fixed delay like Charge. `Idle` isn't timer-driven for this pattern at all: every physics frame checks the live distance to the player, and the instant it drops inside `LungeTriggerRadius` (120px) the boss freezes into a 0.3s `Windup` telegraph, then bursts at 4× `MoveSpeed` for 0.3s in `Execute`, then sits in a 1.8s `Cooldown` before Idle starts checking distance again. It's the "catch up and land the hit" reaction a slow boss needs once it's already close, distinct from Charge's long-range opener. Replaced an earlier `Orbit` pattern (circle the player at a tangent) that turned out unreliable in practice.

Both patterns fall back to the exact same `ComputeAvoidanceDirection(ChaseDirection())` call Chase uses during their own idle/cooldown sub-phases, so a boss spends a good chunk of its time looking like ordinary Chase regardless of which pattern it rolled. Knockback immunity (`ApplyKnockback` no-ops for `Category == Boss`, see above) is untouched by any of this.

This required a small refactor in `Enemy.cs` to give `Boss` a seam to override movement from: `ComputeAvoidanceDirection`, `IsPathBlocked`, `TickBurn`, and the `Visual`/`PlayerNode`/`EnsurePlayer` accessors went from `private` to `protected`, and the tail of `_PhysicsProcess` (knockback decay + final velocity assignment + `MoveAndSlide()`) was extracted into `protected void FinishMovement(Vector2 moveDir, double delta)`. Every other enemy type still goes through the exact same code path via the now-3-line `Enemy._PhysicsProcess`, unchanged.

A **teleport/blink** pattern was considered and deliberately left out: without an in-flight dodge window it doesn't fit this game's "deliberately dodgeable" telegraph philosophy, and it would need extra safety work (arena-bounds clamp + obstacle-overlap check on the landing spot) that Charge/Lunge don't. Worth adding later as a third `Phase`-driven case once a use for it comes up.

## Fixed boss health bar (HUD)

The Boss no longer carries the floating in-world health bar Special enemies get (see below) — it gets its own fixed bar pinned to the top-center of the HUD instead (`HUD.tscn`'s `BossHealthBar`), styled in gold (`Palette.BossHealthBarFill`) rather than the floating bar's magenta, and much thicker (22px vs. 4px) so it reads as a dedicated instrument, not a bigger version of the same sliver. `EnemySpawner.SpawnOne` adds the spawned Boss to a `"boss"` group; `HUD._Process` polls `GetTree().GetFirstNodeInGroup("boss")` every frame the same way it already polls for `_topBarPanel` sizing, shows the bar and syncs it to `CurrentHp`/`MaxHp` while a live boss reference exists, and hides it the instant that reference goes invalid (i.e. the boss died) — no HP-changed or death signal needed.
