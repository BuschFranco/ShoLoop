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

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            if (_player == null) return;
        }

        // Deliberately no Limit* clamping to ArenaHalfExtents: clamping the camera to the arena
        // bounds means the player drifts toward the edge of the screen (and, on mobile, under the
        // player's own thumb/fingers on the touch controls) the moment they get near a wall — the
        // exact opposite of what "the camera follows you" should feel like. Panning past the
        // arena's edge just reveals the dark backdrop (see docs/visuals.md), which is screen-
        // anchored on its own CanvasLayer and already covers the viewport regardless of where the
        // camera points, so there's nothing uglier to clip into by removing the clamp.
        GlobalPosition = _player.GlobalPosition;
    }
}
