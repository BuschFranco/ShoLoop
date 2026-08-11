namespace ShooterLoop;

public partial class OrbitShield : Area2D
{
    [Export] public int Damage = 16;
    [Export] public float OrbitSpeed = 3f;
    [Export] public float OrbitRadius = 100f;

    // Shove dealt to whatever the blade clips, directed away from the player rather than away from
    // the blade — pushing off the blade itself would sometimes fling enemies *inward* past it.
    [Export] public float KnockbackForce = 260f;

    // How often a blade can damage the enemies it's touching. This used to run off BodyEntered,
    // which fires only on *entry* — an enemy sitting inside the blade's area took one hit and then
    // nothing until the blade came all the way back around (~2.1s at OrbitSpeed 3), making the
    // whole reward feel like it did nothing. Ticking on an interval instead gives a predictable
    // Damage/HitInterval DPS (40/s per blade at the current numbers) regardless of orbit speed.
    [Export] public float HitInterval = 0.4f;

    public float AngleOffset = 0f;

    private float _angle;
    private float _hitCooldown;
    private Player _owner;

    public override void _Ready()
    {
        _angle = AngleOffset;

        // Blades are always instantiated as children of the player (Player.RefreshOrbitBlades).
        _owner = GetParent() as Player;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Spin scales with the player's attack speed, so Rapid Fire upgrades visibly wind the
        // blades up as well. Read live rather than cached: it also picks up the temporary doubling
        // from the Sobrecarga ultimate, and blades created at different times all stay in sync
        // because the ratio comes from the player's single _baseFireRate snapshot.
        float spinMult = _owner?.FireRateRatio ?? 1f;

        _angle += OrbitSpeed * spinMult * (float)delta;
        Position = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * OrbitRadius;

        _hitCooldown -= (float)delta;
        if (_hitCooldown > 0f) return;

        _hitCooldown = HitInterval;
        DamageOverlappingEnemies();
    }

    private void DamageOverlappingEnemies()
    {
        Vector2 origin = _owner != null ? _owner.GlobalPosition : GlobalPosition;

        foreach (var body in GetOverlappingBodies())
        {
            if (body is not Enemy enemy || !IsInstanceValid(enemy)) continue;

            // Knock back before damaging: TakeDamage can free the enemy, and a Splitter spawns its
            // children from inside that call — shoving first keeps the ordering obvious either way.
            Vector2 away = enemy.GlobalPosition - origin;
            if (away != Vector2.Zero)
                enemy.ApplyKnockback(away.Normalized() * KnockbackForce);

            // Incendiario reward: blades burn on every tick just like every other weapon does.
            if (_owner != null && _owner.CurrentBurnDps > 0f)
                enemy.ApplyBurn(_owner.CurrentBurnDps, _owner.CurrentBurnDuration);

            // Golpe Crítico reward: blades roll crit like every other weapon. No visual tint here —
            // a per-tick flicker on the blade's own fixed colour would read as jitter, not feedback.
            int finalDamage = _owner != null ? _owner.ApplyCrit(Damage, out _) : Damage;
            enemy.TakeDamage(finalDamage);
        }
    }
}
