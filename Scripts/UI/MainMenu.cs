namespace ShooterLoop;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        var startButton = GetNode<Button>("VBoxContainer/ButtonsRow/StartButton");
        var characterSelect = GetNode<CharacterSelectMenu>("CharacterSelectMenu");
        startButton.Pressed += characterSelect.Open;
        startButton.GrabFocus();

        var highScoreLabel = GetNode<Label>("VBoxContainer/HighScoreLabel");
        highScoreLabel.Text = $"Mejor puntaje: {GameManager.LoadHighScore()}";

        var options = GetNode<OptionsMenu>("OptionsMenu");
        GetNode<Button>("VBoxContainer/ButtonsRow/OptionsButton").Pressed += options.Open;

        var builds = GetNode<BuildsMenu>("BuildsMenu");
        GetNode<Button>("VBoxContainer/ButtonsRow/BuildsButton").Pressed += builds.Open;

        PopulateRecords();
    }

    // The dated top-ten written by GameManager.RegisterFinalScore at the end of every run. Rendered as
    // one Label with newlines rather than a node per row: it's a fixed, short, read-only list, so a
    // container full of children would be more machinery than the content justifies.
    private void PopulateRecords()
    {
        var list = GetNode<RichTextLabel>("VBoxContainer/RecordsRow/RecordsPanel/RecordsBox/RecordsList");
        var records = GameManager.LoadRecords();

        if (records.Count == 0)
        {
            list.Text = "Todavía no hay récords";
            return;
        }

        // Colors: position in gold, score in cyan, date in dim gold
        var lines = new string[records.Count];
        for (int i = 0; i < records.Count; i++)
        {
            string pos = $"{i + 1}.";
            string score = records[i].Score.ToString("N0");
            string date = records[i].Date;
            lines[i] = $"[color=#ffe066]{pos,-4}[/color][color=#7dfdfe]{score,10}[/color]   [color=#b8a040]{date}[/color]";
        }

        list.Text = string.Join("\n", lines);
    }
}
