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
        // Tinted by DangerDirector as rounds climb. The grid lines below are children, so they inherit
        // this node's Modulate — one property shifts the whole map's tone.
        AddToGroup("arena_bounds");

        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player == null) return;

        var e = player.ArenaHalfExtents;
        AddPoint(new Vector2(-e.X, -e.Y));
        AddPoint(new Vector2(e.X, -e.Y));
        AddPoint(new Vector2(e.X, e.Y));
        AddPoint(new Vector2(-e.X, e.Y));
        AddPoint(new Vector2(-e.X, -e.Y));

        Width = 6f;
        DefaultColor = GameManager.Instance.CosmeticColor(CosmeticCategory.Arena, Palette.ArenaBounds);
        ZIndex = -2;

        DrawGrid(e);
    }

    // A faint grid across the playfield. The arena is several screens wide and the camera follows
    // the player, so on an otherwise empty dark field there's nothing for the eye to measure motion
    // against — you can be moving fast and not feel it. Drawn as children of the bounds outline
    // since this node already knows the extents.
    private const float GridSpacing = 200f;

    // How much of the grid's base alpha survives out at the arena edge. Fading the floor toward the
    // rim reads as the ground receding rather than as a flat sheet that simply stops, which is the
    // cheapest depth cue available here — it costs nothing per frame, since the lines are built once
    // and never touched again.
    //
    // Not taken all the way to 0: the grid exists to give the eye something to measure motion
    // against (see DrawGrid), and that job matters most out near the edges where there's least else
    // on screen.
    private const float GridEdgeFade = 0.3f;

    private void DrawGrid(Vector2 extents)
    {
        for (float x = -extents.X + GridSpacing; x < extents.X; x += GridSpacing)
            AddGridLine(new Vector2(x, -extents.Y), new Vector2(x, extents.Y), Mathf.Abs(x) / extents.X);

        for (float y = -extents.Y + GridSpacing; y < extents.Y; y += GridSpacing)
            AddGridLine(new Vector2(-extents.X, y), new Vector2(extents.X, y), Mathf.Abs(y) / extents.Y);
    }

    // edgeRatio: 0 through the middle of the arena, 1 out at the boundary.
    private void AddGridLine(Vector2 from, Vector2 to, float edgeRatio)
    {
        Color color = GameManager.Instance.CosmeticColor(CosmeticCategory.Arena, Palette.GridLine);
        color.A *= Mathf.Lerp(1f, GridEdgeFade, edgeRatio);

        var line = new Line2D();
        line.AddPoint(from);
        line.AddPoint(to);
        line.Width = 2f;
        line.DefaultColor = color;
        line.ZIndex = -3;   // behind the boundary outline and everything else
        AddChild(line);
    }
}
