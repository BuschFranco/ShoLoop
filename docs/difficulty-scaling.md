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
| `LateCrowdMultCurve` | 1.3825 | −0.0425 | 0.66 | 1.0 | late-game crowd **taper**, see below |
| `SpecialChanceCurve` | 0.05 | +0.05 | 0.05 | 0.6 | chance a given spawn is a special enemy |
| `RareChanceCurve` | 0.15 | +0.02 | 0.15 | 0.35 | chance a spawn that isn't Hidden/Special is Rare |
| `CommonSuppressionCurve` | −1.125 | +0.125 | 0 | 1.0 | share of would-be Grunts promoted to Specials, see below |
| `HpMultCurve` | 1.0 | +0.25 | 1.0 | 10.0 | enemy `MaxHp` multiplier |
| `DmgMultCurve` | 1.0 | +0.22 | 1.0 | 10.0 | enemy `ContactDamage` multiplier |
| `SpeedMultCurve` | 1.0 | +0.06 | 1.0 | 2.2 | enemy `MoveSpeed` multiplier |
| `RewardMultCurve` | 1.0 | +0.35 | 1.0 | 12.0 | enemy `XpReward`/`CoinsReward` multiplier |

Applied in `EnemySpawner.SpawnOne()`: each newly-instantiated enemy has `MaxHp`, `ContactDamage`, `MoveSpeed`, `XpReward`, `CoinsReward` multiplied by the current round's evaluated values **before** it's added to the tree (so `Enemy._Ready()` picks up the already-scaled `MaxHp` when it sets `CurrentHp = MaxHp`).

### Late-game composition shift and crowd taper (rounds 10–18)

Two curves work together here, and they only make sense as a pair. The problem they solve: past round ~10 the late game read as *saturating* rather than hard — a wall of bodies where individual enemies stopped mattering.

**First, the diagnosis that shaped the fix: on-screen density is set by the cap, not by the spawn rate.** From round 8 onward the spawn rate (8–67 enemies/sec) vastly exceeds what the cap allows alive, so `OnSpawnTimeout` hits `aliveEnemies >= _maxConcurrentEnemies` and `return`s almost immediately every tick — the spawner is only ever backfilling kills. `SpawnIntervalCurve`/`BurstCountCurve` therefore control how fast the screen *refills after a clear* (under ~2s from round 14), **not** how crowded it is. Only `MaxConcurrentCurve × LateCrowdMultCurve` moves perceived density.

**`CommonSuppressionCurve`** promotes an increasing share of would-be Grunt spawns to Specials instead: `0` through round 9, ramping to `1.0` at round 18, after which no Grunt ever spawns again. It exists as a separate knob rather than simply raising `SpecialChanceCurve`'s `Max` to `1.0` because the rolls in `ChooseEnemyScene` are **sequential** (Hidden → Special → Rare → else Common), so Rare sits *downstream* of Special — driving `_specialChance` to 1.0 would silently zero out Rare too. Suppressing the Common fallback leaves Rare's share untouched at ~13.3%.

**`LateCrowdMultCurve`** was reversed from a bump into a taper: flat **1.0× through round 10**, down to **0.66× by round 18**, held from there. Because the mix above is simultaneously swapping 30 HP Grunts for ~82 HP (weighted average) Specials, average HP per body rises ~30% — so the same threat needs meaningfully fewer bodies, and the old bump was piling extra count on top of an already-heavier mix. Holding at `0.66` rather than tapering forever is deliberate: `MaxConcurrentCurve` keeps adding +4/round underneath, so once the taper bottoms out at round 18 — the same round commons hit zero — the count **resumes climbing on its own**, with no separate re-climb mechanism.

