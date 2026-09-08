namespace ShooterLoop;

// A solid block in the arena that both the player and enemies collide with. Sits on collision
// layer 8 (see the layer map in docs/enemies.md); the player's own bullets mask it too and stop
// dead on contact (Bullet.OnBodyEntered), and Player.FindNearestVisibleEnemy/Companion.
// FindNearestEnemy raycast against it (Scripts/Util/Targeting.cs) so the gun/missile/drone don't
// even choose a target they can't actually hit through it. Enemy bullets and Laser/Onda/Ultimate
// still ignore it — see docs/player.md's Obstacles section for the full breakdown of what does and
// doesn't respect this layer.
//
// Visual + collision shape are built procedurally from Size, matching how every other visual in
// this project is made (no art assets). That means an obstacle is just a StaticBody2D with this
// script and a Size — no per-instance sub-resources or a separate .tscn to keep in sync.
public partial class Obstacle : StaticBody2D
{
    [Export] public Vector2 Size = new(160f, 160f);

    public override void _Ready()
    {
        // Tinted along with the arena wall by DangerDirector, so the map's furniture shifts tone with
        // the floor instead of staying stubbornly purple in a red arena.
        AddToGroup("obstacles");

        float hw = Size.X / 2f;
        float hh = Size.Y / 2f;
        var corners = new[]
        {
            new Vector2(-hw, -hh),
            new Vector2(hw, -hh),
            new Vector2(hw, hh),
            new Vector2(-hw, hh),
        };

        // Added before the fill so it draws underneath, and offset in the same direction as every
        // sprite shadow (see Juice.AttachShadow). Obstacles are the largest objects in the arena, so
        // if their shadows disagreed with the enemies' the fake light would read as broken rather than
        // as depth. ZIndex -1 puts it on the floor — over the grid at -3, under everything that walks.
        var shadow = new Polygon2D();
        shadow.Polygon = corners;
        shadow.Color = Juice.ShadowColor;
        shadow.Position = Juice.ShadowOffsetFor(Size.Y);
        shadow.ZIndex = -1;
        AddChild(shadow);

        // The top face is offset toward the light — the exact opposite of where the shadow falls —
        // and the gap between it and the footprint is filled with two darker "walls". That's the
        // whole trick: the block stops reading as a shape painted on the floor and starts reading as
        // one standing on it.
        //
        // Collision deliberately stays on the base footprint. The extrusion is a dozen pixels of
        // paint, and a wall you collide with somewhere other than where its base is drawn would feel
        // worse than a wall whose top overlaps a little of the floor behind it.
        Vector2 lift = -Juice.ShadowDirection * ExtrudeHeight;
        var top = new Vector2[corners.Length];
        for (int i = 0; i < corners.Length; i++) top[i] = corners[i] + lift;

        // Only two of the four walls are ever visible, and which two follows from the light being up
        // and to the left: the ones facing down and right. The down-facing one is darker because the
        // light is mostly overhead, so a wall facing straight away from it catches least.
        AddWall(new[] { corners[3], corners[2], top[2], top[3] }, Palette.ObstacleFill.Darkened(0.55f));
        AddWall(new[] { corners[1], corners[2], top[2], top[1] }, Palette.ObstacleFill.Darkened(0.35f));

        var fill = new Polygon2D();
        fill.Polygon = top;
        fill.Color = Palette.ObstacleFill;
        AddChild(fill);

        // Neon outline, same visual language as the enemies and the fire-range ring. On the top face
        // only — that's what identifies it as the lit surface rather than the silhouette.
        var outline = new Line2D();
        foreach (var corner in top) outline.AddPoint(corner);
        outline.AddPoint(top[0]);
        outline.Width = 3f;
        outline.DefaultColor = Palette.ObstacleOutline;
        AddChild(outline);

        var collision = new CollisionShape2D();
        collision.Shape = new RectangleShape2D { Size = Size };
        AddChild(collision);
    }

    // Uniform across every obstacle rather than derived from Size: blocks that got taller as they got
    // wider would read as an inconsistent world instead of as furniture at one wall height.
    private const float ExtrudeHeight = 15f;

    private void AddWall(Vector2[] quad, Color color)
    {
        var wall = new Polygon2D();
        wall.Polygon = quad;
        wall.Color = color;
        AddChild(wall);
    }
}
