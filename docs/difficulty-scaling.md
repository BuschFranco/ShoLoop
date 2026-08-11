# Difficulty Scaling

Every difficulty knob in the game is a **percentage curve indexed by round number**, using one shared, reusable building block: [`RoundCurve`](../Scripts/Util/RoundCurve.cs).

## The building block: `RoundCurve`

```csharp
public readonly struct RoundCurve
{
    public readonly float Base, PerRound, Min, Max;
    public float Evaluate(int round) => Mathf.Clamp(Base + (round - 1) * PerRound, Min, Max);
}
```

Round 1 always evaluates to exactly `Base`. Each additional round adds `PerRound`, clamped to `[Min, Max]` so nothing grows (or shrinks) without bound. This is the *only* pattern used for round-based scaling anywhere in the codebase — enemy toughness, spawn quantity, and reward-tier odds are all just different `RoundCurve` instances plugged into the same `.Evaluate(round)` call. To change the pace of the game, edit the four numbers on the relevant curve — no other code needs to change.

## Where the curves live

All enemy/spawn curves are `private static readonly RoundCurve` fields at the top of [EnemySpawner.cs](../Scripts/Enemy/EnemySpawner.cs), recomputed once per round in `ConfigureForRound(round)`:

| Curve | Base | PerRound | Min | Max | Meaning |
|---|---|---|---|---|---|
| `SpawnIntervalCurve` | 0.6s | −0.035s | 0.12s | 0.6s | seconds between spawn ticks (lower = faster) — see the early-round grace below |
| `BurstCountCurve` | 1 | +0.35 | 1 | 8 | enemies spawned per tick |
| `MaxConcurrentCurve` | 25 | +4 | 25 | 200 | cap on enemies alive at once (× `LateCrowdMultCurve`) |
| `LateCrowdMultCurve` | 0.4 | +0.0667 | 1.0 | 1.2 | late-game crowd bump, see below |
| `SpecialChanceCurve` | 0.05 | +0.05 | 0.05 | 0.6 | chance a given spawn is a special enemy |
| `HpMultCurve` | 1.0 | +0.25 | 1.0 | 10.0 | enemy `MaxHp` multiplier |
| `DmgMultCurve` | 1.0 | +0.22 | 1.0 | 10.0 | enemy `ContactDamage` multiplier |
| `SpeedMultCurve` | 1.0 | +0.06 | 1.0 | 2.2 | enemy `MoveSpeed` multiplier |
| `RewardMultCurve` | 1.0 | +0.25 | 1.0 | 12.0 | enemy `XpReward`/`CoinsReward` multiplier |

Applied in `EnemySpawner.SpawnOne()`: each newly-instantiated enemy has `MaxHp`, `ContactDamage`, `MoveSpeed`, `XpReward`, `CoinsReward` multiplied by the current round's evaluated values **before** it's added to the tree (so `Enemy._Ready()` picks up the already-scaled `MaxHp` when it sets `CurrentHp = MaxHp`).

### Late-game crowd bump

`LateCrowdMultCurve` multiplies `MaxConcurrentCurve` to put visibly more enemies on screen at once in the late game: flat **1.0× through round 10**, a linear ramp over rounds 11–12, and pinned at **1.2× from round 13 onward**.

| Round | 10 | 11 | 12 | 13+ |
|---|---|---|---|---|
| Multiplier | 1.00 | 1.067 | 1.133 | 1.20 |
| Concurrent cap | 61 | 69 | 78 | 88 → 92 → … |

`RoundCurve`'s clamps express a flat→ramp→flat shape exactly, so this needed no new mechanism — but the `Base` of `0.4` looks arbitrary out of context. It's derived: `Evaluate` is `Base + (round-1) × PerRound` clamped to `[Min, Max]`, and `0.4` is what makes the `Min`/`Max` clamps land on rounds 10 and 13 specifically.

