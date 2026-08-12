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

        var landscapeButton = GetNode<Button>("VBoxContainer/OrientationRow/LandscapeButton");
        var portraitButton = GetNode<Button>("VBoxContainer/OrientationRow/PortraitButton");

        // Reflect whatever GameManager already applied on boot (see GameManager._Ready) rather
        // than assuming one or the other — a returning player's saved choice shouldn't show the
        // wrong button lit up. Falls back to Portrait (the project default) in the never-expected
        // case GameManager isn't up yet, matching LoadOrientationPreference's own fallback.
        bool isPortrait = GameManager.Instance?.CurrentOrientation != GameManager.ScreenOrientation.Landscape;
        landscapeButton.SetPressedNoSignal(!isPortrait);
        portraitButton.SetPressedNoSignal(isPortrait);

        landscapeButton.Toggled += pressed =>
        {
            if (pressed) GameManager.Instance?.SetOrientation(GameManager.ScreenOrientation.Landscape);
        };
        portraitButton.Toggled += pressed =>
        {
            if (pressed) GameManager.Instance?.SetOrientation(GameManager.ScreenOrientation.Portrait);
        };
    }

    private void OnStartPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Arena.tscn");
    }
}
