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

    // Ricochet: number of additional bounces to nearby enemies (Rebote reward).
    public int Ricochet = 0;

    private float _timeAlive = 0f;
    private readonly HashSet<ulong> _hitEnemies = new();

    // Read off the scene rather than from Palette so the impact spark tracks whatever colour the
    // bullet is actually painted — Bullet.tscn's colour is hand-kept in sync with Palette.PlayerBullet,
    // and this way a drift between the two can't make the spark disagree with the projectile.
    private Polygon2D _visual;

    private const float EnemyImpactSize = 11f;
    private const float WallImpactSize = 6f;

    public override void _Ready()
    {
        _visual = GetNodeOrNull<Polygon2D>("Visual");
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

    // Purely cosmetic: no damage, no knockback, nothing gameplay-visible reads it. Colour is the
    // bullet's own on-screen colour (the crit tint rides on the root's Modulate, so the product is
    // exactly what the player sees), lightened because a spark should read hotter than the projectile
    // that threw it — and because the arena's bloom only catches pixels above 0.85.
    private void SpawnImpact(float size)
    {
        Color color = (_visual?.Color ?? Palette.PlayerBullet) * Modulate;
        Juice.Spark(GetParent(), GlobalPosition, Direction, color.Lightened(0.35f), size);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Enemy enemy)
        {
            _hitEnemies.Add(enemy.GetInstanceId());

            // Fired here, above all three of this method's exits, rather than next to the QueueFree at
            // the bottom: a ricochet and a pierce each return early, so hanging the effect off the
            // bottom would silently skip every bounce and every enemy a piercing shot passes through —
            // precisely the builds that land the most hits.
            SpawnImpact(EnemyImpactSize);

            if (Knockback > 0f)
                enemy.ApplyKnockback(Direction * Knockback);

            if (BurnDps > 0f)
                enemy.ApplyBurn(BurnDps, BurnDuration);

            enemy.TakeDamage(Damage);

            // Ricochet: redirect to nearest unhit enemy
            if (Ricochet > 0)
            {
                Enemy nearest = FindNearestUnhitEnemy();
                if (nearest != null)
                {
                    Ricochet--;
                    Direction = (nearest.GlobalPosition - GlobalPosition).Normalized();
                    Rotation = Direction.Angle();
                    return;
                }
            }

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
        //
        // A wall hit gets its own, smaller spark: it's a miss, so it shouldn't read as loud as
        // connecting with something. Skipped when the bullet already sparked on an enemy this frame
        // (the pierce/ricochet paths return above, so reaching here after the Enemy branch means the
        // shot is genuinely spent on that enemy and has already flashed).
        if (body is not Enemy)
            SpawnImpact(WallImpactSize);

        QueueFree();
    }

    private Enemy FindNearestUnhitEnemy()
    {
        Enemy nearest = null;
        float bestDist = 300f; // max ricochet search range
        var enemies = GetTree().GetNodesInGroup("enemies");
        foreach (var node in enemies)
        {
            if (node is Enemy e && !e.IsQueuedForDeletion() && !_hitEnemies.Contains(e.GetInstanceId()))
            {
                float dist = GlobalPosition.DistanceTo(e.GlobalPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = e;
                }
            }
        }
        return nearest;
    }
}
