# Rounds

A "round" is a fixed-duration wave. See [GameManager.cs](../Scripts/Autoload/GameManager.cs).

## Timing

- `RoundDuration` = 60 seconds (`[Export]` on `GameManager`, one shared timer for the whole run).
- The timer is a plain `Timer` node created in code (`GameManager._Ready`), not a scene child — `GameManager` is registered as a pure-script autoload with no `.tscn`.
- `GameManager.RoundTimeRemaining` exposes seconds left; the HUD polls it every frame (`HUD._Process`) to render the `m:ss` countdown.

## What happens when a round ends

`GameManager.OnRoundTimeout()` calls the shared `EndRound()`:
1. **Clears every enemy and every in-flight enemy bullet** (the `enemies` and `enemy_bullets` groups) — the board is always empty at the start of the round-end shop, regardless of what was still alive, mid-Split, or mid-air when the round ended. Sweeping the bullets is a fairness fix as much as a tidiness one: a shot frozen by the shop's pause would otherwise resume afterwards and land on the player during the next round's "get ready" countdown, before they can meaningfully react.

   **Pickups deliberately survive this sweep** (they used to be cleared here alongside the other two). A Heart/Shield/XP/coin drop that lands in the closing seconds of a round was *earned*, and deleting it meant the kill that produced it silently paid nothing. Because their 10-second lifetime would otherwise expire almost immediately into the new round, `StartNextRound` calls `PickupBase.RefreshLifetime()` on everything still in the `pickups` group — resetting both the age and the partially-applied fade-out, so a carried-over drop gets a full, fully-opaque window to be collected.
