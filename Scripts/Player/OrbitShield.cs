namespace ShooterLoop;

public partial class OrbitShield : Area2D
{
    [Export] public int Damage = 7;
    [Export] public float OrbitSpeed = 3f;
    [Export] public float OrbitRadius = 65f;

    public float AngleOffset = 0f;

    private float _angle;

    public override void _Ready()
    {
        _angle = AngleOffset;
        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        _angle += OrbitSpeed * (float)delta;
        Position = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * OrbitRadius;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Enemy enemy)
        {
            enemy.TakeDamage(Damage);
        }
    }
}
