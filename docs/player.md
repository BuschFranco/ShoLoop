# Player Mechanics

See [Player.cs](../Scripts/Player/Player.cs). Movement/joystick basics aren't covered here — this is about the systems added on top: auto-fire range, lives, shield, level-up nova, orbit blades, the companion drone, the Laser, and Ultimates.

## Controls

- **Movement**: the virtual joystick, or **WASD** on a keyboard. `GetKeyboardDirection()` (polled in `_PhysicsProcess` via `Input.IsKeyPressed`) is only consulted when the joystick itself reports `Vector2.Zero`, so touch input always wins if both are somehow active — they never fight each other.
- **Inertia**: input sets a *target* velocity (`dir * MoveSpeed`, with `MoveSpeed = 290`), and `_moveVelocity` eases toward it via `MoveToward` rather than being assigned outright — giving a short ramp-up and ramp-down instead of snapping between full speed and a dead stop. `MoveAcceleration = 1800` px/s² (~0.15s to top speed) and `MoveDeceleration = 2400` px/s² (~0.11s to stop); braking is deliberately snappier than launching so the ship reads as responsive rather than floaty. The knockback impulse from taking a hit is a separate vector added on top, so it isn't smoothed away by this.
- **Facing**: `UpdateFacing()` turns the `Visual` triangle (polygon tip at `(18, 0)`, i.e. pointing +X — right — at `Rotation = 0`) to face the direction the player is actually moving, rather than staying rigidly pointed right regardless of input. It prefers raw input direction (immediate intent) and falls back to `_moveVelocity` while coasting to a stop with no input held, so the ship still faces forward as it decelerates instead of snapping to whatever the residual coast direction is. Turning is an exponential catch-up (`FacingTurnRate = 16`) rather than an instant snap — a sharp direction change reads as the triangle banking through the turn instead of teleporting to face the new heading, echoing the translation inertia above without being locked to the same rate. Holds its last facing entirely when both input and velocity are ~zero (`_moveVelocity.LengthSquared() <= 25`) — a stationary ship has no "forward" to reset to.
- **Ultimate**: the HUD button, or the **R** key. Handled in `Player._UnhandledInput` (an `InputEventKey` check for `Key.R`, `Pressed` and not `Echo`, so it fires exactly once per press) rather than polled every frame — this also means it's automatically ignored while the tree is paused (shop, pause menu, game over), same as any other gameplay input.

## The arena and the camera

`ArenaHalfExtents` (default `1600 × 1000`, i.e. a 3200×2000 playfield) is the single number defining the arena's size — the player is clamped to it in `_PhysicsProcess` rather than being walled in by physics. Two other things derive from it at runtime instead of duplicating the value:

