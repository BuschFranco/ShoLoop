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
        _scoreLabel.Text = $"Puntaje Final: {GameManager.Instance.Score}";
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
