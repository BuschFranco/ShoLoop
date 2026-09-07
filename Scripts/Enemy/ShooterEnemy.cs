namespace ShooterLoop;

// An Enemy that also fires EnemyBullets at the player on a timer. Everything else — chase
// movement, HP, health bar, death/scoring — is inherited unchanged from Enemy, so a shooting
// variant of any enemy is just a .tscn pointing at this script with an EnemyBulletScene assigned.
public partial class ShooterEnemy : Enemy
{
    [Export] public PackedScene EnemyBulletScene;
    [Export] public float FireRate = 0.3f;

    private const float MuzzleFlashOffset = 16f;
    private const float MuzzleFlashSize = 11f;

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
        // Hacked means inoperable, weapons included. The timer keeps running rather than being paused,
        // so recovering doesn't dump a saved-up volley all at once.
        if (IsHacked) return;

        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (EnemyBulletScene == null || player == null || !IsInstanceValid(player)) return;

        // Aimed at where the player is right now, not where they'll be — deliberately dodgeable.
        var bullet = EnemyBulletScene.Instantiate<EnemyBullet>();
        bullet.GlobalPosition = GlobalPosition;
        bullet.Direction = (player.GlobalPosition - GlobalPosition).Normalized();
        GetParent().AddChild(bullet);

        // Doubles as a tell: a shooter that just fired now flags itself for a beat, which is the
        // information a player needs to know which silhouette in a crowd is the one shooting at them.
        Juice.Spark(GetParent(), GlobalPosition + bullet.Direction * MuzzleFlashOffset,
            bullet.Direction, Palette.EnemyBullet.Lightened(0.4f), MuzzleFlashSize);

        // Lower and duller than the player's shot on purpose (see tools/gen_sfx.py) — incoming fire
        // has to be distinguishable from your own without looking away from where you're aiming.
        AudioManager.Instance?.Play(AudioManager.Sfx.EnemyShoot);
    }
}
