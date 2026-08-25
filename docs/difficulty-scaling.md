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
| `LateCrowdMultCurve` | 1.112 | −0.028 | 0.66 | 1.0 | crowd **taper**, see below |
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

**`LateCrowdMultCurve`** was reversed from a bump into a taper: flat **1.0× through round 5**, down to **0.66× by round ~17**, held from there. Because the mix above is simultaneously swapping 30 HP Grunts for ~82 HP (weighted average) Specials, average HP per body rises ~30% — so the same threat needs meaningfully fewer bodies, and the old bump was piling extra count on top of an already-heavier mix. Holding at `0.66` rather than tapering forever is deliberate: `MaxConcurrentCurve` keeps adding +4/round underneath, so once the taper bottoms out — the same round commons hit zero — the count **resumes climbing on its own**, with no separate re-climb mechanism.

The taper's onset was later moved from round 10 to round 5 (Base/PerRound only — the 0.66 floor and the round it's reached are essentially unchanged) because rounds 6-11 turned out to be a real gap: nothing eased the crowd there at all (the taper was still clamped flat at 1.0), while `MaxConcurrentCurve`/`BurstCountCurve`/`SpawnIntervalCurve` were all climbing hard and the rounds 1-3 onboarding cushion below had long since worn off. The table right below predates that move — its round-10/12/14/16 "Crowd multiplier"/"Concurrent cap (new)" cells are now a little lower than shown (round 18 onward is unaffected, both curves reach the same 0.66 floor by then).

| Round | 10 | 12 | 14 | 16 | 18 | 20 | 25 |
|---|---|---|---|---|---|---|---|
| Suppression | 0.00 | 0.25 | 0.50 | 0.75 | 1.00 | 1.00 | 1.00 |
| Commons (% of spawns) | 31.8% | 18.5% | 12.4% | 6.2% | **0%** | 0% | 0% |
| Specials (% of spawns) | 47.5% | 63.2% | 69.4% | 75.5% | **81.7%** | 81.7% | 81.7% |
| Crowd multiplier | 1.00 | 0.915 | 0.830 | 0.745 | 0.66 | 0.66 | 0.66 |
| Concurrent cap (new) | 61 | 63 | 64 | 63 | **61** | 67 | 80 |
| Concurrent cap (old) | 61 | 78 | 92 | 102 | 112 | 121 | 145 |

Rounds 1–10 are bit-for-bit unchanged by both curves (suppression clamps to 0, crowd multiplier clamps to 1.0). Hidden holds its flat 5% throughout, and Rare holds ~13.3% from round 12 on.

