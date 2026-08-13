namespace ShooterLoop;

// The circular Ultimate button's face: draws the button's own gold-ringed circle plus a
// procedural icon per UltimateKind, with a CooldownIcon-style dark pie sweeping away the
// cooldown and the seconds remaining as text while it cools. No art assets — _Draw() only,
// like every other visual in the game.
public partial class UltimateButtonIcon : Control
{
    public UltimateKind Kind = UltimateKind.Nova;

    // 1 = just used (fully covered), 0 = ready (fully clear).
    public float CooldownFraction = 0f;

    // Shown as a countdown number inside the circle while cooling; 0 hides it.
    public float CooldownSeconds = 0f;

    private static readonly Color Gold = Palette.BossHealthBarFill;
    private static readonly Color BaseBg = new(0.102f, 0.0588f, 0.1686f, 0.85f);
    private static readonly Color CooldownShade = new(0f, 0f, 0f, 0.72f);

    public override void _Ready()
    {
        // The wrapping Button owns all input; this is purely decorative.
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        Vector2 center = Size / 2f;
        float radius = Mathf.Min(Size.X, Size.Y) / 2f - 2f;
        if (radius <= 0f) return;

        bool ready = CooldownFraction <= 0f;
        Color accent = ready ? Gold : Gold.Darkened(0.55f);

        DrawCircle(center, radius, BaseBg);
        DrawArc(center, radius, 0f, Mathf.Tau, 48, accent, 3f, true);

        if (CooldownFraction > 0f)
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
            DrawPolygon(points, new[] { CooldownShade });
        }

        DrawIcon(center, radius * 0.55f, accent);

        if (CooldownFraction > 0f && CooldownSeconds > 0f)
        {
            var font = GetThemeDefaultFont();
            string text = Mathf.CeilToInt(CooldownSeconds).ToString();
            int fontSize = 20;
            Vector2 textSize = font.GetStringSize(text, HorizontalAlignment.Center, -1f, fontSize);
            DrawString(font, center - textSize / 2f + new Vector2(0f, textSize.Y * 0.35f), text,
                HorizontalAlignment.Center, -1f, fontSize, Colors.White);
        }
    }

    private void DrawIcon(Vector2 center, float r, Color color)
    {
        switch (Kind)
        {
            case UltimateKind.Nova:
                DrawNovaStar(center, r, color);
                break;
            case UltimateKind.TimeSlow:
                DrawHourglass(center, r, color);
                break;
            case UltimateKind.Frenzy:
                DrawBolt(center, r, color);
                break;
        }
    }

    // Pulso Nova: an 8-point starburst — the detonation itself.
    private void DrawNovaStar(Vector2 center, float r, Color color)
    {
        const int spikes = 8;
        var points = new Vector2[spikes * 2];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.Tau - Mathf.Pi / 2f;
            float radius = i % 2 == 0 ? r : r * 0.5f;
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        DrawPolygon(points, new[] { color });
    }

    // Zona Lenta: an hourglass — time itself running out.
    private void DrawHourglass(Vector2 center, float r, Color color)
    {
        float pinch = 0.06f * r;
        var top = new[]
        {
            center + new Vector2(0f, -r),
            center + new Vector2(-r * 0.65f, pinch),
            center + new Vector2(r * 0.65f, pinch),
        };
        var bottom = new[]
        {
            center + new Vector2(0f, r),
            center + new Vector2(-r * 0.65f, -pinch),
            center + new Vector2(r * 0.65f, -pinch),
        };
        DrawPolygon(top, new[] { color });
        DrawPolygon(bottom, new[] { color });
    }

    // Sobrecarga: a lightning bolt (FontAwesome's path, normalized to a unit box).
    private void DrawBolt(Vector2 center, float r, Color color)
    {
        var raw = new[]
        {
            new Vector2(0.083f, 0.833f),
            new Vector2(-0.75f, -0.167f),
            new Vector2(-0.25f, -0.167f),
            new Vector2(-0.417f, -0.833f),
            new Vector2(0.583f, 0.167f),
            new Vector2(0.083f, 0.167f),
        };
        var points = new Vector2[raw.Length];
        for (int i = 0; i < raw.Length; i++)
            points[i] = center + raw[i] * r;
        DrawPolygon(points, new[] { color });
    }
}