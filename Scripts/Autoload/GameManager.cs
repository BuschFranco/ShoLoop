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

    public enum ScreenOrientation { Landscape, Portrait }
    public ScreenOrientation CurrentOrientation { get; private set; } = ScreenOrientation.Portrait;

    public float CameraDistance = 2000f;

    // The project's reference/base size (matches project.godot's implicit 1152x648 default, made
    // explicit there) and its portrait counterpart. window/stretch/mode="canvas_items" + "expand"
    // scales UI, and reveals extra world through the camera, relative to whichever of these is
    // currently set as the root Window's ContentScaleSize — swapping it to match the chosen
    // orientation is what keeps both UI layout and how much of the arena is visible consistent
    // between landscape and portrait, instead of "expand" distorting one of them.
    private static readonly Vector2I LandscapeBaseSize = new(1152, 648);
    private static readonly Vector2I PortraitBaseSize = new(648, 1152);

    // Prices track the player's *current* coin balance, not lifetime earnings. Spending in the shop
    // lowers prices on the next refresh; hoarding raises them. Base is 3.5 so a Common (~8) costs a
    // heavy ~28 coins at round 1 — enough to buy one thing, never everything — and every coin over
    // 40 raises it further (~0.3 per 40), so the more you hoard the steeper the shop gets. Late
    // rounds compound +10%/round on top, so income can't outrun the shop forever.
    private const float WealthCostBase = 3.5f;
    private const float WealthCostPerStep = 0.3f;     // 0.3 per 40 coins = 0.0075/coin
    private const float WealthCostStep = 40f;
    private const float WealthCostRoundStep = 0.1f;   // +10% per round
    private const float WealthCostMax = 4000f;

    public int TotalCoinsEarned = 0;

    public float UpgradeCostMultiplier
    {
        get
        {
            float mult = WealthCostBase + (Coins / WealthCostStep) * WealthCostPerStep;
            // Escalado por ronda: +10% por ronda para que rondas altas mantengan presión
            float round = GameManager.Instance?.RoundNumber ?? 1;
            float roundMult = 1f + (round - 1) * WealthCostRoundStep;
            return Mathf.Clamp(mult * roundMult, 1.5f, WealthCostMax);
        }
    }

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

        // Applied before anything else so the very first frame (main menu included) already
        // renders in whatever orientation the player last picked, not a landscape flash that
        // then snaps to portrait.
        CurrentOrientation = LoadOrientationPreference();
        ApplyOrientation();
        LoadSettings();

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
    public int RegisterKill(int xpReward, int coinsReward, EnemyCategory category = EnemyCategory.Common)
    {
        EnemiesKilled++;
        if (category != EnemyCategory.Common) SpecialEnemiesKilled++;

        var player = GetTree().GetFirstNodeInGroup("player") as Player;

        // Kill streak: rapid kills boost XP/coin payouts.
        player?.RegisterKillForStreak();
        float streakMult = player?.KillStreakMultiplier ?? 1f;

        // Sabiduría scales XP; Botín scales coins. Streak only boosts XP, not coins.
        int finalXp = Mathf.RoundToInt(xpReward * (player?.XpMultiplier ?? 1f) * streakMult);
        AddXp(finalXp);
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

        return finalXp;
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

    // Extra XP/coin payout from a round event (Frenesí doubles it). Applied per spawn in
    // EnemySpawner.SpawnOne alongside RewardMultCurve, and reset by RoundEventDirector at round end.
    public float EventRewardMultiplier = 1f;
    private Timer _slowTimer;

    // Set once per round by EnemySpawner.EvaluateStatCurves from DifficultyBalancer's survivability
    // catch-up, and read by Player.TakeHit — living here (rather than a direct EnemySpawner<->Player
    // reference) matches how EnemySpeedMultiplier above already brokers a difficulty signal between
    // systems that otherwise don't know about each other.
    public float SurvivabilityCatchUpMultiplier = 1f;

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

    // Was a flat 1.3x per level. The problem with a flat factor is that it loses badly to how income
    // actually grows: RewardMultCurve alone adds 35% *per round*, and that compounds again with burst
    // count, spawn rate and a progressively heavier enemy mix. By round 20 a player earns ~11,000 XP in
    // a single round against a 1,486 level cost — roughly seven levels per round, which is what made
    // late-game leveling feel free.
    //
    // A factor that itself grows per level fixes the shape rather than just shifting it: the early
    // game is left almost exactly as it was (level 5 even gets slightly cheaper), and the curve only
    // steepens from around level 12 — which is about where a player sits by round 11.
    private const float XpGrowthBase = 1.30f;
    private const float XpGrowthPerLevel = 0.02f;
    private const float XpGrowthMax = 1.75f;

    private float XpGrowthFactor() =>
        Mathf.Min(XpGrowthBase + XpGrowthPerLevel * Level, XpGrowthMax);

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
            XpToNextLevel = Mathf.RoundToInt(XpToNextLevel * XpGrowthFactor());
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
            picker.Open(UpgradeData.PickRandomTiered(3, RoundNumber, RewardSource.LevelUp, isUseless: player != null ? player.IsRewardUseless : null, fortuneBonus: player?.FortuneBonus ?? 0f));
            Pause();
            return;
        }

        if (_pendingUltimateChoice)
        {
            _pickerIsUltimateChoice = true;
            picker.Open(UpgradeData.PickUltimateChoices(3, player != null ? player.IsRewardUseless : null), UpgradePicker.UltimateTitle, isUltimate: true);
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
    // What the round just produced. Snapshotted here rather than recomputed later because the deltas
    // are against _roundStart* values that StartNextRound resets — by the time the shop opens (after
    // any level-up pickers have been resolved) the originals are still intact, but tying the numbers
    // to the moment the round actually ended keeps them correct regardless of how long the
    // interstitial chain takes.
    public readonly record struct RoundRecap(int Round, int Kills, int EliteKills, int Coins, int Score, int Levels);

    public RoundRecap LastRoundRecap { get; private set; }

    private void BeginRoundEnd()
    {
        _pendingRoundEnd = true;

        // The recap used to be its own floating panel on its own CanvasLayer, shown here and left up
        // through the whole interstitial chain. In practice the shop is nearly full-height, so the
        // recap sat entirely behind it and was only ever glimpsed during the shop's fade-out — two
        // windows fighting for the same space to show one moment's worth of information. It's now
        // rendered inside the shop panel itself, which is also where the coins it reports get spent.
        LastRoundRecap = new RoundRecap(
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

        // Pickups deliberately survive the round boundary, unlike the two groups above. A drop that
        // lands in the last seconds of a round was earned, and sweeping it here meant the kill that
        // produced it was silently wasted. StartNextRound refreshes their lifetimes so they get a
        // real window to be collected rather than expiring two seconds into the new round.

        // A Timer only freezes its remaining time while the tree is paused — it doesn't reset.
        // Without this, the spawner's own timer could have almost no time left by the time the
        // shop closes, firing an immediate spawn the instant the tree unpauses and completely
        // ignoring the round-start countdown below.
        var spawner = GetTree().GetFirstNodeInGroup("enemy_spawner") as EnemySpawner;
        spawner?.StopSpawning();

        // Clears every hazard the round's event left on the field and resets its global multipliers, so
        // nothing bleeds into the shop or the next round.
        RoundEventDirectorNode?.EndActiveEvent();

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
        SnapshotRoundStart();

        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        player?.HealFullLives();
        player?.RefillShield();
        player?.RefreshShieldRegenRate();
        player?.ResetKillStreak();

        // Pickups are no longer swept at round end (see EndRound), so anything left on the field gets
        // its full lifetime back here — otherwise a drop from the last second of the previous round
        // would expire almost immediately into this one.
        foreach (Node node in GetTree().GetNodesInGroup("pickups"))
            (node as PickupBase)?.RefreshLifetime();

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

        // The alarm goes off during the countdown, not when the boss lands — the point is to telegraph
        // what's coming. This is also what gives the round-5 boss an alarm at all, since normal round
        // escalation hasn't reached the alarm threshold that early.
        DangerDirectorNode?.SetBossAlert(IsBossRound);

        // Rolled now, applied after the countdown. Splitting it that way means the outcome is settled
        // before the round begins while the announcement still gets the screen to itself.
        RoundEventDirectorNode?.RollForRound(RoundNumber, IsBossRound);

        Resume();
    }

    private DangerDirector DangerDirectorNode =>
        GetTree().GetFirstNodeInGroup("danger_director") as DangerDirector;

    private RoundEventDirector RoundEventDirectorNode =>
        GetTree().GetFirstNodeInGroup("round_event_director") as RoundEventDirector;

    private void BeginRoundAfterCountdown()
    {
        var spawner = GetTree().GetFirstNodeInGroup("enemy_spawner") as EnemySpawner;
        var director = DangerDirectorNode;

        if (IsBossRound)
        {
            spawner?.ConfigureForBossRound(RoundNumber);

            var banner = GetTree().GetFirstNodeInGroup("boss_banner") as BossBanner;
            banner?.Announce(RoundNumber);

            // The pre-boss burst hands back to the round's own danger level now that the boss is here.
            director?.SetBossAlert(false);

            if (GetTree().GetFirstNodeInGroup("camera_rig") is CameraRig camera)
                camera.Shake(DangerLevel.ShakeStrength, DangerLevel.ShakeDuration);
        }
        else
        {
            // Started before ConfigureForRound: Frenesí sets EventRewardMultiplier, and the spawner reads
            // that when it evaluates its stat curves for this round.
            RoundEventDirectorNode?.BeginActiveEvent();

            spawner?.ConfigureForRound(RoundNumber);
            _roundTimer.Start();

            // Fired here rather than from RoundChanged so the callout doesn't share the screen with the
            // countdown it would otherwise overlap. Only lands on the few non-boss rounds that mark an
            // escalation step — see DangerLevel.ThreatCallout.
            string callout = DangerLevel.ThreatCallout(RoundNumber);
            if (callout != null) director?.AnnounceThreat(callout);
        }
    }

    public void OpenPauseMenu()
    {
        var menu = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenu;
        menu?.Open();
        Pause();
    }

    public void UpdateCameraExtents()
    {
        if (GetTree().GetFirstNodeInGroup("camera_rig") is CameraRig camera)
        {
            float defaultDistance = 2000f;
            float zoomValue = defaultDistance / CameraDistance;
            camera.Zoom = new Vector2(zoomValue, zoomValue);
        }
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

    private const string OrientationFilePath = "user://orientation.save";

    // Same plain-text-file pattern as HighScore above. Static since MainMenu (no GameManager
    // instance guaranteed loaded yet the very first time) needs to read the saved choice too.
    // Portrait is the default for a first-ever launch (no save file yet) — only an explicit
    // saved "Landscape" switches it, everything else (missing file, unreadable, unrecognized
    // text) falls back to Portrait.
    public static ScreenOrientation LoadOrientationPreference()
    {
        if (!FileAccess.FileExists(OrientationFilePath)) return ScreenOrientation.Portrait;
        using var file = FileAccess.Open(OrientationFilePath, FileAccess.ModeFlags.Read);
        if (file == null) return ScreenOrientation.Portrait;
        return file.GetLine() == "Landscape" ? ScreenOrientation.Landscape : ScreenOrientation.Portrait;
    }

    private static void SaveOrientationPreference(ScreenOrientation orientation)
    {
        using var file = FileAccess.Open(OrientationFilePath, FileAccess.ModeFlags.Write);
        file?.StoreLine(orientation.ToString());
    }

    // Called from the main menu's orientation picker. Takes effect immediately — no restart
    // needed — since both DisplayServer's orientation request and ContentScaleSize are live
    // runtime properties.
    public void SetOrientation(ScreenOrientation orientation)
    {
        CurrentOrientation = orientation;
        SaveOrientationPreference(orientation);
        ApplyOrientation();
    }

    private void ApplyOrientation()
    {
        bool portrait = CurrentOrientation == ScreenOrientation.Portrait;

        DisplayServer.ScreenSetOrientation(portrait
            ? DisplayServer.ScreenOrientation.Portrait
            : DisplayServer.ScreenOrientation.Landscape);

        GetTree().Root.ContentScaleSize = portrait ? PortraitBaseSize : LandscapeBaseSize;
    }

    private void RegisterFinalScore()
    {
        if (Score > LoadHighScore())
            SaveHighScore(Score);

        AppendRecord(Score);
    }

    // --- Settings and records (ConfigFile) ---
    //
    // HighScore and Orientation above are one-value-per-file plain text, which was fine for a single int
    // each. A slider value plus a dated top-ten list is structured data, so new persistence goes through
    // ConfigFile rather than adding more one-line files.

    private const string SettingsFilePath = "user://settings.cfg";
    private const string SettingsSection = "settings";

    // 0 = fully transparent, 1 = fully opaque. Applied as Modulate.A on the joystick's root Control,
    // which propagates to both of its Polygon2D children and multiplies their own alphas (0.28 base /
    // 0.85 knob), preserving the contrast between them. Note Modulate does not affect hit-testing, so
    // the joystick keeps working even at 0 — which is the point of allowing 0 at all.
    public float JoystickOpacity { get; private set; } = 1f;

    public void SetJoystickOpacity(float opacity)
    {
        JoystickOpacity = Mathf.Clamp(opacity, 0f, 1f);

        var config = new ConfigFile();
        config.Load(SettingsFilePath);   // a missing file just means "defaults", not an error worth acting on
        config.SetValue(SettingsSection, "joystick_opacity", JoystickOpacity);
        config.Save(SettingsFilePath);

        ApplyJoystickOpacity();
    }

    // Called on boot and whenever the slider moves. Safe to call with no joystick in the tree (the main
    // menu has none) — it simply does nothing until a run starts and VirtualJoystick reads the value in
    // its own _Ready.
    public void ApplyJoystickOpacity()
    {
        if (GetTree().GetFirstNodeInGroup("virtual_joystick") is CanvasItem joystick)
            joystick.Modulate = new Color(1f, 1f, 1f, JoystickOpacity);
    }

    // Same shape as JoystickOpacity above, for the circular Ultimate button (HUD.tscn). Applied as
    // Modulate on the button, which propagates to the icon child; Modulate doesn't affect
    // hit-testing, so at 0% the button is invisible but still fully tappable — the same rule the
    // joystick already follows.
    public float UltimateButtonOpacity { get; private set; } = 1f;

    public void SetUltimateButtonOpacity(float opacity)
    {
        UltimateButtonOpacity = Mathf.Clamp(opacity, 0f, 1f);

        var config = new ConfigFile();
        config.Load(SettingsFilePath);
        config.SetValue(SettingsSection, "ultimate_button_opacity", UltimateButtonOpacity);
        config.Save(SettingsFilePath);

        ApplyUltimateButtonOpacity();
    }

    public void ApplyUltimateButtonOpacity()
    {
        if (GetTree().GetFirstNodeInGroup("ultimate_button") is CanvasItem button)
            button.Modulate = new Color(1f, 1f, 1f, UltimateButtonOpacity);
    }

    // Which character was picked at the character-select screen (CharacterSelectMenu), read by
    // Player._Ready() when a run starts. Persisted the same way as the opacity sliders above, so
    // the last pick is remembered across sessions instead of resetting to Equilibrado every launch.
    // A slug rather than an enum value, because player-created characters have no compile-time
    // identity — see CharacterCatalog.
    public string SelectedCharacter { get; private set; } = CharacterCatalog.DefaultSlug;

    public void SetSelectedCharacter(string slug)
    {
        SelectedCharacter = slug;

        var config = new ConfigFile();
        config.Load(SettingsFilePath);
        config.SetValue(SettingsSection, "selected_character_slug", slug);
        config.Save(SettingsFilePath);
    }

    // Reduced motion. The actual flag lives on DangerLevel (which is where the consumers already read
    // it from and which knows nothing about the scene tree); this is the persisted, user-facing half.
    // It existed as a hardcoded `false` with a comment saying it was there so wiring a toggle later
    // would be one line rather than a refactor — this is that line being cashed in.
    public bool ReducedMotion { get; private set; }

    public void SetReducedMotion(bool enabled)
    {
        ReducedMotion = enabled;
        DangerLevel.Reduced = enabled;

        var config = new ConfigFile();
        config.Load(SettingsFilePath);
        config.SetValue(SettingsSection, "reduced_motion", enabled);
        config.Save(SettingsFilePath);
    }

    private void LoadSettings()
    {
        var config = new ConfigFile();
        if (config.Load(SettingsFilePath) != Error.Ok) return;
        JoystickOpacity = Mathf.Clamp((float)config.GetValue(SettingsSection, "joystick_opacity", 1f), 0f, 1f);
        UltimateButtonOpacity = Mathf.Clamp((float)config.GetValue(SettingsSection, "ultimate_button_opacity", 1f), 0f, 1f);

        // Pushed straight onto DangerLevel here rather than waiting for a screen to apply it: the
        // consumers are static and scene-independent, so the flag has to be correct from boot, before
        // any UI exists to set it.
        ReducedMotion = (bool)config.GetValue(SettingsSection, "reduced_motion", false);
        DangerLevel.Reduced = ReducedMotion;

        // The selection used to be stored as an index into the built-in cast. Fall back to it when
        // the slug key is absent, so an existing settings.cfg keeps whoever it had picked instead of
        // silently resetting to Equilibrado.
        string slug = (string)config.GetValue(SettingsSection, "selected_character_slug", "");
        if (string.IsNullOrEmpty(slug))
            slug = CharacterCatalog.SlugForLegacyIndex((int)config.GetValue(SettingsSection, "selected_character", 0));

        SelectedCharacter = slug;
    }

    private const string RecordsFilePath = "user://records.cfg";
    private const int MaxRecords = 10;

    public readonly record struct ScoreRecord(int Score, string Date);

    // Stored as "score|date" strings rather than nested dictionaries: ConfigFile round-trips a
    // PackedStringArray cleanly and there's nothing here worth the extra parsing surface.
    public static List<ScoreRecord> LoadRecords()
    {
        var records = new List<ScoreRecord>();

        var config = new ConfigFile();
        if (config.Load(RecordsFilePath) != Error.Ok) return records;

        var raw = (string[])config.GetValue("records", "entries", System.Array.Empty<string>());
        foreach (string line in raw)
        {
            string[] parts = line.Split('|');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int score)) continue;
            records.Add(new ScoreRecord(score, parts[1]));
        }
        return records;
    }

    private static void AppendRecord(int score)
    {
        var records = LoadRecords();

        var now = Time.GetDatetimeDictFromSystem();
        string date = $"{now["day"].AsInt32():D2}/{now["month"].AsInt32():D2}/{now["year"].AsInt32()} "
                    + $"{now["hour"].AsInt32():D2}:{now["minute"].AsInt32():D2}";
        records.Add(new ScoreRecord(score, date));

        // Highest first, then keep only the top few — this is a leaderboard, not a full history, so an
        // unbounded file would grow forever for no benefit.
        records.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (records.Count > MaxRecords) records.RemoveRange(MaxRecords, records.Count - MaxRecords);

        var lines = new string[records.Count];
        for (int i = 0; i < records.Count; i++) lines[i] = $"{records[i].Score}|{records[i].Date}";

        var config = new ConfigFile();
        config.SetValue("records", "entries", lines);
        config.Save(RecordsFilePath);
    }

    // Leaving a run early on purpose. Records the score first, then resets — abandoning used to call
    // ResetRun directly, and since RegisterFinalScore only ever ran from NotifyPlayerDied, the entire
    // run's result was discarded with no warning and no way to get it back. A run you walked away
    // from still happened, so it still counts toward the high score and the records list.
    public void AbandonRun()
    {
        RegisterFinalScore();
        ResetRun();
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
        EventRewardMultiplier = 1f;
        SurvivabilityCatchUpMultiplier = 1f;
        _slowTimer?.Stop();
        _roundStartTimer?.Stop();
        SnapshotRoundStart();


        Resume();
    }
}
