namespace ShooterLoop;

// The circular Ultimate button's face: draws the button's own gold-ringed circle, plus a small
// chrome-neon icon per UltimateKind (Assets/Sprites/UI/, generated to match the logo's style and
// pixelated the same way — the one deliberate exception to "everything else is _Draw()-only" this
// game otherwise follows, since these are UI iconography rather than gameplay entities), with a
// CooldownIcon-style dark pie sweeping away the cooldown and the seconds remaining as text while it
// cools.
public partial class UltimateButtonIcon : Control
{
    public UltimateKind Kind = UltimateKind.Nova;

    // 1 = just used (fully covered), 0 = ready (fully clear).
    public float CooldownFraction = 0f;

    // Shown as a countdown number inside the circle while cooling; 0 hides it.
    public float CooldownSeconds = 0f;

    private float _lastFraction = -1f;
    private float _lastSeconds = -1f;

    private static readonly Color Gold = Palette.BossHealthBarFill;
    private static readonly Color BaseBg = new(0.102f, 0.0588f, 0.1686f, 0.85f);
    private static readonly Color CooldownShade = new(0f, 0f, 0f, 0.72f);

    // Chrome/neon icon set matching the logo's own style (generated to spec, then pixelated the
    // same way MainMenu's title logo is — see tools/pixelate_logo.py) — replaces the four hand-drawn
    // polygon glyphs below. Already full-colour art, so unlike the procedural glyphs it isn't tinted
    // by `accent`; dimming on cooldown instead multiplies the whole texture toward grey.
    private static readonly Texture2D NovaTexture = GD.Load<Texture2D>("res://Assets/Sprites/UI/ability_nova.png");
    private static readonly Texture2D HourglassTexture = GD.Load<Texture2D>("res://Assets/Sprites/UI/ability_hourglass.png");
    private static readonly Texture2D BoltTexture = GD.Load<Texture2D>("res://Assets/Sprites/UI/ability_bolt.png");
    private static readonly Texture2D ShieldTexture = GD.Load<Texture2D>("res://Assets/Sprites/UI/ability_shield.png");

    public override void _Ready()
    {
        // The wrapping Button owns all input; this is purely decorative.
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        if (!Mathf.IsEqualApprox(CooldownFraction, _lastFraction)
            || !Mathf.IsEqualApprox(CooldownSeconds, _lastSeconds))
            QueueRedraw();
    }

    public override void _Draw()
    {
        _lastFraction = CooldownFraction;
        _lastSeconds = CooldownSeconds;
        Vector2 center = Size / 2f;
        float radius = Mathf.Min(Size.X, Size.Y) / 2f - 2f;
        if (radius <= 0f) return;

        bool ready = CooldownFraction <= 0f;
        Color accent = ready ? Gold : Gold.Darkened(0.55f);

        Juice.DrawPixelCircle(this, center, radius, BaseBg);
        Juice.DrawPixelRing(this, center, radius, 3f, accent);

        if (CooldownFraction > 0.002f)
        {
            var points = Juice.WedgePoints(radius, CooldownFraction);
            for (int i = 0; i < points.Length; i++) points[i] += center;
            DrawPolygon(points, new[] { CooldownShade });
        }

        DrawIcon(center, radius * 0.55f, ready);

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

    private void DrawIcon(Vector2 center, float r, bool ready)
    {
        Texture2D texture = Kind switch
        {
            UltimateKind.Nova => NovaTexture,
            UltimateKind.TimeSlow => HourglassTexture,
            UltimateKind.Frenzy => BoltTexture,
            UltimateKind.Invulnerability => ShieldTexture,
            _ => null,
        };
        if (texture == null) return;

        var rect = new Rect2(center - new Vector2(r, r), new Vector2(r, r) * 2f);
        DrawTextureRect(texture, rect, false, ready ? Colors.White : new Color(0.4f, 0.4f, 0.4f, 1f));
    }
}