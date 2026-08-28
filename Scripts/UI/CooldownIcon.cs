namespace ShooterLoop;

// A small circular icon that draws its own "clock" cooldown overlay — a dark pie sector that
// covers the icon right after use and sweeps away (clockwise, from 12 o'clock) as the cooldown
// clears. No texture/art asset involved, just _Draw(), matching the rest of the game's
// procedural-only visuals.
public partial class CooldownIcon : Control
{
    [Export] public Color IconColor = Colors.White;
    [Export] public string Glyph = "?";

    // 1 = just used (fully covered), 0 = ready (fully clear).
    public float CooldownFraction = 0f;

    private float _lastFraction = -1f;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(36, 36);
    }

    public override void _Process(double delta)
    {
        if (!Mathf.IsEqualApprox(CooldownFraction, _lastFraction))
            QueueRedraw();
    }

    public override void _Draw()
    {
        _lastFraction = CooldownFraction;
        Vector2 center = Size / 2f;
        float radius = Mathf.Min(Size.X, Size.Y) / 2f - 2f;
        if (radius <= 0f) return;

        bool ready = CooldownFraction <= 0f;

        DrawCircle(center, radius, ready ? IconColor : IconColor.Darkened(0.35f));
        DrawArc(center, radius, 0f, Mathf.Tau, 32, Colors.Black.Lightened(0.1f), 2f, true);

        if (CooldownFraction > 0.002f)
        {
            const int segments = 24;
            float startAngle = -Mathf.Pi / 2f;
            float endAngle = startAngle + Mathf.Tau * CooldownFraction;

            var points = new Vector2[segments + 2];
            points[0] = center;
            for (int i = 0; i <= segments; i++)
            {
                float t = Mathf.Lerp(startAngle, endAngle, i / (float)segments);
                points[i + 1] = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius;
            }
            DrawPolygon(points, new[] { new Color(0f, 0f, 0f, 0.72f) });
        }

        // GetThemeDefaultFont() rather than ThemeDB.FallbackFont: the latter always returns the engine's
        // built-in font and ignores the project theme entirely, so these cooldown letters would have
        // stayed in the old typeface while every Label in the game switched.
        var font = GetThemeDefaultFont();
        int fontSize = 14;
        Vector2 textSize = font.GetStringSize(Glyph, HorizontalAlignment.Center, -1f, fontSize);
        Vector2 textPos = center - textSize / 2f + new Vector2(0f, textSize.Y * 0.35f);

        // Outlined, unlike every other DrawString in the project — this letter is the ONLY thing
        // identifying which ability the icon belongs to, and it sits over the live arena. A white
        // glyph on top of a bright cyan or pink explosion simply disappears without this.
        DrawStringOutline(font, textPos, Glyph, HorizontalAlignment.Center, -1f, fontSize, 4, Colors.Black);
        DrawString(font, textPos, Glyph, HorizontalAlignment.Center, -1f, fontSize, Colors.White);
    }
}
