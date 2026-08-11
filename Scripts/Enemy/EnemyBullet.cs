namespace ShooterLoop;

public partial class EnemyBullet : Area2D
{
    [Export] public float Speed = 260f;
    [Export] public float Lifetime = 4f;

    public Vector2 Direction = Vector2.Right;

    private float _timeAlive = 0f;

    public override void _Ready()
    {
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
            player.TakeHit(GlobalPosition);
            QueueFree();
        }
    }
}
