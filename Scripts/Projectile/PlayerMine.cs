namespace ShooterLoop;

// Dropped at the player's position on a slow timer (Mina reward), rather than fired at a target
// like Missile. Named PlayerMine rather than Mine to stay distinct from Scripts/Hazards/Mine.cs —
// the hostile, permanently-placed "Campo Minado" event hazard; the two share a theme but nothing
// else, and reusing the name would collide at compile time regardless.
//
// Sits disarmed for a brief delay — so it never reads as detonating on top of an enemy that was
// already standing there when it dropped — then explodes the instant an enemy gets within its
// radius. Ticks down its own lifetime and disappears harmlessly if nothing ever triggers it, same
// safety net as Missile's unreachable-target case.
public partial class PlayerMine : Node2D
{
    public int Damage = 45;
    public float Radius = 55f;

    // Incendiario reward: burns everything caught in the blast, same as every other player weapon.
    public float BurnDps = 0f;
    public float BurnDuration = 0f;

    private const float ArmTime = 0.6f;
    private const float Lifetime = 9f;

    private float _age;
    private bool _armed;
    private bool _detonated;

    public override void _Ready() => ZIndex = -1;   // on the floor, under enemies and the player

    public override void _Process(double delta)
    {
        if (_detonated) return;

        _age += (float)delta;
        if (!_armed && _age >= ArmTime)
            _armed = true;
        QueueRedraw();

        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }

        if (!_armed) return;

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Enemy enemy || !IsInstanceValid(enemy)) continue;
            if (GlobalPosition.DistanceTo(enemy.GlobalPosition) <= Radius)
            {
                Detonate();
                return;
            }
        }
    }

    // Arming ring pulses faintly so it reads as "not live yet"; once armed it holds steady at full
    // opacity — the same at-a-glance "is this dangerous right now" read as CooldownIcon's darkened
    // vs. full-brightness fill.
    public override void _Draw()
    {
        if (_detonated) return;

        Color color = _armed ? Palette.MineBlast : new Color(Palette.MineBlast, Palette.MineBlast.A * 0.5f);
        Juice.DrawPixelCircle(this, Vector2.Zero, 9f, color);
        Juice.DrawPixelRing(this, Vector2.Zero, Radius, 2f, color);
    }

    private void Detonate()
    {
        if (_detonated) return;
        _detonated = true;

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Enemy enemy || !IsInstanceValid(enemy)) continue;
            if (GlobalPosition.DistanceTo(enemy.GlobalPosition) <= Radius)
            {
                if (BurnDps > 0f) enemy.ApplyBurn(BurnDps, BurnDuration);
                enemy.TakeDamage(Damage);
            }
        }

        SpawnBlastVisual();
        QueueFree();
    }

    // Parented to the Bullets container rather than to the mine, because the mine frees itself on
    // this same frame and would take the visual down with it — same approach as Missile.
    private void SpawnBlastVisual()
    {
        AudioManager.Instance?.Play(AudioManager.Sfx.Explosion);
        Juice.Blast(GetParent(), GlobalPosition, Radius, Palette.MineBlast);
    }
}
