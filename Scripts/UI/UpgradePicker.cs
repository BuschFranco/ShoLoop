namespace ShooterLoop;

public partial class UpgradePicker : Control
{
    private static readonly StyleBoxFlat GlowStyle = CreateGlowStyle();
    private static readonly StyleBoxFlat RewardPanelStyle = CreatePanelStyle(Palette.RewardPanelBorder);
    private static readonly StyleBoxFlat UltimatePanelStyle = CreatePanelStyle(Palette.UltimatePanelBorder);

    public const string LevelUpTitle = "¡SUBISTE DE NIVEL! Elige una mejora";
    public const string UltimateTitle = "¡JEFE DERROTADO! Elige tu Ultimate";

    // Each option's outer node is a PanelContainer (needed so it actually lays out its
    // Label+Button children — a plain Button isn't a Container and won't size/position arbitrary
    // child nodes, which is exactly what made every card's content overlap when this used to be a
    // Button). Tapping anywhere on it that isn't the inner "Elegir" button — which keeps its own
    // default mouse_filter=Stop and so still claims/consumes its own clicks first — falls through
    // (Label/VBox children below are mouse_filter=Ignore) to this panel's own GuiInput.
    private PanelContainer[] _optionPanels;
    private PanelContainer _panel;
    private Button[] _buttons;
    private Label[] _labels;
    private Label _title;
    private List<UpgradeData> _currentChoices;
    private bool[] _useless;

    public override void _Ready()
    {
        AddToGroup("upgrade_picker");
        Visible = false;

        ProcessMode = ProcessModeEnum.Always;

        _panel = GetNode<PanelContainer>("CenterContainer/Panel");
        _optionPanels = new[]
        {
            GetNode<PanelContainer>("CenterContainer/Panel/VBoxContainer/Option1"),
            GetNode<PanelContainer>("CenterContainer/Panel/VBoxContainer/Option2"),
            GetNode<PanelContainer>("CenterContainer/Panel/VBoxContainer/Option3"),
        };
        _buttons = new[]
        {
            GetNode<Button>("CenterContainer/Panel/VBoxContainer/Option1/Content/Button"),
            GetNode<Button>("CenterContainer/Panel/VBoxContainer/Option2/Content/Button"),
            GetNode<Button>("CenterContainer/Panel/VBoxContainer/Option3/Content/Button"),
        };
        _labels = new[]
        {
            GetNode<Label>("CenterContainer/Panel/VBoxContainer/Option1/Content/Label"),
            GetNode<Label>("CenterContainer/Panel/VBoxContainer/Option2/Content/Label"),
            GetNode<Label>("CenterContainer/Panel/VBoxContainer/Option3/Content/Label"),
        };
        _title = GetNode<Label>("CenterContainer/Panel/VBoxContainer/Title");
        _useless = new bool[_buttons.Length];

        for (int i = 0; i < _buttons.Length; i++)
        {
            int index = i;
            _buttons[i].Pressed += () => OnChoicePressed(index);
            _optionPanels[i].GuiInput += @event => OnOptionGuiInput(@event, index);
        }
    }

    private void OnOptionGuiInput(InputEvent @event, int index)
    {
        if (_useless[index]) return;

        bool released = (@event is InputEventScreenTouch touch && !touch.Pressed)
            || (@event is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left && !mouse.Pressed);
        if (released) OnChoicePressed(index);
    }

    // The same picker serves both level-up rewards and the one-time post-boss Ultimate choice —
    // the heading is passed in rather than baked into the scene, and isUltimate swaps the panel's
    // border/title colour (gold vs. the level-up picker's cyan) so the two can't be mistaken for
    // each other at a glance, the way two identically-styled modals easily could.
    public void Open(List<UpgradeData> choices, string title = LevelUpTitle, bool isUltimate = false)
    {
        _currentChoices = choices;
        _title.Text = title;

        var panelStyle = isUltimate ? UltimatePanelStyle : RewardPanelStyle;
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        _title.AddThemeColorOverride("font_color", isUltimate ? Palette.UltimatePanelBorder : Palette.RewardPanelBorder);

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

            if (highlight[i]) _optionPanels[i].AddThemeStyleboxOverride("panel", GlowStyle);
            else _optionPanels[i].RemoveThemeStyleboxOverride("panel");

            bool useless = player != null && player.IsRewardUseless(choice);
            _buttons[i].Disabled = useless;
            _buttons[i].Text = useless ? player.GetUnavailableLabel(choice) : "Elegir";

            // Keeps the whole-card tap target from firing on an item the inner button already
            // refuses (PanelContainer has no Disabled property to sync, so OnOptionGuiInput
            // checks this array instead).
            _useless[i] = useless;
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
        style.BgColor = new Color(Palette.GlowHighlight, 0.14f);
        style.BorderColor = Palette.GlowHighlight;
        style.SetBorderWidthAll(3);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(6);
        return style;
    }

    private static StyleBoxFlat CreatePanelStyle(Color borderColor)
    {
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.043f, 0.024f, 0.078f, 0.92f);
        style.BorderColor = new Color(borderColor, 0.85f);
        style.SetBorderWidthAll(3);
        style.SetCornerRadiusAll(12);
        style.SetContentMarginAll(16f);
        style.ContentMarginTop = 14f;
        style.ContentMarginBottom = 14f;
        return style;
    }
}
