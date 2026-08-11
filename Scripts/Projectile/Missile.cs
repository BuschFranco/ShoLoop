namespace ShooterLoop;

// A slow, hard-hitting area-damage projectile fired on its own low-cadence timer (Misil reward).
//
// It flies to a point, not to a target. TargetPosition is captured once at launch and never
// re-read, so a fast enemy can simply outrun it and the missile still detonates on the spot it was
// aimed at — that miss is the intended tradeoff for the blast damage, not a bug to fix with homing.
public partial class Missile : Area2D
{
    [Export] public float Speed = 340f;

    // Safety net: a missile whose target point is somehow unreachable still cleans itself up.
    [Export] public float Lifetime = 5f;

    // Close enough to count as arrived. Needs to exceed one frame of travel (Speed * delta) or the
    // missile can step past the point without ever landing inside the threshold.
    [Export] public float ArrivalThreshold = 14f;

    public Vector2 TargetPosition;
    public int Damage = 30;
    public float ExplosionRadius = 70f;

    private float _timeAlive = 0f;
    private bool _exploded = false;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        Rotation = (TargetPosition - GlobalPosition).Angle();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_exploded) return;

        Vector2 toTarget = TargetPosition - GlobalPosition;
        if (toTarget.Length() <= ArrivalThreshold)
        {
            Explode();
            return;
        }

        GlobalPosition += toTarget.Normalized() * Speed * (float)delta;

        _timeAlive += (float)delta;
        if (_timeAlive >= Lifetime)
            Explode();
    }

    // Detonates early on contact — an enemy that wanders into the flight path eats the blast rather
    // than the missile passing through it. Obstacles stop it too (mask 10, same as a bullet).
    private void OnBodyEntered(Node2D body)
    {
        if (!_exploded)
            Explode();
    }

    private void Explode()
    {
        // BodyEntered can fire in the same frame we've already detonated from arrival/lifetime, and
        // a blast that ran twice would double its damage.
        if (_exploded) return;
        _exploded = true;

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is Enemy enemy && IsInstanceValid(enemy)
                && GlobalPosition.DistanceTo(enemy.GlobalPosition) <= ExplosionRadius)
            {
                enemy.TakeDamage(Damage);
            }
        }

        SpawnBlastVisual();
        QueueFree();
    }

    // Parented to the Bullets container rather than to the missile, because the missile frees itself
    // on this same frame and would take the visual down with it. Same approach as
    // Enemy.SpawnScorePopup.
    private void SpawnBlastVisual()
    {
        var parent = GetParent();
        if (parent == null) return;

        var visual = new Polygon2D();
        var points = new Vector2[24];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.Tau;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ExplosionRadius;
        }
        visual.Polygon = points;
        visual.Color = new Color(1f, 0.6f, 0.2f, 0.5f);
        visual.Scale = Vector2.Zero;
        visual.ZIndex = 5;
        parent.AddChild(visual);
        visual.GlobalPosition = GlobalPosition;

        var tween = visual.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(visual, "scale", Vector2.One, 0.2f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(visual, "modulate:a", 0f, 0.35f);
        tween.Chain().TweenCallback(Callable.From(() => visual.QueueFree()));
    }
}
