namespace ShooterLoop;

// Side-by-side companion to the pause menu: shows every reward/item the run has equipped
// and the full build list with per-requirement progress — closing the gap (current value vs
// needed) plus the exact catalog rewards that would satisfy each unmet requirement.
// Requirements and thresholds come from Player.GetBuildRequirements, the same source
// CheckBuildClass evaluates, so the display can never disagree with activation logic.
public partial class LoadoutMenu : PanelContainer
{
    private RichTextLabel _itemsLabel;
    private RichTextLabel _buildsLabel;

    private static readonly Player.BuildClass[] ClassOrder =
    {
        Player.BuildClass.Gunner, Player.BuildClass.Tank, Player.BuildClass.Assassin,
        Player.BuildClass.Explorer, Player.BuildClass.Pyromaniac, Player.BuildClass.Armored,
        Player.BuildClass.Hunter,
    };

    public override void _Ready()
    {
        _itemsLabel = GetNode<RichTextLabel>("ScrollContainer/Box/ItemsLabel");
        _buildsLabel = GetNode<RichTextLabel>("ScrollContainer/Box/BuildsLabel");
    }

    public void Refresh(Player player)
    {
        _itemsLabel.Text = player == null ? "" : BuildItemsText(player);
        _buildsLabel.Text = player == null ? "" : BuildBuildsText(player);
    }

    // --- Items equipados ---

    private string BuildItemsText(Player p)
    {
        var lines = new List<string>();
        string group = null;

        void Section(string name)
        {
            if (group == name) return;
            group = name;
            lines.Add($"[color=#7dfdfe]{name}[/color]");
        }

        void Row(Func<Player, bool> owned, string name, Func<Player, string> value)
        {
            if (!owned(p)) return;
            lines.Add($"[color=#cfd8e8]• {name}[/color]  [color=#9aa8ba]{value(p)}[/color]");
        }

        Section("ARMAS");
        Row(p => p.BulletDamage > 0, "Balas Afiladas", p => $"{p.BulletDamage} daño");
        Row(p => p.FireRate > 0, "Fuego Rápido", p => $"{p.FireRate:0.0}/s");
        Row(p => p.FireRange > 0, "Alcance de Disparo", p => $"{p.FireRange:0}");
        Row(p => p.CritChance > 0, "Crítico", p => $"{p.CritChance:0}% (x2)");
        Row(p => p.BulletPierce > 0, "Perforación", p => $"{p.BulletPierce} enemigos");
        Row(p => p.RicochetCount > 0, "Rebote", p => $"{p.RicochetCount} rebotes");
        Row(p => p.BulletKnockback > 0, "Retroceso", p => $"{p.BulletKnockback:0}");
        Row(p => p.HasExtraProjectile, "Disparo Doble", _ => "activo");
        Row(p => p.ExtraFiringLines > 0, "Disparo Lateral", p => $"{p.ExtraFiringLines} líneas (tope {Player.MaxExtraFiringLinesCap})");
        Row(p => p.CoinBonusPercent > 0, "Botín", p => $"+{p.CoinBonusPercent:0}% monedas");
        Row(p => p.XpBonusPercent > 0, "Sabiduría", p => $"+{p.XpBonusPercent:0}% XP");
        Row(p => p.FortuneBonus > 0, "Fortuna", p => $"+{p.FortuneBonus:0}% Rare/Epic/Legendary");
        Row(p => p.DodgeChance > 0, "Esquiva", p => $"{p.DodgeChance:0}%");

        Section("DEFENSAS");
        Row(p => p.MaxLives > 3, "Vidas", p => $"{p.MaxLives} (máx 6)");
        Row(p => p.MaxShieldCharges > 0, "Escudos", p => $"{p.MaxShieldCharges} cargas (tope 4)");
        Row(p => p.ShieldRegenPerMinute > 0, "Regeneración", p => $"{p.ShieldRegenPerMinute:0.#} cargas/min");

        Section("PODERES");
        Row(p => p.OrbitCount > 0, "Cuchillas Orbitales", p => $"{p.OrbitCount}");
        Row(p => p.CompanionStatPercent > 0, "Dron", p => $"{p.CompanionStatPercent * 100f:0}% de tus estadísticas");
        Row(p => p.LaserLevel > 0, "Láser", p => $"nivel {p.LaserLevel}");
        Row(p => p.MissileLevel > 0, "Misil", p => $"nivel {p.MissileLevel}");
        Row(p => p.BurnLevel > 0, "Incendiario", p => $"nivel {p.BurnLevel}");
        Row(p => p.OndaLevel > 0, "Onda de Choque", p => $"nivel {p.OndaLevel}");
        Row(p => p.VendavalLevel > 0, "Vendaval", p => $"nivel {p.VendavalLevel}");
        Row(p => p.ThornsDamage > 0, "Escudo Voltáico", p => $"{p.ThornsDamage:0} daño");

        Section("ULTIMATE");
        Row(p => p.EquippedUltimate != null, "Ultimate", p => UltimateKindNames.Display(p.EquippedUltimate.Value));

        return lines.Count == 0
            ? "[color=#9aa8ba]No tenés items equipados todavía.[/color]"
            : string.Join("\n", lines);
    }

    // --- Builds con progreso ---

    private string BuildBuildsText(Player p)
    {
        var lines = new List<string>();

        foreach (var cls in ClassOrder)
        {
            var reqs = Player.GetBuildRequirements(cls);
            bool allMet = BuildCatalog.AllMet(cls, p);
            string bonus = BuildCatalog.Bonus(cls);

            if (allMet)
            {
                lines.Add($"[color=#7dfdfe]{BuildCatalog.Name(cls)}[/color]  [color=#7fff7f]✓ ACTIVA[/color]  →  [color=#7fff7f]{bonus}[/color]");
            }
            else
            {
                lines.Add($"[color=#9aa0b0]{BuildCatalog.Name(cls)}[/color]  →  [color=#86a0b8]{bonus}[/color]");
            }

            foreach (var req in reqs)
            {
                if (req.IsMet(p))
                {
                    lines.Add($"   [color=#7fff7f]✓[/color] {BuildCatalog.RequirementText(req)}  [color=#9aa8ba](tenés {BuildCatalog.CurrentText(req, p)})[/color]");
                }
                else
                {
                    string missing = string.Join(", ", BuildCatalog.RewardsThatFulfill(req, p));
                    lines.Add($"   [color=#ffc24a]✗[/color] {BuildCatalog.RequirementText(req)}  [color=#9aa8ba](tenés {BuildCatalog.CurrentText(req, p)})[/color]" +
                              (missing.Length > 0 ? $"  [color=#ffc24a]falta: {missing}[/color]" : ""));
                }
            }
        }

        return string.Join("\n", lines);
    }
}