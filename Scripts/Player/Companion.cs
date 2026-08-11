namespace ShooterLoop;

public partial class Companion : Node2D
{
    // Set by Player after instantiating, since this scene isn't pre-placed in the editor.
    public Player OwnerPlayer;
    public PackedScene BulletScene;
    public float StatPercent = 0.3f;

    private Timer _fireCooldown;
    private Node2D _bulletsContainer;

    public override void _Ready()
    {
        _bulletsContainer = GetTree().CurrentScene.GetNode<Node2D>("Bullets");

        _fireCooldown = new Timer();
        AddChild(_fireCooldown);
        _fireCooldown.Timeout += OnFireCooldownTimeout;
        UpdateFireRate();
        _fireCooldown.Start();
    }

    private void UpdateFireRate()
    {
        if (OwnerPlayer == null) return;
        float effectiveRate = Mathf.Max(0.1f, OwnerPlayer.FireRate * StatPercent);
        _fireCooldown.WaitTime = 1f / effectiveRate;
    }

    private void OnFireCooldownTimeout()
    {
        if (OwnerPlayer == null || !IsInstanceValid(OwnerPlayer) || BulletScene == null) return;
        UpdateFireRate();

        var enemies = GetTree().GetNodesInGroup("enemies");
        if (enemies.Count == 0) return;

        Node2D nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var n in enemies)
        {
            if (n is Node2D e2d && IsInstanceValid(e2d))
            {
                float d = GlobalPosition.DistanceSquaredTo(e2d.GlobalPosition);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = e2d;
                }
            }
        }
        if (nearest == null) return;

        var bullet = BulletScene.Instantiate<Bullet>();
        bullet.GlobalPosition = GlobalPosition;
        bullet.Direction = (nearest.GlobalPosition - GlobalPosition).Normalized();
        bullet.Damage = Mathf.Max(1, Mathf.RoundToInt(OwnerPlayer.BulletDamage * StatPercent));
        _bulletsContainer.AddChild(bullet);
    }
}
