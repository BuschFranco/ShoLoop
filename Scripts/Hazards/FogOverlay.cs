namespace ShooterLoop;

// The Niebla event: everything beyond a circle around the player is blacked out, so enemies arrive with
// far less warning and the fire-range ring becomes the only thing you can plan around.
//
// Drawn as a single very thick arc rather than a polygon with a hole. Godot's Polygon2D has no hole
// support and a hand-built "keyhole" polygon triangulates unpredictably; a thick DrawArc traces an
// annulus exactly, in one draw call, with the clear centre falling out for free.
public partial class FogOverlay : Node2D
{
    // Has to out-reach the arena diagonal from any corner, or the player can see past the fog's outer
    // edge to clear space beyond it.
    private const float OuterRadius = 4200f;
    private const float ClearRadius = 430f;

    private static readonly Color FogColor = new(0.02f, 0.01f, 0.04f, 0.93f);

    private Node2D _player;

    public override void _Ready()
    {
        AddToGroup("round_event_hazards");

        // Above the world (enemies and the player sit at 0) but below the floating damage/score numbers
        // at 10, so feedback stays readable through it.
        ZIndex = 8;
    }

    public override void _Process(double delta)
    {
        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            if (_player == null) return;
        }

        // Follows in _Process rather than being parented to the player: as a child it would inherit the
        // player's rotation, and the fog hole must never spin.
        GlobalPosition = _player.GlobalPosition;
    }

    public override void _Draw()
    {
        float width = OuterRadius - ClearRadius;
        DrawArc(Vector2.Zero, ClearRadius + width * 0.5f, 0f, Mathf.Tau, 96, FogColor, width, false);
    }
}
