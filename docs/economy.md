# Economy: Coins, Score, Libras & Shop Pricing

Three separate currencies exist. Confusing them is the most common bug source here.

## Coins (`GameManager.Coins`)

- The **spendable wallet**. Earned per kill, spent in the shop.
- The **Botín** reward (`UpgradeType.CoinBonus`, up to +150%) multiplies the per-kill payout via `Player.CoinMultiplier`, applied in `RegisterKill`. It scales coins *only* — XP and Score are untouched, so stacking it can't accelerate leveling or inflate the high score, just the shop budget. Note it does feed the wealth-based price inflation below, so a coin-heavy build also sees prices climb faster.
- `AddCoins(amount)` / `SpendCoins(amount)` — see [GameManager.cs](../Scripts/Autoload/GameManager.cs).
- Resets to 0 on `ResetRun()` (new game).

## Score (`GameManager.Score`)

- The **cumulative run score**, shown on the HUD, pause menu, and the Game Over screen ("Puntaje Final").
- **Score tracks *base* XP, not boosted XP.** The **Sabiduría** reward (`UpgradeType.XpBonus`, Common +20% / Epic +50%, up to +100%) multiplies the XP fed to `AddXp` but **not** the value passed to `AddScore`. Score is persisted as a high score, so letting a reward choice inflate it would make runs incomparable.
- **Otherwise synced 1:1 with XP**: `RegisterKill` calls `AddXp(xpReward)` and `AddScore(xpReward)` with the same base number — Score is, literally, "cumulative XP earned this run." This replaced an earlier flat per-category table (10/15/20/30/1000) that didn't scale with `RoundNumber` the way XP does, so the two numbers used to drift apart. Coins remain entirely separate and untouched by this — Score/XP sync has nothing to do with the spendable wallet.
- A side effect worth knowing: a mid-chain Splitter kill grants 0 XP (see [enemies.md](enemies.md#splitting-enemysplitter)) and therefore now also adds 0 Score — only the terminal kill in a split lineage pays out either. Coins still pay out on every kill in the chain.
- Resets to 0 on `ResetRun()` (a new run starts scoring from zero — this is separate from the persistent high score, see below).
- Two pieces of visual feedback on a gain, both purely cosmetic: `Enemy.SpawnScorePopup` spawns a "+10"/"+20" `Label` at the death position that drifts up and fades out via a `Tween` (skipped entirely when the gain is 0, e.g. a mid-chain Splitter kill), and `HUD.OnScoreChanged` appends the delta to the total for ~1.2s (`Puntaje: 1000 (+10)`, via a one-shot `Timer`) before reverting to just the total.

## High score (persisted across runs)

- `GameManager.LoadHighScore()` / the private `SaveHighScore(int)` read/write a single integer to `user://highscore.save` (plain text, one line) via Godot's `FileAccess` — this is local, per-install save data, not tied to any account or cloud sync.
- `GameManager.NotifyPlayerDied()` calls `RegisterFinalScore()` right when a run ends, which compares the just-finished `Score` against the saved value and overwrites it only if the run's Score is higher.
- [MainMenu.cs](../Scripts/UI/MainMenu.cs) reads it with the static `GameManager.LoadHighScore()` in `_Ready()` and shows it on the main menu — both on a fresh app launch and whenever you return to the menu via the Game Over screen's "Menú Principal" button.

## Libras (`GameManager.Libras`)

The **persistent meta-currency** — the only one of the three that survives death. Deliberately kept
apart from both Coins (resets every run, never touches disk) and Score (run-scoped, and the only thing
of it that persists is a read-only high score/record — not something spendable). Formerly called
Núcleos; renamed with no change to the underlying mechanism beyond the earn formula below.

- **Earned once, at run end, counted from round 1** — no free/unpaid opening rounds.
  `RegisterFinalScore()` — the single point both exit paths (death via `NotifyPlayerDied()`, manual quit
  via `AbandonRun()`) already funnel through — awards `Max(0, RoundNumber - LibrasFreeRounds) ×
  LibrasPerRound` (`LibrasFreeRounds` currently `0`, `LibrasPerRound` currently `1` — first-guess
  constants pending playtesting; `LibrasFreeRounds` exists as the one knob to reintroduce a grace period
  later without touching the formula) and calls `AddLibras`, read *before* `ResetRun()` zeroes
  `RoundNumber`. The amount is cached on `GameManager.LastRunLibrasEarned` so
  [GameOverScreen.cs](../Scripts/UI/GameOverScreen.cs) can show "+N" without recomputing the formula or
  racing the reset.
- **Persisted immediately on every change**, unlike Coins — `AddLibras`/`TryUnlockCharacter`
  both write straight to `user://settings.cfg` (`libras` key) rather than waiting for some
  later save point, since the balance has to survive the app being killed mid-run on a phone.
- **Spent on characters** — see [characters.md](characters.md#locked-characters--libras) for
  `CharacterInfo.RequiresUnlock`/`UnlockCost` and `GameManager.TryUnlockCharacter`. Bought from the
  Tienda's own "Personajes" tab now, not from character select — see below.
- **Spent on cosmetics** — [CosmeticCatalog.cs](../Scripts/Player/CosmeticCatalog.cs) lists a shared
  set of 15 colours purchasable independently across 9 categories (bullets — which also colour the
  fire-range ring, since it marks where those bullets reach — ship trail, character outline,
  arena/grid accent, shield aura, orbit blades, portrait marco, HUD border, and the on-kill effect),
  bought and equipped from the "Tienda" screen ([CosmeticsShopMenu.cs](../Scripts/UI/CosmeticsShopMenu.cs)).
  135 unlockables in total. Purely visual — no category affects gameplay. `GameManager.TryBuyCosmetic`/
  `EquipCosmetic` are the mutators, same no-signal shape as `TryUnlockCharacter`; ownership lives in
  `GameManager.OwnedCosmetics`, keyed by `CosmeticCatalog.ItemKey(category, id)` since owning a
  colour for one category says nothing about owning it for another. Each category also has a free,
  always-owned "Original" option that reverts to how that system looked before this feature existed —
  the whole thing is opt-in. The one exception is `KillEffect`: there was no on-kill visual before
  this category existed, so its "Original" is just a sensible default rather than a reproduction of
  prior behaviour.
- **Marco and HUD colour a UI panel border, not a `CanvasItem` property**, so they don't go through
  `Juice.ApplyCosmetic` — `Juice.ApplyCosmeticToStyleBox(StyleBoxFlat, category, baseColor)` resolves
  the equipped colour onto a `StyleBoxFlat.BorderColor` directly and, deliberately, never animates:
  a UI border isn't in the arena's `WorldEnvironment` glow pass either way, so an Épico colour would
  just read as one bright fixed tone regardless. Marco applies to two places — the character-select
  carousel's `Swatch` and a new frame around the HUD's in-run portrait — so the purchase is visible
  both while picking a pilot and for the whole run. `KillEffect` resolves inline
  (`GameManager.CosmeticColor(CosmeticCategory.KillEffect, ...)`) right before the existing
  `Juice.Blast` one-shot call in `Enemy.TakeDamage()`'s death branch — every enemy death, not just
  ones that grant XP.
- **Personajes moved from character select into the Tienda.** `CharacterSelectMenu` only shows lock
  state now (dimmed portrait, "Bloqueado — cómpralo en la Tienda") — the actual purchase (still
  `GameManager.TryUnlockCharacter`, unchanged) lives in `CosmeticsShopMenu`'s "Personajes" tab, a
  view mode rather than a `CosmeticCategory` (a locked pilot has a portrait/name/cost, not a colour
  to equip elsewhere), rendered as name/description/price rows — the same row shape
  `AchievementsMenu`/`MissionsMenu` already use — instead of the colour-swatch grid every other tab
  shows.
- **Cosmetic tiers are a rendering difference, not a price label.** Arena.tscn's `WorldEnvironment`
  has `glow_hdr_threshold = 0.85`, so a colour's brightest channel decides what it does to the bloom:
  **Común** stays under the threshold and never blooms (matte), **Raro** peaks at 1.0 and blooms, and
  **Épico** carries channels *above* 1.0 — Godot's `Color` doesn't clamp — so it blows out in a way
  no ordinary colour reaches.
- **Épico is also the only tier that moves.** Each one animates between its `Color` and its `Pulse`,
  and the *period* is what gives each its character rather than the hues: Plasma snaps toward white
  nine times a second and reads as electricity, Fusión rolls between orange and a deep ember at about
  the rate a flame gutters, and Vacío breathes. `Juice.ApplyCosmetic` is the single entry point —
  it resolves the colour, assigns it, and starts the loop if there is one — so the six render sites
  can't each get the "and maybe animate" step half-right. Skipped entirely under reduced motion,
  which promises to remove animations that repeat.
  - Two consequences worth knowing. Transient nodes (bullets, trail puffs) must have the cosmetic
    applied *after* `AddChild`, since `CreateTween` fails on a node that isn't in the tree. And the
    arena's grid stays static while its boundary animates: the grid is one `Line2D` per line across a
    multi-screen arena, which is a lot of looping tweens for something at 0.16 alpha.
  - The main menu has no `WorldEnvironment`, so an Épico swatch previews like a Raro one and only
    separates in the arena. That's what the tier frame on the swatch is for.
- **Secret codes** ([SecretCodeCatalog.cs](../Scripts/Player/SecretCodeCatalog.cs)) are the one way
  meta progression is granted without being paid for. Redeemed from Options, once each
  (`GameManager.RedeemedCodes`), and every code goes through `GrantCharacter`/`GrantCosmetic`/
  `AddLibras` rather than writing save state directly — so a code can't produce a save file the
  game couldn't have reached on its own.
- `GameManager.UnlockedCharacters`/`OwnedCosmetics` (both `HashSet<string>`) are the other half of the
  save — persisted as `PackedStringArray`s under the same `settings.cfg` keys `unlocked_characters`/
  `owned_cosmetics`, no separate file needed (mirrors how `records.cfg` already stores its top-10 list
  the same way), plus `redeemed_codes` for secret codes. What's equipped is one string key per
  category, derived from the enum member's name (`equipped_bullet_cosmetic` and friends) so adding a
  category needs no persistence code at all.

## Logros y Misiones ([AchievementCatalog.cs](../Scripts/Meta/AchievementCatalog.cs), [MissionCatalog.cs](../Scripts/Meta/MissionCatalog.cs))

A third Libras sink/source, alongside characters and cosmetics — except these two *pay* Libras
rather than spend it. Both are evaluated at different points, matching how cheaply each stat is
available to check:

- **Numeric achievements** (rounds, lifetime kills/bosses/crits, account level) are evaluated once,
  in `RegisterFinalScore()` — the same end-of-run funnel that already finalizes Libras/AccountXp/
  CharacterXp — via `EvaluateAchievements`. Nothing is checked per-kill or per-crit; the run's
  in-memory counters (`GameManager.EnemiesKilled`/`BossesKilled`, `Player.CritsLandedThisRun`) are
  rolled into the lifetime totals (`TotalEnemiesKilled`/`TotalBossesKilled`/`TotalCritsLanded`/
  `TotalRoundsCleared`) right before the catalog runs against them.
- **Build/legendary achievements** unlock instantly, the moment they happen —
  `Player.TryActivate()` calls `GameManager.NotifyBuildCompleted` right after a build completes, and
  `Player.ApplyUpgrade()` calls `NotifyLegendaryObtained` the moment a tier is newly raised to
  Legendary — both idempotent HashSet adds (`EverCompletedBuilds`/`EverGotLegendary`), same shape as
  `UnlockedCharacters`.
- Newly-unlocked achievements this run are cached on `GameManager.LastRunNewAchievements` so
  [GameOverScreen.cs](../Scripts/UI/GameOverScreen.cs) can reveal them in its existing one-line-at-a-
  time recap without recomputing anything.
- **Missions** are 3 daily slots (`GameManager.Missions`), rotated by comparing today's date
  (`Time.GetDatetimeDictFromSystem()`, same source `FormatNow` already uses for records) against the
  persisted `_missionsDate`, re-rolled with a date-seeded `Random` so relaunching the same day keeps
  the same 3. Progress is checked at the same low-frequency points the rest of meta-progression
  already touches — `RegisterKill()` (Kill/BossKill), `RegisterFinalScore()` (RoundReached),
  `AddLibras()` (LibrasEarned), `TryBuyCosmetic()` (CosmeticPurchase) — no new per-frame or per-hit
  hook. Completing one pays its Libras reward immediately, no claim step.
- Two separate screens, two separate buttons in MainMenu's top-right corner — Misiones (left) and
  Logros (right, outermost). [AchievementsMenu.cs](../Scripts/UI/AchievementsMenu.cs) is
  Progreso/Combate/Builds tabs over `AchievementCatalog.All`; [MissionsMenu.cs](../Scripts/UI/MissionsMenu.cs)
  is the fixed 3-row list, no tabs. They used to be one screen with a Logros/Misiones tab on top —
  split out once there were enough achievements to want the extra room. All persisted in the same
  `settings.cfg` as everything else above (`total_*` keys, `ever_completed_builds`/
  `ever_got_legendary`/`unlocked_achievements` as `PackedStringArray`s, and a `missions` section with
  one `slot_N` key per mission).

### Estadísticas ([StatsMenu.cs](../Scripts/UI/StatsMenu.cs))

Opened from its own "Estadísticas" button in MainMenu's top-left corner (mirroring "Logros"
opposite it). Two sections, because not every stat means the same thing across a time window:

**Actividad** — the numbers that are genuine sums over time, filterable by **Total / Mes / Semana**:
enemigos eliminados, jefes derrotados, críticos, rondas superadas, monedas ganadas, Libras ganadas,
partidas jugadas, tiempo jugado. Backed by `GameManager.GetStats(StatsPeriod)`:
- "Total" reads 8 lifetime counters directly — the 4 achievement-era ones
  (`TotalEnemiesKilled`/`TotalBossesKilled`/`TotalCritsLanded`/`TotalRoundsCleared`) plus
  `TotalLibrasEarned` (bumped by `RecordLibrasEarned`, called from every existing `Libras +=` site:
  `AddLibras`, `EvaluateAchievements`, `NotifyMissionProgress`/`NotifyMissionRoundReached`),
  `TotalCoinsEarnedLifetime` (bumped by `RecordCoinsEarned`, called from `AddCoins` — distinct from
  the per-run `TotalCoinsEarned` used for shop price inflation, which still resets every run),
  `TotalRunsPlayed`, and `TotalPlayTimeSeconds` (real seconds from `StartRoundOneTimer` to
  `RegisterFinalScore`, including time spent paused/shopping — an approximation, same tolerance the
  rest of this catalog already accepts).
- "Mes"/"Semana" can't read a running total — it has no notion of *when* it was earned — so a small
  per-day breakdown (`GameManager._dailyStats`, keyed by the same `"aaaa-mm-dd"` `TodayKey()`
  missions already use) is written alongside every lifetime-counter update and summed on demand,
  filtered to entries on/after the current calendar week's Monday or the current calendar month's
  1st (`System.DateTime`-based, boundary-based like the missions' daily rotation — not a rolling
  7/30-day window). Persisted as its own `stats_daily` ConfigFile section (one key per date,
  pipe-delimited), pruned past 40 days on every write/load since only "this month" is ever queried
  from it — "Total" never touches this map at all.

**Resumen general** — state/collection stats that don't have a meaningful per-period reading (stays
the same across all 3 tabs): mejor puntaje (`GameManager.LoadHighScore()`), mejor ronda alcanzada
(`BestRoundReached`, the max `RoundNumber` seen across every `RegisterFinalScore`), nivel de cuenta,
precisión (`TotalCritsLanded`/`TotalEnemiesKilled`), logros desbloqueados
(`UnlockedAchievementsCount`/`AchievementCatalog.All.Length`), misiones completadas
(`TotalMissionsCompleted`, bumped alongside every mission payout), personajes desbloqueados,
cosméticos comprados, builds completadas (`EverCompletedBuilds.Count`/`BuildCatalog.ClassOrder.Length`),
and Legendarias distintas (`EverGotLegendary.Count`/27, one per `UpgradeType`).

## Account level (`GameManager.AccountLevel`)

A second, independent permanent-progression track — separate from Libras the same way in-run `Level`
is separate from `Coins`. Every run's `LastRunLibrasEarned` also feeds `AccountXp` via `AddAccountXp`,
using the same threshold-growth shape as the in-run Level/Xp pair (`AccountXpToNextLevel` grows by
`AccountXpGrowthFactor` = 1.35x per level crossed), just re-scaled: Libras per run are small (rarely
above ~15), so the curve starts much lower — `AccountXpToNextLevel` begins at 5.

Nothing consumes `AccountLevel` yet; it exists purely as a number that only ever goes up across the
player's whole history with the game, ahead of deciding what rewards it should unlock. `RegisterFinalScore`
returns how many levels a run crossed as `GameManager.LastRunAccountLevelsGained`, which
[GameOverScreen.cs](../Scripts/UI/GameOverScreen.cs) uses to show a "¡Nivel de cuenta N!" line only on
runs that actually crossed a threshold. Persisted alongside Libras in the same `SaveMetaProgress()`
call (`account_level`/`account_xp`/`account_xp_to_next` keys).

**GameOverScreen always shows an "XP cuenta" line**, even on a 0-Libras run — a silent 0 used to read
as a bug ("did this not work?"), when it's actually `LibrasFreeRounds` telling the player they need to
survive further before anything is paid out. That 0-case spells out the threshold by name
(`GameManager.LibrasFreeRounds`, made `public const` specifically so this message doesn't hardcode a
number that could drift from the real one).

**[MainMenu.tscn](../Scenes/UI/MainMenu.tscn) shows a `ProgressBar`** under the "Nivel de cuenta" label
(`AccountLevelBar`, styled after the HUD's own XP bar), bounds `AccountXp`/`AccountXpToNextLevel` — the
same refresh hook as the Libras/level labels (`CharacterSelectMenu.VisibilityChanged`) keeps it in sync
after spending Libras in the character-select overlay.

## Character level (per pilot, `GameManager.GetCharacterLevel(slug)`)

A THIRD progression track, alongside Libras and AccountLevel — one per character slug rather than one
for the whole account, keyed the same way `UnlockedCharacters` already is (a `slug` string, so custom
characters need no special-casing next to built-ins). Every run's `LastRunLibrasEarned` feeds
`GameManager.SelectedCharacter`'s own Xp via `AddCharacterXp` — the same "1 Libra = 1 XP" rule and
threshold-growth shape as `AddAccountXp`, just scoped to one pilot instead of the whole save. A
brand-new pilot always starts at Level 1 with 0 XP, regardless of how long the account has played or
how leveled its other pilots are.

Persisted as one string per slug (`"level|xp|xpToNext"`) under its own ConfigFile section
(`character_progress`), rather than parallel arrays — `ConfigFile.GetSectionKeys` means loading doesn't
need to already know the full slug list, which matters since custom characters' slugs are generated at
creation time, not fixed like the built-in cast.

Same "always show progress, not just level-ups" treatment as the account line — see
[GameOverScreen.cs](../Scripts/UI/GameOverScreen.cs) — and the same `ProgressBar` treatment on the
character-select carousel ([CharacterSelectMenu.tscn](../Scenes/UI/CharacterSelectMenu.tscn)'s
`LevelBar`, inside `Identity` so it slides with the rest of the framed pilot's info during the carousel
crossfade), refreshed every `RefreshPreview()` call so stepping through the carousel shows each pilot's
own progress, not whichever pilot was framed last. Nothing consumes it yet, same as `AccountLevel`.

Every shop item's final price is the catalog base cost multiplied by **two independent, stacking multipliers**:

```
finalCost = baseCost × UpgradeCostMultiplier × PerTypeSurchargeMultiplier
```

### 1. Wealth-based inflation — `GameManager.UpgradeCostMultiplier`
```
UpgradeCostMultiplier = Clamp(2 + (TotalCoinsEarned / 40) × 1.4, 2, 60)
```
Tied to `GameManager.TotalCoinsEarned` — a **lifetime, monotonically-increasing** counter (bumped in `AddCoins`, reset in `ResetRun`) that tracks every coin ever earned this run, unaffected by spending. This replaced an earlier round-indexed curve (`UpgradeCostCurve.Evaluate(RoundNumber)`) that reliably lost the race against coin income: `EnemySpawner`'s `BurstCountCurve` and `RewardMultCurve` both grow with `RoundNumber` too, so total coin income compounds roughly quadratically per round while a round-indexed price curve only ever grows linearly (and caps) — the shop stopped being a real spending decision once a run got a few rounds in, no matter how the curve's numbers were tuned.

Indexing the multiplier on actual lifetime earnings instead makes it self-correcting: a player who farms aggressively (more kills, more coins) sees prices rise just as fast as their wallet does, and a player who plays passively sees comparably gentle inflation — the pressure tracks the player's real economy rather than a fixed schedule. It also means a big one-off payout (e.g. a Boss kill's high `CoinsReward`, see [enemies.md](enemies.md)) immediately and correctly raises prices in the very next shop, instead of waiting for `RoundNumber` to catch up.

Retune by editing `WealthCostBase`/`WealthCostPerStep`/`WealthCostStep`/`WealthCostMax` in `GameManager.cs` — same "every N units raises the multiplier by X" shape as the old round curve, just re-indexed onto coins-earned instead of round number.

### 2. Per-type repurchase surcharge — `GameManager.GetShopSurchargeMultiplier(UpgradeType)`
```
surcharge = 1.1 ^ (times this UpgradeType has been bought in the shop this run)
```
Every time you **buy** (not level-up-pick — level-ups are free) any tier of a given reward family (e.g. any Speed Boost, at any tier), that entire family gets 10% pricier next time it shows up in a shop, compounding. Tracked per `UpgradeType` in `GameManager._shopPurchaseCounts`, reset on `ResetRun()`.

The shop UI shows the surcharge as a suffix on the price, e.g. `46 (+6) 🪙` — the `+6` is how much extra you're paying purely from the repurchase surcharge. **The wealth multiplier itself is deliberately never surfaced**: it applies equally to all three offers, so showing it couldn't help anyone choose between them, and it would compete for attention with the number that does matter (what this costs versus what you have).

Note the chain rounds **twice**, not once at the end — `RoundToInt(Cost × UpgradeCostMultiplier)` and then `RoundToInt(that × surcharge)`. So `finalCost` isn't exactly `RoundToInt(Cost × mult × surcharge)`; cheap items lose a little precision (base 8 at ×2.0 → 16, ×1.1 → 17.6 → 18).

### Buying and advancing

All 3 offered items can be bought in the same shop visit (no purchase limit) — the per-type surcharge above is what keeps repeatedly buying the same reward family from staying cheap, not a purchase cap.

`Shop.Open()` snapshots which of the 3 offered slots are already maxed out (`Player.IsRewardUseless`, unbuyable — see [rewards.md](rewards.md)) into a `_resolved[]` array. Every time a purchase succeeds, that slot's entry flips to resolved too. Once **every** slot is resolved (bought this visit, or was already maxed before you even opened the shop), `Shop.CheckAutoAdvance()` automatically starts the next round — no need to click "Next Round". The button is still there for leaving early without buying everything, or for the shop to work at all when you can't afford the remaining offers.

`UpgradeData.PickRandomTiered` also takes `Player.IsRewardUseless` as a filter when rolling the 3 offers in the first place, both here and at level-up — so a maxed-out reward is only ever offered when there's genuinely nothing better left to roll instead (avoiding both a wasted slot and, at level-up specifically, the risk of all 3 free choices coming up disabled with no way to proceed).
