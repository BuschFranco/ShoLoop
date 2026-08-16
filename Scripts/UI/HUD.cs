namespace ShooterLoop;

public partial class HUD : Control
{
    private HeartIcon[] _heartIcons;
    private ShieldIcon[] _shieldIcons;
    private Control _shieldRow;
    private Label _statsLabel;
    private ProgressBar _xpBar;
    private Label _levelLabel;
    private Label _roundLabel;
    private Label _roundTimerLabel;
    private Label _coinsLabel;
    private Label _scoreLabel;
    private Timer _scoreDeltaTimer;
    private int _lastScore = 0;
    private Button _ultimateButton;
    private UltimateButtonIcon _ultimateButtonIcon;
    private Label _countdownLabel;
    private Player _player;
    private PanelContainer _topBarPanel;
    private int _lastPulsedSecond = -1;
    private const int TimerPulseThreshold = 5;
    private Control _cooldownIcons;
    private CooldownIcon _laserIcon;
    private CooldownIcon _missileIcon;
    private CooldownIcon _shieldRegenIcon;
    private CooldownIcon _ultimateIcon;
    private CooldownIcon _ondaIcon;
    private CooldownIcon _vendavalIcon;
    private Label _streakLabel;
    private int _lastStreak;
    private Tween _streakPulseTween;
    private Control _bossHealthBar;
    private ProgressBar _bossHpBar;
    private Enemy _currentBoss;

