namespace ShooterLoop;

public partial class Bullet : Area2D
{
    [Export] public float Speed = 500f;
    [Export] public int Damage = 10;
    [Export] public float Lifetime = 3f;

    public Vector2 Direction = Vector2.Right;

    // How many *extra* enemies this bullet can pass through before being spent (Pierce reward).
    // 0 = stops on the first enemy hit, which is the un-upgraded behaviour.
    public int Pierce = 0;

    // Shove applied to each enemy hit, along the bullet's own travel direction (Retroceso reward).
    public float Knockback = 0f;

    // Burn applied to each enemy hit (Incendiario reward). 0 dps = no burn. A piercing bullet
    // ignites everything it passes through, which falls out of the Pierce flow below for free.
    public float BurnDps = 0f;
    public float BurnDuration = 0f;

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
            if (Knockback > 0f)
                enemy.ApplyKnockback(Direction * Knockback);

            if (BurnDps > 0f)
                enemy.ApplyBurn(BurnDps, BurnDuration);

            enemy.TakeDamage(Damage);

            // Area2D raises BodyEntered once per body, so a piercing bullet can't re-hit the same
            // enemy on its way through — no need to track who's already been hit.
            if (Pierce > 0)
            {
                Pierce--;
                return;
            }
        }

        // The collision mask only lets enemies (layer 2) and obstacles (layer 8) through, so
        // anything that isn't an Enemy is a wall. Walls stop even a fully-pierced bullet.
        QueueFree();
    }
}
