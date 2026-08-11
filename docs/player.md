# Player Mechanics

See [Player.cs](../Scripts/Player/Player.cs). Movement/joystick basics aren't covered here — this is about the systems added on top: auto-fire range, lives, shield, level-up nova, orbit blades, the companion drone, the Laser, and Ultimates.

## Controls

- **Movement**: the virtual joystick, or **WASD** on a keyboard. `GetKeyboardDirection()` (polled in `_PhysicsProcess` via `Input.IsKeyPressed`) is only consulted when the joystick itself reports `Vector2.Zero`, so touch input always wins if both are somehow active — they never fight each other.
- **Inertia**: input sets a *target* velocity (`dir * MoveSpeed`, with `MoveSpeed = 290`), and `_moveVelocity` eases toward it via `MoveToward` rather than being assigned outright — giving a short ramp-up and ramp-down instead of snapping between full speed and a dead stop. `MoveAcceleration = 1800` px/s² (~0.15s to top speed) and `MoveDeceleration = 2400` px/s² (~0.11s to stop); braking is deliberately snappier than launching so the ship reads as responsive rather than floaty. Steering mid-move interpolates through the turn as a side effect, which looks like banking rather than pivoting in place. The knockback impulse from taking a hit is a separate vector added on top, so it isn't smoothed away by this.
- **Ultimate**: the HUD button, or the **R** key. Handled in `Player._UnhandledInput` (an `InputEventKey` check for `Key.R`, `Pressed` and not `Echo`, so it fires exactly once per press) rather than polled every frame — this also means it's automatically ignored while the tree is paused (shop, pause menu, game over), same as any other gameplay input.

## The arena and the camera

`ArenaHalfExtents` (default `1600 × 1000`, i.e. a 3200×2000 playfield) is the single number defining the arena's size — the player is clamped to it in `_PhysicsProcess` rather than being walled in by physics. Two other things derive from it at runtime instead of duplicating the value:

- [`ArenaBounds.cs`](../Scripts/ArenaBounds.cs) — a `Line2D` that draws the edge. Without it the boundary is an invisible barrier you only discover by bumping into it, which was fine when the whole arena fit on one screen and isn't now.
- [`CameraRig.cs`](../Scripts/Util/CameraRig.cs) — sets the `Camera2D`'s `Limit*` properties so the view never pans past the edge into empty space.

The arena is now several screens wide, so the camera has to actually follow. `CameraRig` snaps the camera's node position onto the player every physics frame; the *lag* comes from Godot's built-in `position_smoothing_enabled` / `position_smoothing_speed = 4.0` (set in [Arena.tscn](../Scenes/Arena.tscn)), which eases the rendered position toward the node position — so the player visibly drags the camera along rather than being welded to its center. Using the engine's smoothing instead of a hand-rolled lerp makes it framerate-independent for free. `zoom = 0.85` pulls the view back a little to suit the larger space.

The camera is a **sibling** of the player, not a child, so `Player.tscn` stays reusable without dragging a camera around with it.

## Obstacles

[`Obstacle.cs`](../Scripts/Obstacle.cs) is a `StaticBody2D` that builds its own fill, neon outline, and `RectangleShape2D` collider procedurally from an exported `Size` — same no-art-assets approach as everything else here, and it means an obstacle instance is just a node with a script and a size, with no per-instance sub-resources or separate `.tscn` to keep in sync. Ten of them are placed by hand in `Arena.tscn` under an `Obstacles` node, all well clear of the origin where the player spawns.

