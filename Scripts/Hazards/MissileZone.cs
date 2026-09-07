namespace ShooterLoop;

// One telegraphed strike from the Lluvia de Misiles event: a marked circle on the floor that fills up,
// then detonates and damages everything inside it — the player included.
//
// Built and drawn entirely in code like every other visual in the project. It's a plain Node2D rather
// than an Area2D because the hit is a single instantaneous radius check at detonation, not a continuous
// overlap: an Area2D would need its own shape node and would still have to be polled at the same moment.
public partial class MissileZone : Node2D
{
    public float Radius = 130f;
    public int Damage = 40;

    // Long enough to read and walk out of, which is the entire point — this is a hazard that rewards
    // paying attention, not a random tax.
    public const float WarningTime = 1.9f;

    private float _age;
    private bool _detonated;

    private static readonly Color WarnColor = new(1f, 0.24f, 0.16f, 0.30f);
    private static readonly Color RingColor = new(1f, 0.3f, 0.2f, 0.95f);

    public override void _Ready() => ZIndex = -1;   // on the floor, under the enemies and the player

    public override void _Process(double delta)
    {
        if (_detonated) return;

        _age += (float)delta;
        QueueRedraw();

        if (_age >= WarningTime) Detonate();
    }

    public override void _Draw()
    {
        if (_detonated) return;

        float fill = Mathf.Clamp(_age / WarningTime, 0f, 1f);

        // The growing inner disc is the countdown: when it reaches the ring, it goes off.
        DrawCircle(Vector2.Zero, Radius * fill, WarnColor);
        DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 40, RingColor, 3f, true);
    }

    private void Detonate()
    {
        _detonated = true;

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Enemy enemy || !IsInstanceValid(enemy)) continue;
            if (GlobalPosition.DistanceTo(enemy.GlobalPosition) <= Radius)
                enemy.TakeDamage(Damage);
        }

        // Hits the player too — a hazard only the enemies suffer isn't a hazard. TakeHit routes through
        // the normal shield/invulnerability path, so it can't chain-kill through the i-frames.
        if (GetTree().GetFirstNodeInGroup("player") is Player player
            && player.GlobalPosition.DistanceTo(GlobalPosition) <= Radius)
        {
            player.TakeHit(GlobalPosition);
        }

        SpawnBlast();
    }

    // Same expanding-and-fading shape as the missile/nova blasts. Handed to the parent so the zone —
    // which has already done its damage by this point — can go now instead of being kept alive purely
    // to host its own farewell animation.
    private void SpawnBlast()
    {
        AudioManager.Instance?.Play(AudioManager.Sfx.Explosion);
        Juice.Blast(GetParent(), GlobalPosition, Radius, Palette.MissileBlast, growTime: 0.22f, segments: 28);
        QueueFree();
    }
}
