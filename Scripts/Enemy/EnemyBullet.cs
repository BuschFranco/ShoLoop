namespace ShooterLoop;

public partial class EnemyBullet : Area2D
{
    [Export] public float Speed = 290f;

    // Speed x Lifetime is the shot's effective range: 290 x 5.5 ~= 1600px, up from ~1040. Shooters are
    // slow artillery, so a shot that dies before it can threaten anyone made them safe to simply
    // outwalk.
    [Export] public float Lifetime = 5.5f;

    public Vector2 Direction = Vector2.Right;

    private float _timeAlive = 0f;

    // Same reasoning as Bullet._visual: read the colour off the scene so the impact spark can't drift
    // away from whatever the projectile is actually painted.
    private Polygon2D _visual;

    private const float ImpactSize = 12f;

    public override void _Ready()
    {
        _visual = GetNodeOrNull<Polygon2D>("Visual");

        // Grouped so GameManager.EndRound() can sweep in-flight shots along with the enemies that
        // fired them — otherwise a bullet frozen mid-air by the shop's pause resumes afterwards and
        // can land on the player during the next round's "get ready" countdown.
        AddToGroup("enemy_bullets");

        BodyEntered += OnBodyEntered;
        Rotation = Direction.Angle();
    }

    public override void _PhysicsProcess(double delta)
    {
        Position += Direction * Speed * (float)delta;
        _timeAlive += (float)delta;
        if (_timeAlive >= Lifetime)
            QueueFree();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            // Cosmetic only, and spawned before TakeHit rather than after: TakeHit can end the run,
            // and the spark should already be parented to the container (not to this dying bullet) by
            // the time anything downstream starts tearing the scene down.
            Color color = (_visual?.Color ?? Palette.EnemyBullet) * Modulate;
            Juice.Spark(GetParent(), GlobalPosition, Direction, color.Lightened(0.35f), ImpactSize);

            player.TakeHit(GlobalPosition);
            QueueFree();
        }
    }
}
