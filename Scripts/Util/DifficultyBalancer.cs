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

    // Fraction of the overshoot that's handed back. At 0.3, a player at 3× the expected power
    // faces enemies with 1 + (3-1)*0.3 = 1.6× HP/damage — noticeably tougher, but still a long
    // way from erasing the advantage they earned.
    private const float CatchUpStrength = 0.3f;
    private const float MaxCatchUp = 2.5f;

    public static float GetCatchUpMultiplier(float playerPower, int round)
    {
        float expected = ExpectedPowerCurve.Evaluate(round);
        if (expected <= 0f || playerPower <= expected) return 1f;

        float overshoot = playerPower / expected;
        return Mathf.Clamp(1f + (overshoot - 1f) * CatchUpStrength, 1f, MaxCatchUp);
    }
}
