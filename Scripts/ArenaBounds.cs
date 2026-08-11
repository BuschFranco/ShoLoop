namespace ShooterLoop;

// Draws the playfield edge. The player is clamped to ArenaHalfExtents in code rather than by a
// physical wall, so without this the boundary is an invisible barrier you only discover by
// bumping into it — very confusing now that the arena is far larger than one screen.
//
// Reads the extents off the player instead of hardcoding them, so the arena size stays a single
// number to change (Player.ArenaHalfExtents), matching how CameraRig derives its limits.
public partial class ArenaBounds : Line2D
{
    public override void _Ready()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player == null) return;

        var e = player.ArenaHalfExtents;
        AddPoint(new Vector2(-e.X, -e.Y));
        AddPoint(new Vector2(e.X, -e.Y));
        AddPoint(new Vector2(e.X, e.Y));
        AddPoint(new Vector2(-e.X, e.Y));
        AddPoint(new Vector2(-e.X, -e.Y));

        Width = 6f;
        DefaultColor = Palette.ArenaBounds;
        ZIndex = -2;

        DrawGrid(e);
    }

    // A faint grid across the playfield. The arena is several screens wide and the camera follows
    // the player, so on an otherwise empty dark field there's nothing for the eye to measure motion
    // against — you can be moving fast and not feel it. Drawn as children of the bounds outline
    // since this node already knows the extents.
    private const float GridSpacing = 200f;

    private void DrawGrid(Vector2 extents)
    {
        for (float x = -extents.X + GridSpacing; x < extents.X; x += GridSpacing)
            AddGridLine(new Vector2(x, -extents.Y), new Vector2(x, extents.Y));

        for (float y = -extents.Y + GridSpacing; y < extents.Y; y += GridSpacing)
            AddGridLine(new Vector2(-extents.X, y), new Vector2(extents.X, y));
    }

    private void AddGridLine(Vector2 from, Vector2 to)
    {
        var line = new Line2D();
        line.AddPoint(from);
        line.AddPoint(to);
        line.Width = 2f;
        line.DefaultColor = Palette.GridLine;
        line.ZIndex = -3;   // behind the boundary outline and everything else
        AddChild(line);
    }
}
