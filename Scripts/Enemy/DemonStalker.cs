namespace ShooterLoop;

// The ambushing demon. Two things separate it from a fast chaser:
//
// 1. It spawns on the side the player is *travelling toward* rather than a random angle (see
//    EnemySpawner.PickInterceptPosition), so it arrives in the player's path instead of behind them.
// 2. Once it closes to LungeTriggerRadius it commits to a short burst at several times its own speed,
//    which is what makes it land the hit rather than trailing a retreating player forever.
//
// It still respects the spawn clearance ring — it never materialises inside the player's fire range.
// "Ambush" here means better positioning and a closing burst, not appearing on top of you.
public partial class DemonStalker : Enemy
{
    private const float LungeTriggerRadius = 300f;
    private const float LungeDuration = 0.35f;
    private const float LungeCooldown = 2.2f;
    private const float LungeSpeedMultiplier = 3.2f;

    private float _lungeLeft;
    private float _cooldownLeft;
    private Vector2 _lungeDirection = Vector2.Right;
    private Tween _telegraphTween;

    // Hooks into the base movement seam rather than overriding _PhysicsProcess, so the burn tick and the
    // straggler relocation in Enemy still run — the mistake Boss made and has to duplicate around.
    protected override Vector2 ComputeMoveDirection(double delta)
    {
        float step = (float)delta;

        if (_lungeLeft > 0f)
        {
            _lungeLeft -= step;
            if (_lungeLeft <= 0f) _cooldownLeft = LungeCooldown;

            // A committed burst doesn't politely steer around obstacles; it checked the way was clear
            // before starting.
            return _lungeDirection * LungeSpeedMultiplier;
        }

        if (_cooldownLeft > 0f)
        {
            _cooldownLeft -= step;
            return ComputeAvoidanceDirection(ChaseDirection());
        }

        TryStartLunge();
        return ComputeAvoidanceDirection(ChaseDirection());
    }

    private void TryStartLunge()
    {
        if (!EnsurePlayer()) return;

        float triggerSq = LungeTriggerRadius * LungeTriggerRadius;
        if (GlobalPosition.DistanceSquaredTo(PlayerNode.GlobalPosition) > triggerSq) return;

        Vector2 direction = ChaseDirection();
        float dashDistance = MoveSpeed * LungeSpeedMultiplier * LungeDuration;
        if (IsPathBlocked(direction, dashDistance))
        {
            // Blocked: wait out a cooldown instead of retrying every frame against the same wall.
            _cooldownLeft = LungeCooldown;
            return;
        }

        _lungeDirection = direction;
        _lungeLeft = LungeDuration;
        PlayTelegraph();
    }

    // Brief tint so the burst is readable rather than feeling like a teleport. Modulate is borrowed,
    // not owned — it carries this enemy's identity colour — so the tween has to settle back on
    // VisualBaseColor. Returning to Colors.White instead left the stalker a plain white shape from
    // its first lunge onwards.
    private void PlayTelegraph()
    {
        if (Visual == null) return;

        _telegraphTween?.Kill();
        _telegraphTween = CreateTween();
        _telegraphTween.TweenProperty(Visual, "modulate", Palette.Warning, 0.08f);
        _telegraphTween.TweenProperty(Visual, "modulate", VisualBaseColor, LungeDuration);
    }
}
