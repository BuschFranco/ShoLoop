namespace ShooterLoop;

// The Zona que se Encoge event: a safe circle centred on the arena that contracts over the round, hurting
// the player whenever they're outside it.
//
// Centred on the arena rather than on the player on purpose — a zone that followed you would be no
// constraint at all. Squeezing everyone toward the middle is also what stops the round being won by
// running in circles at the map's edge.
public partial class ShrinkingZone : Node2D
{
    private const float StartRadius = 1350f;
    private const float EndRadius = 430f;

    // Reaches its smallest a little before the round ends, so the tightest phase is actually played
    // rather than arriving as the timer runs out.
    private const float ShrinkDuration = 48f;

    // Being outside is a steady drain, not an instant kill. TakeHit runs the normal shield and
    // invulnerability path, so the real floor on how fast this can hurt you is the i-frame window.
    private const float DamageInterval = 0.9f;

    private static readonly Color DangerColor = new(0.62f, 0.03f, 0.12f, 0.36f);
    private static readonly Color EdgeColor = new(1f, 0.24f, 0.2f, 0.95f);

    private float _age;
    private float _damageAccumulator;
    private Node2D _player;

    private float CurrentRadius =>
        Mathf.Lerp(StartRadius, EndRadius, Mathf.Clamp(_age / ShrinkDuration, 0f, 1f));

    public override void _Ready()
    {
        AddToGroup("round_event_hazards");
        ZIndex = -1;   // on the floor, so it never hides an enemy
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        QueueRedraw();

        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            if (_player == null) return;
        }

        bool outside = _player.GlobalPosition.Length() > CurrentRadius;
        if (!outside)
        {
            // Reset rather than decay, so stepping back inside for a moment genuinely resets the clock
            // instead of leaving a partial tick banked against you.
            _damageAccumulator = 0f;
            return;
        }

        _damageAccumulator += (float)delta;
        if (_damageAccumulator < DamageInterval) return;
        _damageAccumulator = 0f;

        // TakeHit knocks the player *away* from the position it's given, so the source has to be a point
        // further out along the same ray — that shoves them back toward the safe circle. Passing the arena
        // centre (the intuitive choice) would do the exact opposite and drive them deeper into the danger.
        if (_player is Player player)
        {
            Vector2 outward = player.GlobalPosition.Normalized();
            if (outward == Vector2.Zero) outward = Vector2.Right;
            player.TakeHit(player.GlobalPosition + outward * 120f);
        }
    }

    public override void _Draw()
    {
        float radius = CurrentRadius;

        // The lethal region is painted as a thick annulus from the safe edge outward — showing where you
        // must not be reads better than outlining where you may.
        const float Reach = 3600f;
        DrawArc(Vector2.Zero, radius + Reach * 0.5f, 0f, Mathf.Tau, 96, DangerColor, Reach, false);
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 96, EdgeColor, 4f, true);
    }
}
