namespace ShooterLoop;

// A small circular ability icon that draws its own "clock" cooldown overlay — a dark pie sector that
// covers the icon right after use and sweeps away (clockwise, from 12 o'clock) as the cooldown
// clears. No texture/art asset involved, just _Draw(), matching the rest of the game's
// procedural-only visuals.
//
// Kind is a plain field set once from HUD._Ready; Accent is set from there too but re-set every
// frame in HUD.UpdateCooldownIcons (it tracks the best tier reached, which changes mid-run). Neither
// is an [Export] read from HUD.tscn. That is deliberate: the previous version exported an icon_color
// and a one-letter glyph, the scene set both on all seven icons, and they silently never arrived —
// every icon rendered with the compiled-in defaults, i.e. a white circle with a "?" in it. Setting
// them in code removes a whole class of failure (stale export metadata, a scene saved against an
// older build of the assembly) and matches what UltimateButtonIcon already does with its own Kind.
public partial class CooldownIcon : Control
{
    public enum Ability { Laser, Missile, ShieldRegen, Ultimate, Onda, Vendaval, Mine }

    public Ability Kind = Ability.Laser;

    // Half the project's art grid (Juice.PixelSize). These icons are the one place where the full
    // grid is too coarse to carry a readable symbol -- see the note in _Draw.
    private const float IconPixel = Juice.PixelSize * 0.5f;

    // Applied to Accent only while the ability is off cooldown -- see _Draw.
    private const float ReadyGlowBoost = 1.6f;

    // 1 = just used (fully covered), 0 = ready (fully clear).
    public float CooldownFraction = 0f;

    private float _lastFraction = -1f;
    private Ability _lastKind = (Ability)(-1);
    private Color _lastAccent = Colors.Transparent;

