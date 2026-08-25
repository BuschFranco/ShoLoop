namespace ShooterLoop;

public partial class GameOverScreen : Control
{
    private Button _restartButton;
    private Button _menuButton;
    private Button _quitButton;
    private Label _scoreLabel;
    private PanelContainer _panel;
    private VBoxContainer _summaryContainer;

    public override void _Ready()
    {
        AddToGroup("game_over_screen");
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;

        _panel = GetNode<PanelContainer>("Panel");
        _scoreLabel = GetNode<Label>("Panel/VBoxContainer/ScoreLabel");
        _summaryContainer = GetNode<VBoxContainer>("Panel/VBoxContainer/SummaryContainer");
        _restartButton = GetNode<Button>("Panel/VBoxContainer/RestartButton");
        _menuButton = GetNode<Button>("Panel/VBoxContainer/MenuButton");
        _quitButton = GetNode<Button>("Panel/VBoxContainer/QuitButton");

        _restartButton.Pressed += OnRestartPressed;
        _menuButton.Pressed += OnMenuPressed;
        _quitButton.Pressed += OnQuitPressed;

        Juice.WireButtonFeedback(_restartButton);
        Juice.WireButtonFeedback(_menuButton);
        Juice.WireButtonFeedback(_quitButton);
    }

    // This is the win/loss moment of a whole run, so it gets the heaviest treatment in the pass:
    // the panel fades+scales in, then once that settles the score — the one number the player
    // actually came here to see — gets a bigger punch than the standard ValuePop, and the run
    // recap reveals one line at a time instead of landing as a single block of text.
    public void Open()
    {
        var gm = GameManager.Instance;
        var player = GetTree().GetFirstNodeInGroup("player") as Player;

        _scoreLabel.Text = $"Puntaje Final: {gm.Score}";

        var lines = new List<string>
        {
            $"Ronda {gm.RoundNumber}   Nv {gm.Level}   {Glossary.Kills}: {gm.EnemiesKilled}",
            $"Monedas: {gm.Coins}",
        };

        if (player != null)
        {
            var builds = new List<string>();
            foreach (var cls in BuildCatalog.ClassOrder)
                if (player.IsClassActive(cls)) builds.Add(BuildCatalog.Name(cls));
            if (builds.Count > 0)
                lines.Add($"Build: {string.Join(" + ", builds)}");
        }

        foreach (Node child in _summaryContainer.GetChildren())
            child.QueueFree();

        var summaryLabels = new List<Label>();
        foreach (string line in lines)
        {
            var label = new Label();
            label.Text = line;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", Palette.FontSize.Caption);
            label.AddThemeColorOverride("font_color", new Color(0.65f, 0.72f, 0.82f));
            label.Modulate = new Color(1f, 1f, 1f, 0f);
            _summaryContainer.AddChild(label);
            summaryLabels.Add(label);
        }

        Visible = true;
        Juice.ModalIn(_panel);

        var scorePop = GetTree().CreateTimer(0.18f);
        scorePop.Timeout += () => Juice.ValuePop(_scoreLabel, 1.6f, 0.35f);

        for (int i = 0; i < summaryLabels.Count; i++)
        {
            var label = summaryLabels[i];
            var timer = GetTree().CreateTimer(0.2f + i * 0.08f);
            timer.Timeout += () =>
            {
                if (IsInstanceValid(label))
                    label.CreateTween().TweenProperty(label, "modulate:a", 1f, 0.2f);
            };
        }
    }

    private void OnRestartPressed()
    {
        GameManager.Instance.ResetRun();
        GetTree().ReloadCurrentScene();
    }

    private void OnMenuPressed()
    {
        GameManager.Instance.ResetRun();
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
    }

    // Confirmed because it's the one button here that ends the process rather than the run, and it
    // sits in a stack of three similarly-styled buttons where a mistap costs the player the screen
    // they're still reading.
    private void OnQuitPressed()
    {
        var dialog = GetTree().GetFirstNodeInGroup("confirm_dialog") as ConfirmDialog;
        if (dialog == null)
        {
            GetTree().Quit();
            return;
        }

        dialog.Ask(
            "¿Salir del juego?",
            "Tu puntaje ya quedó guardado. Se va a cerrar la aplicación.",
            "Salir",
            () => GetTree().Quit());
    }
}
