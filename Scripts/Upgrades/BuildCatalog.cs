namespace ShooterLoop;

// Names/benefits/requirement formatting shared by every build list in the game — the main
// menu's BuildsMenu and the pause menu's LoadoutMenu — so their texts can't drift apart.
// The stat thresholds themselves live in Player.GetBuildRequirements (the single source
// CheckBuildClass evaluates); BuildCatalog is only presentation on top of that.
public static class BuildCatalog
{
    // Indexed by BuildClass (None=0, so entries start at 1).
    private static readonly (string Name, string Bonus)[] Entries =
    {
        ("ARTILLERO", "+15% de daño en balas"),
        ("TANQUE", "25% de no perder vida y -40% retroceso"),
        ("ASESINO", "+25% de daño en críticos"),
        ("EXPLORADOR", "+30% velocidad y +10% alcance"),
        ("PIRÓMANO", "+75% quema y +1s de duración"),
        ("ACORAZADO", "Refleja 60 y escudo se regenera cada 12s"),
        ("CAZADOR", "+1 rebote y dron +10% más fuerte"),
    };

    public static string Name(Player.BuildClass cls) => Entries[(int)cls - 1].Name;
    public static string Bonus(Player.BuildClass cls) => Entries[(int)cls - 1].Bonus;

    public static bool AllMet(Player.BuildClass cls, Player p)
    {
        foreach (var req in Player.GetBuildRequirements(cls))
            if (!req.IsMet(p)) return false;
        return true;
    }

    // "Cadencia 5.0+" / "Dron 30%" — the short label used on one-line build summaries.
    public static string RequirementText(Player.BuildRequirement req) =>
        $"{req.Label} {FormatValue(req.Needed)}+";

    // The live "tenés X" readout — same units as RequirementText, but the player's value.
    public static string CurrentText(Player.BuildRequirement req, Player p) => FormatValue(req.Current(p));

    private static string FormatValue(float value)
    {
        if (value >= 1f) return value >= 10f ? $"{value:0}" : $"{value:0.#}";
        return $"{value * 100f:0}%";
    }

    // For an unmet requirement: which catalog rewards (by display name, ascending tier) would
    // satisfy it on their own. Additive stats need an offer covering at least the remaining gap;
    // max-style stats just need any offer at or above the required level.
    public static string[] RewardsThatFulfill(Player.BuildRequirement req, Player p)
    {
        float remaining = req.Needed - req.Current(p);
        if (remaining <= 0f) return Array.Empty<string>();

        var names = new List<string>();
        var seen = new HashSet<string>();
        var catalog = UpgradeData.BuildCatalog(RewardSource.Both);
        bool ownsAny = p.OwnedTiers.TryGetValue(req.Type, out var ownedTier);

        // RewardTier enum order (Common -> Rare -> Epic -> Legendary) doubles as ascending display order.
        foreach (var tier in Enum.GetValues<RewardTier>())
        {
            // A tier already owned can't be "pendiente" — skip it so the list only points at
            // upgrades that would actually move the stat.
            if (ownsAny && ownedTier >= tier) continue;
            foreach (var u in catalog[tier])
            {
                if (u.Type != req.Type || seen.Contains(u.Name)) continue;
                bool helps = req.Additive ? u.Value >= remaining : u.Value >= req.Needed;
                if (!helps) continue;
                names.Add(u.Name);
                seen.Add(u.Name);
            }
        }
        return names.ToArray();
    }
}