- [`ArenaBounds.cs`](../Scripts/ArenaBounds.cs) — a `Line2D` that draws the edge. Without it the boundary is an invisible barrier you only discover by bumping into it, which was fine when the whole arena fit on one screen and isn't now.
- [`CameraRig.cs`](../Scripts/Util/CameraRig.cs) does **not** use the `Camera2D`'s `Limit*` properties, on purpose — clamping the camera to the arena bounds means the player drifts toward the edge of the screen (and, on mobile, under the player's own thumb on the touch controls) the instant they get near a wall, which defeats the point of "the camera follows you". Panning past the edge just reveals the dark backdrop, which is screen-anchored on its own `CanvasLayer` (see [visuals.md](visuals.md#dark-backdrop)) and already covers the viewport regardless of where the camera points.

The arena is now several screens wide, so the camera has to actually follow. `CameraRig` snaps the camera's node position onto the player every physics frame; the *lag* comes from Godot's built-in `position_smoothing_enabled` / `position_smoothing_speed = 4.0` (set in [Arena.tscn](../Scenes/Arena.tscn)), which eases the rendered position toward the node position — so the player visibly drags the camera along rather than being welded to its center. Using the engine's smoothing instead of a hand-rolled lerp makes it framerate-independent for free. `zoom = 0.85` pulls the view back a little to suit the larger space.

The camera is a **sibling** of the player, not a child, so `Player.tscn` stays reusable without dragging a camera around with it.

## Obstacles

[`Obstacle.cs`](../Scripts/Obstacle.cs) is a `StaticBody2D` that builds its own fill, neon outline, and `RectangleShape2D` collider procedurally from an exported `Size` — same no-art-assets approach as everything else here, and it means an obstacle instance is just a node with a script and a size, with no per-instance sub-resources or separate `.tscn` to keep in sync. Ten of them are placed by hand in `Arena.tscn` under an `Obstacles` node, all well clear of the origin where the player spawns.

They sit on collision layer 8, which the player and every enemy now mask (see the [layer map](enemies.md#collision-layer-map)). Enemies have no pathfinding, so they slide along obstacle edges via `MoveAndSlide` rather than routing around them — which is what makes obstacles useful for kiting.

## Auto-fire range

`FireRange` starts deliberately tight (220px) and gates `OnFireCooldownTimeout`: the player only fires at the nearest enemy if it's within `FireRange` — otherwise the cooldown ticks with no shot, so early on enemies have to actually close the distance before you can hit them. A faint cyan ring (`SpawnFireRangeIndicator`/`RebuildFireRangeIndicator`, a `Line2D` circle parented to the player so it always follows) makes the boundary visible instead of it being an invisible gameplay number. This only affects the player's own gun — the Companion drone still always fires at the nearest enemy regardless of distance.

The **Fire Range** reward (`UpgradeType.FireRange`, replaced the old Speed Boost reward) raises it: same tiered-stacking shape as FireRate/BulletDamage (`_baseFireRange` snapshotted in `_Ready`, `MaxFireRangeBonus = 500`), and `RebuildFireRangeIndicator()` runs again on every pickup so the visible ring always matches the current range.

Two ceilings apply, and the second is the one that actually binds: `MaxFireRangeBonus = 500` caps the stacked *bonus*, while **`MaxFireRange = 600` is an absolute ceiling on the final value**. Before the absolute cap existed the only limit was "base + bonus cap" — an emergent side effect rather than a stated ceiling. It matters because a range that outgrows the visible screen stops being a meaningful stat: you can't see what you're shooting at. The shop disables further Fire Range offers ("Al tope") once `FireRange >= MaxFireRange`.

## Lives

- `MaxLives` (default 3), `CurrentLives`. Every enemy contact hit costs **exactly 1 life**, regardless of that enemy's `ContactDamage` value (see the note in [enemies.md](enemies.md#contact-damage-no-longer-matters-for-the-player) — this stat is currently vestigial for the player).
- Hits are rate-limited by `_invulnTimer` (`InvulnDuration = 2f` seconds of invulnerability after any hit that reaches the shield or lives), so sustained contact with an enemy doesn't melt you in one frame. During that window something toggles every `BlinkInterval = 0.1s` in `_PhysicsProcess` — **which node blinks depends on what the hit actually cost**: `TakeHit` sets `_blinkShieldInstead` based on whether the hit was absorbed by a shield charge (`CurrentShieldCharges > 0`) or reached a life. A shield-absorbed hit blinks `_shieldAura` instead of `_visual`, so the tell matches what was actually spent — the ship itself only blinks when a life was really at risk. Either way the blinked node snaps back to its correct steady-state visibility (`_visual.Visible = true`, `_shieldAura.Visible = CurrentShieldCharges > 0`) the instant the timer expires.
- Every such hit also gives a small knockback: `TakeHit(Vector2? sourcePosition)` computes `(GlobalPosition - sourcePosition).Normalized() * KnockbackForce` and stores it in `_knockbackVelocity`, which `_PhysicsProcess` adds on top of the normal movement velocity and decays linearly (`MoveToward(Vector2.Zero, KnockbackDecay * delta)`) over a fraction of a second — a quick shove away from whatever hit you (an enemy's own position for melee contact, the bullet's position for the Boss's ranged attack — see `EnemyBullet.cs`), independent of whatever direction the joystick/WASD is currently asking for.
- `CurrentLives <= 0` → `GameManager.NotifyPlayerDied()` → Game Over screen.
- Fully refilled to `MaxLives` at the **start of every round** (`Player.HealFullLives()`, called from `GameManager.StartNextRound()`) — see [rounds.md](rounds.md).
- The Heart reward (a single Legendary, shop-only) adds `+1` to `MaxLives` up to `MaxLivesCap = 10` and fully heals you. See [rewards.md](rewards.md#additive--full-heal-heart) — it used to exist at all four tiers under a more convoluted "max tracks best tier" rule, which stopped making sense once there was only one tier.
- `Player.AddLife(int amount = 1)` heals without touching `MaxLives`, capped at whatever `MaxLives` currently is — a separate, smaller top-up used by the Heart *pickup* enemies can drop (see [enemies.md](enemies.md#world-pickups-heart--shield-drops)), not the Legendary reward above.
- HUD shows up to 3 individual heart icons (`HeartIcon.cs`, filled = a life you have, dim outline = a life you've lost) instead of "Vidas: x/y" text — falls back to the text once `MaxLives > 3`, since past that a row of tiny hearts stops reading cleanly.

## Shield (Barrier reward)

- `MaxShieldCharges`, `CurrentShieldCharges`. A charge, if any are available, is consumed **before** a life is lost on a hit (`Player.OnHurtboxBodyEntered`) — shield always takes priority over lives.
- Does **not** regenerate on its own during a round — only another Barrier pickup adds charges mid-round (up to the tier-tracked max — same rule as Heart above, see [rewards.md](rewards.md#max-tracks-best-tier--additive-refill-heart-barrier-hitshield)). It **is** topped back up to 100% at the start of every round though (`Player.RefillShield()`, called from `GameManager.StartNextRound()` right next to `HealFullLives()`), so shield follows the same "every round starts fresh" rule lives already did. No-ops for a player who's never bought a Barrier.
- HUD shows a "Shield: X/Y" row, hidden entirely while `MaxShieldCharges == 0` so it doesn't clutter the display for players who haven't picked one up. The blue aura around the player (`ShieldAura` in [Player.tscn](../Scenes/Player.tscn)) is visible exactly while `CurrentShieldCharges > 0`.
- `Player.AddShieldCharge()` adds one charge capped at `MaxShieldCharges`, no-oping entirely for a player with no Barrier. Shared by the passive Regeneración timer (`OnShieldRegenTimeout`) and the Shield *pickup* enemies can drop (see [enemies.md](enemies.md#world-pickups-heart--shield-drops)).

## Level-up nova

- Every level-up (`GameManager.OnLevelGained`, once per XP threshold crossed) calls `Player.TriggerLevelUpBurst()`, which **no-ops entirely during round 1** (`GameManager.Instance.RoundNumber == 1`) — leveling up that early happens before there's any real threat on screen yet, so it shouldn't hand out a free screen-clear. The nova starts working from round 2 onward. Otherwise:
  1. Instantly kills (`enemy.TakeDamage(99999)`) every non-Boss enemy within `LevelUpBurstRadius` (default 220px) of the player. This reuses each enemy's normal death path, so kills still register XP/coins, and a Splitter caught in the blast still splits normally. **Bosses are explicitly excluded** (`enemy.Category != EnemyCategory.Boss`) — without this, leveling up while a boss happened to be in range deleted it in one hit regardless of the actual fight, which defeated the entire point of a boss round (see [rounds.md](rounds.md#boss-rounds)).
  2. Spawns a purely cosmetic expanding/fading ring (`Polygon2D` + `CreateTween`) at the burst radius, then frees itself — no gameplay effect, just visual feedback for the nova.
- This happens *before* the tree pauses for the upgrade picker, so the burst plays out live.
- The nova's own kills grant XP too, which can chain into *more* level-ups within the same `AddXp` call (a big single reward, or several kills at once, crossing more than one threshold) — see below for how those get queued rather than lost.

## Multiple level-ups at once

`GameManager.AddXp` uses a `while` loop (not a single `if`), so one large XP reward that crosses several thresholds correctly increments `Level` once per threshold, not just once — this used to be a real bug: killing a high-value enemy (or the level-up nova itself chaining into more kills) could jump several levels but only ever show one upgrade picker, silently losing the other picks.

Each threshold crossed increments `GameManager._pendingLevelUps` instead of immediately opening the picker again. `ShowNextLevelUpIfIdle()` only opens the picker if it isn't already showing one — so a 4-level jump still opens exactly one picker at first, but `UpgradePicker.OnChoicePressed` already hides itself before handing control back to `GameManager.ApplyUpgradeAndResume()`, which decrements `_pendingLevelUps` and, if more are owed, immediately reopens the picker with a fresh set of 3 choices (still paused) instead of resuming. Only once `_pendingLevelUps` reaches 0 does the game actually unpause. Net effect: a 4-level jump means picking 4 separate upgrades in a row, one screen at a time — none of them skipped.

### The XP bar reset bug

`XpChanged` (which the HUD's XP `ProgressBar` listens to) used to fire **before** the roll-over `while` loop, against the pre-rollover `Xp`/`XpToNextLevel`. That reads as "at or over max" the instant a level-up happens, and since nothing re-fired the event once the loop finished correcting `Xp`/`XpToNextLevel`, the bar just sat there looking full — until the *next* kill's `AddXp` call happened to invoke `XpChanged` again with values that were, by then, already correct. `XpChanged` now fires exactly once per `AddXp` call, **after** the loop, so the bar always reflects the final post-rollover state immediately.

### The level-up popup

`LevelsGained` is a second event, distinct from `LevelUp`: `LevelUp` fires once per threshold crossed (so it can update the "Nv N" label to its final value one increment at a time), while `LevelsGained` fires **once per `AddXp` call** with the total levels crossed. `HUD.OnLevelsGained` spawns a floating `+N` label next to the level number — punch-scaled in via `Back`/`Out` easing, drifts up, fades — the same procedural-popup shape used everywhere else (`Enemy.SpawnScorePopup`/`SpawnDamageNumber`). A triple level-up therefore shows one `+3`, not three overlapping `+1`s in the same frame. The popup is added as a sibling of the stat panel, not a child of the level `Label` — parenting it inside the `VBoxContainer` would make the container try to lay it out as part of the stat list instead of letting it float freely over everything.

## Orbit Blades (Orbit Blade reward)

- `OrbitCount` is the target number of blades (0–4, tier-determined — see [rewards.md](rewards.md)). `Player.RefreshOrbitBlades()` instantiates [`OrbitBlade.tscn`](../Scenes/Player/OrbitBlade.tscn) instances as children of the player until `_orbitBlades.Count == OrbitCount`, never removing any.
- Each blade ([`OrbitShield.cs`](../Scripts/Player/OrbitShield.cs)) is an independent `Area2D` that orbits at `OrbitRadius = 100` from its own `AngleOffset` (evenly spaced across however many blades exist *at the moment it's created* — adding a 4th blade later doesn't reshuffle the existing 3, it just adds one more starting at its own slot). Each blade damages any `Enemy` it overlaps and shoves it back with `KnockbackForce = 260`.
- **Spin couples to attack speed**: `_angle` advances by `OrbitSpeed × Player.FireRateRatio`, so Rapid Fire upgrades visibly wind the blades up (and the Sobrecarga ultimate's temporary doubling spins them up mid-buff, since the ratio is read live rather than cached at pickup). Bounded by `MaxFireRate`, so the ceiling is 4× the base spin. This affects *coverage*, not DPS — a faster blade sweeps more ground and is more likely to be overlapping something when its damage tick fires, but the tick rate itself is unchanged. `FireRateRatio` is exposed on `Player` so `_baseFireRate` can stay private and every blade shares one baseline regardless of when it was created.
- **Damage ticks on an interval** (`HitInterval = 0.4s`, `Damage = 16` → 40 DPS per blade), driven by `GetOverlappingBodies()` in `_PhysicsProcess`. This replaced a `BodyEntered` hook, which fires only on *entry*: an enemy that stayed inside the blade's area took one hit and then nothing until the blade came all the way back around (~2.1s at `OrbitSpeed = 3`), i.e. roughly 3 DPS. The reward felt like it did nothing, and the fix was the tick rate at least as much as the damage number. Ticking also decouples DPS from orbit speed, so tuning one doesn't silently change the other.
- The shove is directed **away from the player**, not away from the blade. Pushing off the blade itself would sometimes fling enemies *inward* past it, which is the opposite of what a defensive orbital is for. It's applied before `TakeDamage`, since that call can free the enemy (and spawns a Splitter's children from inside itself).
- Enemies take knockback through `Enemy.ApplyKnockback`, which decays back to zero in `_PhysicsProcess` — the same shape as the player's own knockback. It's deliberately added *outside* the `EnemySpeedMultiplier`, so a Zona Lenta ultimate doesn't also weaken the shove: it's an impulse from your weapon, not part of the enemy's own movement.
- `Enemy.ApplyKnockback` no-ops for `EnemyCategory.Boss` — bosses are immune to every knockback source (blades, bullets), since flinching around under a swarm of hits reads as weak rather than as a boss, and it was disrupting their own attack patterns.
- `HasOrbitShield => OrbitCount > 0` is the public flag other systems (like the shop's "Owned" check) read.

## Companion (Drone reward)

- `CompanionStatPercent` (0–0.5, i.e. 0–50%) is the fraction of the player's *current* `FireRate`/`BulletDamage` the companion fires at. Buying a better tier raises this (never lowers it — `Max`).
- Only **one** companion ever exists (`Player._companion`); the first Companion-type pickup instantiates [`Companion.tscn`](../Scenes/Player/Companion.tscn) as a child of the player at a fixed local offset `(-36, -36)` (so it just follows the player automatically, no tracking logic needed) and gives it a live reference (`Companion.Owner = this`).
- [`Companion.cs`](../Scripts/Player/Companion.cs) re-reads `Owner.FireRate` / `Owner.BulletDamage` on every shot (not a frozen snapshot), so the companion's output scales up automatically as the player's own stats improve later in the run. It targets the nearest enemy exactly like the player does, independently.
- **Facing**: same fix as the player's own triangle (see [Facing](#controls) above), just facing what it's shooting at instead of a movement direction — the drone doesn't move independently, so "direction" for it means "current target," not travel. `_PhysicsProcess` finds the nearest enemy every frame (`FindNearestEnemy()`, shared with `OnFireCooldownTimeout` so both read off the same target) and turns the `Visual` triangle toward it with the same exponential catch-up as the player (`FacingTurnRate = 16`) — running every frame rather than only at fire time keeps it tracking a moving target smoothly between shots instead of snapping to a new heading on each timeout. Holds its last facing when no enemy exists, same "nothing to reset to" reasoning as the player.

## Side Shot

- `ExtraFiringLines` (0–5, capped) adds extra bullets fired **parallel** to the main shot every time the player fires, spaced out sideways — a different shape from Twin Shot's fixed ±25° diagonal pair. Alternates sides as it stacks (line 1 right, line 2 left, line 3 further right, ...), via `baseDir.Rotated(Mathf.Pi / 2f)` as the perpendicular offset axis, `SideShotSpacing = 14px` between lines.
- `FireInDirection(dir, positionOffset)` grew an optional spawn-position offset to support this — the extra lines still travel in the exact same direction as the main shot, they just start from a different point next to the player.
- Additive across tiers (Rare +1, Legendary +2), hard-capped at 5 — see [rewards.md](rewards.md#additive-with-a-hard-cap-side-shot).

## Laser

- `LaserLevel` (0–4, tier-determined, take-the-best-ever like Orbit Blade — see [rewards.md](rewards.md#take-the-best-ever-orbit-blade-drone-companion-laser)) gates `TryFireLaser()`, polled every physics frame (not a repeating `Timer`) while `LaserLevel > 0` and `LaserCooldownFraction <= 0`. A Timer fires on its own fixed schedule regardless of whether anything's in range; polling instead means the laser goes off the instant a target enters range with the cooldown already clear, rather than waiting out a whole extra interval because the target showed up between two scheduled ticks.
- `LaserTiers` is a `(Interval, Damage, MaxTargets)` lookup array — `Interval` now only sets how fast `LaserCooldownFraction` decays, not a Timer's `WaitTime`. Only **tier 1** caps how many enemies a single zap can hit (5) — higher tiers hit everything in range uncapped. `TryFireLaser` gathers every in-range enemy, and when the tier is capped, sorts by distance and keeps only the closest `MaxTargets` before applying damage — so a capped zap always prioritizes the nearest threats, not an arbitrary subset. It's a no-op (cooldown stays at 0, retried next frame) when nothing's in range.
- Deals finite damage (`Enemy.TakeDamage`) to everything it hits, **including a Boss** — unlike the level-up nova, this isn't an instant-kill, so it doesn't need a Boss exclusion.
- Each hit draws a brief fading magenta `Line2D` beam from the player to the enemy (`SpawnLaserBeam`), visually distinct from the nova's expanding ring.

## Missile (Misil)

- `MissileLevel` (0–4, take-the-best-ever) gates `TryFireMissile()`, polled every physics frame the same way `TryFireLaser` is (see [Laser](#laser) above) rather than a repeating `Timer` — `MissileCooldownFraction` decays on its own low cadence, 4.5s at tier 1 down to 2.6s at tier 4, deliberately slow because it trades uptime for area damage. `MissileTiers` holds `(Interval, Damage, Radius)`.
- `TryFireMissile` targets the nearest enemy inside `FireRange` (via the shared `FindNearestEnemyInRange()`, so every auto-weapon respects the same ring) and no-ops (cooldown holds at 0, retried next frame) when nothing's in range.
- **[`Missile.cs`](../Scripts/Projectile/Missile.cs) flies to a *point*, not a target.** `TargetPosition` is snapshotted at launch and never re-read, so a fast enemy can outrun it and the missile still detonates where it was aimed. That miss is the intended tradeoff for the blast, not a bug awaiting homing.
- It detonates on whichever comes first: arriving at the point, touching an enemy, touching an obstacle (mask 10, same as a bullet), or a lifetime timeout as a safety net. An `_exploded` guard makes `Explode()` idempotent — `BodyEntered` can fire on the same frame an arrival already detonated, and a double blast would deal double damage.
- The expanding blast ring is parented to the **Bullets container, not the missile**, because the missile frees itself on that same frame and would otherwise take the visual with it. Same trick as `Enemy.SpawnScorePopup`.
- `ArrivalThreshold` (14px) must stay larger than one frame of travel (`Speed × delta`), or the missile can step straight past the point without ever landing inside it.

## Burn (Incendiario)

- `BurnLevel` (0–4, take-the-best-ever) is exposed as two read-only properties, `CurrentBurnDps` / `CurrentBurnDuration`, rather than each weapon keeping its own copy of the `BurnTiers` lookup. It's a **modifier applied by every source of player damage**, not a separate weapon of its own.
- **Every weapon applies it**, each calling `Enemy.ApplyBurn` right alongside its own `TakeDamage`:
  - Basic shots (`Player.FireInDirection`) — including Twin Shot and Side Shot lines, and a piercing bullet lights up everything it passes through for free.
  - The Laser (`OnLaserTimeout`) — every enemy caught in a zap.
  - Orbit Blades (`OrbitShield.DamageOverlappingEnemies`) — every 0.4s tick.
  - Missiles (`Missile.Explode`) — everything caught in the blast radius.
  - The Companion drone (`Companion.OnFireCooldownTimeout`) — at full DPS/duration, *not* scaled by `StatPercent` the way `Damage` is. Incendiario is a status the target catches, not a magnitude stat, so there's no "35% of the burn" to apply.
- Enemy-side state (ticking, refresh-don't-stack, the orange tint) lives on `Enemy` — see [enemies.md](enemies.md#burn-incendiario).

## Crit (Golpe Crítico)

- `CritChance` (0–60%, additive + cap) and the roll itself are centralized in one method, `Player.ApplyCrit(int baseDamage, out bool isCrit)`, rather than each weapon re-rolling `_critRng` against `CritChance` on its own. It's a **modifier applied by every source of player damage**, same shape as Burn above.
- **Every weapon calls it**, right where it would otherwise just use its own flat damage value:
  - Basic shots (`Player.FireInDirection`) — rolled per bullet, not per volley, so a Twin/Side Shot spread can crit on some lines and not others.
  - The Laser (`OnLaserTimeout`) — rolled per enemy hit per tick; a crit hit also tints that beam gold instead of magenta.
  - Orbit Blades (`OrbitShield.DamageOverlappingEnemies`) — rolled every 0.4s tick, no visual tint (a per-tick flicker on the blade's own fixed colour would read as jitter, not feedback).
  - Missiles (`OnMissileTimeout`) — rolled once at launch, same as `BurnDps`/`BurnDuration` being snapshotted then; a crit tints the whole missile gold.
  - The Companion drone (`Companion.OnFireCooldownTimeout`) — rolled against the drone's own `StatPercent`-scaled damage, tinting that bullet gold on a crit same as the player's own.
- `GetOffensivePower()`'s `critMult` term now scales `laserDps`, `missileDps`, and `orbitDps` in addition to `gunDps`, since all four can actually crit — otherwise a crit-heavy Laser/Blade build would under-report its power and dodge the [adaptive difficulty](difficulty-scaling.md#adaptive-difficulty-difficultybalancer) correction.
- Deliberately **not** applied to the level-up nova's instant-kill burst or the Nova ultimate's blast damage — both are one-off utility effects, not weapons, matching the same boundary Burn draws.

## Ultimates

- `EquippedUltimate` (nullable `UltimateKind`), `UltimateCooldownRemaining`/`UltimateCooldownDuration`, and `TriggerUltimate()` — full mechanics documented in [rewards.md](rewards.md#ultimates) since it's really a reward-system concern (shop-only acquisition, swap rules, the round-scaled cooldown curve). The HUD's Ultimate button/bar (`HUD.cs`) is hidden until one is equipped and disabled until the cooldown clears, showing a `{remaining:0.0}s` countdown on the button meanwhile.

## Cooldown icons (HUD)

- `HUD.cs`'s `CooldownIcons` row sits beside the top-left stats panel — repositioned every frame off `TopBarPanel`'s live size/position rather than a fixed offset, since that panel's width breathes with its own text. One `CooldownIcon` (`Scripts/UI/CooldownIcon.cs`) per timed ability — Laser, Missile, Shield Regen, Ultimate — each hidden until its ability is actually owned/equipped.
- `Player` exposes a `*CooldownFraction` (0–1) per ability. `ShieldRegenCooldownFraction` is computed live from `_shieldRegenTimer`'s `TimeLeft / WaitTime` (0 when the timer isn't running). `LaserCooldownFraction`/`MissileCooldownFraction` are plain fields, not read off a `Timer` at all — see [Laser](#laser)/[Missile](#missile-misil) above — set to `1` only from `TryFireLaser`/`TryFireMissile` on a tick that actually found a target, and decayed toward `0` every physics frame, holding at exactly `0` until the next real hit resets them. The Ultimate icon reads `UltimateCooldownRemaining / UltimateCooldownDuration` directly, since that pair *is* already a 0–1-shaped fraction (1 = just used, 0 = ready) — no translation needed the way the old Score-charge meter required.
- `CooldownIcon` draws its own clock: a filled circle in the ability's colour, with a dark pie sector covering the fraction still on cooldown, swept from 12 o'clock. No texture — plain `_Draw()`/`DrawPolygon`, same procedural-only approach as the rest of the game's visuals. 1 = just used (fully covered), 0 = ready (fully clear).

## Stacking

Speed/fire-rate/damage stacking rules (same-tier-only bucketing, each with its own hard cap), the flat-additive-with-a-cap Side Shot, and the max-tracks-best-tier-plus-refill Heart/Barrier rule all live in [rewards.md](rewards.md#stacking-rules) since they're really a reward-system concern, not a player-mechanic one — `Player.ApplyTieredStack()` is the tier-bucketing implementation, but the *rules* (and all the cap constants) are documented there.