2. **Stops the spawner's own timer** (`EnemySpawner.StopSpawning()`). This one matters more than it looks: a Godot `Timer` only *freezes* its remaining time while the tree is paused, it doesn't reset — so left running, it could have almost no time left by the time the shop closes, firing an immediate spawn burst the instant the tree unpauses and completely ignoring the round-start countdown below. This was a real bug (enemies visibly spawning during the "Preparate..." countdown) fixed by explicitly stopping it here, so the only thing that ever restarts it is `ConfigureForRound`/`ConfigureForBossRound` once the countdown actually elapses.
3. Opens the [Shop](rewards.md#the-shop-round-end-rewards) (`Shop.Open()`).
4. Pauses the game (`GameManager.Pause()` — sets `SceneTree.Paused = true`; the shop UI is one of the few nodes with `ProcessMode.Always` so it keeps responding to input while everything else freezes).

A boss round doesn't use the timer at all — see [Boss Rounds](#boss-rounds) below for how it ends instead.

## The round recap

`BeginRoundEnd()` is the single entry point for "the round is over" — called from both `OnRoundTimeout()` and a boss kill. It puts [`RoundSummary`](../Scripts/UI/RoundSummary.cs) on screen *first*, then hands off to `ResolveNextInterstitial()`.

The recap therefore stays visible for the whole interstitial chain — level-up picks, the first-boss Ultimate choice, and the shop — so the round's numbers are still in front of you while you decide what to spend them on. `StartNextRound()` takes it down, which is exactly the moment the "get ready" countdown begins.

It shows enemies killed, elite/boss kills, coins earned, score earned, and levels gained (that last line only when it actually happened, so it doesn't read as a permanent "0"). Every one of those counters on `GameManager` is **cumulative for the run**, so the recap diffs against a snapshot taken by `SnapshotRoundStart()` at the start of each round (and in `ResetRun`).

Two placement details worth knowing:
- It sits on its own `CanvasLayer` at **layer 17 — above the shop's 15**, because the shop draws a full-screen dim that would otherwise grey the recap out.
- Being above the shop means it would also sit on top of the shop's buttons, so `RoundSummary` sets `MouseFilter = Ignore` on itself and every child in the scene. It's purely decorative and must never swallow a click meant for a Buy button.

## Starting the next round

Pressing "Next Round" in the shop calls `GameManager.StartNextRound()`, which:
1. Increments `RoundNumber` and fires `RoundChanged` (HUD updates the "Round N" label).
2. Hides the [round recap](#the-round-recap) and re-snapshots the per-round counters.
3. Fully heals the player's lives (`Player.HealFullLives()`) **and** tops their shield charges back to 100% (`Player.RefillShield()`). Every round starts at full health and full shield regardless of how banged-up you were at the end of the previous one.
4. Unpauses (`GameManager.Resume()`) — but does **not** yet spawn anything.
5. Starts a 3-second "get ready" countdown (`_roundStartTimer`, `RoundStartDelay = 3f`, exposed as `GameManager.RoundStartTimeRemaining`/`IsRoundStarting`). `HUD._Process` checks `IsRoundStarting` first, ahead of the boss-round check, and shows the remaining seconds in a dedicated **`CountdownLabel`** — 72px, yellow, centered on screen — while the small top-bar timer just reads "Preparate...". The countdown originally lived in that corner label alone and was effectively invisible there.
6. When the countdown elapses, `BeginRoundAfterCountdown()` branches on `IsBossRound` (`RoundNumber % 5 == 0`):
   - **Boss round**: `EnemySpawner.ConfigureForBossRound(RoundNumber)` (spawns one Boss scaled to this round) and triggers `BossBanner.Announce(RoundNumber)`.
   - **Normal round**: `EnemySpawner.ConfigureForRound(RoundNumber)` (starts normal spawning) and starts the 60s round timer — see [difficulty-scaling.md](difficulty-scaling.md) for exactly what that changes.

The player can move around freely during the 3s countdown (the game is already unpaused) — there's just nothing to fight yet, since spawning is deliberately deferred until it ends. Before a boss round the countdown is also when the danger alarm fires its pre-arrival burst (see [visuals.md](visuals.md#danger-escalation)).

## Boss Rounds

Every 5th round (5, 10, 15, ...) is a Boss Round: `GameManager.IsBossRound => RoundNumber % 5 == 0`.

- The Boss only appears once the 3s "get ready" countdown elapses (see above); a red **"¡RONDA DE JEFE N!"** banner (`BossBanner.cs`/`.tscn`, in its own `CanvasLayer`) announces the round right as it spawns, without pausing gameplay, fading out after ~2 seconds. The banner now also carries a row of hazard chevrons along its top edge, and the Boss's arrival kicks a short camera shake — see [visuals.md](visuals.md#danger-escalation).
- The spawner keeps running, but **only spawns Grunts, and roughly a third as many as a normal round would**. `ConfigureForBossRound` clears the board (any stragglers from the previous round were already cleared by `EndRound()` anyway), then sets `_forcedSpawnScene = EnemyScene` so `OnSpawnTimeout` bypasses `ChooseEnemyScene()` entirely, and scales the interval/burst/cap by `BossRoundIntervalMult`/`BossRoundCrowdMult`. The chaff is there to crowd the player's movement while the Boss remains the only real threat — and keeping it Common-only means the Boss stays the sole non-Common on the field, so `RoundSummary`'s "De élite / jefes" line still reads exactly 1.

  Resulting chaff cap (plus the Boss): ~15 at round 5, ~22 at round 10, ~23 at round 15, ~24 at round 20. The `+1` in that calculation is the Boss's own slot — it joins the `enemies` group too, and the cap is enforced by counting that group.

  Note this method has to set **every** spawn field explicitly. It used to just `Stop()` the timer, so none of them were ever assigned for a boss round; simply restarting the timer would have silently inherited the previous round's interval, burst and cap wholesale.

  Side effect worth knowing: `Player.TriggerLevelUpBurst` excludes only `EnemyCategory.Boss`, so a level-up during a boss round is now a real screen-clear of the chaff rather than the no-op it used to be.
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
