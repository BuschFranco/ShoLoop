namespace ShooterLoop;

// A small pixel-art pie readout for a "N of M" stat, e.g. "12/20 logros" — same drawing primitives
// CooldownIcon already uses for its radial cooldown sweep (a track disc plus a Juice.WedgePoints
// slice), just filled forward instead of shaded backward: here the coloured wedge IS the progress,
// growing from nothing to a full disc as Fraction goes 0 -> 1.
//
// Built and added via `new()` from StatsMenu rather than its own .tscn — it has no child nodes, so a
// scene file would add nothing but ceremony.
public partial class StatProgressRing : Control
{
    public float Fraction;
    public Color RingColor = Colors.White;
    public string CenterText = "";

    private static readonly Color TrackColor = new(0.102f, 0.0588f, 0.1686f, 0.85f);

    public override void _Draw()
    {
        Vector2 center = Size / 2f;
        float radius = Mathf.Min(Size.X, Size.Y) / 2f - 2f;
        if (radius <= 0f) return;

        Juice.DrawPixelCircle(this, center, radius, TrackColor);
        Juice.DrawPixelRing(this, center, radius, 2f, new Color(RingColor, 0.5f));

        float fraction = Mathf.Clamp(Fraction, 0f, 1f);
        if (fraction > 0.002f)
        {
            var points = Juice.WedgePoints(radius - 3f, fraction);
            for (int i = 0; i < points.Length; i++) points[i] += center;
            DrawPolygon(points, new[] { RingColor });
        }

        if (string.IsNullOrEmpty(CenterText)) return;

        var font = GetThemeDefaultFont();
        const int fontSize = 13;
        Vector2 textSize = font.GetStringSize(CenterText, HorizontalAlignment.Center, -1f, fontSize);
        DrawString(font, center - textSize / 2f + new Vector2(0f, textSize.Y * 0.35f), CenterText,
            HorizontalAlignment.Center, -1f, fontSize, Colors.White);
    }

    public void SetValue(float fraction, string centerText, Color ringColor)
    {
        Fraction = fraction;
        CenterText = centerText;
        RingColor = ringColor;
        QueueRedraw();
    }
}
