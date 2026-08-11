# Rounds

A "round" is a fixed-duration wave. See [GameManager.cs](../Scripts/Autoload/GameManager.cs).

## Timing

- `RoundDuration` = 60 seconds (`[Export]` on `GameManager`, one shared timer for the whole run).
- The timer is a plain `Timer` node created in code (`GameManager._Ready`), not a scene child — `GameManager` is registered as a pure-script autoload with no `.tscn`.
- `GameManager.RoundTimeRemaining` exposes seconds left; the HUD polls it every frame (`HUD._Process`) to render the `m:ss` countdown.

## What happens when a round ends

`GameManager.OnRoundTimeout()` calls the shared `EndRound()`:
1. **Clears every enemy and every in-flight enemy bullet** (the `enemies` and `enemy_bullets` groups) — the board is always empty at the start of the round-end shop, regardless of what was still alive, mid-Split, or mid-air when the round ended. Sweeping the bullets is a fairness fix as much as a tidiness one: a shot frozen by the shop's pause would otherwise resume afterwards and land on the player during the next round's "get ready" countdown, before they can meaningfully react.
2. **Stops the spawner's own timer** (`EnemySpawner.StopSpawning()`). This one matters more than it looks: a Godot `Timer` only *freezes* its remaining time while the tree is paused, it doesn't reset — so left running, it could have almost no time left by the time the shop closes, firing an immediate spawn burst the instant the tree unpauses and completely ignoring the round-start countdown below. This was a real bug (enemies visibly spawning during the "Preparate..." countdown) fixed by explicitly stopping it here, so the only thing that ever restarts it is `ConfigureForRound`/`ConfigureForBossRound` once the countdown actually elapses.
3. Opens the [Shop](rewards.md#the-shop-round-end-rewards) (`Shop.Open()`).
4. Pauses the game (`GameManager.Pause()` — sets `SceneTree.Paused = true`; the shop UI is one of the few nodes with `ProcessMode.Always` so it keeps responding to input while everything else freezes).

A boss round doesn't use the timer at all — see [Boss Rounds](#boss-rounds) below for how it ends instead.

## Starting the next round

Pressing "Next Round" in the shop calls `GameManager.StartNextRound()`, which:
1. Increments `RoundNumber` and fires `RoundChanged` (HUD updates the "Round N" label).
2. Fully heals the player's lives (`Player.HealFullLives()`) **and** tops their shield charges back to 100% (`Player.RefillShield()`). Every round starts at full health and full shield regardless of how banged-up you were at the end of the previous one.
3. Unpauses (`GameManager.Resume()`) — but does **not** yet spawn anything.
4. Starts a 5-second "get ready" countdown (`_roundStartTimer`, `RoundStartDelay = 5f`, exposed as `GameManager.RoundStartTimeRemaining`/`IsRoundStarting`). `HUD._Process` checks `IsRoundStarting` first, ahead of the boss-round check, and shows the remaining seconds in a dedicated **`CountdownLabel`** — 72px, yellow, centered on screen — while the small top-bar timer just reads "Preparate...". The countdown originally lived in that corner label alone and was effectively invisible there.
5. When the countdown elapses, `BeginRoundAfterCountdown()` branches on `IsBossRound` (`RoundNumber % 5 == 0`):
   - **Boss round**: `EnemySpawner.ConfigureForBossRound(RoundNumber)` (spawns one Boss scaled to this round) and triggers `BossBanner.Announce(RoundNumber)`.
   - **Normal round**: `EnemySpawner.ConfigureForRound(RoundNumber)` (starts normal spawning) and starts the 60s round timer — see [difficulty-scaling.md](difficulty-scaling.md) for exactly what that changes.

The player can move around freely during the 5s countdown (the game is already unpaused) — there's just nothing to fight yet, since spawning is deliberately deferred until it ends.

## Boss Rounds

Every 5th round (5, 10, 15, ...) is a Boss Round: `GameManager.IsBossRound => RoundNumber % 5 == 0`.

- The Boss only appears once the 5s "get ready" countdown elapses (see above); a red **"¡RONDA DE JEFE!"** banner (`BossBanner.cs`/`.tscn`, in its own `CanvasLayer`) announces the round right as it spawns, without pausing gameplay, fading out after ~2 seconds.
- The normal enemy spawner stays stopped for the whole round — only the Boss is present (any straggling enemies from the previous round were already cleared by `EndRound()`).
- **There is no timer during a boss round** — the round only ends when the Boss dies. `RegisterKill` detects `EnemyCategory.Boss` and *queues* the round end (`_pendingRoundEnd`) rather than calling `EndRound()` outright: the boss's own XP payout may already have opened the level-up picker earlier in the same call, and dropping the shop straight on top of it would lose that pick (the shop's `CanvasLayer` sits above the picker's). `ResolveNextInterstitial()` drains the queue one modal at a time — level-ups first, then the first-boss Ultimate choice, then the shop. Its Score/XP payout is just its own round-scaled `XpReward`, same as any other enemy — see [enemies.md](enemies.md).
- Killing the **first** boss of a run additionally grants a free choice of Ultimate — see [rewards.md](rewards.md#ultimates).
- The Boss itself scales with the same round-based multipliers (`HpMultCurve`/`DmgMultCurve`/`SpeedMultCurve`/`RewardMultCurve`) as every other enemy, evaluated at the boss round's number — a round-15 boss is tougher than a round-5 one.
- The Boss is immune to the level-up nova's instant kill (see [player.md](player.md#level-up-nova)) — otherwise any level-up that happened to fire while the boss was in range would delete it in a single hit regardless of the actual fight, undermining the whole point of a boss encounter. It can only be brought down through sustained damage.
- The HUD shows "¡JEFE!" in place of the countdown while `IsBossRound` is true (the round timer is stopped, so there's nothing meaningful to count down).

## Round number as the master difficulty/economy knob

`RoundNumber` is the single input that drives almost every other scaling system in the game:
- Enemy spawn rate, burst size, concurrent cap, special-enemy chance, and enemy HP/damage/speed/reward multipliers — all functions of `RoundNumber` (see [difficulty-scaling.md](difficulty-scaling.md)).
- Shop price inflation (`UpgradeCostMultiplier`) — see [economy.md](economy.md).
- Reward tier odds (`RewardTierRoller.GetWeights(round)`) — see [rewards.md](rewards.md).

There is intentionally no other notion of "elapsed time" driving difficulty — everything is round-indexed so the whole curve can be retuned by editing the handful of `RoundCurve` constants described in [difficulty-scaling.md](difficulty-scaling.md).
