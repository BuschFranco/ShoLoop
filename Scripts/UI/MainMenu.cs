namespace ShooterLoop;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        var startButton = GetNode<Button>("VBoxContainer/ButtonsRow/StartButton");
        var optionsButton = GetNode<Button>("VBoxContainer/ButtonsRow/OptionsButton");
        var buildsButton = GetNode<Button>("VBoxContainer/ButtonsRow/BuildsButton");
        var characterSelect = GetNode<CharacterSelectMenu>("CharacterSelectMenu");
        startButton.Pressed += characterSelect.Open;
        startButton.GrabFocus();

        Juice.WireButtonFeedback(startButton);
        Juice.WireButtonFeedback(optionsButton);
        Juice.WireButtonFeedback(buildsButton);

        var highScoreLabel = GetNode<Label>("VBoxContainer/HighScoreLabel");
        highScoreLabel.Text = $"Mejor puntaje: {GameManager.LoadHighScore()}";

        var options = GetNode<OptionsMenu>("OptionsMenu");
        optionsButton.Pressed += options.Open;

        var builds = GetNode<BuildsMenu>("BuildsMenu");
        buildsButton.Pressed += builds.Open;

        PopulateRecords();
        PlayEntranceAnimation(highScoreLabel);
    }

    // The title screen's own "arrival" — the first thing a player sees, so it fades+scales up as a
    // whole rather than snapping into place, and the high score/records callout (the part most
    // worth a second look) settles in a beat after the rest.
    private void PlayEntranceAnimation(Label highScoreLabel)
    {
        var vbox = GetNode<Control>("VBoxContainer");
        var recordsPanel = GetNode<Control>("VBoxContainer/RecordsRow/RecordsPanel");

        highScoreLabel.Modulate = new Color(1f, 1f, 1f, 0f);
        recordsPanel.Modulate = new Color(1f, 1f, 1f, 0f);

        Juice.ModalIn(vbox, 0.35f, 0.92f);

        var timer = GetTree().CreateTimer(0.2f);
        timer.Timeout += () =>
        {
            highScoreLabel.CreateTween().TweenProperty(highScoreLabel, "modulate:a", 1f, 0.25f);
        };

        var timer2 = GetTree().CreateTimer(0.32f);
        timer2.Timeout += () =>
        {
            recordsPanel.CreateTween().TweenProperty(recordsPanel, "modulate:a", 1f, 0.25f);
        };
    }

    private void PopulateRecords()
    {
        var list = GetNode<RichTextLabel>("VBoxContainer/RecordsRow/RecordsPanel/RecordsBox/RecordsList");
        var records = GameManager.LoadRecords();

        if (records.Count == 0)
        {
            list.Text = "Todavía no hay récords";
            return;
        }

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
