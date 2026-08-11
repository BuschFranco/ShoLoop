namespace ShooterLoop;

public static class RewardTierRoller
{
    private static readonly RoundCurve Common = new(65f, -3.5f, 10f, 65f);
    private static readonly RoundCurve Rare = new(27f, 0.3f, 15f, 35f);
    private static readonly RoundCurve Epic = new(7f, 1.4f, 5f, 35f);
    private static readonly RoundCurve Legendary = new(1f, 1.3f, 0f, 25f);

    public static Dictionary<RewardTier, float> GetWeights(int round)
    {
        var raw = new Dictionary<RewardTier, float>
        {
            [RewardTier.Common] = Common.Evaluate(round),
            [RewardTier.Rare] = Rare.Evaluate(round),
            [RewardTier.Epic] = Epic.Evaluate(round),
            [RewardTier.Legendary] = Legendary.Evaluate(round),
        };

        float sum = 0f;
        foreach (var value in raw.Values) sum += value;

        var normalized = new Dictionary<RewardTier, float>();
        foreach (var kv in raw) normalized[kv.Key] = kv.Value / sum;
        return normalized;
    }

    public static RewardTier RollTier(Dictionary<RewardTier, float> weights, Random rng)
    {
        double roll = rng.NextDouble();
        double cumulative = 0;
        foreach (var kv in weights)
        {
            cumulative += kv.Value;
            if (roll <= cumulative) return kv.Key;
        }
        return RewardTier.Common;
    }

    // Brightened into the neon range to match the rest of the palette, but the four hues stay
    // *deliberately distinct* rather than being pulled into the pink family. Tier is information the
    // player reads at a glance while deciding what to buy — four shades of pink would cost more in
    // legibility than it gains in cohesion.
    public static Color GetTierColor(RewardTier tier) => tier switch
    {
        RewardTier.Common => new Color(0.45f, 1f, 0.6f),      // mint
        RewardTier.Rare => new Color(0.42f, 0.72f, 1f),       // sky
        RewardTier.Epic => new Color(0.78f, 0.42f, 1f),       // violet
        RewardTier.Legendary => new Color(1f, 0.82f, 0.35f),  // gold
        _ => Colors.White,
    };

    public static string GetTierName(RewardTier tier) => tier switch
    {
        RewardTier.Common => "Común",
        RewardTier.Rare => "Raro",
        RewardTier.Epic => "Épico",
        RewardTier.Legendary => "Legendario",
        _ => tier.ToString(),
    };
}
