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

    // Which build classes were fully met the last time Refresh ran, so a class flipping
    // unmet→met between two pause-menu opens gets a pop instead of the whole panel just
    // silently reprinting with a checkmark added.
    private readonly HashSet<Player.BuildClass> _metLastRefresh = new();

    public override void _Ready()
    {
        _itemsLabel = GetNode<RichTextLabel>("ScrollContainer/Box/ItemsLabel");
        _buildsLabel = GetNode<RichTextLabel>("ScrollContainer/Box/BuildsLabel");
    }

    public void Refresh(Player player)
    {
        _itemsLabel.Text = player == null ? "" : BuildItemsText(player);
        _buildsLabel.Text = player == null ? "" : BuildBuildsText(player);

        if (player == null) return;

        bool anyNewlyMet = false;
        foreach (var cls in BuildCatalog.ClassOrder)
        {
            bool allMet = BuildCatalog.AllMet(cls, player);
            if (allMet && !_metLastRefresh.Contains(cls))
                anyNewlyMet = true;
            if (allMet) _metLastRefresh.Add(cls);
            else _metLastRefresh.Remove(cls);
        }

        if (anyNewlyMet)
            Juice.ValuePop(_buildsLabel, 1.05f, 0.25f);
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
        Row(p => p.HasExtraProjectile, "Disparo en Diagonal", _ => "activo");
        Row(p => p.ExtraFiringLines > 0, "Disparo Paralelo", p => $"{p.ExtraFiringLines} líneas (tope {Player.MaxExtraFiringLinesCap})");
        Row(p => p.CoinBonusPercent > 0, "Botín", p => $"+{p.CoinBonusPercent:0}% monedas");
        Row(p => p.XpBonusPercent > 0, "Sabiduría", p => $"+{p.XpBonusPercent:0}% XP");
        Row(p => p.FortuneBonus > 0, "Fortuna", p => $"+{p.FortuneBonus:0}% Raro/Épico/Legendario");
        Row(p => p.DodgeChance > 0, "Esquiva", p => $"{p.DodgeChance:0}%");

        Section("DEFENSAS");
        // Both ceilings are read from the constants rather than typed here. This line used to say
        // "(máx 6)" against a cap that is 4 — the UI was promising two lives the game would never
        // grant, and a player could spend coins chasing them.
        Row(p => p.MaxLives > 0, "Vidas", p => $"{p.MaxLives} (tope {Player.MaxLivesCap})");
        Row(p => p.MaxShieldCharges > 0, "Escudos", p => $"{p.MaxShieldCharges} cargas (tope {Player.MaxShieldChargesCap})");
        Row(p => p.ShieldRegenPerMinute > 0, "Regeneración", p => $"{p.ShieldRegenPerMinute:0.#} cargas/min");

        // The HUD identifies each of these by a single letter and nothing else — no legend, and no
        // hover to hang a tooltip off since this is a touch game. Printing the glyph beside the name
        // here turns the pause screen into that legend, in the one place the player is already
        // looking when they wonder what "N" was.
        void PowerRow(Func<Player, bool> owned, string glyph, string name, Func<Player, string> value)
        {
            if (!owned(p)) return;
            lines.Add($"[color=#cfd8e8]• [{glyph}] {name}[/color]  [color=#9aa8ba]{value(p)}[/color]");
        }

        Section("PODERES");
        Row(p => p.OrbitCount > 0, "Cuchillas Orbitales", p => $"{p.OrbitCount}");
        Row(p => p.CompanionStatPercent > 0, "Dron", p => $"{p.CompanionStatPercent * 100f:0}% de tus estadísticas");
        PowerRow(p => p.LaserLevel > 0, "L", "Láser", p => $"{Glossary.LevelPrefix}{p.LaserLevel}");
        PowerRow(p => p.MissileLevel > 0, "M", "Misil", p => $"{Glossary.LevelPrefix}{p.MissileLevel}");
        PowerRow(p => p.MineLevel > 0, "N", "Mina", p => $"{Glossary.LevelPrefix}{p.MineLevel}");
        PowerRow(p => p.OndaLevel > 0, "O", "Onda de Choque", p => $"{Glossary.LevelPrefix}{p.OndaLevel}");
        PowerRow(p => p.VendavalLevel > 0, "V", "Vendaval", p => $"{Glossary.LevelPrefix}{p.VendavalLevel}");
        PowerRow(p => p.ShieldRegenPerMinute > 0, "R", "Regeneración de escudo", p => $"{p.ShieldRegenPerMinute:0.#}/min");
        Row(p => p.BurnLevel > 0, "Incendiario", p => $"{Glossary.LevelPrefix}{p.BurnLevel}");
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

        foreach (var cls in BuildCatalog.ClassOrder)
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