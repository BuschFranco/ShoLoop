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

    public event Action<int, int> XpChanged;
    public event Action<int> LevelUp;
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
        AddXp(xpReward);
        AddCoins(coinsReward);
        AddScore(xpReward);

        if (category == EnemyCategory.Boss)
        {
            // Queued rather than ending the round outright: AddXp above may already have opened
            // the level-up picker, and calling EndRound() here would drop the shop straight on top
            // of it (the shop's CanvasLayer sits above the picker's, so the pick would be lost).
            _pendingRoundEnd = true;

            // Beating the very first boss is what unlocks Ultimates as a mechanic — a free choice
            // between three of them, once per run.
            if (!_bossUltimateGranted)
            {
                _bossUltimateGranted = true;
                _pendingUltimateChoice = true;
            }

            ResolveNextInterstitial();
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

        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        player?.AddUltimateProgress(amount);
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
        XpChanged?.Invoke(Xp, XpToNextLevel);

        while (Xp >= XpToNextLevel)
        {
            Xp -= XpToNextLevel;
            Level++;
            XpToNextLevel = Mathf.RoundToInt(XpToNextLevel * 1.3f);
            OnLevelGained();
        }
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
            picker.Open(UpgradeData.PickRandomTiered(3, RoundNumber, isUseless: player != null ? player.IsRewardUseless : null));
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
        EndRound();
    }

    private void EndRound()
    {
        // Enemies vanish the instant a round ends — the next round's board should start clean,
        // not with whatever was left standing (or mid-Split) from the previous one.
        foreach (Node enemy in GetTree().GetNodesInGroup("enemies"))
            enemy.QueueFree();

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

    private const float RoundStartDelay = 5f;
    private Timer _roundStartTimer;

    public float RoundStartTimeRemaining => (float)(_roundStartTimer?.TimeLeft ?? 0f);
    public bool IsRoundStarting => _roundStartTimer != null && !_roundStartTimer.IsStopped();

    public void StartNextRound()
    {
        RoundNumber++;
        RoundChanged?.Invoke(RoundNumber);

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
        Resume();
    }
}
