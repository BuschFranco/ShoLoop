namespace ShooterLoop;

public partial class HUD : Control
{
    private Label _livesLabel;
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

    public override void _Ready()
    {
        _roundLabel = GetNode<Label>("TopBar/RoundLabel");
        _roundTimerLabel = GetNode<Label>("TopBar/RoundTimerLabel");
        _levelLabel = GetNode<Label>("TopBar/LevelLabel");
        _livesLabel = GetNode<Label>("TopBar/LivesLabel");
        _shieldLabel = GetNode<Label>("TopBar/ShieldLabel");
        _xpBar = GetNode<ProgressBar>("TopBar/XpBar");
        _coinsLabel = GetNode<Label>("TopBar/PointsLabel");
        _scoreLabel = GetNode<Label>("TopBar/ScoreLabel");
        _countdownLabel = GetNode<Label>("CountdownLabel");

        _scoreDeltaTimer = new Timer();
        _scoreDeltaTimer.OneShot = true;
        _scoreDeltaTimer.WaitTime = 1.2;
        AddChild(_scoreDeltaTimer);
        _scoreDeltaTimer.Timeout += () => _scoreLabel.Text = $"Puntaje: {GameManager.Instance.Score}";

        GameManager.Instance.XpChanged += OnXpChanged;
        GameManager.Instance.LevelUp += OnLevelUp;
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

            _player.UltimateChargeChanged += OnUltimateChargeChanged;
        }

        _ultimateButton = GetNode<Button>("UltimateButton");
        _ultimateBar = GetNode<ProgressBar>("UltimateBar");
        _ultimateButton.Pressed += OnUltimatePressed;
        UpdateUltimateUi();

        var pauseButton = GetNode<Button>("PauseButton");
        pauseButton.Pressed += OnPausePressed;
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

    private void OnUltimateChargeChanged(float current, float target)
    {
        UpdateUltimateUi();
    }

    private void UpdateUltimateUi()
    {
        bool hasUltimate = _player != null && _player.EquippedUltimate != null;
        _ultimateButton.Visible = hasUltimate;
        _ultimateBar.Visible = hasUltimate;
        if (!hasUltimate) return;

        _ultimateBar.MaxValue = Player.UltimateChargeTarget;
        _ultimateBar.Value = _player.UltimateProgress;
        bool ready = _player.UltimateProgress >= Player.UltimateChargeTarget;
        _ultimateButton.Disabled = !ready;
        _ultimateButton.Text = ready ? "¡ULTIMATE!" : $"{_player.EquippedUltimate}";
    }

    public override void _Process(double delta)
    {
        // The pre-round countdown gets its own big centered label rather than the small top-bar
        // one — at 72px in the middle of the screen it's actually noticeable, which the corner
        // timer wasn't.
        if (GameManager.Instance.IsRoundStarting)
        {
            _countdownLabel.Visible = true;
            _countdownLabel.Text = Mathf.CeilToInt(GameManager.Instance.RoundStartTimeRemaining).ToString();
            _roundTimerLabel.Text = "Preparate...";
            return;
        }

        _countdownLabel.Visible = false;

        if (GameManager.Instance.IsBossRound)
        {
            _roundTimerLabel.Text = "¡JEFE!";
            return;
        }

        int secondsLeft = Mathf.CeilToInt(GameManager.Instance.RoundTimeRemaining);
        _roundTimerLabel.Text = $"{secondsLeft / 60}:{secondsLeft % 60:D2}";
    }

    private void OnLivesChanged(int current, int max)
    {
        _livesLabel.Text = $"Vidas: {current}/{max}";
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
