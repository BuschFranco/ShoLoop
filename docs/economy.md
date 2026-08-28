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
- **Spent on characters today** — see [characters.md](characters.md#locked-characters--libras) for
  `CharacterInfo.RequiresUnlock`/`UnlockCost` and `GameManager.TryUnlockCharacter`. Framed as "first
  thing to spend it on," not the only one; `docs/characters.md` has the unlock-flow details.
- `GameManager.UnlockedCharacters` (a `HashSet<string>` of slugs) is the other half of the save —
  persisted as a `PackedStringArray` under the same `settings.cfg` key `unlocked_characters`, no
  separate file needed (mirrors how `records.cfg` already stores its top-10 list the same way).

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
