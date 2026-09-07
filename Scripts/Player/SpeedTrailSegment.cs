namespace ShooterLoop;

// Legendary Movement Speed's Tron-style trail — dropped periodically behind the player
// (Player.SpawnSpeedTrailSegment) while moving. Each segment is its own short-lived hazard: it ticks
// damage against anything still overlapping it, then fades and frees itself.
//
// Ticks on an interval rather than reacting to BodyEntered for the same reason OrbitShield does
// (Scripts/Player/OrbitShield.cs): BodyEntered only fires on entry, so an enemy that walks in and
// stays would take one hit and then nothing until it happened to leave and re-enter.
public partial class SpeedTrailSegment : Area2D
{
    private const int Damage = 8;
    private const float HitInterval = 0.4f;
    private const float Lifetime = 1.2f;

    private Player _owner;
    private float _hitCooldown;
    private float _lifeRemaining;

    // Called by Player right after instancing and placing this — GlobalPosition has to be set
    // before this runs, since the fade tween below reads the node's already-final Modulate alpha.
    public void Launch(Player owner)
    {
        _owner = owner;
        _lifeRemaining = Lifetime;

        CreateTween()
            .TweenProperty(this, "modulate:a", 0f, Lifetime)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
    }

    public override void _PhysicsProcess(double delta)
    {
        _lifeRemaining -= (float)delta;
        if (_lifeRemaining <= 0f)
        {
            QueueFree();
            return;
        }

        _hitCooldown -= (float)delta;
        if (_hitCooldown > 0f) return;

        _hitCooldown = HitInterval;
        DamageOverlappingEnemies();
    }

    private void DamageOverlappingEnemies()
    {
        foreach (var body in GetOverlappingBodies())
        {
            if (body is not Enemy enemy || !IsInstanceValid(enemy)) continue;

            // Same crit/burn integration every other damage source gets (Player.ApplyCrit is the one
            // shared roll for all of them) — a hazard the player's own upgrade created is still the
            // player's damage, not a separate untouched source.
            if (_owner != null && _owner.CurrentBurnDps > 0f)
                enemy.ApplyBurn(_owner.CurrentBurnDps, _owner.CurrentBurnDuration);

            int finalDamage = _owner != null ? _owner.ApplyCrit(Damage, out _) : Damage;
            enemy.TakeDamage(finalDamage);
        }
    }
}