    public override void _Ready()
    {
        _roundLabel = GetNode<Label>("TopBarPanel/TopBar/RoundLabel");
        _roundTimerLabel = GetNode<Label>("RoundTimerLabel");
        _levelLabel = GetNode<Label>("TopBarPanel/TopBar/LevelLabel");
        // One icon per point of the real caps (MaxLivesCap 6, MaxShieldChargesCap 4), so neither row ever
        // needs a text fallback. The old code fell back to "Vidas: 4/4" the moment a single Corazón was
        // bought, because it only had three heart nodes.
        _heartIcons = new[]
        {
            GetNode<HeartIcon>("TopBarPanel/TopBar/LivesRow/Heart1"),
            GetNode<HeartIcon>("TopBarPanel/TopBar/LivesRow/Heart2"),
            GetNode<HeartIcon>("TopBarPanel/TopBar/LivesRow/Heart3"),
            GetNode<HeartIcon>("TopBarPanel/TopBar/LivesRow/Heart4"),
            GetNode<HeartIcon>("TopBarPanel/TopBar/LivesRow/Heart5"),
            GetNode<HeartIcon>("TopBarPanel/TopBar/LivesRow/Heart6"),
        };
        _shieldRow = GetNode<Control>("TopBarPanel/TopBar/ShieldRow");
        _shieldIcons = new[]
        {
            GetNode<ShieldIcon>("TopBarPanel/TopBar/ShieldRow/Shield1"),
            GetNode<ShieldIcon>("TopBarPanel/TopBar/ShieldRow/Shield2"),
            GetNode<ShieldIcon>("TopBarPanel/TopBar/ShieldRow/Shield3"),
            GetNode<ShieldIcon>("TopBarPanel/TopBar/ShieldRow/Shield4"),
        };
        _statsLabel = GetNode<Label>("TopBarPanel/TopBar/StatsLabel");
        _xpBar = GetNode<ProgressBar>("TopBarPanel/TopBar/XpBar");
        _coinsLabel = GetNode<Label>("TopBarPanel/TopBar/PointsLabel");
        _scoreLabel = GetNode<Label>("TopBarPanel/TopBar/ScoreLabel");
        _countdownLabel = GetNode<Label>("CountdownLabel");
        _topBarPanel = GetNode<PanelContainer>("TopBarPanel");
        _cooldownIcons = GetNode<Control>("CooldownIcons");
        _laserIcon = GetNode<CooldownIcon>("CooldownIcons/LaserIcon");
        _missileIcon = GetNode<CooldownIcon>("CooldownIcons/MissileIcon");
        _shieldRegenIcon = GetNode<CooldownIcon>("CooldownIcons/ShieldRegenIcon");
        _ultimateIcon = GetNode<CooldownIcon>("CooldownIcons/UltimateIcon");
        _ondaIcon = GetNode<CooldownIcon>("CooldownIcons/OndaIcon");
        _vendavalIcon = GetNode<CooldownIcon>("CooldownIcons/VendavalIcon");
        _bossHealthBar = GetNode<Control>("BossHealthBar");
        _bossHpBar = GetNode<ProgressBar>("BossHealthBar/BossHpBar");

        // Kill streak label — positioned center-right, below the round timer.
        _streakLabel = new Label();
        _streakLabel.Text = "";
        _streakLabel.AddThemeFontSizeOverride("font_size", 20);
        _streakLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.15f));
        _streakLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _streakLabel.AddThemeConstantOverride("outline_size", 3);
        _streakLabel.HorizontalAlignment = HorizontalAlignment.Right;
        AddChild(_streakLabel);

        // Build class lives inside the stats panel (UpdateStatsLabel), not as a floating label
        // below the top bar.

        _scoreDeltaTimer = new Timer();
        _scoreDeltaTimer.OneShot = true;
        _scoreDeltaTimer.WaitTime = 1.2;
        AddChild(_scoreDeltaTimer);
        _scoreDeltaTimer.Timeout += () => _scoreLabel.Text = $"Puntaje: {GameManager.Instance.Score}";

        GameManager.Instance.XpChanged += OnXpChanged;
        GameManager.Instance.LevelUp += OnLevelUp;
        GameManager.Instance.LevelsGained += OnLevelsGained;
        GameManager.Instance.RoundChanged += OnRoundChanged;
        GameManager.Instance.CoinsChanged += OnCoinsChanged;
        GameManager.Instance.ScoreChanged += OnScoreChanged;

        OnXpChanged(GameManager.Instance.Xp, GameManager.Instance.XpToNextLevel);
        OnLevelUp(GameManager.Instance.Level);
        OnRoundChanged(GameManager.Instance.RoundNumber);
        OnCoinsChanged(GameManager.Instance.Coins);
        OnScoreChanged(GameManager.Instance.Score);

        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_player != null)
        {
            _player.LivesChanged += OnLivesChanged;
            OnLivesChanged(_player.CurrentLives, _player.MaxLives);

            _player.ShieldChanged += OnShieldChanged;
            OnShieldChanged(_player.CurrentShieldCharges, _player.MaxShieldCharges);
        }

        _ultimateButton = GetNode<Button>("UltimateButton");
        _ultimateButtonIcon = GetNode<UltimateButtonIcon>("UltimateButton/UltimateButtonIcon");
        _ultimateButton.Pressed += OnUltimatePressed;
        UpdateUltimateUi();

        // GameManager's ApplyUltimateButtonOpacity targets this group (same pattern as the
        // joystick) so the setting can be applied from the main menu's options screen too.
        _ultimateButton.AddToGroup("ultimate_button");

        // Read here rather than pushed from the options screen — like the joystick, this scene
        // only exists during a run and the setting can't change mid-run. Modulate propagates to
        // the icon child; it doesn't affect hit-testing, so 0% keeps the button functional.
        _ultimateButton.Modulate = new Color(1f, 1f, 1f, GameManager.Instance?.UltimateButtonOpacity ?? 1f);

        var pauseButton = GetNode<Button>("PauseButton");
        pauseButton.Pressed += OnPausePressed;

        // The .tscn's own offsets give TopBarPanel a fixed 200x200 box purely as a safe non-zero
        // fallback. ResetSize() (called here and every frame below) shrinks it to exactly fit its
        // labels instead, so the panel hugs whatever text is actually showing rather than leaving
        // dead space or, worse, clipping it.
        _topBarPanel.ResetSize();
    }

    public override void _ExitTree()
    {
        // GameManager is a persistent autoload; HUD is scene-scoped and gets freed on
        // ReloadCurrentScene(). Without unsubscribing, these delegates would keep pointing at
        // this freed instance and blow up (aborting whatever triggered the event) the next
        // time GameManager raises them after a restart.
        if (GameManager.Instance == null) return;
        GameManager.Instance.XpChanged -= OnXpChanged;
        GameManager.Instance.LevelUp -= OnLevelUp;
        GameManager.Instance.LevelsGained -= OnLevelsGained;
        GameManager.Instance.RoundChanged -= OnRoundChanged;
        GameManager.Instance.CoinsChanged -= OnCoinsChanged;
        GameManager.Instance.ScoreChanged -= OnScoreChanged;
    }

    private void OnPausePressed()
    {
        GameManager.Instance.OpenPauseMenu();
    }

    private void OnUltimatePressed()
    {
        _player?.TriggerUltimate();
        UpdateUltimateUi();
    }

    // Polled every frame from _Process rather than pushed off an event — the cooldown just
    // decays continuously (see Player._PhysicsProcess), there's no discrete "it changed" moment
    // to hang an event off of the way the old Score-driven charge meter had.
    private void UpdateUltimateUi()
    {
        bool hasUltimate = _player != null && _player.EquippedUltimate != null;
        _ultimateButton.Visible = hasUltimate;
        if (!hasUltimate) return;

        _ultimateButtonIcon.Kind = _player.EquippedUltimate.Value;

        float remaining = _player.UltimateCooldownRemaining;
        float duration = _player.UltimateCooldownDuration;
        bool ready = remaining <= 0f;

        _ultimateButtonIcon.CooldownFraction = duration > 0f ? Mathf.Clamp(remaining / duration, 0f, 1f) : 0f;
        _ultimateButtonIcon.CooldownSeconds = ready ? 0f : remaining;
        _ultimateButton.Disabled = !ready;
    }

    public override void _Process(double delta)
    {
        // Re-fit every frame: round number, timer, and coin/score text all change length at
        // runtime (e.g. "Ronda 9" -> "Ronda 10"), and a stale size would either clip the new text
        // or leave the panel oversized for shorter text. Cheap for a handful of labels.
        _topBarPanel.ResetSize();

        // Pinned to the stats panel's right edge rather than a fixed offset, since the panel's
        // own width breathes with its text (see above) — a fixed offset would drift away from or
        // overlap the panel as "Ronda 9" becomes "Ronda 10".
        _cooldownIcons.Position = _topBarPanel.Position + new Vector2(_topBarPanel.Size.X + 12f, 0f);

        // Same reasoning, but pinned below the panel's bottom edge instead of its right — the
        // panel's height also breathes (e.g. the Shield row only appears once you own a Barrier).
        _roundTimerLabel.Position = _topBarPanel.Position + new Vector2(0f, _topBarPanel.Size.Y + 10f);

        // Kill streak to the right of the round timer, same vertical level.
        _streakLabel.Position = _topBarPanel.Position + new Vector2(150f, _topBarPanel.Size.Y + 10f);
        _streakLabel.Size = new Vector2(200f, 30f);

        UpdateCooldownIcons();
        UpdateUltimateUi();
        UpdateBossHealthBar();
        UpdateStatsLabel();

        // The pre-round countdown gets its own big centered label rather than the small top-bar
        // one — at 72px in the middle of the screen it's actually noticeable, which the corner
        // timer wasn't.
        if (GameManager.Instance.IsRoundStarting)
        {
            _countdownLabel.Visible = true;
            _countdownLabel.Text = Mathf.CeilToInt(GameManager.Instance.RoundStartTimeRemaining).ToString();
            _roundTimerLabel.Text = "Preparate...";
            ResetRoundTimerLook();
            return;
        }

        _countdownLabel.Visible = false;

        if (GameManager.Instance.IsBossRound)
        {
            _roundTimerLabel.Text = "¡JEFE!";
            ResetRoundTimerLook();
            return;
        }

        int secondsLeft = Mathf.CeilToInt(GameManager.Instance.RoundTimeRemaining);
        _roundTimerLabel.Text = $"{secondsLeft / 60}:{secondsLeft % 60:D2}";

        // The countdown used to be buried at font_size 15 inside the corner stat list — easy to
        // miss even though it's arguably the most time-pressured number on screen. Pulsing on
        // every one of the last 5 seconds (rather than just turning red once) is what actually
        // catches the eye at a glance without having to read the number.
        bool urgent = secondsLeft > 0 && secondsLeft <= TimerPulseThreshold;
        _roundTimerLabel.AddThemeColorOverride("font_color", urgent ? Palette.Warning : new Color(0.85f, 0.9f, 0.95f));

        if (urgent && secondsLeft != _lastPulsedSecond)
        {
            _lastPulsedSecond = secondsLeft;
            PulseRoundTimer();
        }
        else if (!urgent)
        {
            _lastPulsedSecond = -1;
        }

        // Kill streak display
        if (_player != null && _player.KillStreak >= 3)
        {
            _streakLabel.Text = $"RACHA DE {_player.KillStreak} (×{_player.KillStreakMultiplier:0.0})";
            _streakLabel.Visible = true;
            // Color progresivo: blanco → rojo intenso según racha
            float t = Mathf.Min(_player.KillStreak / 40f, 1f);
            _streakLabel.Modulate = new Color(1f, Mathf.Lerp(1f, 0.15f, t), Mathf.Lerp(1f, 0f, t));

            // Pulse animation when streak increases.
            if (_player.KillStreak > _lastStreak)
            {
                _streakPulseTween?.Kill();
                _streakPulseTween = CreateTween();
                _streakPulseTween.TweenProperty(_streakLabel, "scale", new Vector2(1.4f, 1.4f), 0.1f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                _streakPulseTween.TweenProperty(_streakLabel, "scale", Vector2.One, 0.15f)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            }
            _lastStreak = _player.KillStreak;
        }
        else
        {
            _streakLabel.Visible = false;
            _lastStreak = 0;
        }
    }

    private Tween _roundTimerPulseTween;

    private void PulseRoundTimer()
    {
        _roundTimerPulseTween?.Kill();
        _roundTimerLabel.Scale = Vector2.One * 1.5f;
        _roundTimerPulseTween = _roundTimerLabel.CreateTween();
        _roundTimerPulseTween.TweenProperty(_roundTimerLabel, "scale", Vector2.One, 0.35)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void ResetRoundTimerLook()
    {
        _lastPulsedSecond = -1;
        _roundTimerPulseTween?.Kill();
        _roundTimerLabel.Scale = Vector2.One;
        _roundTimerLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 0.95f));
    }

    private void UpdateCooldownIcons()
    {
        if (_player == null) return;

        _laserIcon.Visible = _player.LaserLevel > 0;
        _laserIcon.CooldownFraction = _player.LaserCooldownFraction;

        _missileIcon.Visible = _player.MissileLevel > 0;
        _missileIcon.CooldownFraction = _player.MissileCooldownFraction;

        _shieldRegenIcon.Visible = _player.ShieldRegenPerMinute > 0f;
        _shieldRegenIcon.CooldownFraction = _player.ShieldRegenCooldownFraction;

        _ondaIcon.Visible = _player.OndaLevel > 0;
        _ondaIcon.CooldownFraction = _player.OndaCooldownFraction;

        _vendavalIcon.Visible = _player.VendavalLevel > 0;
        _vendavalIcon.CooldownFraction = _player.VendavalCooldownFraction;

        bool hasUltimate = _player.EquippedUltimate != null;
        _ultimateIcon.Visible = hasUltimate;
        if (hasUltimate)
            _ultimateIcon.CooldownFraction = _player.UltimateCooldownRemaining / _player.UltimateCooldownDuration;
    }

    // Polled the same way _currentBoss/_topBarPanel are — no HP-changed signal exists on Enemy,
    // and everything else in this file already reads live state per frame instead.
    private void UpdateBossHealthBar()
    {
        if (_currentBoss == null || !IsInstanceValid(_currentBoss))
            _currentBoss = GetTree().GetFirstNodeInGroup("boss") as Enemy;

        bool bossAlive = _currentBoss != null && IsInstanceValid(_currentBoss);
        _bossHealthBar.Visible = bossAlive;
        if (!bossAlive) return;

        _bossHpBar.MaxValue = _currentBoss.MaxHp;
        _bossHpBar.Value = _currentBoss.CurrentHp;
    }

    // One heart per life slot, filled/unfilled to show current vs. lost. There are six nodes because
    // MaxLivesCap is six — no text fallback any more, which is what the old three-icon version degraded
    // to as soon as a single Corazón was bought.
    private void OnLivesChanged(int current, int max)
    {
        for (int i = 0; i < _heartIcons.Length; i++)
        {
            _heartIcons[i].Visible = i < max;
            _heartIcons[i].Filled = i < current;
            _heartIcons[i].QueueRedraw();
        }
    }

    // Mirrors the lives row exactly. The whole row hides when the player owns no Barrier, same as the
    // old text line did — an empty row of four unfilled shields would suggest charges they can't have.
    private void OnShieldChanged(int current, int max)
    {
        _shieldRow.Visible = max > 0;
        for (int i = 0; i < _shieldIcons.Length; i++)
        {
            _shieldIcons[i].Visible = i < max;
            _shieldIcons[i].Filled = i < current;
            _shieldIcons[i].QueueRedraw();
        }
    }

    // Polled rather than pushed: none of these five have a change event. Damage/fire rate/crit/range only
    // move inside Player.ApplyUpgrade, and EnemiesKilled is a plain field on GameManager — so this follows
    // the same per-frame-read approach UpdateCooldownIcons and UpdateUltimateUi already use.
    private void UpdateStatsLabel()
    {
        if (_player == null) return;

        string stats =
            $"DAÑO {_player.BulletDamage} · CAD {_player.FireRate:0.0}/s · CRIT {_player.CritChance:0}%\n" +
            $"RANGO {_player.FireRange:0} · BAJAS {GameManager.Instance.EnemiesKilled}";

        // Fixed display order so the panel doesn't re-shuffle as builds unlock — the set grows,
        // but listing order stays stable from run to run.
        var names = new List<string>();
        if (_player.IsClassActive(Player.BuildClass.Gunner)) names.Add("ARTILLERO");
        if (_player.IsClassActive(Player.BuildClass.Tank)) names.Add("TANQUE");
        if (_player.IsClassActive(Player.BuildClass.Assassin)) names.Add("ASESINO");
        if (_player.IsClassActive(Player.BuildClass.Explorer)) names.Add("EXPLORADOR");
        if (_player.IsClassActive(Player.BuildClass.Pyromaniac)) names.Add("PIRÓMANO");
        if (_player.IsClassActive(Player.BuildClass.Armored)) names.Add("ACORAZADO");
        if (_player.IsClassActive(Player.BuildClass.Hunter)) names.Add("CAZADOR");

        _statsLabel.Text = names.Count == 0 ? stats : $"{stats}\nBUILD {string.Join(" · ", names)}";
    }

    private void OnXpChanged(int xp, int xpToNext)
    {
        _xpBar.MaxValue = xpToNext;
        _xpBar.Value = xp;
    }

    private void OnLevelUp(int level)
    {
        _levelLabel.Text = $"Nv {level}";
    }

    // Fires once per AddXp call with the TOTAL levels crossed (not once per threshold like
    // LevelUp above), so a triple level-up from one big XP reward shows a single "+3" rather than
    // three "+1"s landing on top of each other in the same frame. Spawned as a sibling of TopBar,
    // not a child of _levelLabel — parenting it into the VBoxContainer would make VBoxContainer try
    // to lay it out as part of the stat list instead of letting it float freely over everything.
    private void OnLevelsGained(int count)
    {
        var label = new Label();
        label.Text = $"+{count}";
        label.AddThemeColorOverride("font_color", Palette.LevelPopup);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 2);
        label.AddThemeFontSizeOverride("font_size", 16);
        label.MouseFilter = MouseFilterEnum.Ignore;
        label.ZIndex = 20;
        AddChild(label);
        label.GlobalPosition = _levelLabel.GlobalPosition + new Vector2(_levelLabel.Size.X + 6f, 0f);
        label.Scale = Vector2.One * 1.6f;

        var tween = label.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "scale", Vector2.One, 0.25f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "position", label.Position + new Vector2(0f, -20f), 0.7f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 0.5f).SetDelay(0.35f);
        tween.Chain().TweenCallback(Callable.From(() => label.QueueFree()));
    }

    private void OnRoundChanged(int round)
    {
        _roundLabel.Text = $"Ronda {round}";
    }

    private void OnCoinsChanged(int coins)
    {
        _coinsLabel.Text = $"Monedas: {coins}";
    }

    private void OnScoreChanged(int score)
    {
        int delta = score - _lastScore;
        _lastScore = score;

        if (delta > 0)
        {
            _scoreLabel.Text = $"Puntaje: {score} (+{delta})";
            _scoreDeltaTimer.Start();
        }
        else
        {
            _scoreLabel.Text = $"Puntaje: {score}";
        }
    }
}
