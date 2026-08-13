namespace ShooterLoop;

// The boss-round encounter: a big, slow, very tanky melee threat that also plinks away with a
// low-cadence ranged attack (inherited unchanged from ShooterEnemy — the fire timer keeps ticking
// no matter what movement pattern below is doing). On top of that baseline, each boss instance
// rolls ONE movement pattern once at spawn and commits to it for its whole life, so different boss
// rounds actually read as different encounters instead of every boss being the same straight-line
// chaser. See docs/enemies.md for the full breakdown of what each pattern does and why.
public partial class Boss : ShooterEnemy
{
    private enum Pattern { Chase, Charge, Lunge }
    private enum Phase { Idle, Windup, Execute, Cooldown }

    // Own RNG, own timer — matches the "every class rolls its own randomness" convention used by
    // Enemy/EnemySpawner rather than sharing Enemy's private `_rng`.
    private readonly Random _rng = new();
    private Pattern _pattern;
    private Phase _phase = Phase.Idle;
    private Timer _phaseTimer;

    // Idle/Cooldown sub-phases look identical to plain Chase, so they're kept short relative to
    // the special sub-phase itself — otherwise the pattern spends most of its time looking like it
    // isn't doing anything special at all, which read as "the boss has no special moves" during
    // testing even though the state machine was running correctly.
    private const float ChargeIdleDuration = 1.1f;
    private const float ChargeWindupDuration = 0.45f;
    private const float ChargeExecuteDuration = 0.7f;
    private const float ChargeCooldownDuration = 1.5f;
    private const float ChargeSpeedMultiplier = 6f;

    // How far the Execute phase actually carries the boss, used to probe for obstacles before
    // committing. Derived rather than hardcoded so retuning the two constants above can't silently
    // desync it from the real dash length.
    private float ChargeDashDistance => MoveSpeed * ChargeSpeedMultiplier * ChargeExecuteDuration;

    // Lunge: a short, sharp burst that only triggers once the boss has actually closed to melee range —
    // it's the "catch up and land the hit" reaction a slow boss needs, rather than a long-range opener
    // like Charge. Unlike Charge, Idle isn't timer-driven for this pattern; every physics frame checks
    // the live distance to the player and fires the windup the instant it closes inside LungeTriggerRadius.
    private const float LungeTriggerRadius = 190f;
    private const float LungeWindupDuration = 0.22f;
    private const float LungeExecuteDuration = 0.42f;
    private const float LungeCooldownDuration = 1.3f;
    private const float LungeSpeedMultiplier = 5f;

    // Shared by Charge and Lunge — only one pattern is ever active per boss instance, so both can use
    // the same "direction locked at windup-end" field without stepping on each other.
    private Vector2 _dashDirection = Vector2.Right;
    private Tween _telegraphTween;
    private static readonly Color TelegraphTint = Palette.Warning;

    public override void _Ready()
    {
        base._Ready();

        // Chase (the plain baseline behavior, unchanged) is a real possible outcome of the roll —
        // not every boss should feel gimmicked, and it keeps Charge/Lunge from feeling like "the"
        // boss pattern rather than one of a few.
        _pattern = (Pattern)_rng.Next(3);
        if (_pattern == Pattern.Chase) return;

        _phaseTimer = new Timer { OneShot = true };
        AddChild(_phaseTimer);
        _phaseTimer.Timeout += OnPhaseTimeout;

        // Charge's Idle is a fixed delay before its next windup. Lunge's Idle has no timer at all —
        // it just sits in Phase.Idle until ComputeLungeMoveDir's per-frame proximity check fires it.
        if (_pattern == Pattern.Charge)
            EnterPhase(Phase.Idle, ChargeIdleDuration);
    }

    private void EnterPhase(Phase phase, float duration)
    {
        _phase = phase;
        _phaseTimer.WaitTime = duration;
        _phaseTimer.Start();
    }

    private void OnPhaseTimeout()
    {
        if (_pattern == Pattern.Charge) AdvanceChargePhase();
        else AdvanceLungePhase();
    }

    private void AdvanceChargePhase()
    {
        switch (_phase)
        {
            case Phase.Idle:
                EnterPhase(Phase.Windup, ChargeWindupDuration);
                PlayTelegraph(ChargeWindupDuration);
                SpawnPatternLabel("¡EMBESTIDA!");
                break;
            case Phase.Windup:
                StartCharge();
                break;
            case Phase.Execute:
                EnterPhase(Phase.Cooldown, ChargeCooldownDuration);
                break;
            case Phase.Cooldown:
                EnterPhase(Phase.Idle, ChargeIdleDuration);
                break;
        }
    }

    // Locks the dash direction at the moment the windup ends (not re-aimed mid-dash), and bails
    // into Cooldown instead of dashing if the straight line ahead is blocked — a committed lunge
    // shouldn't clip through an obstacle just because the telegraph already played.
    private void StartCharge()
    {
        // Probes the dash's own full travel distance, not the shorter steering probe — see the
        // IsPathBlocked overload in Enemy.cs for why those are deliberately different numbers.
        if (!EnsurePlayer() || IsPathBlocked(ChaseDirection(), ChargeDashDistance))
        {
            EnterPhase(Phase.Cooldown, ChargeCooldownDuration);
            return;
        }

        _dashDirection = ChaseDirection();
        EnterPhase(Phase.Execute, ChargeExecuteDuration);
    }

