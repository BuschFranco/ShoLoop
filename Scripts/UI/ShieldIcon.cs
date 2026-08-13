namespace ShooterLoop;

// A single shield glyph in the HUD's shield row, drawn procedurally — the exact counterpart to
// HeartIcon, and deliberately built the same way so the two rows read as one family. Filled = a charge
// you currently hold; unfilled = a charge slot you've spent.
//
// Replaced a plain "Escudo: 2/4" text line. Shields are consumed one at a time under pressure, which is
// precisely the kind of value you want to read at a glance rather than parse as a fraction.
public partial class ShieldIcon : Control
{
    public bool Filled = true;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(24, 24);
    }

    public override void _Draw()
    {
        var points = ShieldPoints(Size);

        if (Filled)
        {
            DrawColoredPolygon(points, Palette.ShieldPickupColor);
        }
        else
        {
            DrawColoredPolygon(points, new Color(Palette.ShieldPickupColor, 0.18f));
            var outline = new Vector2[points.Length + 1];
            points.CopyTo(outline, 0);
            outline[points.Length] = points[0];
            DrawPolyline(outline, new Color(Palette.ShieldPickupColor, 0.6f), 1.5f, true);
        }
    }

    // Same shape as the ShieldPickup drop's own polygon (see Scenes/Pickup/ShieldPickup.tscn), scaled to
    // whatever size the node ends up: a crest that tapers to a point at the bottom. Scaling off
    // min(size)/20 mirrors HeartIcon so both icons stay resolution-independent.
    private static Vector2[] ShieldPoints(Vector2 size)
    {
        float s = Mathf.Min(size.X, size.Y) / 20f;
        Vector2 c = size / 2f;
        return new[]
        {
            c + new Vector2(-8, -9) * s,
            c + new Vector2(8, -9) * s,
            c + new Vector2(9, 1) * s,
            c + new Vector2(0, 10) * s,
            c + new Vector2(-9, 1) * s,
        };
    }
}
