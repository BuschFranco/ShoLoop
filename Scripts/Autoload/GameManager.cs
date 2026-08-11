namespace ShooterLoop;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    [Export] public float RoundDuration = 60f;

    public int Level = 1;
    public int Xp = 0;
    public int XpToNextLevel = 10;
    public int EnemiesKilled = 0;
    public int SpecialEnemiesKilled = 0;
    public int Coins = 0;
    public int Score = 0;
    public int RoundNumber = 1;
    public bool IsPaused = false;

    // Coin income compounds faster than RoundNumber (more enemies AND a higher per-kill payout
    // each round — see EnemySpawner's BurstCountCurve/RewardMultCurve), so a round-indexed price
    // curve always loses the race and the shop stops being a real spending decision. Tying prices
    // to the player's actual lifetime coin income instead keeps the pressure matched to how much
    // they're really earning, regardless of how aggressively (or passively) they farm any given
    // round. Every WealthCostStep coins ever earned raises the multiplier by WealthCostPerStep,
    // same shape as the old round curve — just re-indexed onto wealth instead of round number.
    private const float WealthCostBase = 2f;
    private const float WealthCostPerStep = 1.4f;
    private const float WealthCostStep = 40f;
    private const float WealthCostMax = 60f;

    public int TotalCoinsEarned = 0;

    public float UpgradeCostMultiplier =>
        Mathf.Clamp(WealthCostBase + (TotalCoinsEarned / WealthCostStep) * WealthCostPerStep, WealthCostBase, WealthCostMax);

    private readonly Dictionary<UpgradeType, int> _shopPurchaseCounts = new();

    // Everything that has to interrupt gameplay with a modal, queued rather than opened directly.
    // A single kill can trigger several of these at once — the boss's XP payout alone can cross
    // multiple level thresholds *and* end the round *and* (the first time) grant an Ultimate — and
    // they all share one UpgradePicker plus the shop, so opening any of them eagerly means the last
    // one silently clobbers the rest. ResolveNextInterstitial() drains this queue one modal at a
    // time instead, in priority order: level-ups, then the Ultimate choice, then the round-end shop.
    private int _pendingLevelUps = 0;
    private bool _pendingUltimateChoice = false;
    private bool _pendingRoundEnd = false;
    private bool _bossUltimateGranted = false;

    // Which queue the currently-open picker is resolving, so ApplyUpgradeAndResume decrements the
    // right one instead of having to infer it from the chosen upgrade's type.
    private bool _pickerIsUltimateChoice = false;

    // Every counter above is cumulative for the run, so the end-of-round recap has to diff against
    // a snapshot taken when the round began. Re-taken in StartNextRound and ResetRun.
    private int _roundStartKills;
    private int _roundStartEliteKills;
    private int _roundStartCoins;
    private int _roundStartScore;
    private int _roundStartLevel;

    public event Action<int, int> XpChanged;
    public event Action<int> LevelUp;

    // Distinct from LevelUp above: LevelUp fires once per threshold crossed (so a triple level-up
    // invokes it 3 times, correctly updating "Nv N" to its final value one step at a time).
    // LevelsGained fires once per AddXp call with the *total* crossed, so the HUD popup reads as
    // one "+3" rather than three overlapping "+1"s in the same frame.
    public event Action<int> LevelsGained;
    public event Action<int> CoinsChanged;
    public event Action<int> ScoreChanged;
    public event Action<int> RoundChanged;
    public event Action PlayerDied;

    public float RoundTimeRemaining => (float)(_roundTimer?.TimeLeft ?? 0f);

    private Timer _roundTimer;

    public override void _Ready()
    {
        Instance = this;

        _roundTimer = new Timer();
        _roundTimer.OneShot = true;
        _roundTimer.WaitTime = RoundDuration;
        AddChild(_roundTimer);
        _roundTimer.Timeout += OnRoundTimeout;
        _roundTimer.Start();
    }

    public bool IsBossRound => RoundNumber % 5 == 0;

    // Score is intentionally synced 1:1 with XP — same value feeds both, so the number shown as
    // "Puntaje" always tracks the same progression that drives leveling, instead of following its
    // own separate (and previously flat, un-scaled) per-category table. Coins remain completely
    // separate — this only concerns Score/XP.
    public void RegisterKill(int xpReward, int coinsReward, EnemyCategory category = EnemyCategory.Common)
    {
        EnemiesKilled++;
        if (category != EnemyCategory.Common) SpecialEnemiesKilled++;

        var player = GetTree().GetFirstNodeInGroup("player") as Player;

        // Sabiduría scales XP; Botín scales coins. Each touches only its own currency.
        AddXp(Mathf.RoundToInt(xpReward * (player?.XpMultiplier ?? 1f)));
        AddCoins(Mathf.RoundToInt(coinsReward * (player?.CoinMultiplier ?? 1f)));

        // Deliberately the *unmultiplied* xpReward, which breaks the otherwise 1:1 Score/XP sync.
        // Score is persisted as a high score, so letting a reward choice inflate it would make runs
        // incomparable.
        AddScore(xpReward);

        if (category == EnemyCategory.Boss)
        {
            // Beating the very first boss is what unlocks Ultimates as a mechanic — a free choice
            // between three of them, once per run. Queued *before* BeginRoundEnd so it's already in
            // the queue when that drains it, and so the recap is up when the picker opens.
            if (!_bossUltimateGranted)
            {
                _bossUltimateGranted = true;
                _pendingUltimateChoice = true;
            }

            BeginRoundEnd();
        }
    }

    public void AddCoins(int amount)
    {
        Coins += amount;
        TotalCoinsEarned += amount;
        CoinsChanged?.Invoke(Coins);
    }

    public void SpendCoins(int amount)
    {
        Coins = Mathf.Max(0, Coins - amount);
        CoinsChanged?.Invoke(Coins);
    }

    public void AddScore(int amount)
    {
        Score += amount;
        ScoreChanged?.Invoke(Score);
    }

    // Global multiplier every Enemy's velocity is scaled by — centralized here (rather than
    // mutating each enemy's own MoveSpeed) so the Zona Lenta ultimate affects enemies spawned
    // mid-effect too, not just the ones that existed at the moment it was triggered.
    public float EnemySpeedMultiplier = 1f;
    private Timer _slowTimer;

    public void ApplyTemporarySlow(float multiplier, float duration)
    {
        EnemySpeedMultiplier = multiplier;

        if (_slowTimer == null)
        {
            _slowTimer = new Timer { OneShot = true };
            AddChild(_slowTimer);
            _slowTimer.Timeout += () => EnemySpeedMultiplier = 1f;
        }
        _slowTimer.WaitTime = duration;
        _slowTimer.Start();
    }

    public void AddXp(int amount)
    {
        Xp += amount;

        // Counted rather than fired inline per threshold: a single big XP reward can cross several
        // thresholds in one call (see _pendingLevelUps above), and the level-up popup should read
        // as one "+3", not three separate "+1"s stacking on top of each other in the same frame.
        int levelsGained = 0;
        while (Xp >= XpToNextLevel)
        {
            Xp -= XpToNextLevel;
            Level++;
            XpToNextLevel = Mathf.RoundToInt(XpToNextLevel * 1.3f);
            levelsGained++;
            OnLevelGained();
        }

        // Fired once, AFTER any rollover — this used to fire before the loop, against the
        // pre-rollover Xp and XpToNextLevel. That reads as "at/over max" the instant a level-up
        // happens, and since nothing re-fired the event once the loop corrected Xp/XpToNextLevel,
        // the bar just sat there looking full until the next kill's AddXp call happened to invoke
        // it again with values that were, by then, already correct.
        XpChanged?.Invoke(Xp, XpToNextLevel);

        if (levelsGained > 0)
            LevelsGained?.Invoke(levelsGained);
    }

    private void OnLevelGained()
    {
        LevelUp?.Invoke(Level);

        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        player?.TriggerLevelUpBurst();

        _pendingLevelUps++;
        ResolveNextInterstitial();
    }

    // Opens the next queued modal, or unpauses if the queue is empty. Bails out while a picker is
    // already on screen — whatever is left in the queue gets picked up by ApplyUpgradeAndResume
    // when that pick resolves, so this is safe to call from anywhere that enqueues something.
    private void ResolveNextInterstitial()
    {
        var picker = GetTree().GetFirstNodeInGroup("upgrade_picker") as UpgradePicker;
        if (picker == null || picker.Visible) return;

        var player = GetTree().GetFirstNodeInGroup("player") as Player;

        if (_pendingLevelUps > 0)
        {
            _pickerIsUltimateChoice = false;
            picker.Open(UpgradeData.PickRandomTiered(3, RoundNumber, RewardSource.LevelUp, isUseless: player != null ? player.IsRewardUseless : null));
            Pause();
            return;
        }

        if (_pendingUltimateChoice)
        {
            _pickerIsUltimateChoice = true;
            picker.Open(UpgradeData.PickUltimateChoices(3, player != null ? player.IsRewardUseless : null), UpgradePicker.UltimateTitle);
            Pause();
            return;
        }

        if (_pendingRoundEnd)
        {
            _pendingRoundEnd = false;
            EndRound();
            return;
        }

        Resume();
    }

    private void OnRoundTimeout()
    {
        BeginRoundEnd();
    }

    // Single entry point for "the round is over", from either the timer or a boss kill. Puts the
    // recap on screen up front so it's already visible behind the level-up / Ultimate / shop
    // modals, then hands off to the queue. StartNextRound is what takes it back down.
    private void BeginRoundEnd()
    {
        _pendingRoundEnd = true;

        var summary = GetTree().GetFirstNodeInGroup("round_summary") as RoundSummary;
        summary?.ShowSummary(
            RoundNumber,
            EnemiesKilled - _roundStartKills,
            SpecialEnemiesKilled - _roundStartEliteKills,
            TotalCoinsEarned - _roundStartCoins,
            Score - _roundStartScore,
            Level - _roundStartLevel);

        ResolveNextInterstitial();
    }

    private void SnapshotRoundStart()
    {
        _roundStartKills = EnemiesKilled;
        _roundStartEliteKills = SpecialEnemiesKilled;
        _roundStartCoins = TotalCoinsEarned;
        _roundStartScore = Score;
        _roundStartLevel = Level;
    }

    private void EndRound()
    {
        // Enemies and their in-flight shots both vanish the instant a round ends — the next round's
        // board should start clean, not with whatever was left standing (or mid-Split, or mid-air)
        // from the previous one. Clearing the bullets matters for fairness as much as tidiness: one
        // frozen by the shop's pause would otherwise resume and land on the player during the next
        // round's "get ready" countdown.
        foreach (Node enemy in GetTree().GetNodesInGroup("enemies"))
            enemy.QueueFree();

        foreach (Node bullet in GetTree().GetNodesInGroup("enemy_bullets"))
            bullet.QueueFree();

        // Same reasoning as above: a Heart/Shield drop still sitting on the field shouldn't carry
        // over into the next round's clean board.
        foreach (Node pickup in GetTree().GetNodesInGroup("pickups"))
            pickup.QueueFree();

        // A Timer only freezes its remaining time while the tree is paused — it doesn't reset.
        // Without this, the spawner's own timer could have almost no time left by the time the
        // shop closes, firing an immediate spawn the instant the tree unpauses and completely
        // ignoring the round-start countdown below.
        var spawner = GetTree().GetFirstNodeInGroup("enemy_spawner") as EnemySpawner;
        spawner?.StopSpawning();

        var shop = GetTree().GetFirstNodeInGroup("shop") as Shop;
        shop?.Open();

        Pause();
    }

    private const float RoundStartDelay = 3f;
    private Timer _roundStartTimer;

    public float RoundStartTimeRemaining => (float)(_roundStartTimer?.TimeLeft ?? 0f);
    public bool IsRoundStarting => _roundStartTimer != null && !_roundStartTimer.IsStopped();

    public void StartNextRound()
    {
        RoundNumber++;
        RoundChanged?.Invoke(RoundNumber);

        // The recap has been up since the round ended, through every modal. This is the point the
        // player has finished choosing everything, so it comes down as the countdown begins.
        var summary = GetTree().GetFirstNodeInGroup("round_summary") as RoundSummary;
        summary?.HideSummary();
        SnapshotRoundStart();

        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        player?.HealFullLives();
        player?.RefillShield();

        // Enemies don't start appearing immediately — a short "get ready" countdown plays first
        // (HUD shows it in place of the round timer/boss label), with the round's own timer/spawns
        // deferred until it elapses.
        _roundTimer.Stop();

        if (_roundStartTimer == null)
        {
            _roundStartTimer = new Timer { OneShot = true };
            AddChild(_roundStartTimer);
            _roundStartTimer.Timeout += BeginRoundAfterCountdown;
        }
        _roundStartTimer.WaitTime = RoundStartDelay;
        _roundStartTimer.Start();

        Resume();
    }

    private void BeginRoundAfterCountdown()
    {
        var spawner = GetTree().GetFirstNodeInGroup("enemy_spawner") as EnemySpawner;

        if (IsBossRound)
        {
            spawner?.ConfigureForBossRound(RoundNumber);

            var banner = GetTree().GetFirstNodeInGroup("boss_banner") as BossBanner;
            banner?.Announce(RoundNumber);
        }
        else
        {
            spawner?.ConfigureForRound(RoundNumber);
            _roundTimer.Start();
        }
    }

    public void OpenPauseMenu()
    {
        var menu = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenu;
        menu?.Open();
        Pause();
    }

    public float GetShopSurchargeMultiplier(UpgradeType type)
    {
        return Mathf.Pow(1.1f, _shopPurchaseCounts.GetValueOrDefault(type, 0));
    }

    public void RegisterShopPurchase(UpgradeType type)
    {
        _shopPurchaseCounts[type] = _shopPurchaseCounts.GetValueOrDefault(type, 0) + 1;
    }

    public void ApplyUpgrade(UpgradeData upgrade)
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        player?.ApplyUpgrade(upgrade);
    }

    public void ApplyUpgradeAndResume(UpgradeData upgrade)
    {
        ApplyUpgrade(upgrade);

        if (_pickerIsUltimateChoice)
        {
            _pickerIsUltimateChoice = false;
            _pendingUltimateChoice = false;
        }
        else if (_pendingLevelUps > 0)
        {
            _pendingLevelUps--;
        }

        // Whatever's still queued (another level-up, the Ultimate choice, the round-end shop) opens
        // next; ResolveNextInterstitial unpauses on its own once nothing is left.
        ResolveNextInterstitial();
    }

    public void Pause()
    {
        GetTree().Paused = true;
        IsPaused = true;
    }

    public void Resume()
    {
        GetTree().Paused = false;
        IsPaused = false;
    }

    public void NotifyPlayerDied()
    {
        if (IsPaused) return;
        PlayerDied?.Invoke();

        RegisterFinalScore();

        var screen = GetTree().GetFirstNodeInGroup("game_over_screen") as GameOverScreen;
        screen?.Open();

        Pause();
    }

    private const string HighScoreFilePath = "user://highscore.save";

    // Local-install save data — a single plain-text integer, not tied to any account or cloud
    // sync. Static since MainMenu (no live run in progress) needs to read it too.
    public static int LoadHighScore()
    {
        if (!FileAccess.FileExists(HighScoreFilePath)) return 0;
        using var file = FileAccess.Open(HighScoreFilePath, FileAccess.ModeFlags.Read);
        if (file == null) return 0;
        int.TryParse(file.GetLine(), out int score);
        return score;
    }

    private static void SaveHighScore(int score)
    {
        using var file = FileAccess.Open(HighScoreFilePath, FileAccess.ModeFlags.Write);
        file?.StoreLine(score.ToString());
    }

    private void RegisterFinalScore()
    {
        if (Score > LoadHighScore())
            SaveHighScore(Score);
    }

    public void ResetRun()
    {
        Xp = 0;
        Level = 1;
        XpToNextLevel = 10;
        EnemiesKilled = 0;
        SpecialEnemiesKilled = 0;
        Coins = 0;
        TotalCoinsEarned = 0;
        Score = 0;
        RoundNumber = 1;
        _pendingLevelUps = 0;
        _pendingUltimateChoice = false;
        _pendingRoundEnd = false;
        _bossUltimateGranted = false;
        _pickerIsUltimateChoice = false;
        _shopPurchaseCounts.Clear();
        _roundTimer.Stop();
        _roundTimer.Start();
        EnemySpeedMultiplier = 1f;
        _slowTimer?.Stop();
        _roundStartTimer?.Stop();
        SnapshotRoundStart();

        var summary = GetTree().GetFirstNodeInGroup("round_summary") as RoundSummary;
        summary?.HideSummary();

        Resume();
    }
}