They sit on collision layer 8, which the player and every enemy now mask (see the [layer map](enemies.md#collision-layer-map)). Enemies have no pathfinding, so they slide along obstacle edges via `MoveAndSlide` rather than routing around them — which is what makes obstacles useful for kiting.

## Auto-fire range

`FireRange` starts deliberately tight (190px) and gates `OnFireCooldownTimeout`: the player only fires at the nearest enemy if it's within `FireRange` — otherwise the cooldown ticks with no shot, so early on enemies have to actually close the distance before you can hit them. A faint cyan ring (`SpawnFireRangeIndicator`/`RebuildFireRangeIndicator`, a `Line2D` circle parented to the player so it always follows) makes the boundary visible instead of it being an invisible gameplay number. This only affects the player's own gun — the Companion drone still always fires at the nearest enemy regardless of distance.

The **Fire Range** reward (`UpgradeType.FireRange`, replaced the old Speed Boost reward) raises it: same tiered-stacking/hard-cap shape as FireRate/BulletDamage (`_baseFireRange` snapshotted in `_Ready`, `MaxFireRangeBonus = 500`), and `RebuildFireRangeIndicator()` runs again on every pickup so the visible ring always matches the current range. A fully-stacked run can reach 650px total, well past the original static 450px default.

## Lives

- `MaxLives` (default 3), `CurrentLives`. Every enemy contact hit costs **exactly 1 life**, regardless of that enemy's `ContactDamage` value (see the note in [enemies.md](enemies.md#contact-damage-no-longer-matters-for-the-player) — this stat is currently vestigial for the player).
- Hits are rate-limited by `_invulnTimer` (`InvulnDuration = 2f` seconds of invulnerability after any hit that reaches the shield or lives), so sustained contact with an enemy doesn't melt you in one frame. During that window `_visual.Visible` toggles every `BlinkInterval = 0.1s` in `_PhysicsProcess`, giving a blinking "you're currently invulnerable" tell, and turns back fully visible the instant the timer expires.
- Every such hit also gives a small knockback: `TakeHit(Vector2? sourcePosition)` computes `(GlobalPosition - sourcePosition).Normalized() * KnockbackForce` and stores it in `_knockbackVelocity`, which `_PhysicsProcess` adds on top of the normal movement velocity and decays linearly (`MoveToward(Vector2.Zero, KnockbackDecay * delta)`) over a fraction of a second — a quick shove away from whatever hit you (an enemy's own position for melee contact, the bullet's position for the Boss's ranged attack — see `EnemyBullet.cs`), independent of whatever direction the joystick/WASD is currently asking for.
- `CurrentLives <= 0` → `GameManager.NotifyPlayerDied()` → Game Over screen.
- Fully refilled to `MaxLives` at the **start of every round** (`Player.HealFullLives()`, called from `GameManager.StartNextRound()`) — see [rounds.md](rounds.md).
- The Heart reward raises `MaxLives` to the best tier ever picked and tops up `CurrentLives` by that pickup's own value (capped at the new max) — it does **not** simply sum every tier ever picked, and it does **not** force a full heal on pickup. See [rewards.md](rewards.md#max-tracks-best-tier--additive-refill-heart-barrier-hitshield) for the exact rule and why (this was a real bug — weaker tiers picked after stronger ones used to both inflate the max further *and* fully heal, as if they were just as good).

## Shield (Barrier reward)

- `MaxShieldCharges`, `CurrentShieldCharges`. A charge, if any are available, is consumed **before** a life is lost on a hit (`Player.OnHurtboxBodyEntered`) — shield always takes priority over lives.
- Does **not** regenerate on its own during a round — only another Barrier pickup adds charges mid-round (up to the tier-tracked max — same rule as Heart above, see [rewards.md](rewards.md#max-tracks-best-tier--additive-refill-heart-barrier-hitshield)). It **is** topped back up to 100% at the start of every round though (`Player.RefillShield()`, called from `GameManager.StartNextRound()` right next to `HealFullLives()`), so shield follows the same "every round starts fresh" rule lives already did. No-ops for a player who's never bought a Barrier.
- HUD shows a "Shield: X/Y" row, hidden entirely while `MaxShieldCharges == 0` so it doesn't clutter the display for players who haven't picked one up. The blue aura around the player (`ShieldAura` in [Player.tscn](../Scenes/Player.tscn)) is visible exactly while `CurrentShieldCharges > 0`.

## Level-up nova

- Every level-up (`GameManager.OnLevelGained`, once per XP threshold crossed) calls `Player.TriggerLevelUpBurst()`, which **no-ops entirely during round 1** (`GameManager.Instance.RoundNumber == 1`) — leveling up that early happens before there's any real threat on screen yet, so it shouldn't hand out a free screen-clear. The nova starts working from round 2 onward. Otherwise:
  1. Instantly kills (`enemy.TakeDamage(99999)`) every non-Boss enemy within `LevelUpBurstRadius` (default 220px) of the player. This reuses each enemy's normal death path, so kills still register XP/coins, and a Splitter caught in the blast still splits normally. **Bosses are explicitly excluded** (`enemy.Category != EnemyCategory.Boss`) — without this, leveling up while a boss happened to be in range deleted it in one hit regardless of the actual fight, which defeated the entire point of a boss round (see [rounds.md](rounds.md#boss-rounds)).
  2. Spawns a purely cosmetic expanding/fading ring (`Polygon2D` + `CreateTween`) at the burst radius, then frees itself — no gameplay effect, just visual feedback for the nova.
- This happens *before* the tree pauses for the upgrade picker, so the burst plays out live.
- The nova's own kills grant XP too, which can chain into *more* level-ups within the same `AddXp` call (a big single reward, or several kills at once, crossing more than one threshold) — see below for how those get queued rather than lost.

## Multiple level-ups at once

`GameManager.AddXp` uses a `while` loop (not a single `if`), so one large XP reward that crosses several thresholds correctly increments `Level` once per threshold, not just once — this used to be a real bug: killing a high-value enemy (or the level-up nova itself chaining into more kills) could jump several levels but only ever show one upgrade picker, silently losing the other picks.

Each threshold crossed increments `GameManager._pendingLevelUps` instead of immediately opening the picker again. `ShowNextLevelUpIfIdle()` only opens the picker if it isn't already showing one — so a 4-level jump still opens exactly one picker at first, but `UpgradePicker.OnChoicePressed` already hides itself before handing control back to `GameManager.ApplyUpgradeAndResume()`, which decrements `_pendingLevelUps` and, if more are owed, immediately reopens the picker with a fresh set of 3 choices (still paused) instead of resuming. Only once `_pendingLevelUps` reaches 0 does the game actually unpause. Net effect: a 4-level jump means picking 4 separate upgrades in a row, one screen at a time — none of them skipped.

## Orbit Blades (Orbit Blade reward)

- `OrbitCount` is the target number of blades (0–4, tier-determined — see [rewards.md](rewards.md)). `Player.RefreshOrbitBlades()` instantiates [`OrbitBlade.tscn`](../Scenes/Player/OrbitBlade.tscn) instances as children of the player until `_orbitBlades.Count == OrbitCount`, never removing any.
- Each blade ([`OrbitShield.cs`](../Scripts/Player/OrbitShield.cs)) is an independent `Area2D` that orbits at its own `AngleOffset` (evenly spaced across however many blades exist *at the moment it's created* — adding a 4th blade later doesn't reshuffle the existing 3, it just adds one more starting at its own slot). Each blade damages any `Enemy` it overlaps.
- `HasOrbitShield => OrbitCount > 0` is the public flag other systems (like the shop's "Owned" check) read.

## Companion (Drone reward)

- `CompanionStatPercent` (0–0.5, i.e. 0–50%) is the fraction of the player's *current* `FireRate`/`BulletDamage` the companion fires at. Buying a better tier raises this (never lowers it — `Max`).
- Only **one** companion ever exists (`Player._companion`); the first Companion-type pickup instantiates [`Companion.tscn`](../Scenes/Player/Companion.tscn) as a child of the player at a fixed local offset `(-36, -36)` (so it just follows the player automatically, no tracking logic needed) and gives it a live reference (`Companion.Owner = this`).
- [`Companion.cs`](../Scripts/Player/Companion.cs) re-reads `Owner.FireRate` / `Owner.BulletDamage` on every shot (not a frozen snapshot), so the companion's output scales up automatically as the player's own stats improve later in the run. It targets the nearest enemy exactly like the player does, independently.

## Side Shot

- `ExtraFiringLines` (0–5, capped) adds extra bullets fired **parallel** to the main shot every time the player fires, spaced out sideways — a different shape from Twin Shot's fixed ±25° diagonal pair. Alternates sides as it stacks (line 1 right, line 2 left, line 3 further right, ...), via `baseDir.Rotated(Mathf.Pi / 2f)` as the perpendicular offset axis, `SideShotSpacing = 14px` between lines.
- `FireInDirection(dir, positionOffset)` grew an optional spawn-position offset to support this — the extra lines still travel in the exact same direction as the main shot, they just start from a different point next to the player.
- Additive across tiers (Rare +1, Legendary +2), hard-capped at 5 — see [rewards.md](rewards.md#additive-with-a-hard-cap-side-shot).

## Laser

- `LaserLevel` (0–4, tier-determined, take-the-best-ever like Orbit Blade — see [rewards.md](rewards.md#take-the-best-ever-orbit-blade-drone-companion-laser)) drives a `Timer` (`_laserTimer`, lazily created by `EnsureLaserTimer` on first pickup) that periodically zaps enemies within `FireRange` — reusing the same range the auto-fire gun uses, and the same visible ring.
- `LaserTiers` is a `(Interval, Damage, MaxTargets)` lookup array. Only **tier 1** caps how many enemies a single zap can hit (5) — higher tiers hit everything in range uncapped. `OnLaserTimeout` gathers every in-range enemy, and when the tier is capped, sorts by distance and keeps only the closest `MaxTargets` before applying damage — so a capped zap always prioritizes the nearest threats, not an arbitrary subset.
- Deals finite damage (`Enemy.TakeDamage`) to everything it hits, **including a Boss** — unlike the level-up nova, this isn't an instant-kill, so it doesn't need a Boss exclusion.
- Each hit draws a brief fading magenta `Line2D` beam from the player to the enemy (`SpawnLaserBeam`), visually distinct from the nova's expanding ring.

## Ultimates

- `EquippedUltimate` (nullable `UltimateKind`), `UltimateProgress`/`UltimateChargeTarget` (250), and `TriggerUltimate()` — full mechanics documented in [rewards.md](rewards.md#ultimates) since it's really a reward-system concern (shop-only acquisition, swap rules, Score-driven charging). The HUD's Ultimate button/bar (`HUD.cs`) is hidden until one is equipped and disabled until the bar is full.

## Stacking

Speed/fire-rate/damage stacking rules (same-tier-only bucketing, each with its own hard cap), the flat-additive-with-a-cap Side Shot, and the max-tracks-best-tier-plus-refill Heart/Barrier rule all live in [rewards.md](rewards.md#stacking-rules) since they're really a reward-system concern, not a player-mechanic one — `Player.ApplyTieredStack()` is the tier-bucketing implementation, but the *rules* (and all the cap constants) are documented there.