**A prerequisite bug fix made this actually work.** `OnSpawnTimeout` enforced the cap with `_enemiesContainer.GetChildCount()`, but enemies parent their score popups, damage numbers, and fired bullets into that same container — so the "max concurrent enemies" cap was really "enemies + transient VFX + bullets", and bound far earlier than its number suggested, worst exactly in the busy late rounds this bump targets. It now counts the `enemies` group instead, read once per tick and tracked locally rather than re-queried per burst item.

### Where enemies appear

`EnemySpawner.ChooseSpawnPosition()` picks a random angle at a distance of `max(SpawnRadius, player.FireRange + SpawnClearanceMargin)`. The `FireRange` term matters: the player's auto-fire ring grows with Fire Range upgrades, and an enemy appearing *inside* that ring reads as materializing on top of you — deriving the clearance from the live ring guarantees that can't happen no matter how far the ring has been upgraded.

It then re-rolls the angle up to 8 times to find a spot inside `ArenaHalfExtents`. Near an arena edge a random angle easily lands off-map, which would otherwise leave the enemy with a long walk in from nowhere. If every attempt is out of bounds (player cornered), it uses the first pick and lets the enemy walk in rather than skipping the spawn entirely.

### Early-round grace (onboarding cushion)

`EarlyRoundIntervalMult = { 1.6f, 1.25f }` multiplies the spawn interval for rounds 1 and 2 only, tapering back to `1×` from round 3 on:

| | Round 1 | Round 2 | Round 3+ |
|---|---|---|---|
| Interval multiplier | ×1.6 | ×1.25 | ×1.0 |
| Effective interval | 0.96s | 0.71s | curve value, unchanged |
| Enemies/sec | ~1.0 | ~1.4 | unchanged |

This exists as a separate array rather than a curve tweak because a linear `RoundCurve` **cannot** express "extra gentle at the start, then rejoin the normal ramp" — that shape is piecewise by definition, and re-sloping `SpawnIntervalCurve` to soften the opening would drag every later round along with it. Keeping it as an explicit, clearly-bounded override leaves the main curve meaning exactly what it says for rounds 3+.

