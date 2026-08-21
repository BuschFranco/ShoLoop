namespace ShooterLoop;

public partial class GameOverScreen : Control
{
    private Button _restartButton;
    private Button _menuButton;
    private Button _quitButton;
    private Label _scoreLabel;

    public override void _Ready()
    {
        AddToGroup("game_over_screen");
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;

        _scoreLabel = GetNode<Label>("Panel/VBoxContainer/ScoreLabel");
        _restartButton = GetNode<Button>("Panel/VBoxContainer/RestartButton");
        _menuButton = GetNode<Button>("Panel/VBoxContainer/MenuButton");
        _quitButton = GetNode<Button>("Panel/VBoxContainer/QuitButton");

        _restartButton.Pressed += OnRestartPressed;
        _menuButton.Pressed += OnMenuPressed;
        _quitButton.Pressed += OnQuitPressed;
    }

    public void Open()
    {
        var gm = GameManager.Instance;
        var player = GetTree().GetFirstNodeInGroup("player") as Player;

        _scoreLabel.Text = $"Puntaje Final: {gm.Score}";

        var summary = GetNodeOrNull<Label>("Panel/VBoxContainer/RunSummary");
        if (summary != null)
        {
            var lines = new List<string>
            {
                $"Ronda {gm.RoundNumber}   Nv {gm.Level}   Enemigos: {gm.EnemiesKilled}",
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

            summary.Text = string.Join("\n", lines);
        }

        Visible = true;
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

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
