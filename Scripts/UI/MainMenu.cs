namespace ShooterLoop;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        var startButton = GetNode<Button>("VBoxContainer/ButtonsRow/StartButton");
        startButton.Pressed += OnStartPressed;
        startButton.GrabFocus();

        var highScoreLabel = GetNode<Label>("VBoxContainer/HighScoreLabel");
        highScoreLabel.Text = $"Mejor puntaje: {GameManager.LoadHighScore()}";

        var options = GetNode<OptionsMenu>("OptionsMenu");
        GetNode<Button>("VBoxContainer/ButtonsRow/OptionsButton").Pressed += options.Open;

        PopulateRecords();
    }

    // The dated top-ten written by GameManager.RegisterFinalScore at the end of every run. Rendered as
    // one Label with newlines rather than a node per row: it's a fixed, short, read-only list, so a
    // container full of children would be more machinery than the content justifies.
    private void PopulateRecords()
    {
        var list = GetNode<Label>("VBoxContainer/RecordsRow/RecordsPanel/RecordsBox/RecordsList");
        var records = GameManager.LoadRecords();

        if (records.Count == 0)
        {
            list.Text = "Todavía no hay récords";
            return;
        }

        var lines = new string[records.Count];
        for (int i = 0; i < records.Count; i++)
            lines[i] = $"{i + 1}.  {records[i].Score}  —  {records[i].Date}";

        list.Text = string.Join("\n", lines);
    }

    private void OnStartPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Arena.tscn");
    }
}
