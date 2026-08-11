namespace ShooterLoop;

public readonly struct RoundCurve
{
    public readonly float Base;
    public readonly float PerRound;
    public readonly float Min;
    public readonly float Max;

    public RoundCurve(float baseValue, float perRound, float min, float max)
    {
        Base = baseValue;
        PerRound = perRound;
        Min = min;
        Max = max;
    }

    public float Evaluate(int round) => Mathf.Clamp(Base + (round - 1) * PerRound, Min, Max);
}
