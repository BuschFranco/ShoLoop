namespace ShooterLoop;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        var startButton = GetNode<Button>("VBoxContainer/StartButton");
        startButton.Pressed += OnStartPressed;
        startButton.GrabFocus();

        var highScoreLabel = GetNode<Label>("VBoxContainer/HighScoreLabel");
        highScoreLabel.Text = $"Mejor puntaje: {GameManager.LoadHighScore()}";
    }

    private void OnStartPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Arena.tscn");
    }
}