Split children (see [enemies.md](enemies.md#splitting-enemysplitter)) inherit their stats from their already-scaled parent, so round scaling propagates through a splitter's lineage automatically without `Enemy.cs` needing direct access to the spawner's curves.

## Rough pacing at a glance

| | Round 1 | Round 2 | Round 5 | Round 10 |
|---|---|---|---|---|
| Enemies/sec (interval × burst) | ~1.7 | ~2.5 | ~5.4 | capped ~50 |
| Enemy HP ×| 1.0 | 1.25 | 2.0 | 3.25 |
| Contact damage × | 1.0 | 1.22 | 1.88 | 3.0 |
| Enemy speed × | 1.0 | 1.06 | 1.24 | 1.54 |
| XP/coins reward × | 1.0 | 1.25 | 2.0 | 3.25 |

Enemy volume (spawn interval + burst size + concurrent cap) was deliberately softened from an earlier, steeper set of `PerRound` values that roughly doubled the total enemies spawned each round — round-over-round total enemy count now grows by roughly 30-40% instead of ~100%, while the eventual late-round ceilings (`Min`/`Max` clamps) are unchanged, just reached more gradually.

Enemy toughness (`HpMultCurve`/`DmgMultCurve`/`SpeedMultCurve`) was separately softened too — a second, distinct request from slowing enemy *volume* down: "escala de enemigos" here means how fast individual enemies get tankier/harder-hitting/faster per round, not how many spawn. All three `PerRound` rates were cut by roughly 25-30% (0.35→0.25 HP, 0.3→0.22 damage, 0.08→0.06 speed); the eventual `Max` ceilings and `RewardMultCurve` (kill payout) are untouched, so kills still stay worth it relative to toughness, just ramping up more gradually. The level-up XP curve is unrelated to both of these — see the next section.

Reward multiplier deliberately scales alongside toughness so kills stay worth it as enemies get harder to kill, and the shop's own [wealth-based price inflation](economy.md#1-wealth-based-inflation--gamemanagerupgradecostmultiplier) keeps the economy from getting relatively cheaper over time.

## Player leveling pace

`GameManager.AddXp` grows `XpToNextLevel` by ×1.3 per level (`XpToNextLevel = RoundToInt(XpToNextLevel * 1.3f)`), up from an earlier ×1.2 — each subsequent level asks for a bit more XP than before, so overall leveling (and therefore level-up reward picks) comes a little slower over a long run. This is independent of the enemy-volume curves above; it only affects how far `Xp` needs to climb per level; enemy `XpReward` itself is untouched.

## Adaptive difficulty (`DifficultyBalancer`)

Everything above is indexed purely on `RoundNumber`, which quietly assumes the player's power grows on a predictable schedule. A lucky reward streak breaks that assumption badly: the round curves keep their gentle pace while the player's DPS multiplies, and the game stops posing any threat. [`DifficultyBalancer`](../Scripts/Util/DifficultyBalancer.cs) closes that gap.

1. **Measure.** [`Player.GetOffensivePower()`](../Scripts/Player/Player.cs) returns a rough DPS-equivalent of the player's whole kit as a ratio against the round-1 baseline (an untouched player scores exactly `1.0`): gun DPS (`BulletDamage × FireRate × shotsPerVolley`, where `shotsPerVolley` counts Twin Shot and Side Shot lines), plus Laser DPS and an approximate Orbit Blade contribution, all scaled by `(1 + CompanionStatPercent)`. It's an approximation on purpose — the goal is spotting order-of-magnitude runaway builds, not simulating combat.
2. **Compare.** `ExpectedPowerCurve = RoundCurve(1, +0.6, 1, 40)` is what a normally-progressing player is expected to score by a given round.
3. **Correct.** `GetCatchUpMultiplier` returns `Clamp(1 + (power/expected - 1) × CatchUpStrength, 1, MaxCatchUp)` with `CatchUpStrength = 0.3` and `MaxCatchUp = 2.5`. A player at 3× expected power faces enemies with `1 + (3-1)×0.3 = 1.6×` HP/damage.

**It is deliberately one-directional.** The multiplier floors at `1`, so a player *below* the curve gets no help whatsoever — this is a ceiling-limiter, not rubber-banding. Falling behind is allowed to be hard; running away with it shouldn't be free.

`EnemySpawner.EvaluateStatCurves(round)` applies it to **`_hpMult` and `_dmgMult` only**, once per round alongside every other curve. `_speedMult` and especially `_rewardMult` are left alone on purpose — an over-powered player shouldn't earn *less* per kill for the same work, which would turn the correction into a punishment spiral.

Why it's imperceptible early: at rounds 1–3 a normally-progressing player sits at or just under `ExpectedPowerCurve`, so the multiplier is `1.00`–`1.05`. It only bites once someone is genuinely multiples ahead. The pause menu shows both numbers live (`Poder: 4.2x   Ajuste enemigo: 1.15x`) so the system is inspectable while playtesting instead of silently changing enemy stats.

## Reward tier odds use the same mechanism

`RewardTierRoller` (see [rewards.md](rewards.md)) has its own four `RoundCurve`s — one per tier — that shift the probability of rolling Common/Rare/Epic/Legendary as rounds progress. Structurally identical to the enemy curves above, just feeding a tier roll instead of a stat multiplier.

## Why this design

The previous version of the spawner used a raw elapsed-time ramp (a `Timer` ticking down the spawn interval regardless of round). That made difficulty depend on how long the app had been running, not on player progress, and it wasn't obviously tunable — the numbers were buried inline. Moving everything to named `RoundCurve` constants keyed strictly on `RoundNumber` makes the pacing deliberate, inspectable in one place per file, and safe to retune without touching any control-flow logic.
