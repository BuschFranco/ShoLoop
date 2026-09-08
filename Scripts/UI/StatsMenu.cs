namespace ShooterLoop;

// Read-only lifetime stats. Two sections: an "Actividad" block filtered by GameManager.StatsPeriod
// (Total/Mes/Semana — the numbers that are genuinely sums over time, backed by GameManager's daily
// log), and a "Resumen general" block of state/collection stats that don't have a meaningful
// per-period reading (current account level, best-ever round, how many of the 7 builds/27
// Legendarias/achievements have been reached) — those stay the same regardless of which tab is
// selected. No progress bar on any row: a stat has no "target" to fill toward, just a number.
public partial class StatsMenu : Control
{
    private PanelContainer _panel;
    private ScrollContainer _scroll;
    private Button _closeButton;
    private VBoxContainer _content;
    private VBoxContainer _rows;

    private GameManager.StatsPeriod _period = GameManager.StatsPeriod.Total;
    private readonly Dictionary<GameManager.StatsPeriod, Button> _tabs = new();

    public override void _Ready()
    {
        Visible = false;

        _panel = GetNode<PanelContainer>("CenterContainer/Panel");
        _scroll = GetNode<ScrollContainer>("CenterContainer/Panel/Scroll");
        _closeButton = GetNode<Button>("CenterContainer/Panel/Scroll/Box/CloseButton");
        _content = GetNode<VBoxContainer>("CenterContainer/Panel/Scroll/Box/Content");

        _panel.AddThemeStyleboxOverride("panel", UIUtil.CreatePanelStyle(Palette.OndaBlast));

        BuildTabs();
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 8);
        _content.AddChild(_rows);

        _closeButton.Pressed += Close;
        Juice.WireButtonFeedback(_closeButton);

        FitToOrientation();
    }

    // --- Total / Mes / Semana tabs ----------------------------------------------------------------

    private void BuildTabs()
    {
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        _content.AddChild(grid);

        AddTab(grid, GameManager.StatsPeriod.Total, "Total");
        AddTab(grid, GameManager.StatsPeriod.Month, "Mes");
        AddTab(grid, GameManager.StatsPeriod.Week, "Semana");
    }

    private void AddTab(GridContainer grid, GameManager.StatsPeriod period, string label)
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

        button.Pressed += () => SelectPeriod(period);
        _tabs[period] = button;
    }

    private void SelectPeriod(GameManager.StatsPeriod period)
    {
        if (_period == period) return;
        _period = period;
        RefreshTabs();
        RebuildRows();
    }

    private void RefreshTabs()
    {
        foreach (var (period, button) in _tabs)
        {
            bool active = period == _period;
            var style = new StyleBoxFlat
            {
                BgColor = active ? new Color(Palette.OndaBlast, 0.28f) : new Color(0.043f, 0.024f, 0.078f, 0.7f),
                BorderColor = Palette.OndaBlast,
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

        var gm = GameManager.Instance;
        var stats = gm.GetStats(_period);

        _rows.AddChild(SectionHeading("ACTIVIDAD"));
        AddRow("Enemigos eliminados", stats.EnemiesKilled.ToString());
        AddRow("Jefes derrotados", stats.BossesKilled.ToString());
        AddRow("Críticos", stats.CritsLanded.ToString());
        AddRow("Rondas superadas", stats.RoundsCleared.ToString());
        AddRow("Monedas ganadas", stats.CoinsEarned.ToString());
        AddRow("Libras ganadas", stats.LibrasEarned.ToString());
        AddRow("Partidas jugadas", stats.RunsPlayed.ToString());
        AddRow("Tiempo jugado", FormatDuration(stats.PlayTimeSeconds));

        _rows.AddChild(SectionHeading("RESUMEN GENERAL"));
        AddRow("Mejor puntaje", GameManager.LoadHighScore().ToString());
        AddRow("Mejor ronda alcanzada", gm.BestRoundReached.ToString());
        AddRow("Nivel de cuenta", gm.AccountLevel.ToString());
        AddRow("Precisión (críticos)", FormatPercent(gm.TotalCritsLanded, gm.TotalEnemiesKilled));
        AddRow("Logros desbloqueados", $"{gm.UnlockedAchievementsCount}/{AchievementCatalog.All.Length}");
        AddRow("Misiones completadas", gm.TotalMissionsCompleted.ToString());
        AddRow("Personajes desbloqueados", gm.UnlockedCharacters.Count.ToString());
        AddRow("Cosméticos comprados", gm.OwnedCosmetics.Count.ToString());
        AddRow("Builds completadas", $"{gm.EverCompletedBuilds.Count}/{BuildCatalog.ClassOrder.Length}");
        AddRow("Legendarias distintas", $"{gm.EverGotLegendary.Count}/{System.Enum.GetValues<UpgradeType>().Length}");

        FitToOrientation();
    }

    private static Label SectionHeading(string text)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", Palette.FontSize.Caption);
        label.AddThemeColorOverride("font_color", Palette.OndaBlast);
        return label;
    }

    private void AddRow(string label, string value)
    {
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.102f, 0.0588f, 0.1686f, 0.75f),
            BorderColor = new Color(Palette.OndaBlast, 0.4f),
        };
        style.SetBorderWidthAll(2);
        style.SetContentMarginAll(8f);
        panel.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer();
        panel.AddChild(row);

        var nameLabel = new Label { Text = label, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        nameLabel.AddThemeFontSizeOverride("font_size", Palette.FontSize.Body);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.84f));
        row.AddChild(nameLabel);

        var valueLabel = new Label { Text = value };
        valueLabel.AddThemeFontSizeOverride("font_size", Palette.FontSize.Subtitle);
        valueLabel.AddThemeColorOverride("font_color", Colors.White);
        row.AddChild(valueLabel);

        _rows.AddChild(panel);
    }

    // "Xh Ym" above an hour, "Xm Ys" above a minute, "Xs" below — never more than 2 units, so it
    // reads at a glance instead of as a stopwatch readout.
    private static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds >= 3600)
            return $"{totalSeconds / 3600}h {totalSeconds % 3600 / 60}m";
        if (totalSeconds >= 60)
            return $"{totalSeconds / 60}m {totalSeconds % 60}s";
        return $"{totalSeconds}s";
    }

    private static string FormatPercent(int part, int total) =>
        total > 0 ? $"{part * 100f / total:0.#}%" : "—";

    private void FitToOrientation()
    {
        UIUtil.FitScrollToViewport(_scroll, _panel);
    }

    public void Open()
    {
        RefreshTabs();
        RebuildRows();

        _scroll.ScrollVertical = 0;

        Visible = true;
        Juice.ModalIn(_panel);
    }

    private void Close() => Juice.ModalOut(_panel, () => Visible = false);
}
