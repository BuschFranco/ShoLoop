namespace ShooterLoop;

public partial class BuildsMenu : Control
{
    private PanelContainer _panel;

    public void Open()
    {
        Visible = true;
        Juice.ModalIn(_panel);
    }

    private void Close() => Juice.ModalOut(_panel, () => Visible = false);

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("CenterContainer/Panel");

        var closeButton = GetNode<Button>("CenterContainer/Panel/Box/CloseButton");
        closeButton.Pressed += Close;
        Juice.WireButtonFeedback(closeButton);
        GetNode<Control>("Dim").GuiInput += e =>
        {
            if (e is InputEventMouseButton m && m.Pressed) Close();
        };
        Populate();
    }

    private void Populate()
    {
        var list = GetNode<RichTextLabel>("CenterContainer/Panel/Box/ScrollContainer/BuildsList");
        var lines = new List<string>();
        var player = GetTree().GetFirstNodeInGroup("player") as Player;

        // Opened from the main menu there's no run in progress, so every requirement renders
        // "(tenés ?)" and the "which reward fulfils this" hint comes back empty. That's correct but
        // it looked broken — a screen full of question marks with nothing explaining them.
        if (player == null)
        {
            lines.Add("[color=#ffc24a]Empezá una partida para ver tu progreso en cada construcción.[/color]");
            lines.Add("");
        }

        foreach (var cls in BuildCatalog.ClassOrder)
        {
            var reqs = Player.GetBuildRequirements(cls);
            bool allMet = player != null && BuildCatalog.AllMet(cls, player);
            string bonus = BuildCatalog.Bonus(cls);

            if (allMet)
                lines.Add($"[color=#7fff7f][b]{BuildCatalog.Name(cls)}[/b]  ✓ ACTIVA[/color]");
            else
                lines.Add($"[color=#b0bec5][b]{BuildCatalog.Name(cls)}[/b][/color]");

            lines.Add($"  [color=#90a4ae]{bonus}[/color]");

            foreach (var req in reqs)
            {
                if (player != null && req.IsMet(player))
                {
                    lines.Add($"  [color=#7fff7f]✓[/color] {BuildCatalog.RequirementText(req)}  [color=#9aa8ba](tenés {BuildCatalog.CurrentText(req, player)})[/color]");
                }
                else
                {
                    string current = player != null ? BuildCatalog.CurrentText(req, player) : "?";
                    string missing = player != null ? string.Join(", ", BuildCatalog.RewardsThatFulfill(req, player)) : "";
                    lines.Add($"  [color=#ffc24a]✗[/color] {BuildCatalog.RequirementText(req)}  [color=#9aa8ba](tenés {current})[/color]" +
                              (missing.Length > 0 ? $"  [color=#ffc24a]→ {missing}[/color]" : ""));
                }
            }

            lines.Add("");
        }

        list.Text = string.Join("\n", lines);
    }
}