| Round | 10 | 12 | 14 | 16 | 18 | 20 | 25 |
|---|---|---|---|---|---|---|---|
| Suppression | 0.00 | 0.25 | 0.50 | 0.75 | 1.00 | 1.00 | 1.00 |
| Commons (% of spawns) | 31.8% | 18.5% | 12.4% | 6.2% | **0%** | 0% | 0% |
| Specials (% of spawns) | 47.5% | 63.2% | 69.4% | 75.5% | **81.7%** | 81.7% | 81.7% |
| Crowd multiplier | 1.00 | 0.915 | 0.830 | 0.745 | 0.66 | 0.66 | 0.66 |
| Concurrent cap (new) | 61 | 63 | 64 | 63 | **61** | 67 | 80 |
| Concurrent cap (old) | 61 | 78 | 92 | 102 | 112 | 121 | 145 |

Rounds 1–10 are bit-for-bit unchanged by both curves (suppression clamps to 0, crowd multiplier clamps to 1.0). Hidden holds its flat 5% throughout, and Rare holds ~13.3% from round 12 on.

Both `Base` values look arbitrary out of context and are derived the same way: `Evaluate` is `Base + (round-1) × PerRound` clamped to `[Min, Max]`, so `−1.125` is what pins suppression's clamps to rounds 10 and 18, and `1.3825` is what pins the crowd taper's to the same two rounds.

**Net effect and the knob to turn.** Total HP on screen at round 18 drops ~29% (46% fewer bodies × ~30% more HP each), so the late game is genuinely more survivable — which was the goal. If it proves *too* soft in playtesting, raise `LateCrowdMultCurve`'s `Min` (0.66 → 0.75 cuts the count reduction from −46% to −38%) rather than touching the composition curve; the two are independent.

`RewardMultCurve`'s `PerRound` was raised 0.25 → 0.35 alongside this. Coins largely self-correct — [`UpgradeCostMultiplier`](economy.md) indexes on `TotalCoinsEarned` rather than round number, so lower income also means cheaper prices — but XP has no such damper, and at the old rate ~46% fewer bodies would have cut XP income ~32%, directly slowing the level-ups that are the only source of free rewards. The bump brings that to roughly −10%.

**A prerequisite bug fix made the cap meaningful at all.** `OnSpawnTimeout` used to enforce it with `_enemiesContainer.GetChildCount()`, but enemies parent their score popups, damage numbers, and fired bullets into that same container — so "max concurrent enemies" was really "enemies + transient VFX + bullets", binding far earlier than its number suggested, worst exactly in the busy late rounds. It now counts the `enemies` group instead, read once per tick and tracked locally rather than re-queried per burst item.

**Known gap:** `Enemy.Split()` calls `GetParent().AddChild(child)` with no cap check — the cap is only enforced at spawner tick time, so a Splitter-heavy wave can transiently push the live count above `_maxConcurrentEnemies` (one Splitter eventually becomes 15 bodies). This is pre-existing, and it's part of why Splitter carries the lowest weight in the special pool (see [enemies.md](enemies.md#special-enemy-weights)); a real fix would mean exposing `_maxConcurrentEnemies` and consulting it inside `Split()`.

### Where enemies appear

`EnemySpawner.ChooseSpawnPosition()` picks a random angle at a distance of `max(SpawnRadius, player.FireRange + SpawnClearanceMargin)`. The `FireRange` term matters: the player's auto-fire ring grows with Fire Range upgrades, and an enemy appearing *inside* that ring reads as materializing on top of you — deriving the clearance from the live ring guarantees that can't happen no matter how far the ring has been upgraded.

It then re-rolls the angle up to 8 times to find a spot inside `ArenaHalfExtents`. Near an arena edge a random angle easily lands off-map, which would otherwise leave the enemy with a long walk in from nowhere. If every attempt is out of bounds (player cornered), it uses the first pick and lets the enemy walk in rather than skipping the spawn entirely.

### Early-round grace (onboarding cushion)

Four parallel tables cushion rounds 1–2 only, all tapering back to `1×` from round 3 on. They're read through one shared `EarlyRoundMult(table, round)` helper:

