# Rewards: Tiers, Catalog & Stacking Rules

Rewards come from two places — **level-up picks** (free, XP-driven) and **shop items** (cost Coins, offered at round end). Both are defined in [UpgradeData.cs](../Scripts/Upgrades/UpgradeData.cs), but they are deliberately *separate pools*: see [Level-up picks vs. shop items](#level-up-picks-vs-shop-items) below.

## Tiers

Four tiers, each with a color used everywhere a reward is displayed (`RewardTierRoller.GetTierColor`):

| Tier | Color |
|---|---|
| Common | green |
| Rare | blue |
| Epic | violet |
| Legendary | gold |

### Tier odds scale with round

`RewardTierRoller.GetWeights(round)` evaluates four `RoundCurve`s (see [difficulty-scaling.md](difficulty-scaling.md)) and normalizes them into a probability distribution:

| Tier | Base | PerRound | Min | Max |
|---|---|---|---|---|
| Common | 65% | −3.5%/round | 10% | 65% |
| Rare | 27% | +0.3%/round | 15% | 35% |
| Epic | 7% | +1.4%/round | 5% | 35% |
| Legendary | 1% | +1.3%/round | 0% | 25% |

Rough result: round 1 ≈ 65/27/7/1%, round 10 ≈ 35/31/21/13%, round 20 ≈ 10/32/33/25%. Common's share shrinks, everything else grows, gradually and capped — no tier ever hits 0% or 100%.

### Picking rewards: `UpgradeData.PickRandomTiered(count, round, source)`

For each of the `count` slots offered: roll a tier via the weights above, then pick a random not-yet-offered item from that tier's list (falls back to any tier with something left if the rolled tier is exhausted).

## Level-up picks vs. shop items

Every catalog entry declares a `RewardSource`, a `[Flags]` enum:

```csharp
[Flags] public enum RewardSource { LevelUp = 1, Shop = 2, Both = LevelUp | Shop }
```

`BuildCatalog(source)` filters the single full catalog by that flag, and the two callers each ask for their own pool:

| | Call | Pool |
|---|---|---|
| **Level-up picks** | `PickRandomTiered(3, round, RewardSource.LevelUp)` from `GameManager.ResolveNextInterstitial` | combat/stat rewards only |
| **Shop items** | `PickRandomTiered(3, round, RewardSource.Shop)` from `Shop.Open` | everything above **plus** the shop exclusives |

**Shop exclusives** (`RewardSource.Shop`) — things you deliberately spend Coins on rather than get handed for free:

| Item | Tier | Cost | Effect |
|---|---|---|---|
| Corazón | Legendary | 60💰 | +1 max life & full heal |
| Ultimate ×3 | Epic | 80💰 | equips one of the 3 Ultimates |
| Regeneración | Epic | 55💰 | +1 shield charge every 12s |
| Regeneración+ | Legendary | 75💰 | +1 shield charge every 7.5s |
| Segunda Oportunidad | Legendary | 95💰 | revive once at full life on death (max 2) |

This replaced a pair of ad-hoc `includeHearts` / `includeUltimates` bools threaded through the catalog builder. That approach needed a new flag per exclusive and encoded the rule at the *call site* rather than on the reward itself — with five exclusives it stops scaling. A per-entry `Source` keeps the rule next to the thing it describes, and makes "which pool is this in?" answerable by reading one field.

Note this is availability only: a shop exclusive is still a normal tiered reward and still obeys every stacking rule below.

## The full catalog

Most rewards exist in all 4 tiers. Exceptions: Twin Shot (Epic only), Side Shot (Rare and Legendary only), and the shop exclusives marked *(shop)*. Base values shown; see [economy.md](economy.md) for how shop cost is further adjusted at purchase time.

| Reward | Type | Common | Rare | Epic | Legendary | Hard cap |
|---|---|---|---|---|---|---|
| Fire Range | `FireRange` | +70 (8💰) | +130 (16💰) | +200 (28💰) | +300 (45💰) | +500 bonus |
| Rapid Fire | `FireRate` | +0.5/s (8💰) | +1/s (16💰) | +1.8/s (28💰) | +3/s (45💰) | 12/s total |
| Sharper Rounds | `BulletDamage` | +3 (8💰) | +5 (16💰) | +9 (28💰) | +15 (45💰) | +200 bonus |
| Orbit Blade | `OrbitShield` | 1 blade (20💰) | 2 blades (32💰) | 3 blades (46💰) | 4 blades (60💰) | 4 blades, 100px radius, 16 dmg every 0.4s each + knockback |
| Barrier | `HitShield` | 1 charge (14💰) | 2 charges (24💰) | 3 charges (36💰) | 4 charges (48💰) | 8 charges |
| Drone | `Companion` | 30% stats (12💰) | 35% stats (22💰) | 45% stats (34💰) | 50% stats (50💰) | 50% stats |
| Side Shot | `SideShot` | — | +1 line (22💰) | — | +2 lines (55💰) | 5 lines |
| Laser | `Laser` | Nv1: 4.0s/12dmg, máx. 5 blancos (20💰) | Nv2: 3.2s/20dmg (34💰) | Nv3: 2.5s/31dmg (50💰) | Nv4: 1.8s/50dmg (65💰) | Nv4 |
| Twin Shot | `ExtraProjectile` | — | — | one-time (30💰) | — | on/off |
| Pierce | `Pierce` | +1 (18💰) | +1 (26💰) | +2 (38💰) | +3 (55💰) | 5 extra enemies |
| Crit | `CritChance` | +8% (12💰) | +12% (20💰) | +18% (32💰) | +25% (48💰) | 60% |
| Bullet Speed | `BulletSpeed` | +80 (8💰) | +150 (14💰) | +240 (24💰) | +350 (38💰) | +700 bonus |
| Knockback | `BulletKnockback` | +60 (10💰) | +110 (18💰) | +170 (28💰) | +240 (42💰) | +400 |
| Coin Bonus | `CoinBonus` | +15% (14💰) | +25% (24💰) | +40% (36💰) | +60% (52💰) | +150% |
| Missile | `Missile` | 4.5s / 30 dmg / r70 (24💰) | 3.8s / 50 / r85 (38💰) | 3.2s / 75 / r100 (54💰) | 2.6s / 110 / r120 (72💰) | Nv4 |
| Burn | `Burn` | 6/s for 3s (20💰) | 10/s for 3s (32💰) | 16/s for 3.5s (46💰) | 24/s for 4s (62💰) | Nv4 |
| XP Bonus | `XpBonus` | +20% (18💰) | — | +50% (44💰) | — | +100% |
| Heart *(shop)* | `Heart` | — | — | — | +1 max life & full heal (60💰) | 10 lives |
| Ultimate *(shop)* | `Ultimate` | — | — | one of 3 kinds (80💰) | — | see [Ultimates](#ultimates) |
| Shield Regen *(shop)* | `ShieldRegen` | — | — | 1/12s (55💰) | 1/7.5s (75💰) | best tier taken |
| Revive *(shop)* | `Revive` | — | — | — | +1 charge (95💰) | 2 charges |

See [player.md](player.md) for what each `UpgradeType` actually does mechanically. Every hard cap below is a named constant in [Player.cs](../Scripts/Player/Player.cs) — that's the one file to touch to retune any of them.

## Stacking rules

This is the part most likely to need re-tuning if the game feels too easy or too hard, so it's spelled out precisely.

### Same-tier-only stacking: FireRange, FireRate, BulletDamage

These three feed `Player.ApplyTieredStack(upgrade)`, picked at any tier from either the level-up picker or the shop:

1. Adds `upgrade.Value` to a running total kept **per (UpgradeType, RewardTier)** bucket (`Player._tierStackTotals`).
2. The stat's final bonus is **the single highest bucket total**, not the sum of every bucket.

So: two Rare Sharper Rounds picks (+5 each) stack to +10 bonus damage, because they're the same tier. But a Common pick (+3) and a Legendary pick (+15) do **not** add up to +18 — the Legendary bucket (15) simply wins, and the Common pick contributed nothing once you already had it. If you'd instead picked *four* Common Sharper Rounds (4×3=12) against one Legendary (15), the Legendary still wins (15 > 12) — but three Commons (9) plus a Rare pickup that also had, say, 2 more Common-tier stacks could in principle out-total a single Epic pick, which is an intentional, mildly interesting tradeoff rather than a bug.

**Why:** these three stats compound player DPS/reach, which is exactly the axis that made the game trivially easy in earlier playtesting. Free-summing every tier ever picked has no ceiling; bucketing by tier means your total is bounded by "how many of your best-populated tier you've found," which grows much slower than "everything you've ever picked, added together."

The player's *base* value (`_baseFireRange` / `_baseFireRate` / `_baseBulletDamage`, snapshotted once in `_Ready()`) is always added back on top of the winning bucket — the tier system only governs the *bonus*, never regresses you below your starting stats.

**On top of the tier-bucketing, each stat also has a hard ceiling** (`MaxFireRangeBonus = 500`, `MaxBulletDamageBonus = 200`, and `FireRate` was already clamped to `MaxFireRate = 12`/s from the start). Tier-bucketing alone doesn't stop the *same* tier from being re-rolled many times over a long run (e.g. Legendary Fire Range ×10 would be +3000 with no other limit) — the flat cap is what actually bounds that. `Mathf.Min(ApplyTieredStack(upgrade), cap)` is applied before adding the bucket total on top of the base value.

### Additive with a hard cap: Side Shot

Every Side Shot pickup — at either tier, mixed freely, no tier-bucketing — simply adds to `ExtraFiringLines`, up to a flat ceiling (`MaxExtraFiringLinesCap = 5`). There's no reason to ever want more than 5 simultaneous bullet lines, so the shop hides it as "Owned" once maxed, same as the take-the-best-ever rewards below.

### Max-tracks-best-tier + additive refill: Barrier (HitShield)

Barrier is a **Current/Max pair that depletes with use** (charges consumed absorbing hits) — a different shape from the pure permanent stat bonuses above, so it gets its own rule:

- **Max** (`MaxShieldCharges`) tracks the single best tier ever picked — `Mathf.Max(currentMax, upgrade.Value)`, capped at `MaxShieldChargesCap = 8`. Picking a *weaker* tier than your current max never lowers it, and doesn't stack on top of it either.
- **Current** (`CurrentShieldCharges`) gets *this pickup's own value* added to it, capped at the (possibly just-raised) new max — `Mathf.Min(current + upgrade.Value, newMax)`. It does **not** jump straight to the new max.

Concretely: pick a Legendary Barrier (4 charges) → max 4, current 4. Take 3 hits → current 1. Pick a Common Barrier (+1) afterward → max stays 4 (1 < 4, doesn't raise the ceiling), current becomes `min(1+1, 4) = 2` — you gained exactly the 1 charge that pickup promised, not a full refill and not a max of 5. This was a real bug in an earlier version (both numbers used to just add forever and current always snapped to the new max, so weaker tiers picked after stronger ones acted as if they were just as good) — fixed to match how `OrbitShield`/`Companion` already computed max (best-tier, via `Max`), just with an added current-side refill since Barrier is consumable rather than a static "you always have N of these" value.

The shop disables Barrier only when you're already fully topped off **and** this tier's value wouldn't raise your ceiling either — otherwise it still helps (refills some current), so it stays purchasable. Note this rule no longer covers Heart, which used to share it — see below.

### Additive + full heal: Heart

Heart used to exist at all four tiers (+1/+2/+3/+3 max lives) and followed the Barrier rule above. It's now a **single Legendary option**: `+1` max life *and* a full heal, for 60💰. Max lives is the most swingy stat in the game — it's literally how many mistakes you're allowed — so it shouldn't be something a cheap Common roll hands out repeatedly.

With only one tier, "max tracks the best tier ever picked" became meaningless (every pick *is* the same tier), so the rule is simply:

```csharp
MaxLives = Mathf.Min(MaxLives + 1, MaxLivesCap);   // additive, capped at 10
CurrentLives = MaxLives;                            // always a full heal
```

It's disabled only when you're at `MaxLivesCap` **and** already on full lives — until then it always does something, either raising the ceiling or undoing damage.

### Take-the-best-ever: Orbit Blade, Drone (Companion), Laser

`OrbitCount = Max(OrbitCount, upgrade.Value)` and `CompanionStatPercent = Max(CompanionStatPercent, upgrade.Value / 100)`. Each tier's number is already an absolute target (e.g. "4 blades", "50% of your stats"), not a delta — so re-picking a lower-or-equal tier than what you already have is a genuine no-op. The [shop](economy.md) proactively hides ("Owned") any offer that would be a no-op, so you never spend your one purchase-per-round on it by accident.

`Laser`, `Missile`, and `Burn` all work the same way — `Level = Max(Level, upgrade.Value)` against a per-tier lookup table (`LaserTiers` / `MissileTiers` / `BurnTiers`), since every tier is strictly better than the last so there's nothing to sum. Details of the first two in [player.md](player.md#missile-misil); burn's enemy-side state is in [enemies.md](enemies.md#burn-incendiario).

`Laser` specifically: `LaserLevel = Max(LaserLevel, upgrade.Value)` (`upgrade.Value` is the tier level, 1–4), and `Player.EnsureLaserTimer()` re-reads `LaserLevel` from a fixed `LaserTiers` lookup array to set the firing `Timer`'s interval, per-tick damage, and max targets per zap. Every tier is a strictly-better version of the same ability (faster + harder-hitting), so there's nothing to sum — same "take the best" shape as Orbit Blade/Drone. Tier 1 additionally caps a single zap at 5 enemies (the closest 5 in range); tiers 2–4 hit everyone in range uncapped.

**Epic and Legendary (`LaserLevel >= 3`) couple to the player's live stats instead of using `LaserTiers`' fixed numbers**: `GetLaserDamage()` scales the tier's base damage by `BulletDamage / _baseBulletDamage`, and `GetLaserInterval()` scales the tier's base interval by `_baseFireRate / FireRate` (capped at a 0.3s floor so it can't go arbitrarily fast). `OnLaserTimeout()` re-reads `GetLaserInterval()` at the end of every tick and writes it back into `_laserTimer.WaitTime`, so the interval keeps tracking the player's current `FireRate` live rather than freezing at whatever it was when the Laser was picked or last upgraded — buying more Rapid Fire/Sharper Rounds later makes an Epic+ Laser faster/stronger too, automatically.

### The new combat rewards, by model

The five rewards added alongside the source split each reuse an existing stacking model rather than inventing one:

| Reward | Model | Notes |
|---|---|---|
| **Pierce** | additive + cap (SideShot) | `Bullet.Pierce` counts *extra* enemies passed through. `Area2D.BodyEntered` fires once per body, so a piercing bullet can't re-hit the same enemy — no hit-tracking needed. Walls still stop it. |
| **Crit** | additive + cap | Rolled **per bullet, not per volley**, so a Twin/Side Shot spread can crit on some lines and not others — much more visible feedback than all-or-nothing. Crit bullets are tinted gold. |
| **Bullet Speed** | tier-bucketed (FireRange) | `BulletSpeed` moved onto `Player` from `Bullet.tscn`'s own default, so the reward has a baseline to grow from and one place to apply it. |
| **Knockback** | tier-bucketed | Reuses the same `Enemy.ApplyKnockback` the orbit blades use; shoves along the bullet's travel direction. |
| **Coin Bonus** | additive + cap | Multiplies **coins only** in `RegisterKill`. XP/Score are untouched on purpose, so stacking it can't accelerate leveling or inflate the high score — just the shop budget. |

Crit and Pierce also feed `Player.GetOffensivePower()` as real damage multipliers. Without that a crit/pierce-heavy build would read as far weaker than it plays and would dodge the [adaptive difficulty](difficulty-scaling.md#adaptive-difficulty-difficultybalancer) correction entirely.

### The new shop items

- **Regeneración / Regeneración+** — take-the-best-ever, stored as *charges per minute* (5 / 8) so "higher is better" stays uniform with every other take-best reward; the timer interval is derived as `60 / rate`. Regen only tops up toward `MaxShieldCharges` and no-ops if you own no Barrier, so it complements Barrier rather than replacing it.
- **Segunda Oportunidad** — additive up to 2 charges. `Player.LoseLife` spends a charge *before* calling `NotifyPlayerDied`, restoring full lives and shield, so the game-over screen never appears. It reuses the level-up nova for the screen-clear and visual, which also buys breathing room at the moment you'd otherwise have died.

### One-time toggle: Twin Shot

`_hasExtraProjectile = true`. Boolean, Epic-only, no tiers. Also shop-"Owned"-gated once acquired.

## Ultimates

Three practical, distinct abilities — no tiers, each a single fixed pickup in the Epic bucket, marked `RewardSource.Shop` so they never show up for free at level-up (see [Level-up picks vs. shop items](#level-up-picks-vs-shop-items)).

| Ultimate | Effect | Duration |
|---|---|---|
| Pulso Nova | 500 finite damage (not instant-kill) to every enemy within a fixed 500px radius | instant |
| Zona Lenta | Multiplies every enemy's move speed by 0.3 (see [enemies.md](enemies.md#enemy-speed-multiplier)) | 6s |
| Sobrecarga | Doubles the player's current `FireRate` and `BulletDamage` | 6s |

**First-boss unlock**: killing the **first** boss of a run (round 5, tracked by `GameManager._bossUltimateGranted`) hands out a free Ultimate — the `UpgradePicker` reopens with a choice of 3, drawn by `UpgradeData.PickUltimateChoices(3)`, under the heading "¡JEFE DERROTADO! Elige tu Ultimate". It prefers kinds the player doesn't already have equipped. Subsequent bosses don't repeat it; after the choice resolves, the normal round-end shop opens as usual.

**Owning and swapping**: `Player.EquippedUltimate` (nullable `UltimateKind`) holds at most one at a time. `IsRewardUseless(Ultimate)` is true only if you already have that *exact* kind — a different kind is always buyable even with one equipped, and buying it **replaces** your current one (`ApplyUpgrade`'s `Ultimate` case always does `EquippedUltimate = upgrade.Ultimate; UltimateProgress = 0f;`, resetting the charge bar on every equip, first pick or swap alike).

**Charging from Score**: `GameManager.AddScore(amount)` calls `Player.AddUltimateProgress(amount)` on every gain — since Score is synced 1:1 with XP (see [economy.md](economy.md)), the meter fills at the same rate you level up. Only accumulates while an Ultimate is equipped, capped at `Player.UltimateChargeTarget = 250`.

**Triggering**: the HUD's Ultimate button (hidden until you own one, disabled until the bar is full) calls `Player.TriggerUltimate()`, which no-ops unless both an Ultimate is equipped and the charge is full, then resets the charge to 0 after running the effect. Zona Lenta's revert and Sobrecarga's revert both run on their own one-shot `Timer`s (`GameManager.ApplyTemporarySlow` / `Player`'s `_frenzyTimer`); Sobrecarga's revert specifically calls `RecomputeFireRateAndDamage()` (re-derives from base + tier-bucket totals) rather than restoring a stale pre-buff snapshot, so it's correct even if you pick a permanent Fire Rate/Damage upgrade mid-buff.

## Unbuyable offers, and which one glows

Two separate per-offer questions, both answered on [Player.cs](../Scripts/Player/Player.cs) so the shop and the level-up picker can't drift apart:

### `IsRewardUseless(upgrade)` → disable the button

True when taking this exact offer right now would change literally nothing. The offer **stays visible** — only its button is disabled, labelled by `GetUnavailableLabel(upgrade)` ("Adquirido" for a one-off you already own, "Al tope" for a stat that's simply maxed).

Covers the take-the-best/consumable families (Orbit Blade, Drone, Side Shot, Heart, Barrier, Laser, Ultimate, Twin Shot) via `!IsUpgradeOverCurrent`, **plus** the tiered-stacking stats once they hit their hard cap:
```csharp
case UpgradeType.FireRange:    return FireRange >= MaxFireRange;
case UpgradeType.BulletDamage: return BulletDamage - _baseBulletDamage >= MaxBulletDamageBonus;
case UpgradeType.FireRate:     return FireRate >= MaxFireRate;
```
Note the asymmetry: for those three, merely being a *losing tier* is **not** useless — picking one still feeds its own tier bucket, which is a legitimate long-game strategy (see the stacking rules above), and that case is communicated through `GetStackInfoText`'s "no suma" wording rather than blocked. Only being at the absolute cap, where feeding the bucket provably can't matter, disables the button.

This also feeds `UpgradeData.PickRandomTiered`'s `isUseless` filter, so capped rewards additionally stop being *rolled* when something useful is available — with the existing fallback still preventing an all-disabled level-up soft-lock.

### `GetHighlightFlags(offers)` → the glow

`IsUpgradeOverCurrent` alone can't decide the glow: it's a **per-offer** question ("does this beat what I already have?"). With a tier-1 stat owned and tier 2 + tier 3 both on the table, both pass, so the strongest isn't distinguished — the reported bug.

`GetHighlightFlags` adds the missing **cross-offer** pass, run once per screen-open over all 3 offers at once:
1. Filter to offers that pass `IsUpgradeOverCurrent` (something that improves nothing never glows).
2. Group the survivors by `UpgradeType` and keep only the single strongest per family — comparing by `Value`, with `Tier` as the tiebreak for families where two tiers share a value (e.g. Heart's Epic/Legendary both being +3).
3. Only those winners get the glow stylebox.

So in the reported case both tier 2 and tier 3 still "beat current", but tier 3 wins its family and is the only one highlighted.

## The shop's extra layers

On top of everything above, the shop applies its own [pricing rules](economy.md) — round-based inflation and a per-`UpgradeType` repurchase surcharge — and only allows one purchase per round visit. None of that affects level-up picks, which are always free.