Both `Base` values look arbitrary out of context and are derived the same way: `Evaluate` is `Base + (round-1) × PerRound` clamped to `[Min, Max]`, so `−1.125` is what pins suppression's clamps to rounds 10 and 18, and `1.112` is what pins the crowd taper's Max clamp to round 5 (its Min clamp lands around round 17, a round earlier than suppression's — the two curves no longer share both endpoints, only roughly the floor).

**Net effect and the knob to turn.** Total HP on screen at round 18 drops ~29% (46% fewer bodies × ~30% more HP each), so the late game is genuinely more survivable — which was the goal. If it proves *too* soft in playtesting, raise `LateCrowdMultCurve`'s `Min` (0.66 → 0.75 cuts the count reduction from −46% to −38%) rather than touching the composition curve; the two are independent.

`RewardMultCurve`'s `PerRound` was raised 0.25 → 0.35 alongside this. Coins largely self-correct — [`UpgradeCostMultiplier`](economy.md) indexes on `TotalCoinsEarned` rather than round number, so lower income also means cheaper prices — but XP has no such damper, and at the old rate ~46% fewer bodies would have cut XP income ~32%, directly slowing the level-ups that are the only source of free rewards. The bump brings that to roughly −10%.

**A prerequisite bug fix made the cap meaningful at all.** `OnSpawnTimeout` used to enforce it with `_enemiesContainer.GetChildCount()`, but enemies parent their score popups, damage numbers, and fired bullets into that same container — so "max concurrent enemies" was really "enemies + transient VFX + bullets", binding far earlier than its number suggested, worst exactly in the busy late rounds. It now counts the `enemies` group instead, read once per tick and tracked locally rather than re-queried per burst item.

**Known gap:** `Enemy.Split()` calls `GetParent().AddChild(child)` with no cap check — the cap is only enforced at spawner tick time, so a Splitter-heavy wave can transiently push the live count above `_maxConcurrentEnemies` (one Splitter eventually becomes 15 bodies). This is pre-existing, and it's part of why Splitter carries the lowest weight in the special pool (see [enemies.md](enemies.md#special-enemy-weights)); a real fix would mean exposing `_maxConcurrentEnemies` and consulting it inside `Split()`.

### Where enemies appear

`EnemySpawner.ChooseSpawnPosition()` picks a random angle at a distance of `max(SpawnRadius, player.FireRange + SpawnClearanceMargin)`. The `FireRange` term matters: the player's auto-fire ring grows with Fire Range upgrades, and an enemy appearing *inside* that ring reads as materializing on top of you — deriving the clearance from the live ring guarantees that can't happen no matter how far the ring has been upgraded.

It then re-rolls the angle up to 8 times to find a spot inside `ArenaHalfExtents`. Near an arena edge a random angle easily lands off-map, which would otherwise leave the enemy with a long walk in from nowhere. If every attempt is out of bounds (player cornered), it uses the first pick and lets the enemy walk in rather than skipping the spawn entirely.

### Early-round grace (onboarding cushion)

Three parallel tables cushion rounds 1–3 only, all tapering back to `1×` from round 4 on. They're read through one shared `EarlyRoundMult(table, round)` helper:

| | Round 1 | Round 2 | Round 3+ |
|---|---|---|---|
| `EarlyRoundIntervalMult` | ×1.60 | ×1.25 | ×1.0 |
| → effective spawn interval | 0.96s | 0.71s | curve value |
| → enemies/sec | ~1.0 | ~1.4 | unchanged |
| `EarlyRoundConcurrentMult` | ×0.40 | ×0.60 | ×1.0 |
| → max alive at once | 10 | 17 | 25 → 61 → … |
| `EarlyRoundSpeedMult` | ×0.80 | ×0.80 | ×1.0 |

`EarlyRoundVarietyMult` uses the same helper but is no longer a 3-round table — see below.

### Mid-game variety dip (rounds 6-11)

`EarlyRoundVarietyMult` scales `_specialChance`, `_rareChance` **and** `_hiddenChance` together. At round 1 it's `×0`, so all three rolls in `ChooseEnemyScene` fail and it falls through to `EnemyScene` — the player's first-ever look at the game used to throw ~13.5% Rare, 5% Hidden and 5% Specials at them before they'd learned what a plain Grunt even does.

It carries a second dip past round 3, because by round 6 alone `SpecialChanceCurve`/`RareChanceCurve` are already at 30%/25% with nothing easing them — the crowd taper above didn't reach that far back either before its own fix — so rounds 6-11 stacked hard variety on top of hard density with no cushion at all:

| Round | 1 | 2–5 | 6 | 7 | 8 | 9 | 10 | 11 | 12+ |
|---|---|---|---|---|---|---|---|---|---|
| `EarlyRoundVarietyMult` | ×0 | ×1.0 | ×0.75 | ×0.65 | ×0.60 | ×0.60 | ×0.65 | ×0.75 | ×1.0 |
| Special chance (effective) | 0% | curve | 22% | 23% | 24% | 27% | 33% | 41% | curve |
| Rare chance (effective) | 0% | curve | 19% | 18% | 17% | 19% | 21% | 26% | curve |

A `RoundCurve` can't express "ease, hold the normal ramp, dip again, resume" — that shape is piecewise by definition — so this is the same per-round-override-table mechanism as the rounds 1-3 cushion, just with more entries; nothing past index 10 is listed because `EarlyRoundMult` falls back to `1f` once the round is past the table's length, same as it always has for rounds 4/5 today.

Note this is why Hidden's rate now lives in a `_hiddenChance` field: it was a flat `const` checked before everything else, which meant no multiplier could reach it. The `HiddenChance` constant is still the base value, it's just read through the field now.

Spawn *rate* and concurrent *cap* are separate levers and both matter: slowing the tick alone still lets a beginner accumulate 25 simultaneous enemies over a 60-second round. The cap is what bounds how overwhelming the screen gets; the interval controls how fast it fills.

These live as explicit arrays rather than curve tweaks because a linear `RoundCurve` **cannot** express "extra gentle at the start, then rejoin the normal ramp" — that shape is piecewise by definition, and re-sloping the underlying curves to soften the opening would drag every later round along with it. Keeping them as clearly-bounded overrides leaves the main curves meaning exactly what they say from round 3 on.

Note `EarlyRoundSpeedMult` multiplies `_speedMult`, which is also what `ConfigureForBossRound` evaluates — harmless, since the first boss round is round 5 and the tables are only 2 entries long.

Split children (see [enemies.md](enemies.md#splitting-enemysplitter)) inherit their stats from their already-scaled parent, so round scaling propagates through a splitter's lineage automatically without `Enemy.cs` needing direct access to the spawner's curves.

## Rough pacing at a glance

| | Round 1 | Round 2 | Round 5 | Round 10 | Round 18 |
|---|---|---|---|---|---|
| Spawn rate ceiling (burst ÷ interval) | ~1.0/s | ~1.4/s | ~4.3/s | ~14/s | ~58/s |
| Concurrent cap (the real constraint) | 10 | 17 | 37 | 52 | 54 |
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
3. **Correct.** `GetCatchUpMultiplier` returns `Clamp(1 + CatchUpLogStrength × ln(power/expected), 1, MaxCatchUp)` with `CatchUpLogStrength = 0.6` and `MaxCatchUp = 5`.

   This used to be linear (`1 + (overshoot-1) × 0.3`, capped at `2.5`), and that saturated far too early to matter in practice: the cap was reached at just `6×` overshoot, but a "decent, non-exploit" round-20 build (fire rate near cap, solid damage/crit/pierce, one extra weapon system) measures at **300-650× overshoot** — so the balancer went inert from roughly round 12-15 onward for almost anyone shopping normally, long before any run reached round 20. A log response fixes this without needing a menu of ad-hoc thresholds: each further *order of magnitude* of overshoot buys the same fixed increment instead of needing linearly more room, so the same formula stays gentle at `2×` and is still doing real work at `650×`. The new saturation point is `~786×` overshoot — about 130× wider than before, and above everything measured. Total enemy HP ceiling moves from `HpMultCurve.Max(8) × 2.5 = 20×` round-1 baseline to `8 × 5 = 40×` (a base Grunt tops out around 1200 HP, an elite around 1800 — still nowhere near the point of tedious fights or UI trouble, since only a build already deep in overshoot territory ever gets there, and that same build kills just as fast in absolute terms).

**It is deliberately one-directional.** The multiplier floors at `1`, so a player *below* the curve gets no help whatsoever — this is a ceiling-limiter, not rubber-banding. Falling behind is allowed to be hard; running away with it shouldn't be free.

`EnemySpawner.EvaluateStatCurves(round)` applies it to **`_hpMult` and `_dmgMult` only**, once per round alongside every other curve. `_speedMult` and especially `_rewardMult` are left alone on purpose — an over-powered player shouldn't earn *less* per kill for the same work, which would turn the correction into a punishment spiral.

Why it's imperceptible early: at rounds 1–3 a normally-progressing player sits at or just under `ExpectedPowerCurve`, so the multiplier is `1.00`–`1.05`. It only bites once someone is genuinely multiples ahead. The pause menu shows both numbers live (`Poder: 4.2x   Ajuste enemigo: 1.15x`) so the system is inspectable while playtesting instead of silently changing enemy stats.

### Closing the "wall-hug + spam Ultimate" exploit

`GetOffensivePower()` used to only read persistent stat fields (`BulletDamage`, `FireRate`, `LaserLevel`, …), which meant a player who survives almost entirely on their Ultimate — hugging the arena wall, walking forward, and firing it the instant it's off cooldown — measured as *weak* to this system no matter how trivial the run actually was, since nothing about equipping an Ultimate touches any of those fields. Combined with `UltimateCooldownDuration` flooring at 3.5s from round 11 on, this made late rounds fully skippable: Pulso Nova (a flat 500 damage in a 500px radius, never scaled by round) simply wiped anything nearby every 3.5s regardless of how tanky enemies had gotten, and Zona Lenta's flat 6s duration outlasted that same 3.5s cooldown, giving up to 100% uptime on a 30%-speed slow — i.e. enemies that can, in practice, never catch a player who just keeps walking.

Two fixes, both in [Player.cs](../Scripts/Player/Player.cs):

1. **`GetOffensivePower()` now includes an `ultimateDps` term**, modeled as an average over one full cooldown cycle using the *current*, round-scaled `UltimateCooldownDuration` — Nova contributes `UltimateNovaDamage / cooldown` (a real average DPS), Zona Lenta and Sobrecarga contribute `baselineDps` / `gunDps` respectively, scaled by how much of the cooldown their effect actually covers (`GetUltimateEffectDuration() / cooldown`). This is what makes a Nova-only build read as genuinely powerful to `DifficultyBalancer`, which then hands back tougher enemies exactly as it already does for a runaway gun/Laser build — see the worked example below.
2. **`GetUltimateEffectDuration()` caps Zona Lenta/Sobrecarga's nominal 6s duration to `UltimateCooldownDuration × 0.8`** whenever the cooldown is short enough for that to bind (round 11+, where cooldown is 3.5s → capped duration ≈ 2.8s). This guarantees a real ~20% downtime window no matter how far the cooldown has shrunk, closing the *literal* 100%-uptime case directly and immediately — the balancer's reaction is round-by-round and shouldn't be the only thing standing between the player and a mechanically permanent effect.

This isn't about any single number crossing the line in isolation — `ultimateDps` simply *adds* to the same `total` every other weapon term already feeds into. Before this fix, a player who put their level-up picks toward survival-flavored rewards (Barrier, Heart, Regeneración) rather than raw kill power, and instead leaned on a 3.5s-cooldown Nova to do the actual clearing, had that entire playstyle read as *approximately zero* offensive power — `DifficultyBalancer` saw a harmless-looking build and never engaged the catch-up multiplier no matter how trivially the run was actually going. Now that reliance shows up in the same `total` as everything else, so it's measured on the same terms as a player who instead over-invested in, say, Orbit Blades or Missiles — which the balancer was already correctly catching.

### Survivability catch-up

`GetOffensivePower()` only measures damage output, so a build that stacks Dodge, the Tank class's free-shrug passive, a maxed Barrier, and Regeneración reads as *weak* to it — plausibly the opposite of the truth, since that combination can make the player nearly unkillable regardless of how tough enemies get. Raising enemy toughness can't fix this either way: every hit in this game costs a flat, fixed `1` life or `1` shield charge (`Player.TakeHit` takes no damage parameter at all, and `Enemy.ContactDamage` — despite being scaled up every round — is never read against the player anywhere; see [enemies.md](enemies.md#contact-damage-no-longer-matters-for-the-player)). "Enemies hit harder" simply isn't a lever that exists today.

So this is a second, parallel mechanism rather than a tweak to the one above:

1. **Measure.** [`Player.GetSurvivabilityScore()`](../Scripts/Player/Player.cs) returns a ratio against a round-1 baseline of `1.0` (`MaxLives=3`, no shield/dodge/Tank/regen), the same contract `GetOffensivePower()` uses. It treats `MaxLives + MaxShieldCharges` as an absorbable pool, stretched by `1/(1-DodgeChance)` and (if the Tank class is active) `1/(1-0.25)` — since Dodge/Tank make each point of that pool absorb more incoming hits before it's actually spent — then adds `ShieldRegenPerMinute` on top, since regen refills the pool rather than being part of it.
2. **Compare.** `ExpectedSurvivabilityCurve = RoundCurve(1, +0.05, 1, 1.6)` — deliberately much flatter than `ExpectedPowerCurve`, since most of a normal build's survivability comes from a couple of Heart/Barrier pickups, not a curve that keeps climbing all game.
3. **Correct.** `GetSurvivabilityCatchUpMultiplier` returns `Clamp(1 + (ratio/expected - 1) × 0.25, 1, 2.0)` — linear, unlike the offensive side, since survivability stacking has nowhere near the same multiplicative-across-many-systems blowup that damage does (it's a handful of additive/reciprocal terms, not several independently-maxable weapon systems multiplying together).

The result isn't applied to enemy stats — there's nothing to apply it *to*, since damage magnitude doesn't exist. Instead `EnemySpawner.EvaluateStatCurves` publishes it as `GameManager.SurvivabilityCatchUpMultiplier` once per round, and `Player.TakeHit` converts it into an integer hit cost via `ComputeHitCost()`: a running accumulator (`_hitCostAccumulator += multiplier - 1`, emitting a `2` whenever it crosses `1`) rather than a second dice roll layered on top of Dodge/Tank — a `1.5×` multiplier means "every other hit costs double", not added variance. Dodge, the Armored class's reflect, and Thorns are untouched: they still fully negate a hit before cost is ever computed. The Tank shrug check runs *per point of cost* rather than once per hit — a cost-2 hit is conceptually "hit twice", and two separate `TakeHit` calls would already give Tank two independent rolls today, so this preserves the existing rule rather than bending it.

Worked example, round 20: a heavily-tanked build (`MaxLives=4, MaxShieldCharges=4, DodgeChance=25, Tank active, Regeneración=8`) scores `(4+4) × 1.33 × 1.33 + 8 = 22.2`, a ratio of `7.4` against the `1.6` expected — multiplier `≈1.9`, so nearly every landed hit costs `2`. A normal build (`MaxLives=4`, nothing else) scores `4`, a ratio of `1.33` — *below* the `1.6` expected, so the multiplier is exactly `1.0` and nothing changes. Same one-directional guarantee as the offensive side: a build that hasn't stacked defense sees zero difference.

## Reward tier odds use the same mechanism

`RewardTierRoller` (see [rewards.md](rewards.md)) has its own four `RoundCurve`s — one per tier — that shift the probability of rolling Common/Rare/Epic/Legendary as rounds progress. Structurally identical to the enemy curves above, just feeding a tier roll instead of a stat multiplier.

## Why this design

The previous version of the spawner used a raw elapsed-time ramp (a `Timer` ticking down the spawn interval regardless of round). That made difficulty depend on how long the app had been running, not on player progress, and it wasn't obviously tunable — the numbers were buried inline. Moving everything to named `RoundCurve` constants keyed strictly on `RoundNumber` makes the pacing deliberate, inspectable in one place per file, and safe to retune without touching any control-flow logic.
