namespace ShooterLoop;

// A solid block in the arena that both the player and enemies collide with. Sits on collision
// layer 8 (see the layer map in docs/enemies.md); projectiles deliberately ignore it, so obstacles
// shape movement and kiting routes without ever blocking your own shots.
//
// Visual + collision shape are built procedurally from Size, matching how every other visual in
// this project is made (no art assets). That means an obstacle is just a StaticBody2D with this
// script and a Size — no per-instance sub-resources or a separate .tscn to keep in sync.
public partial class Obstacle : StaticBody2D
{
    [Export] public Vector2 Size = new(160f, 160f);

    public override void _Ready()
    {
        float hw = Size.X / 2f;
        float hh = Size.Y / 2f;
        var corners = new[]
        {
            new Vector2(-hw, -hh),
            new Vector2(hw, -hh),
            new Vector2(hw, hh),
            new Vector2(-hw, hh),
        };

        var fill = new Polygon2D();
        fill.Polygon = corners;
        fill.Color = new Color(0.14f, 0.15f, 0.22f, 1f);
        AddChild(fill);

        // Neon outline, same visual language as the enemies and the fire-range ring.
        var outline = new Line2D();
        foreach (var corner in corners) outline.AddPoint(corner);
        outline.AddPoint(corners[0]);
        outline.Width = 3f;
        outline.DefaultColor = new Color(0.35f, 0.5f, 0.9f, 0.9f);
        AddChild(outline);

        var collision = new CollisionShape2D();
        collision.Shape = new RectangleShape2D { Size = Size };
        AddChild(collision);
    }
}