    // Set by HUD every frame from Player.OwnedTiers[the ability's UpgradeType] via
    // RewardTierRoller.GetTierColor — the icon now reads "how strong is this" (which tier you've
    // reached) rather than "which ability is this". Used to be a fixed per-Ability colour so the 5-7
    // icons stayed distinguishable from each other despite every player weapon being green; now two
    // different abilities at the same tier do share a colour, but each still has its own silhouette
    // (see DrawAbility below), so they stay tellable apart by shape instead of by colour.
    public Color Accent = Colors.White;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(36, 36);
    }

    public override void _Process(double delta)
    {
        if (!Mathf.IsEqualApprox(CooldownFraction, _lastFraction) || Kind != _lastKind || Accent != _lastAccent)
            QueueRedraw();
    }

    public override void _Draw()
    {
        _lastFraction = CooldownFraction;
        _lastKind = Kind;
        _lastAccent = Accent;

        Vector2 center = Size / 2f;
        float radius = Mathf.Min(Size.X, Size.Y) / 2f - 2f;
        if (radius <= 0f) return;

        bool ready = CooldownFraction <= 0f;
        Color accent = Accent;

        // Pushed past 1.0 so the icon actually blooms under the arena's WorldEnvironment once
        // ready, instead of just sitting at whatever raw brightness its tier colour happens to have.
        // Without this, Común's mint (peaking at G=1.0, R/B well below) cleared the bloom threshold
        // on one channel at most and read as noticeably dimmer/"darker" than Rare/Epic/Legendary next
        // to it, even though nothing was actually wrong with its state -- every tier now gets the
        // same glow treatment regardless of how saturated its own base colour is.
        Color boostedAccent = new(accent.R * ReadyGlowBoost, accent.G * ReadyGlowBoost, accent.B * ReadyGlowBoost, accent.A);

        // Dark disc with a coloured rim, rather than the old solid-colour disc. A filled disc left
        // the glyph fighting the fill for contrast; an outlined one gives the symbol a dark field to
        // sit on and reads better at 36px over a bright arena.
        // IconPixel, not Juice.PixelSize: at 36px this icon's radius is ~16, and the project's 4px
        // grid would render it five blocks wide with no room left for the glyph inside.
        Juice.DrawPixelCircle(this, center, radius, new Color(0.102f, 0.0588f, 0.1686f, 0.88f), IconPixel);
        Juice.DrawPixelRing(this, center, radius, 2f, ready ? boostedAccent : accent.Darkened(0.5f), IconPixel);

        DrawAbility(center, radius * 0.62f, ready ? boostedAccent : accent.Darkened(0.45f));

        if (CooldownFraction > 0.002f)
        {
            var points = Juice.WedgePoints(radius, CooldownFraction, IconPixel);
            for (int i = 0; i < points.Length; i++) points[i] += center;
            DrawPolygon(points, new[] { new Color(0f, 0f, 0f, 0.72f) });
        }
    }

    // Every shape below is drawn inside a radius-r box around center. They're built as silhouettes
    // rather than outlines because at 36px an outline collapses into a smudge.
    private void DrawAbility(Vector2 center, float r, Color color)
    {
        switch (Kind)
        {
            case Ability.Laser: DrawBeam(center, r, color); break;
            case Ability.Missile: DrawDart(center, r, color); break;
            case Ability.ShieldRegen: DrawShield(center, r, color); break;
            case Ability.Ultimate: DrawStar(center, r, color); break;
            case Ability.Onda: DrawRings(center, r, color); break;
            case Ability.Vendaval: DrawCone(center, r, color); break;
            case Ability.Mine: DrawMine(center, r, color); break;
        }
    }

    // Láser: a horizontal beam with a tapered tip — long and thin, which is the one silhouette
    // nothing else here shares. h used to be r * 0.28, a sliver so thin it had barely any filled
    // area next to Missile's dart or Onda's rings — at 36px that reads as "dim" even at full
    // brightness, just from having so little colour on screen, so this reads dark compared to its
    // siblings even when the ability is fully off cooldown. Thickened so it carries similar visual
    // weight, while staying clearly flatter/wider than Vendaval's tall cone.
    private void DrawBeam(Vector2 center, float r, Color color)
    {
        float h = r * 0.42f;
        DrawPolygon(new[]
        {
            center + new Vector2(-r, -h),
            center + new Vector2(r * 0.45f, -h),
            center + new Vector2(r, 0f),
            center + new Vector2(r * 0.45f, h),
            center + new Vector2(-r, h),
        }, new[] { color });
    }

    // Misil: the projectile's own silhouette from Missile.tscn, normalised — notched tail included,
    // since that notch is what separates it from Vendaval's cone at this size.
    private void DrawDart(Vector2 center, float r, Color color)
    {
        var raw = new[]
        {
            new Vector2(1f, 0f),
            new Vector2(-0.67f, -0.5f),
            new Vector2(-0.33f, 0f),
            new Vector2(-0.67f, 0.5f),
        };
        var points = new Vector2[raw.Length];
        for (int i = 0; i < raw.Length; i++) points[i] = center + raw[i] * r;
        DrawPolygon(points, new[] { color });
    }

    // Regeneración de escudo: a shield outline with a cross in it, so it reads as "shield coming
    // back" rather than just "shield".
    private void DrawShield(Vector2 center, float r, Color color)
    {
        DrawPolygon(new[]
        {
            center + new Vector2(0f, -r),
            center + new Vector2(r * 0.8f, -r * 0.5f),
            center + new Vector2(r * 0.8f, r * 0.2f),
            center + new Vector2(0f, r),
            center + new Vector2(-r * 0.8f, r * 0.2f),
            center + new Vector2(-r * 0.8f, -r * 0.5f),
        }, new[] { color });

        var dark = new Color(0.102f, 0.0588f, 0.1686f, 0.95f);
        DrawLine(center + new Vector2(-r * 0.35f, 0f), center + new Vector2(r * 0.35f, 0f), dark, 2f);
        DrawLine(center + new Vector2(0f, -r * 0.35f), center + new Vector2(0f, r * 0.35f), dark, 2f);
    }

    // Ultimate: the same 8-point starburst the Ultimate button uses, so the HUD's two references to
    // the same ability agree with each other.
    private void DrawStar(Vector2 center, float r, Color color)
    {
        const int spikes = 8;
        var points = new Vector2[spikes * 2];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.Tau - Mathf.Pi / 2f;
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? r : r * 0.45f);
        }
        DrawPolygon(points, new[] { color });
    }

    // Onda de Choque: concentric rings radiating out — the blast expanding, which is literally what
    // the ability draws in the arena.
    private void DrawRings(Vector2 center, float r, Color color)
    {
        Juice.DrawPixelCircle(this, center, r * 0.22f, color, IconPixel);
        Juice.DrawPixelRing(this, center, r * 0.6f, 1.8f, color, IconPixel);
        Juice.DrawPixelRing(this, center, r, 1.8f, color, IconPixel);
    }

    // Vendaval: a wide forward cone. Deliberately short and fat where the missile dart is long and
    // thin — that proportion is the whole distinction between them at icon size.
    private void DrawCone(Vector2 center, float r, Color color)
    {
        DrawPolygon(new[]
        {
            center + new Vector2(r, -r * 0.9f),
            center + new Vector2(r, r * 0.9f),
            center + new Vector2(-r * 0.75f, 0f),
        }, new[] { color });
    }

    // Mina: the classic spiked sea mine — a disc with spokes, unmistakable even this small.
    private void DrawMine(Vector2 center, float r, Color color)
    {
        const int spikes = 6;
        for (int i = 0; i < spikes; i++)
        {
            float angle = i / (float)spikes * Mathf.Tau;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            DrawLine(center + dir * r * 0.45f, center + dir * r, color, 2f);
        }
        Juice.DrawPixelCircle(this, center, r * 0.55f, color, IconPixel);
    }
}
