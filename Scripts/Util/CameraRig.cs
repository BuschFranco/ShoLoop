namespace ShooterLoop;

// Follows the player with a deliberate lag, so the camera reads as being *dragged* along rather
// than welded to the ship.
//
// The follow itself is a hard snap to the player's position every physics frame; the softness
// comes from Godot's built-in PositionSmoothingEnabled/Speed (set in Arena.tscn), which makes the
// camera's *rendered* position ease toward its actual node position. Using the engine's smoothing
// rather than a hand-rolled lerp keeps it framerate-independent for free.
//
// Kept as a sibling of the player (not a child) so the arena scene owns the camera and Player.tscn
// stays reusable without dragging a camera along with it.
public partial class CameraRig : Camera2D
{
    private Node2D _player;
    private bool _limitsApplied;

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            if (_player == null) return;
        }

        // Camera limits are derived from the player's own ArenaHalfExtents rather than duplicated
        // in the scene file, so the playfield size stays a single number to change.
        if (!_limitsApplied && _player is Player player)
        {
            var extents = player.ArenaHalfExtents;
            LimitLeft = Mathf.RoundToInt(-extents.X);
            LimitRight = Mathf.RoundToInt(extents.X);
            LimitTop = Mathf.RoundToInt(-extents.Y);
            LimitBottom = Mathf.RoundToInt(extents.Y);
            _limitsApplied = true;
        }

        GlobalPosition = _player.GlobalPosition;
    }
}
