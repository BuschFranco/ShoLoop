namespace ShooterLoop;

public partial class HUD : Control
{
    private Label _livesLabel;
    private HeartIcon[] _heartIcons;
    private const int MaxHeartIcons = 3;
    private Label _shieldLabel;
    private ProgressBar _xpBar;
    private Label _levelLabel;
    private Label _roundLabel;
    private Label _roundTimerLabel;
    private Label _coinsLabel;
    private Label _scoreLabel;
    private Timer _scoreDeltaTimer;
    private int _lastScore = 0;
    private Button _ultimateButton;
    private ProgressBar _ultimateBar;
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

    public override void _Ready()
    {
        _roundLabel = GetNode<Label>("TopBarPanel/TopBar/RoundLabel");
        _roundTimerLabel = GetNode<Label>("RoundTimerLabel");
        _levelLabel = GetNode<Label>("TopBarPanel/TopBar/LevelLabel");
        _livesLabel = GetNode<Label>("TopBarPanel/TopBar/LivesRow/LivesLabel");
        _heartIcons = new[]
        {
            GetNode<HeartIcon>("TopBarPanel/TopBar/LivesRow/Heart1"),
            GetNode<HeartIcon>("TopBarPanel/TopBar/LivesRow/Heart2"),
            GetNode<HeartIcon>("TopBarPanel/TopBar/LivesRow/Heart3"),
        };
        _shieldLabel = GetNode<Label>("TopBarPanel/TopBar/ShieldLabel");
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
        _ultimateBar = GetNode<ProgressBar>("UltimateBar");
        _ultimateButton.Pressed += OnUltimatePressed;
        UpdateUltimateUi();

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
        _ultimateBar.Visible = hasUltimate;
        if (!hasUltimate) return;

        float remaining = _player.UltimateCooldownRemaining;
        float duration = _player.UltimateCooldownDuration;
        bool ready = remaining <= 0f;

        _ultimateBar.MaxValue = duration;
        _ultimateBar.Value = duration - remaining;
        _ultimateButton.Disabled = !ready;
        _ultimateButton.Text = ready ? "¡ULTIMATE!" : $"{remaining:0.0}s";
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

        UpdateCooldownIcons();
        UpdateUltimateUi();

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

        bool hasUltimate = _player.EquippedUltimate != null;
        _ultimateIcon.Visible = hasUltimate;
        if (hasUltimate)
            _ultimateIcon.CooldownFraction = _player.UltimateCooldownRemaining / _player.UltimateCooldownDuration;
    }

    // Up to MaxHeartIcons individual hearts, filled/unfilled to show current vs. lost lives —
    // falls back to the old "Vidas: x/y" text once MaxLives grows past what reads cleanly as
    // separate icons (Corazón Legendario can push it well past 3).
    private void OnLivesChanged(int current, int max)
    {
        bool showIcons = max <= MaxHeartIcons;
        _livesLabel.Visible = !showIcons;
        if (!showIcons)
        {
            _livesLabel.Text = $"Vidas: {current}/{max}";
            foreach (var heart in _heartIcons) heart.Visible = false;
            return;
        }

        for (int i = 0; i < _heartIcons.Length; i++)
        {
            _heartIcons[i].Visible = i < max;
            _heartIcons[i].Filled = i < current;
            _heartIcons[i].QueueRedraw();
        }
    }

    private void OnShieldChanged(int current, int max)
    {
        _shieldLabel.Visible = max > 0;
        _shieldLabel.Text = $"Escudo: {current}/{max}";
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
