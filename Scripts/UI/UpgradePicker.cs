namespace ShooterLoop;

public partial class UpgradePicker : Control
{
    private static readonly StyleBoxFlat GlowStyle = CreateGlowStyle();

    public const string LevelUpTitle = "¡SUBISTE DE NIVEL! Elige una mejora";
    public const string UltimateTitle = "¡JEFE DERROTADO! Elige tu Ultimate";

    private PanelContainer[] _panels;
    private Button[] _buttons;
    private Label[] _labels;
    private Label _title;
    private List<UpgradeData> _currentChoices;

    public override void _Ready()
    {
        AddToGroup("upgrade_picker");
        Visible = false;

        ProcessMode = ProcessModeEnum.Always;

        _panels = new[]
        {
            GetNode<PanelContainer>("Panel/VBoxContainer/Option1"),
            GetNode<PanelContainer>("Panel/VBoxContainer/Option2"),
            GetNode<PanelContainer>("Panel/VBoxContainer/Option3"),
        };
        _buttons = new[]
        {
            GetNode<Button>("Panel/VBoxContainer/Option1/Content/Button"),
            GetNode<Button>("Panel/VBoxContainer/Option2/Content/Button"),
            GetNode<Button>("Panel/VBoxContainer/Option3/Content/Button"),
        };
        _labels = new[]
        {
            GetNode<Label>("Panel/VBoxContainer/Option1/Content/Label"),
            GetNode<Label>("Panel/VBoxContainer/Option2/Content/Label"),
            GetNode<Label>("Panel/VBoxContainer/Option3/Content/Label"),
        };
        _title = GetNode<Label>("Panel/VBoxContainer/Title");

        for (int i = 0; i < _buttons.Length; i++)
        {
            int index = i;
            _buttons[i].Pressed += () => OnChoicePressed(index);
        }
    }

    // The same picker serves both level-up rewards and the one-time post-boss Ultimate choice, so
    // the heading is passed in rather than baked into the scene.
    public void Open(List<UpgradeData> choices, string title = LevelUpTitle)
    {
        _currentChoices = choices;
        _title.Text = title;
        var player = GetTree().GetFirstNodeInGroup("player") as Player;

        // Computed across all 3 offers at once so only the strongest of each reward family glows,
        // rather than every offer that merely beats the player's current state.
        bool[] highlight = player?.GetHighlightFlags(choices) ?? new bool[choices.Count];

        for (int i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            string text = $"[{RewardTierRoller.GetTierName(choice.Tier)}] {choice.Name}\n{choice.Description}";

            if (player != null)
            {
                string info = player.GetStackInfoText(choice);
                if (info != null) text += $"\n{info}";
            }

            _labels[i].Text = text;
            _labels[i].AddThemeColorOverride("font_color", RewardTierRoller.GetTierColor(choice.Tier));

            if (highlight[i]) _panels[i].AddThemeStyleboxOverride("panel", GlowStyle);
            else _panels[i].RemoveThemeStyleboxOverride("panel");

            bool useless = player != null && player.IsRewardUseless(choice);
            _buttons[i].Disabled = useless;
            _buttons[i].Text = useless ? player.GetUnavailableLabel(choice) : "Elegir";
        }

        Visible = true;
    }

    private void OnChoicePressed(int index)
    {
        var chosen = _currentChoices[index];
        Visible = false;
        GameManager.Instance.ApplyUpgradeAndResume(chosen);
    }

    private static StyleBoxFlat CreateGlowStyle()
    {
        var style = new StyleBoxFlat();
        style.BgColor = new Color(1f, 0.95f, 0.6f, 0.12f);
        style.BorderColor = new Color(1f, 0.9f, 0.3f, 1f);
        style.SetBorderWidthAll(3);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(6);
        return style;
    }
}
