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
    }
}