| | Round 1 | Round 2 | Round 3+ |
|---|---|---|---|
| `EarlyRoundIntervalMult` | ×1.60 | ×1.25 | ×1.0 |
| → effective spawn interval | 0.96s | 0.71s | curve value |
| → enemies/sec | ~1.0 | ~1.4 | unchanged |
| `EarlyRoundConcurrentMult` | ×0.40 | ×0.60 | ×1.0 |
| → max alive at once | 10 | 17 | 25 → 61 → … |
| `EarlyRoundSpeedMult` | ×0.80 | ×0.80 | ×1.0 |
| `EarlyRoundVarietyMult` | ×0 | ×1.0 | ×1.0 |
| → enemy mix | **Grunts only** | normal | normal |

`EarlyRoundVarietyMult` scales `_specialChance`, `_rareChance` **and** `_hiddenChance` together, so at `×0` all three rolls in `ChooseEnemyScene` fail and it falls through to `EnemyScene`. Round 1 is the player's first-ever look at the game and used to throw ~13.5% Rare, 5% Hidden and 5% Specials at them before they'd learned what a plain Grunt even does.

Note this is why Hidden's rate now lives in a `_hiddenChance` field: it was a flat `const` checked before everything else, which meant no multiplier could reach it. The `HiddenChance` constant is still the base value, it's just read through the field now.

Spawn *rate* and concurrent *cap* are separate levers and both matter: slowing the tick alone still lets a beginner accumulate 25 simultaneous enemies over a 60-second round. The cap is what bounds how overwhelming the screen gets; the interval controls how fast it fills.

These live as explicit arrays rather than curve tweaks because a linear `RoundCurve` **cannot** express "extra gentle at the start, then rejoin the normal ramp" — that shape is piecewise by definition, and re-sloping the underlying curves to soften the opening would drag every later round along with it. Keeping them as clearly-bounded overrides leaves the main curves meaning exactly what they say from round 3 on.

Note `EarlyRoundSpeedMult` multiplies `_speedMult`, which is also what `ConfigureForBossRound` evaluates — harmless, since the first boss round is round 5 and the tables are only 2 entries long.

