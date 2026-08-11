namespace ShooterLoop;

public partial class Bullet : Area2D
{
    [Export] public float Speed = 500f;
    [Export] public int Damage = 10;
    [Export] public float Lifetime = 3f;

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
        if (body is Enemy enemy)
        {
            enemy.TakeDamage(Damage);
            QueueFree();
        }
    }
}
