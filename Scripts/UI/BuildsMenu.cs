namespace ShooterLoop;

public partial class BuildsMenu : Control
{
    public void Open() => Visible = true;

    public override void _Ready()
    {
        GetNode<Button>("CenterContainer/Panel/Box/CloseButton").Pressed += () => Visible = false;
        GetNode<Control>("Dim").GuiInput += e =>
        {
            if (e is InputEventMouseButton m && m.Pressed) Visible = false;
        };
        Populate();
    }

    private void Populate()
    {
        var list = GetNode<RichTextLabel>("CenterContainer/Panel/Box/ScrollContainer/BuildsList");
        var lines = new List<string>();
        var player = GetTree().GetFirstNodeInGroup("player") as Player;

        foreach (var cls in BuildCatalog.ClassOrder)
        {
            var reqs = Player.GetBuildRequirements(cls);
            bool allMet = player != null && BuildCatalog.AllMet(cls, player);
            string bonus = BuildCatalog.Bonus(cls);

            if (allMet)
                lines.Add($"[color=#7fff7f][b]{BuildCatalog.Name(cls)}[/b]  ✓ ACTIVO[/color]");
            else
                lines.Add($"[color=#b0bec5][b]{BuildCatalog.Name(cls)}[/b][/color]");

            lines.Add($"  [color=#90a4ae]{bonus}[/color]");

            foreach (var req in reqs)
            {
                if (player != null && req.IsMet(player))
                {
                    lines.Add($"  [color=#7fff7f]✓[/color] {BuildCatalog.RequirementText(req)}  [color=#6a7a8a](tenés {BuildCatalog.CurrentText(req, player)})[/color]");
                }
                else
                {
                    string current = player != null ? BuildCatalog.CurrentText(req, player) : "?";
                    string missing = player != null ? string.Join(", ", BuildCatalog.RewardsThatFulfill(req, player)) : "";
                    lines.Add($"  [color=#ffc24a]✗[/color] {BuildCatalog.RequirementText(req)}  [color=#6a7a8a](tenés {current})[/color]" +
                              (missing.Length > 0 ? $"  [color=#ffc24a]→ {missing}[/color]" : ""));
                }
            }

            lines.Add("");
        }

        list.Text = string.Join("\n", lines);
    }
}