Split children (see [enemies.md](enemies.md#splitting-enemysplitter)) inherit their stats from their already-scaled parent, so round scaling propagates through a splitter's lineage automatically without `Enemy.cs` needing direct access to the spawner's curves.

## Rough pacing at a glance

| | Round 1 | Round 2 | Round 5 | Round 10 | Round 18 |
|---|---|---|---|---|---|
| Spawn rate ceiling (burst ÷ interval) | ~1.0/s | ~1.4/s | ~4.3/s | ~14/s | ~58/s |
| Concurrent cap (the real constraint) | 10 | 17 | 41 | 61 | 61 |
| Enemy HP × | 1.0 | 1.25 | 2.0 | 3.25 | 5.25 |
| Contact damage × | 1.0 | 1.22 | 1.88 | 3.0 | 4.74 |
| Enemy speed × | 1.0 | 1.06 | 1.24 | 1.54 | 2.02 |
| XP/coins reward × | 1.0 | 1.35 | 2.4 | 4.15 | 6.95 |

The two top rows are easy to misread as one number: the spawn-rate row is a **ceiling**, not a throughput. From round ~8 on the cap is what actually binds (see the taper section above), so past that point the rate row only describes how fast the screen refills after a clear, and the cap row is what determines how crowded it is.

Enemy volume (spawn interval + burst size + concurrent cap) was deliberately softened from an earlier, steeper set of `PerRound` values that roughly doubled the total enemies spawned each round — round-over-round total enemy count now grows by roughly 30-40% instead of ~100%, while the eventual late-round ceilings (`Min`/`Max` clamps) are unchanged, just reached more gradually.

Enemy toughness (`HpMultCurve`/`DmgMultCurve`/`SpeedMultCurve`) was separately softened too — a second, distinct request from slowing enemy *volume* down: "escala de enemigos" here means how fast individual enemies get tankier/harder-hitting/faster per round, not how many spawn. All three `PerRound` rates were cut by roughly 25-30% (0.35→0.25 HP, 0.3→0.22 damage, 0.08→0.06 speed); the eventual `Max` ceilings are untouched, so kills still stay worth it relative to toughness, just ramping up more gradually. `RewardMultCurve` (kill payout) was untouched by *that* pass, and later raised 0.25→0.35 for a separate reason — compensating the late-game crowd taper's hit to XP income, see that section above. The level-up XP curve is unrelated to all of these — see the next section.

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

### Closing the "wall-hug + spam Ultimate" exploit

`GetOffensivePower()` used to only read persistent stat fields (`BulletDamage`, `FireRate`, `LaserLevel`, …), which meant a player who survives almost entirely on their Ultimate — hugging the arena wall, walking forward, and firing it the instant it's off cooldown — measured as *weak* to this system no matter how trivial the run actually was, since nothing about equipping an Ultimate touches any of those fields. Combined with `UltimateCooldownDuration` flooring at 3.5s from round 11 on, this made late rounds fully skippable: Pulso Nova (a flat 500 damage in a 500px radius, never scaled by round) simply wiped anything nearby every 3.5s regardless of how tanky enemies had gotten, and Zona Lenta's flat 6s duration outlasted that same 3.5s cooldown, giving up to 100% uptime on a 30%-speed slow — i.e. enemies that can, in practice, never catch a player who just keeps walking.

Two fixes, both in [Player.cs](../Scripts/Player/Player.cs):

1. **`GetOffensivePower()` now includes an `ultimateDps` term**, modeled as an average over one full cooldown cycle using the *current*, round-scaled `UltimateCooldownDuration` — Nova contributes `UltimateNovaDamage / cooldown` (a real average DPS), Zona Lenta and Sobrecarga contribute `baselineDps` / `gunDps` respectively, scaled by how much of the cooldown their effect actually covers (`GetUltimateEffectDuration() / cooldown`). This is what makes a Nova-only build read as genuinely powerful to `DifficultyBalancer`, which then hands back tougher enemies exactly as it already does for a runaway gun/Laser build — see the worked example below.
2. **`GetUltimateEffectDuration()` caps Zona Lenta/Sobrecarga's nominal 6s duration to `UltimateCooldownDuration × 0.8`** whenever the cooldown is short enough for that to bind (round 11+, where cooldown is 3.5s → capped duration ≈ 2.8s). This guarantees a real ~20% downtime window no matter how far the cooldown has shrunk, closing the *literal* 100%-uptime case directly and immediately — the balancer's reaction is round-by-round and shouldn't be the only thing standing between the player and a mechanically permanent effect.

This isn't about any single number crossing the line in isolation — `ultimateDps` simply *adds* to the same `total` every other weapon term already feeds into. Before this fix, a player who put their level-up picks toward survival-flavored rewards (Barrier, Heart, Regeneración) rather than raw kill power, and instead leaned on a 3.5s-cooldown Nova to do the actual clearing, had that entire playstyle read as *approximately zero* offensive power — `DifficultyBalancer` saw a harmless-looking build and never engaged the catch-up multiplier no matter how trivially the run was actually going. Now that reliance shows up in the same `total` as everything else, so it's measured on the same terms as a player who instead over-invested in, say, Orbit Blades or Missiles — which the balancer was already correctly catching.

## Reward tier odds use the same mechanism

`RewardTierRoller` (see [rewards.md](rewards.md)) has its own four `RoundCurve`s — one per tier — that shift the probability of rolling Common/Rare/Epic/Legendary as rounds progress. Structurally identical to the enemy curves above, just feeding a tier roll instead of a stat multiplier.

## Why this design

The previous version of the spawner used a raw elapsed-time ramp (a `Timer` ticking down the spawn interval regardless of round). That made difficulty depend on how long the app had been running, not on player progress, and it wasn't obviously tunable — the numbers were buried inline. Moving everything to named `RoundCurve` constants keyed strictly on `RoundNumber` makes the pacing deliberate, inspectable in one place per file, and safe to retune without touching any control-flow logic.
