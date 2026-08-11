namespace ShooterLoop;

// An Enemy that also fires EnemyBullets at the player on a timer. Everything else — chase
// movement, HP, health bar, death/scoring — is inherited unchanged from Enemy, so a shooting
// variant of any enemy is just a .tscn pointing at this script with an EnemyBulletScene assigned.
public partial class ShooterEnemy : Enemy
{
    [Export] public PackedScene EnemyBulletScene;
    [Export] public float FireRate = 0.3f;

    public override void _Ready()
    {
        base._Ready();

        var fireTimer = new Timer { WaitTime = 1f / FireRate };
        AddChild(fireTimer);
        fireTimer.Timeout += OnFireTimeout;
        fireTimer.Start();
    }

    private void OnFireTimeout()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (EnemyBulletScene == null || player == null || !IsInstanceValid(player)) return;

        // Aimed at where the player is right now, not where they'll be — deliberately dodgeable.
        var bullet = EnemyBulletScene.Instantiate<EnemyBullet>();
        bullet.GlobalPosition = GlobalPosition;
        bullet.Direction = (player.GlobalPosition - GlobalPosition).Normalized();
        GetParent().AddChild(bullet);
    }
}
