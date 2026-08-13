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
    private readonly Random _rng = new();

    // Transient shake, currently used to punctuate a boss's arrival. Perturbs Offset rather than
    // GlobalPosition because the follow below overwrites GlobalPosition outright every physics frame —
    // and because Offset is applied *after* Godot's position smoothing, so the shake reads crisp
    // instead of being smoothed into a soft drift.
    private float _shakeStrength;
    private float _shakeRemaining;
    private float _shakeDuration;

    public void Shake(float strength, float duration)
    {
        if (DangerLevel.Reduced) strength *= 0.5f;

        // Takes the stronger of the two rather than restarting, so a second trigger mid-shake can't
        // damp an ongoing stronger one.
        _shakeStrength = Mathf.Max(_shakeStrength, strength);
        _shakeDuration = Mathf.Max(_shakeDuration, duration);
        _shakeRemaining = Mathf.Max(_shakeRemaining, duration);
    }

    public override void _Ready() => AddToGroup("camera_rig");

    public override void _PhysicsProcess(double delta)
    {
        TickShake((float)delta);

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

    // Offset is explicitly zeroed on completion rather than just left wherever the last frame put it:
    // a leftover non-zero Offset would survive the player's death and leave the camera permanently
    // mis-framed for the rest of the session.
    private void TickShake(float delta)
    {
        if (_shakeRemaining <= 0f) return;

        _shakeRemaining -= delta;
        if (_shakeRemaining <= 0f)
        {
            _shakeRemaining = 0f;
            _shakeStrength = 0f;
            Offset = Vector2.Zero;
            return;
        }

        // Linear decay, so the shake tapers out instead of stopping dead on a full-amplitude frame.
        float falloff = _shakeDuration > 0f ? _shakeRemaining / _shakeDuration : 0f;
        float amplitude = _shakeStrength * falloff;

        Offset = new Vector2(
            (float)(_rng.NextDouble() * 2.0 - 1.0) * amplitude,
            (float)(_rng.NextDouble() * 2.0 - 1.0) * amplitude);
    }
}
