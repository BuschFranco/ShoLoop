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

    public override void _Ready()
    {
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
            player.TakeHit(GlobalPosition);
            QueueFree();
        }
    }
}
