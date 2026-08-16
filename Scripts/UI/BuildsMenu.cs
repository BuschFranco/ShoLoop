namespace ShooterLoop;

public partial class BuildsMenu : Control
{
    private static readonly (string Name, string Require, string Bonus)[] Builds =
    {
        ("ARTILLERO",    "Cadencia de disparo 5.0+",            "+10% de daño en balas"),
        ("TANQUE",       "4+ vidas Y 3+ escudos",               "20% de no perder vida al recibir golpe"),
        ("ASESINO",      "20%+ de crítico Y 20+ de daño de bala",  "+15% de daño en críticos"),
        ("EXPLORADOR",   "350+ de alcance de disparo",          "+20% de velocidad de movimiento"),
        ("PIRÓMANO",     "3+ niveles de Incendiario",           "+50% de daño de quemadura"),
        ("ACORAZADO",    "Escudo orbital + 3+ escudos",         "Refleja 30 de daño al recibir golpe"),
    };

    public void Open() => Visible = true;

    public override void _Ready()
    {
        GetNode<Button>("CenterContainer/Panel/Box/CloseButton").Pressed += () => Visible = false;
        Populate();
    }

    private void Populate()
    {
        var list = GetNode<RichTextLabel>("CenterContainer/Panel/Box/BuildsList");
        var lines = new string[Builds.Length];
        for (int i = 0; i < Builds.Length; i++)
        {
            lines[i] = $"[color=#7dfdfe]{Builds[i].Name}[/color]  [color=#b8a040]{Builds[i].Require}[/color]  →  [color=#7fff7f]{Builds[i].Bonus}[/color]";
        }
        list.Text = string.Join("\n", lines);
    }
}