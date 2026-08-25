namespace ShooterLoop;

// Keeps a runaway build from trivializing the game, without punishing a struggling one.
//
// Every other difficulty knob is indexed purely on RoundNumber (see RoundCurve and
// EnemySpawner), which assumes the player's power grows at a predictable pace. A lucky reward
// streak breaks that assumption badly — the round curves keep their gentle schedule while the
// player's DPS multiplies. This compares what the player is *actually* worth against what the
// round expected them to be worth, and hands a fraction of any overshoot back to the enemies as
// extra HP/damage.
//
// Deliberately one-directional: a player *below* the expected curve gets no help at all (the
// multiplier floors at 1). This is a ceiling-limiter, not rubber-banding — falling behind is
// allowed to be hard, but running away with it shouldn't be free.
public static class DifficultyBalancer
{
    // What a player who picks up rewards at a normal pace is expected to score on
    // Player.GetOffensivePower() by a given round (round 1 = exactly 1.0, an untouched player).
    private static readonly RoundCurve ExpectedPowerCurve = new(1f, 0.6f, 1f, 40f);

    // Logarithmic, not linear. A linear response needs linearly more overshoot to keep climbing,
    // which is exactly what made this saturate in practice: a "normal", non-exploit round-20 build
    // (fire rate near cap, decent damage/crit/pierce, one extra weapon system) measures at
    // 300-650x the expected power, while the old formula (strength 0.3, cap 2.5) hit its ceiling
    // at just 6x overshoot — meaning the balancer went inert from roughly round 12-15 onward for
    // almost anyone shopping normally. A log response only needs another *order of magnitude* of
    // overshoot to buy the same fixed increment, so the same formula stays sane at 2x and is still
    // doing real work at 650x: it now saturates around 786x overshoot instead of 6x, a ~130x wider
    // working range that covers every measured case with room to spare.
    private const float CatchUpLogStrength = 0.6f;
    private const float MaxCatchUp = 5f;   // was 2.5 — total enemy HP ceiling is now HpMultCurve.Max(8) * 5 = 40x round-1 baseline (was 20x)

    public static float GetCatchUpMultiplier(float playerPower, int round)
    {
        float expected = ExpectedPowerCurve.Evaluate(round);
        if (expected <= 0f || playerPower <= expected) return 1f;

        float overshoot = playerPower / expected;
        float mult = 1f + CatchUpLogStrength * Mathf.Log(overshoot);
        return Mathf.Clamp(mult, 1f, MaxCatchUp);
    }

    // Symmetric counterpart for survivability rather than damage. GetCatchUpMultiplier exists
    // because a runaway DPS build reads as "overpowered"; a runaway defensive build (dodge +
    // Tank's free shrug + shield + regen) does NOT — it reads as approximately zero offensive
    // power, so the mechanism above never touches it, and it can become nearly unkillable with no
    // correction at all. This is deliberately flatter than ExpectedPowerCurve: most of a normal
    // build's survivability comes from a couple of Heart/Barrier pickups, not a curve that keeps
    // climbing all game.
    private static readonly RoundCurve ExpectedSurvivabilityCurve = new(1f, 0.05f, 1f, 1.6f);
    private const float SurvivabilityCatchUpStrength = 0.25f;
    private const float MaxSurvivabilityCatchUp = 2.0f;

    public static float GetSurvivabilityCatchUpMultiplier(float survivabilityRatio, int round)
    {
        float expected = ExpectedSurvivabilityCurve.Evaluate(round);
        if (expected <= 0f || survivabilityRatio <= expected) return 1f;

        float overshoot = survivabilityRatio / expected;
        return Mathf.Clamp(1f + (overshoot - 1f) * SurvivabilityCatchUpStrength, 1f, MaxSurvivabilityCatchUp);
    }
}