    private void AdvanceLungePhase()
    {
        switch (_phase)
        {
            case Phase.Windup:
                StartLunge();
                break;
            case Phase.Execute:
                EnterPhase(Phase.Cooldown, LungeCooldownDuration);
                break;
            case Phase.Cooldown:
                // No timer to start here — Idle just sits and waits for the next proximity check to
                // trip it, same as it did before this pattern ever triggered.
                _phase = Phase.Idle;
                break;
        }
    }

    // Checked every physics frame while Idle (see ComputeLungeMoveDir) rather than on a fixed delay:
    // the whole point of this pattern is reacting to the player actually being close, not to a clock.
    private void TryTriggerLunge()
    {
        if (!EnsurePlayer()) return;
        if (GlobalPosition.DistanceSquaredTo(PlayerNode.GlobalPosition) > LungeTriggerRadius * LungeTriggerRadius) return;

        EnterPhase(Phase.Windup, LungeWindupDuration);
        PlayTelegraph(LungeWindupDuration);
        SpawnPatternLabel("¡ACELERÓN!");
    }

    private void StartLunge()
    {
        if (!EnsurePlayer())
        {
            EnterPhase(Phase.Cooldown, LungeCooldownDuration);
            return;
        }

        _dashDirection = ChaseDirection();
        EnterPhase(Phase.Execute, LungeExecuteDuration);
    }

    // Fair warning before the dash: a warm tint ramping in (Modulate, not Color — Color is owned
    // by Enemy's own hit-flash tween, and the two would otherwise fight over the same property)
    // plus a slow scale pulse, so a player watching the boss actually sees the tell.
    private void PlayTelegraph(float duration)
    {
        if (Visual == null) return;

        _telegraphTween?.Kill();
        _telegraphTween = CreateTween();
        _telegraphTween.SetParallel(true);
        _telegraphTween.TweenProperty(Visual, "modulate", TelegraphTint, duration * 0.6f);
        _telegraphTween.TweenProperty(Visual, "scale", Vector2.One * 1.25f, duration * 0.5f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    // Announces a pattern switch in text, on top of the colour telegraph — the fastest way to
    // confirm the state machine is actually running, and a clearer tell than a subtle speed/colour
    // change alone against a busy, bullet-filled screen. Same drift-up-and-fade shape as every
    // other floating label in the game (SpawnDamageNumber, SpawnScorePopup, pickup labels).
    private void SpawnPatternLabel(string text)
    {
        var parent = GetParent();
        if (parent == null) return;

        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", TelegraphTint);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);
        label.AddThemeFontSizeOverride("font_size", 22);
        label.ZIndex = 15;
        parent.AddChild(label);
        label.GlobalPosition = GlobalPosition + new Vector2(-50f, HealthBarOffset - 30f);

        var tween = label.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", label.Position + new Vector2(0f, -24f), 0.9f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 0.6f).SetDelay(0.4f);
        tween.Chain().TweenCallback(Callable.From(() => label.QueueFree()));
    }

    private void ResetTelegraph()
    {
        _telegraphTween?.Kill();
        if (Visual != null) Visual.Modulate = Colors.White;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!EnsurePlayer()) return;

        // Same burn-then-death guard as Enemy._PhysicsProcess (TickBurn/CurrentHp are shared with
        // the base class; this override replaces the movement half only, not the whole method).
        TickBurn((float)delta);
        if (CurrentHp <= 0) return;

        Vector2 moveDir = _pattern switch
        {
            Pattern.Charge => ComputeChargeMoveDir(),
            Pattern.Lunge => ComputeLungeMoveDir(),
            _ => ComputeAvoidanceDirection(ChaseDirection()),
        };

        FinishMovement(moveDir, delta);
    }

    private Vector2 ComputeChargeMoveDir()
    {
        if (_phase == Phase.Execute)
        {
            // A committed lunge doesn't politely detour around obstacles it meets mid-dash — it
            // already checked the way was clear at windup-end.
            ResetTelegraph();
            return _dashDirection * ChargeSpeedMultiplier;
        }

        if (_phase != Phase.Windup) return ComputeAvoidanceDirection(ChaseDirection());

        // Frozen in place during the telegraph — a boss that keeps chasing while flashing a
        // warning undercuts the "get ready to dodge" read.
        return Vector2.Zero;
    }

    private Vector2 ComputeLungeMoveDir()
    {
        // Checked before the phase switch below so a trigger this frame freezes immediately instead
        // of taking one more frame of normal chase before the windup visibly starts.
        if (_phase == Phase.Idle) TryTriggerLunge();

        return _phase switch
        {
            Phase.Execute => LungeBurst(),
            Phase.Windup => Vector2.Zero,
            _ => ComputeAvoidanceDirection(ChaseDirection()),
        };
    }

    private Vector2 LungeBurst()
    {
        ResetTelegraph();
        return _dashDirection * LungeSpeedMultiplier;
    }
}
