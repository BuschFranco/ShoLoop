namespace ShooterLoop;

// Achievements only — missions live in their own screen (MissionsMenu) now that the two are
// separate buttons on MainMenu. Same tab-row-on-top pattern CosmeticsShopMenu uses, just one level
// (category: Progreso/Combate/Builds). Rows are read-only — an achievement resolves itself as stats
// change, there's nothing to tap.
public partial class AchievementsMenu : Control
{
    private PanelContainer _panel;
    private ScrollContainer _scroll;
    private Button _closeButton;
    private VBoxContainer _content;

    private AchievementCategory _category = AchievementCategory.Progress;
    private readonly Dictionary<AchievementCategory, Button> _categoryTabs = new();
    private VBoxContainer _rows;

    public override void _Ready()
    {
        Visible = false;

        _panel = GetNode<PanelContainer>("CenterContainer/Panel");
        _scroll = GetNode<ScrollContainer>("CenterContainer/Panel/Scroll");
        _closeButton = GetNode<Button>("CenterContainer/Panel/Scroll/Box/CloseButton");
        _content = GetNode<VBoxContainer>("CenterContainer/Panel/Scroll/Box/Content");

        _panel.AddThemeStyleboxOverride("panel", UIUtil.CreatePanelStyle(Palette.UltimatePanelBorder));
        var title = GetNode<Label>("CenterContainer/Panel/Scroll/Box/Title");
        UIUtil.AddSpeedLines(title.GetParent<Control>(), title.GetIndex());

        BuildCategoryTabs();
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 8);
        _content.AddChild(_rows);

        _closeButton.Pressed += Close;
        Juice.WireButtonFeedback(_closeButton);

        FitToOrientation();
    }

    // --- Category tabs -----------------------------------------------------------------------

    private void BuildCategoryTabs()
    {
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        _content.AddChild(grid);

        AddCategoryTab(grid, AchievementCategory.Progress, "Progreso");
        AddCategoryTab(grid, AchievementCategory.Combat, "Combate");
        AddCategoryTab(grid, AchievementCategory.Builds, "Builds");
    }

    private void AddCategoryTab(GridContainer grid, AchievementCategory category, string label)
    {
        var button = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        button.AddThemeFontSizeOverride("font_size", Palette.FontSize.Body);
        grid.AddChild(button);
        Juice.WireButtonFeedback(button);

        button.Pressed += () => SelectCategory(category);
        _categoryTabs[category] = button;
    }

    private void SelectCategory(AchievementCategory category)
    {
        if (_category == category) return;
        _category = category;
        RefreshCategoryTabs();
        RebuildRows();
    }

    private void RefreshCategoryTabs()
    {
        foreach (var (category, button) in _categoryTabs)
        {
            bool active = category == _category;
            var style = new StyleBoxFlat
            {
                BgColor = active ? new Color(Palette.Player, 0.28f) : new Color(0.043f, 0.024f, 0.078f, 0.7f),
                BorderColor = Palette.Player,
            };
            style.SetBorderWidthAll(active ? 3 : 1);
            style.SetContentMarginAll(4f);

            button.AddThemeStyleboxOverride("normal", style);
            button.AddThemeStyleboxOverride("hover", style);
            button.AddThemeStyleboxOverride("pressed", style);
            button.AddThemeColorOverride("font_color", active ? Colors.White : new Color(0.72f, 0.76f, 0.84f));
        }
    }

    // --- Rows ----------------------------------------------------------------------------------

    private void RebuildRows()
    {
        foreach (var child in _rows.GetChildren()) child.QueueFree();

        foreach (var def in AchievementCatalog.All)
            if (def.Category == _category) _rows.AddChild(BuildAchievementRow(def));

        FitToOrientation();
    }

    private Control BuildAchievementRow(AchievementDef def)
    {
        var gm = GameManager.Instance;
        bool unlocked = gm.IsAchievementUnlocked(def.Id);
        float current = Mathf.Min(def.Current(gm), def.Needed);

        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.102f, 0.0588f, 0.1686f, 0.75f),
            BorderColor = unlocked ? Palette.UltimatePanelBorder : new Color(0.4902f, 0.9922f, 0.9961f, 0.25f),
        };
        style.SetBorderWidthAll(2);
        style.SetContentMarginAll(8f);
        panel.AddThemeStyleboxOverride("panel", style);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);
        panel.AddChild(box);

        var nameRow = new HBoxContainer();
        box.AddChild(nameRow);

        var nameLabel = new Label { Text = def.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        nameLabel.AddThemeFontSizeOverride("font_size", Palette.FontSize.Body);
        nameLabel.AddThemeColorOverride("font_color", unlocked ? Colors.White : new Color(0.72f, 0.76f, 0.84f));
        nameRow.AddChild(nameLabel);

        var rewardLabel = new Label { Text = unlocked ? "✓" : $"+{def.RewardLibras}" };
        rewardLabel.AddThemeFontSizeOverride("font_size", Palette.FontSize.Body);
        rewardLabel.AddThemeColorOverride("font_color", unlocked ? Palette.UltimatePanelBorder : new Color(0.75f, 0.55f, 1f));
        nameRow.AddChild(rewardLabel);

        var descLabel = new Label { Text = def.Description };
        descLabel.AddThemeFontSizeOverride("font_size", Palette.FontSize.Caption);
        descLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.72f, 0.82f));
        box.AddChild(descLabel);

        var bar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(0f, 10f),
            MaxValue = def.Needed,
            Value = current,
            ShowPercentage = false,
        };
        bar.AddThemeStyleboxOverride("background", BarStyle(new Color(0.102f, 0.0588f, 0.1686f, 0.8f)));
        bar.AddThemeStyleboxOverride("fill", BarStyle(unlocked ? Palette.UltimatePanelBorder : Palette.Player));
        box.AddChild(bar);

        return panel;
    }

    private static StyleBoxFlat BarStyle(Color color) => new() { BgColor = color };

    private void FitToOrientation()
    {
        UIUtil.FitScrollToViewport(_scroll, _panel);
    }

    public void Open()
    {
        RefreshCategoryTabs();
        RebuildRows();

        _scroll.ScrollVertical = 0;

        Visible = true;
        Juice.ModalIn(_panel);
    }

    private void Close() => Juice.ModalOut(_panel, () => Visible = false);
}
