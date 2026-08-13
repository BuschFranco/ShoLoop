namespace ShooterLoop;

// A single heart glyph in the HUD's lives row, drawn procedurally (no texture) — same approach
// as CooldownIcon. Filled = a life you currently have; unfilled = a life slot you've lost.
public partial class HeartIcon : Control
{
    public bool Filled = true;

    public override void _Ready()
    {
        // 18 was too small to read at a glance on a phone. HeartPoints scales off min(Size)/20, so this
        // one number is the whole knob — the geometry follows.
        CustomMinimumSize = new Vector2(26, 26);
    }

    public override void _Draw()
    {
        var points = HeartPoints(Size);

        if (Filled)
        {
            DrawColoredPolygon(points, Palette.HeartPickup);
        }
        else
        {
            DrawColoredPolygon(points, new Color(Palette.HeartPickup, 0.18f));
            var outline = new Vector2[points.Length + 1];
            points.CopyTo(outline, 0);
            outline[points.Length] = points[0];
            DrawPolyline(outline, new Color(Palette.HeartPickup, 0.6f), 1.5f, true);
        }
    }

    private static Vector2[] HeartPoints(Vector2 size)
    {
        float s = Mathf.Min(size.X, size.Y) / 20f;
        Vector2 c = size / 2f;
        return new[]
        {
            c + new Vector2(0, 5) * s,
            c + new Vector2(-8, -3) * s,
            c + new Vector2(-8, -9) * s,
            c + new Vector2(-3, -12) * s,
            c + new Vector2(0, -7) * s,
            c + new Vector2(3, -12) * s,
            c + new Vector2(8, -9) * s,
            c + new Vector2(8, -3) * s,
        };
    }
}
