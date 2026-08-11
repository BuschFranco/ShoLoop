namespace ShooterLoop;

public partial class Companion : Node2D
{
    // Set by Player after instantiating, since this scene isn't pre-placed in the editor.
    public Player OwnerPlayer;
    public PackedScene BulletScene;
    public float StatPercent = 0.3f;

    // Same exponential catch-up rate the player's own triangle turns at (Player.FacingTurnRate) —
    // the drone should read as part of the same visual language, not its own thing.
    private const float FacingTurnRate = 16f;

    private Timer _fireCooldown;
    private Node2D _bulletsContainer;
    private Polygon2D _visual;

    public override void _Ready()
    {
        _bulletsContainer = GetTree().CurrentScene.GetNode<Node2D>("Bullets");
        _visual = GetNode<Polygon2D>("Visual");

        _fireCooldown = new Timer();
        AddChild(_fireCooldown);
        _fireCooldown.Timeout += OnFireCooldownTimeout;
        UpdateFireRate();
        _fireCooldown.Start();
    }

    public override void _PhysicsProcess(double delta)
    {
        // Was rigidly pointing right regardless of what it's shooting at — same fix as the
        // player's own triangle, just facing the current target instead of the movement direction
        // (the drone doesn't move independently, so "direction" for it means "what it's aiming at").
        // Runs every frame rather than only at fire time so it keeps tracking a moving target
        // smoothly between shots instead of snapping to a new heading on each timeout.
        var nearest = FindNearestEnemy();
        if (nearest == null) return;   // no target: hold the last facing rather than resetting

        Vector2 toTarget = nearest.GlobalPosition - GlobalPosition;
        if (toTarget == Vector2.Zero) return;

        float weight = 1f - Mathf.Exp(-FacingTurnRate * (float)delta);
        _visual.Rotation = Mathf.LerpAngle(_visual.Rotation, toTarget.Angle(), weight);
    }

    private void UpdateFireRate()
    {
        if (OwnerPlayer == null) return;
        float effectiveRate = Mathf.Max(0.1f, OwnerPlayer.FireRate * StatPercent);
        _fireCooldown.WaitTime = 1f / effectiveRate;
    }

    private Node2D FindNearestEnemy()
    {
        Node2D nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var n in GetTree().GetNodesInGroup("enemies"))
        {
            if (n is not Node2D e2d || !IsInstanceValid(e2d)) continue;

            float d = GlobalPosition.DistanceSquaredTo(e2d.GlobalPosition);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = e2d;
            }
        }

        return nearest;
    }

    private void OnFireCooldownTimeout()
    {
        if (OwnerPlayer == null || !IsInstanceValid(OwnerPlayer) || BulletScene == null) return;
        UpdateFireRate();

        var nearest = FindNearestEnemy();
        if (nearest == null) return;

        var bullet = BulletScene.Instantiate<Bullet>();
        bullet.GlobalPosition = GlobalPosition;
        bullet.Direction = (nearest.GlobalPosition - GlobalPosition).Normalized();
        int baseDamage = Mathf.Max(1, Mathf.RoundToInt(OwnerPlayer.BulletDamage * StatPercent));
        bullet.Damage = OwnerPlayer.ApplyCrit(baseDamage, out bool isCrit);
        if (isCrit) bullet.Modulate = Palette.CritBullet;

        // Incendiario reward: the drone's shots burn too, at full DPS/duration — Incendiario is a
        // status the target catches, not a magnitude stat, so there's no "35% of the burn" to scale
        // it by the way Damage above is scaled.
        bullet.BurnDps = OwnerPlayer.CurrentBurnDps;
        bullet.BurnDuration = OwnerPlayer.CurrentBurnDuration;

        _bulletsContainer.AddChild(bullet);
    }
}
