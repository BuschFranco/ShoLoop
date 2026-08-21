using System.Linq;

namespace ShooterLoop;

public partial class BuildsMenu : Control
{
    // Names, benefits and requirement thresholds live in BuildCatalog / Player.GetBuildRequirements,
    // shared with the in-run Loadout panel — this menu is just rendering that same data.

    public void Open() => Visible = true;

    public override void _Ready()
    {
        GetNode<Button>("CenterContainer/Panel/Box/CloseButton").Pressed += () => Visible = false;
        Populate();
    }

    private void Populate()
    {
        var list = GetNode<RichTextLabel>("CenterContainer/Panel/Box/BuildsList");
        var lines = new string[BuildCatalog.ClassOrder.Length];
        for (int i = 0; i < BuildCatalog.ClassOrder.Length; i++)
        {
            var cls = BuildCatalog.ClassOrder[i];
            string reqText = string.Join(" y ", Player.GetBuildRequirements(cls)
                .Select(BuildCatalog.RequirementText));
            lines[i] = $"[color=#7dfdfe]{BuildCatalog.Name(cls)}[/color]  " +
                       $"[color=#b8a040]{reqText}[/color]  →  " +
                       $"[color=#7fff7f]{BuildCatalog.Bonus(cls)}[/color]";
        }
        list.Text = string.Join("\n", lines);
    }
}