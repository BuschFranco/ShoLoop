namespace ShooterLoop;

// A static mine from the Campo Minado event. Unlike the missile strike's timed circles, these are placed
// once at round start and simply sit there for the whole round — the threat is spatial rather than
// temporal, so movement has to be planned around the map instead of reacted to.
//
// Detonates for anything that touches it, player or enemy, so kiting the horde over a minefield is a real
// (and intended) tactic.
public partial class Mine : Area2D
{
    public int Damage = 55;
    public float BlastRadius = 110f;

    private const float BodyRadius = 15f;
    private const float PulseSpeed = 3f;

    private Polygon2D _visual;
    private float _age;
    private bool _detonated;

    private static readonly Color MineColor = new(1f, 0.42f, 0.12f, 1f);

    public override void _Ready()
    {
        AddToGroup("round_event_hazards");
        ZIndex = -1;

        // Layer 0 / mask 3 so it detects the player (layer 1) and enemies (layer 2) without being
        // something either of them can collide against.
        CollisionLayer = 0;
        CollisionMask = 1 | 2;

        _visual = new Polygon2D();
        var points = new Vector2[3];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = -Mathf.Pi / 2f + i / (float)points.Length * Mathf.Tau;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * BodyRadius;
        }
        _visual.Polygon = points;
        _visual.Color = MineColor;
        AddChild(_visual);

        var shape = new CollisionShape2D { Shape = new CircleShape2D { Radius = BodyRadius } };
        AddChild(shape);

        BodyEntered += OnBodyEntered;
    }

    // A slow pulse so a stationary hazard still reads as live rather than as scenery — the same reason
    // the world pickups pulse.
    public override void _Process(double delta)
    {
        if (_detonated) return;
        _age += (float)delta;
        _visual.Scale = Vector2.One * (1f + Mathf.Sin(_age * PulseSpeed) * 0.15f);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_detonated) return;
        if (body is not Player && body is not Enemy) return;

        _detonated = true;
        _visual.Visible = false;

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Enemy enemy || !IsInstanceValid(enemy)) continue;
            if (GlobalPosition.DistanceTo(enemy.GlobalPosition) <= BlastRadius)
                enemy.TakeDamage(Damage);
        }

        if (GetTree().GetFirstNodeInGroup("player") is Player player
            && player.GlobalPosition.DistanceTo(GlobalPosition) <= BlastRadius)
        {
            player.TakeHit(GlobalPosition);
        }

        SpawnBlast();
    }

    private void SpawnBlast()
    {
        var blast = new Polygon2D();
        var points = new Vector2[24];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.Tau;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * BlastRadius;
        }
        blast.Polygon = points;
        blast.Color = Palette.MissileBlast;
        blast.Scale = Vector2.One * 0.3f;
        AddChild(blast);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(blast, "scale", Vector2.One, 0.2f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(blast, "modulate:a", 0f, 0.3f);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
