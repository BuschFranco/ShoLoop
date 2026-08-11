namespace ShooterLoop;

public partial class Player : CharacterBody2D
{
    [Export] public float MoveSpeed = 290f;
    [Export] public int MaxLives = 3;
    [Export] public float FireRate = 3f;
    [Export] public int BulletDamage = 10;
    [Export] public float FireRange = 190f;

    // Owned here rather than left to Bullet.tscn's own default, since the Bala Veloz reward needs a
    // baseline to grow from and a single place to apply it when spawning each shot.
    [Export] public float BulletSpeed = 500f;
    [Export] public PackedScene BulletScene;
    [Export] public PackedScene MissileScene;
    [Export] public PackedScene OrbitBladeScene;
    [Export] public PackedScene CompanionScene;
    [Export] public Vector2 ArenaHalfExtents = new(1600f, 1000f);
    [Export] public float LevelUpBurstRadius = 220f;

    private const float MaxFireRate = 12f;

    // Hard ceilings for every freely-stacking reward, so repeated purchases can keep helping
    // without any single stat spiraling unboundedly over a long run.
    private const float MaxBulletDamageBonus = 200f;
    private const float MaxFireRangeBonus = 500f;

    // Absolute ceiling on FireRange, separate from the bonus cap above. Without it the only limit
    // was "base + 500", which isn't a stated ceiling so much as a side effect — and a range that
    // outgrows the visible screen stops being a meaningful stat, since you can't see what you're
    // shooting at anyway. This is the number that actually binds.
    private const float MaxFireRange = 600f;
    private const int MaxLivesCap = 10;
    private const int MaxShieldChargesCap = 8;
    public const int MaxExtraFiringLinesCap = 5;
    private const float SideShotSpacing = 14f;

    private const int MaxPierceCap = 5;
    private const float MaxCritChance = 60f;        // percent
    private const float MaxBulletSpeedBonus = 700f;
    private const float MaxBulletKnockbackBonus = 400f;
    private const float MaxCoinBonusPercent = 150f;
    private const int MaxReviveCharges = 2;

    public int CurrentLives;
    public int MaxShieldCharges = 0;
    public int CurrentShieldCharges = 0;
    public int OrbitCount = 0;
    public float CompanionStatPercent = 0f;
    public int ExtraFiringLines = 0;
    public int LaserLevel = 0;
    public int MissileLevel = 0;
    public int BurnLevel = 0;
    public int BulletPierce = 0;
    public float CritChance = 0f;            // percent, 0-60
    public float BulletKnockback = 0f;
    public float CoinBonusPercent = 0f;      // percent, 0-150
    public int ReviveCharges = 0;
    public float ShieldRegenPerMinute = 0f;
    public bool HasExtraProjectile => _hasExtraProjectile;
    public bool HasOrbitShield => OrbitCount > 0;

    // Coin payout multiplier applied in GameManager.RegisterKill (Botín reward).
    public float CoinMultiplier => 1f + CoinBonusPercent / 100f;

    public UltimateKind? EquippedUltimate = null;
    public float UltimateProgress = 0f;
    public const float UltimateChargeTarget = 250f;

    public event Action<int, int> LivesChanged;
    public event Action<int, int> ShieldChanged;
    public event Action<float, float> UltimateChargeChanged;

    // Level → (fire interval, damage, max targets per zap). Index 0 = tier 1 (Common) ... index 3
    // = tier 4 (Legendary). Only tier 1 caps how many enemies a single zap can hit — higher tiers
    // are strong enough already without also needing a target-count leash.
    private static readonly (float Interval, float Damage, int MaxTargets)[] LaserTiers =
    {
        (4.0f, 12f, 5),
        (3.2f, 20f, int.MaxValue),
        (2.5f, 31f, int.MaxValue),
        (1.8f, 50f, int.MaxValue),
    };

    // Level → (fire interval, blast damage, blast radius). Deliberately slow: the missile trades
    // uptime for burst area damage, so tier 1 fires only every 4.5s.
    private static readonly (float Interval, int Damage, float Radius)[] MissileTiers =
    {
        (4.5f, 30, 70f),
        (3.8f, 50, 85f),
        (3.2f, 75, 100f),
        (2.6f, 110, 120f),
    };

    // Level → (damage per second, duration). Applied by the player's own bullets on hit.
    private static readonly (float Dps, float Duration)[] BurnTiers =
    {
        (6f, 3.0f),
        (10f, 3.0f),
        (16f, 3.5f),
        (24f, 4.0f),
    };

    private VirtualJoystick _joystick;
    private Timer _fireCooldown;
    private Node2D _bulletsContainer;
    private readonly List<OrbitShield> _orbitBlades = new();
    private Companion _companion;
    private Polygon2D _shieldAura;
    private bool _hasExtraProjectile = false;
    private float _invulnTimer = 0f;
    private const float InvulnDuration = 2f;
    private float _blinkTimer = 0f;
    private const float BlinkInterval = 0.1f;
    private Polygon2D _visual;
    private Vector2 _knockbackVelocity = Vector2.Zero;
    private const float KnockbackForce = 220f;
    private const float KnockbackDecay = 900f;

    // Movement ramps in and out instead of snapping between full speed and a dead stop, so the
    // player carries a little inertia. Both rates are px/s²: at MoveSpeed 265 that's ~0.15s to
    // reach top speed and ~0.11s to stop — deliberately short. The stop is snappier than the start
    // so the ship still feels responsive rather than floaty/slippery.
    private Vector2 _moveVelocity = Vector2.Zero;
    private const float MoveAcceleration = 1800f;
    private const float MoveDeceleration = 2400f;

    // Per-type, per-tier accumulated bonus. Same tier stacks additively (2 Rare picks of the
    // same reward add up); different tiers do NOT sum on top of each other — the final bonus is
    // whichever single tier-bucket is currently highest. This caps how far a stat can snowball
    // from freely mixing every tier ever picked, while still rewarding deliberately re-rolling
    // the same tier.
    private readonly Dictionary<UpgradeType, Dictionary<RewardTier, float>> _tierStackTotals = new();

    private float _baseFireRate;
    private float _baseBulletDamage;
    private float _baseFireRange;
    private float _baseBulletSpeed;
    private Timer _shieldRegenTimer;
    private Line2D _fireRangeRing;
    private Timer _laserTimer;
    private Timer _missileTimer;
    private Timer _frenzyTimer;

    public override void _Ready()
    {
        AddToGroup("player");
        CurrentLives = MaxLives;

        _baseFireRate = FireRate;
        _baseBulletDamage = BulletDamage;
        _baseFireRange = FireRange;
        _baseBulletSpeed = BulletSpeed;

        _fireCooldown = GetNode<Timer>("FireCooldown");
        _fireCooldown.WaitTime = 1f / FireRate;
        _fireCooldown.Timeout += OnFireCooldownTimeout;
        _fireCooldown.Start();

        _bulletsContainer = GetTree().CurrentScene.GetNode<Node2D>("Bullets");

        var hurtbox = GetNode<Area2D>("Hurtbox");
        hurtbox.BodyEntered += OnHurtboxBodyEntered;

        _joystick = GetTree().GetFirstNodeInGroup("virtual_joystick") as VirtualJoystick;

        _visual = GetNode<Polygon2D>("Visual");

        _shieldAura = GetNode<Polygon2D>("ShieldAura");
        var pulse = CreateTween();
        pulse.SetLoops();
        pulse.TweenProperty(_shieldAura, "scale", Vector2.One * 1.1f, 0.6f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        pulse.TweenProperty(_shieldAura, "scale", Vector2.One, 0.6f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        SpawnFireRangeIndicator();
    }

    // A faint ring showing FireRange — the player only auto-fires at enemies inside it, so it
    // needs to be visible rather than an invisible gameplay number. Rebuilt whenever the Fire
    // Range reward raises FireRange, since a Line2D's points are baked at the radius they were
    // drawn at, not tied live to the FireRange field.
    private void SpawnFireRangeIndicator()
    {
        _fireRangeRing = new Line2D();
        _fireRangeRing.Width = 2f;
        _fireRangeRing.DefaultColor = new Color(0.3f, 1f, 1f, 0.18f);
        _fireRangeRing.ZIndex = -1;
        AddChild(_fireRangeRing);
        RebuildFireRangeIndicator();
    }

    private void RebuildFireRangeIndicator()
    {
        const int segments = 48;
        _fireRangeRing.ClearPoints();
        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.Tau;
            _fireRangeRing.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * FireRange);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_joystick == null)
            _joystick = GetTree().GetFirstNodeInGroup("virtual_joystick") as VirtualJoystick;

        Vector2 dir = _joystick?.GetVector() ?? Vector2.Zero;
        if (dir == Vector2.Zero)
            dir = GetKeyboardDirection();

        // Ease toward the requested velocity rather than assigning it outright — this is what gives
        // the movement its arranque/frenada. Steering mid-move interpolates through the turn too,
        // which reads as the ship banking rather than pivoting on the spot.
        Vector2 targetVelocity = dir * MoveSpeed;
        float rate = dir == Vector2.Zero ? MoveDeceleration : MoveAcceleration;
        _moveVelocity = _moveVelocity.MoveToward(targetVelocity, rate * (float)delta);

        _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, KnockbackDecay * (float)delta);

        Velocity = _moveVelocity + _knockbackVelocity;
        MoveAndSlide();

        GlobalPosition = new Vector2(
            Mathf.Clamp(GlobalPosition.X, -ArenaHalfExtents.X, ArenaHalfExtents.X),
            Mathf.Clamp(GlobalPosition.Y, -ArenaHalfExtents.Y, ArenaHalfExtents.Y)
        );

        if (_invulnTimer > 0f)
        {
            _invulnTimer -= (float)delta;
            _blinkTimer += (float)delta;
            _visual.Visible = ((int)(_blinkTimer / BlinkInterval)) % 2 == 0;
            if (_invulnTimer <= 0f)
                _visual.Visible = true;
        }
    }

    // WASD as a desktop-friendly alternative to the virtual joystick — only consulted when the
    // joystick itself isn't providing input, so touch input always takes priority and the two
    // never fight over movement.
    private static Vector2 GetKeyboardDirection()
    {
        Vector2 dir = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W)) dir.Y -= 1f;
        if (Input.IsKeyPressed(Key.S)) dir.Y += 1f;
        if (Input.IsKeyPressed(Key.A)) dir.X -= 1f;
        if (Input.IsKeyPressed(Key.D)) dir.X += 1f;
        return dir.Normalized();
    }

    // R triggers the Ultimate — via _UnhandledInput (not polled every physics frame) so it fires
    // exactly once per key-down and is automatically ignored while the tree is paused (shop,
    // pause menu, game over), same as every other gameplay input.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.R)
            TriggerUltimate();
    }

    private void OnHurtboxBodyEntered(Node2D body)
    {
        if (body is Enemy enemy)
            TakeHit(enemy.GlobalPosition);
    }

    public void TakeHit(Vector2? sourcePosition = null)
    {
        if (_invulnTimer > 0f) return;

        if (CurrentShieldCharges > 0)
        {
            CurrentShieldCharges--;
            RaiseShieldChanged();
        }
        else
        {
            LoseLife();
        }

        _invulnTimer = InvulnDuration;
        _blinkTimer = 0f;

        if (sourcePosition.HasValue)
        {
            Vector2 away = (GlobalPosition - sourcePosition.Value);
            if (away != Vector2.Zero)
                _knockbackVelocity = away.Normalized() * KnockbackForce;
        }
    }

    public void LoseLife()
    {
        CurrentLives--;

        // Segunda Oportunidad (shop-only): spend a charge to come back at full lives instead of
        // ending the run. Checked before NotifyPlayerDied so the game-over screen never appears.
        if (CurrentLives <= 0 && ReviveCharges > 0)
        {
            ReviveCharges--;
            CurrentLives = MaxLives;
            CurrentShieldCharges = MaxShieldCharges;
            RaiseShieldChanged();
            TriggerLevelUpBurst();   // reuse the nova as a "you got a second wind" clear + visual
        }

        LivesChanged?.Invoke(CurrentLives, MaxLives);

        if (CurrentLives <= 0)
        {
            GameManager.Instance?.NotifyPlayerDied();
        }
    }

    private void RaiseShieldChanged()
    {
        ShieldChanged?.Invoke(CurrentShieldCharges, MaxShieldCharges);
        _shieldAura.Visible = CurrentShieldCharges > 0;
    }

    public void HealFullLives()
    {
        CurrentLives = MaxLives;
        LivesChanged?.Invoke(CurrentLives, MaxLives);
    }

    // Shield charges are consumable and never regenerate during a round, but a new round tops them
    // back up to full — the same "every round starts fresh" rule lives already follow. No-ops for a
    // player who's never bought a Barrier, so it can be called unconditionally on round start.
    public void RefillShield()
    {
        if (MaxShieldCharges <= 0) return;
        CurrentShieldCharges = MaxShieldCharges;
        RaiseShieldChanged();
    }

    public void TriggerLevelUpBurst()
    {
        // No nova on round 1 — leveling up that early (before the player has any real threat
        // pressure yet) shouldn't hand out a free screen-clear; the nova starts from round 2 on.
        if (GameManager.Instance != null && GameManager.Instance.RoundNumber == 1)
            return;

        var enemies = GetTree().GetNodesInGroup("enemies");
        foreach (var n in enemies)
        {
            // Bosses are excluded from the instant-kill nova — otherwise any level-up that fires
            // while a boss is in range deletes it in one hit regardless of the fight's actual
            // difficulty, which defeats the point of a boss encounter.
            if (n is Enemy enemy && IsInstanceValid(enemy) && enemy.Category != EnemyCategory.Boss)
            {
                float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);
                if (dist <= LevelUpBurstRadius)
                    enemy.TakeDamage(99999);
            }
        }

        SpawnBurstVisual();
    }

    private void SpawnBurstVisual()
    {
        var visual = new Polygon2D();
        var points = new Vector2[24];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.Tau;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * LevelUpBurstRadius;
        }
        visual.Polygon = points;
        visual.Color = new Color(1f, 0.9f, 0.3f, 0.5f);
        visual.Scale = Vector2.Zero;
        AddChild(visual);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(visual, "scale", Vector2.One, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(visual, "modulate:a", 0f, 0.35f);
        tween.Chain().TweenCallback(Callable.From(() => visual.QueueFree()));
    }

    // From the Epic tier up (LaserLevel 3+), the Laser couples to the player's current attack
    // stats instead of using its own fixed numbers — its damage scales with BulletDamage and its
    // fire interval scales with FireRate (both relative to their base values), so buying more
    // damage/fire-rate upgrades later also makes the Laser stronger/faster, and it'd scale back
    // down too if those stats were ever reduced.
    private const int LaserStatCoupledMinLevel = 3;

    private void EnsureLaserTimer()
    {
        if (_laserTimer == null)
        {
            _laserTimer = new Timer();
            AddChild(_laserTimer);
            _laserTimer.Timeout += OnLaserTimeout;
        }
        _laserTimer.WaitTime = GetLaserInterval();
        _laserTimer.Start();
    }

    private float GetLaserInterval()
    {
        float baseInterval = LaserTiers[LaserLevel - 1].Interval;
        if (LaserLevel < LaserStatCoupledMinLevel) return baseInterval;
        return Mathf.Max(0.3f, baseInterval * (_baseFireRate / FireRate));
    }

    private float GetLaserDamage()
    {
        float baseDamage = LaserTiers[LaserLevel - 1].Damage;
        if (LaserLevel < LaserStatCoupledMinLevel) return baseDamage;
        return baseDamage * (BulletDamage / _baseBulletDamage);
    }

    private void OnLaserTimeout()
    {
        float damage = GetLaserDamage();
        int maxTargets = LaserTiers[LaserLevel - 1].MaxTargets;
        var enemies = GetTree().GetNodesInGroup("enemies");

        var inRange = new List<Enemy>();
        foreach (var n in enemies)
        {
            if (n is Enemy enemy && IsInstanceValid(enemy) && GlobalPosition.DistanceTo(enemy.GlobalPosition) <= FireRange)
                inRange.Add(enemy);
        }

        // Capped tiers hit the closest N instead of an arbitrary N from group order.
        if (inRange.Count > maxTargets)
        {
            inRange.Sort((a, b) => GlobalPosition.DistanceSquaredTo(a.GlobalPosition)
                .CompareTo(GlobalPosition.DistanceSquaredTo(b.GlobalPosition)));
            inRange.RemoveRange(maxTargets, inRange.Count - maxTargets);
        }

        foreach (var enemy in inRange)
        {
            enemy.TakeDamage(Mathf.RoundToInt(damage));
            SpawnLaserBeam(enemy.GlobalPosition);
        }

        // Stat-coupled tiers re-read the player's current FireRate every tick so the interval
        // keeps tracking it live (a fixed WaitTime set once at pickup time would go stale the
        // moment FireRate changed again).
        if (LaserLevel >= LaserStatCoupledMinLevel)
            _laserTimer.WaitTime = GetLaserInterval();
    }

    private void EnsureMissileTimer()
    {
        if (_missileTimer == null)
        {
            _missileTimer = new Timer();
            AddChild(_missileTimer);
            _missileTimer.Timeout += OnMissileTimeout;
        }
        _missileTimer.WaitTime = MissileTiers[MissileLevel - 1].Interval;
        _missileTimer.Start();
    }

    private void OnMissileTimeout()
    {
        if (MissileScene == null || _bulletsContainer == null) return;

        var target = FindNearestEnemyInRange();
        if (target == null) return;   // nothing to shoot at; the timer just ticks again

        var (_, damage, radius) = MissileTiers[MissileLevel - 1];

        var missile = MissileScene.Instantiate<Missile>();
        missile.GlobalPosition = GlobalPosition;

        // Snapshot of where the target is *now*. Missile.cs never re-reads it, which is what makes
        // the shot dodgeable rather than homing.
        missile.TargetPosition = target.GlobalPosition;
        missile.Damage = damage;
        missile.ExplosionRadius = radius;

        _bulletsContainer.AddChild(missile);
    }

    // Shared by the gun and the missile so every auto-weapon respects FireRange identically.
    private Node2D FindNearestEnemyInRange()
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

        return nearestDist <= FireRange * FireRange ? nearest : null;
    }

    private void SpawnLaserBeam(Vector2 targetGlobalPos)
    {
        var beam = new Line2D();
        beam.AddPoint(Vector2.Zero);
        beam.AddPoint(ToLocal(targetGlobalPos));
        beam.Width = 3f;
        beam.DefaultColor = new Color(1f, 0.2f, 0.8f, 0.8f);
        AddChild(beam);

        var tween = beam.CreateTween();
        tween.TweenProperty(beam, "modulate:a", 0f, 0.15f);
        tween.TweenCallback(Callable.From(() => beam.QueueFree()));
    }

    // Current fire rate as a multiple of the round-1 baseline — 1.0 at run start, rising to
    // MaxFireRate/base (4x) when fully upgraded. Orbit blades read this so their spin couples to
    // attack speed; exposing the ratio rather than _baseFireRate keeps that snapshot private and
    // the conversion in one place.
    public float FireRateRatio => _baseFireRate > 0f ? FireRate / _baseFireRate : 1f;

    // Rough DPS-equivalent of the player's whole offensive kit, expressed as a ratio against the
    // round-1 baseline (an untouched player scores exactly 1.0). Feeds DifficultyBalancer, which
    // compares it to what the current round expected and nudges enemy HP/damage if the player has
    // run far ahead of the curve. Lives here rather than in the balancer because it needs the
    // private _base* snapshots and the LaserTiers lookup.
    //
    // It's an approximation on purpose — the point is detecting order-of-magnitude runaway builds,
    // not simulating combat exactly.
    private const float OrbitHitsPerSecondEstimate = 2f;

    public float GetOffensivePower()
    {
        float baselineDps = _baseBulletDamage * _baseFireRate;
        if (baselineDps <= 0f) return 1f;

        int shotsPerVolley = 1 + (_hasExtraProjectile ? 2 : 0) + ExtraFiringLines;

        // Crit and pierce are genuine damage multipliers, so they belong here — without them a
        // crit/pierce-heavy build would read as far weaker than it plays and dodge the adaptive
        // difficulty correction entirely.
        float critMult = 1f + (CritChance / 100f) * (CritMultiplier - 1f);
        float pierceMult = 1f + BulletPierce * 0.5f;   // extra enemies hit per shot, discounted
        float gunDps = BulletDamage * FireRate * shotsPerVolley * critMult * pierceMult;

        float laserDps = LaserLevel > 0 ? GetLaserDamage() / GetLaserInterval() : 0f;

        // Both AoE, so their nominal single-target DPS understates them — a modest multiplier keeps
        // a missile/burn build from reading as weaker than it plays and slipping past the balancer.
        float missileDps = 0f;
        if (MissileLevel > 0)
        {
            var (interval, damage, _) = MissileTiers[MissileLevel - 1];
            missileDps = damage / interval * 1.5f;
        }

        // Sustained fire keeps burn permanently refreshed, so its DPS is effectively always on.
        float burnDps = BurnLevel > 0 ? BurnTiers[BurnLevel - 1].Dps : 0f;

        float orbitDps = OrbitCount * OrbitBladeDamage * OrbitHitsPerSecondEstimate;

        // The companion re-derives its own output from the player's live stats, so it's a flat
        // percentage bonus on top of everything rather than its own independent term.
        float total = (gunDps + laserDps + missileDps + burnDps + orbitDps) * (1f + CompanionStatPercent);
        return total / baselineDps;
    }

    // Mirrors OrbitShield.Damage's default. Kept in sync manually rather than read off a live
    // blade so the power estimate still works before any blade has been instantiated.
    private const int OrbitBladeDamage = 7;

    // Called by GameManager.AddScore whenever Score goes up — Score is synced 1:1 with XP, so
    // this is what fills the Ultimate meter. Only accumulates once an Ultimate is actually
    // equipped; a bare Score gain with nothing equipped does nothing here.
    public void AddUltimateProgress(int amount)
    {
        if (EquippedUltimate == null) return;
        UltimateProgress = Mathf.Min(UltimateProgress + amount, UltimateChargeTarget);
        UltimateChargeChanged?.Invoke(UltimateProgress, UltimateChargeTarget);
    }

    public void TriggerUltimate()
    {
        if (EquippedUltimate == null || UltimateProgress < UltimateChargeTarget) return;

        switch (EquippedUltimate.Value)
        {
            case UltimateKind.Nova:
                TriggerUltimateNova();
                break;
            case UltimateKind.TimeSlow:
                GameManager.Instance?.ApplyTemporarySlow(0.3f, 6f);
                break;
            case UltimateKind.Frenzy:
                TriggerUltimateFrenzy();
                break;
        }

        UltimateProgress = 0f;
        UltimateChargeChanged?.Invoke(UltimateProgress, UltimateChargeTarget);
    }

    private const float UltimateNovaRadius = 500f;
    private const int UltimateNovaDamage = 500;

    private void TriggerUltimateNova()
    {
        var enemies = GetTree().GetNodesInGroup("enemies");
        foreach (var n in enemies)
        {
            if (n is Enemy enemy && IsInstanceValid(enemy))
            {
                float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);
                if (dist <= UltimateNovaRadius)
                    enemy.TakeDamage(UltimateNovaDamage);
            }
        }

        var visual = new Polygon2D();
        var points = new Vector2[32];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.Tau;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * UltimateNovaRadius;
        }
        visual.Polygon = points;
        visual.Color = new Color(1f, 0.2f, 0.8f, 0.4f);
        visual.Scale = Vector2.Zero;
        AddChild(visual);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(visual, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(visual, "modulate:a", 0f, 0.5f);
        tween.Chain().TweenCallback(Callable.From(() => visual.QueueFree()));
    }


    private void TriggerUltimateFrenzy()
    {
        FireRate *= 2f;
        BulletDamage *= 2;
        _fireCooldown.WaitTime = 1f / FireRate;

        if (_frenzyTimer == null)
        {
            _frenzyTimer = new Timer { OneShot = true };
            AddChild(_frenzyTimer);
            _frenzyTimer.Timeout += () =>
            {
                RecomputeFireRateAndDamage();
                _fireCooldown.WaitTime = 1f / FireRate;
            };
        }
        _frenzyTimer.WaitTime = 6f;
        _frenzyTimer.Start();
    }

    // Re-derives the permanent FireRate/BulletDamage from base + tier-bucket totals — the same
    // formula ApplyUpgrade uses, but without recording a new pick. Called when the Sobrecarga
    // ultimate's temporary doubling expires, so it reverts to whatever the real permanent stat is
    // at that moment (correct even if a permanent Fire Rate/Damage upgrade was picked mid-buff),
    // not a stale pre-buff snapshot.
    private void RecomputeFireRateAndDamage()
    {
        FireRate = Mathf.Min(_baseFireRate + GetCurrentTierBonus(UpgradeType.FireRate), MaxFireRate);
        BulletDamage = Mathf.RoundToInt(_baseBulletDamage + Mathf.Min(GetCurrentTierBonus(UpgradeType.BulletDamage), MaxBulletDamageBonus));
    }

    // Current winning bucket total for a type, with no new pick recorded — 0 if never picked.
    private float GetCurrentTierBonus(UpgradeType type)
    {
        if (!_tierStackTotals.TryGetValue(type, out var tierTotals)) return 0f;
        float best = 0f;
        foreach (var total in tierTotals.Values)
            if (total > best) best = total;
        return best;
    }

    private void OnFireCooldownTimeout()
    {
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
        if (nearestDist > FireRange * FireRange) return;

        Vector2 baseDir = (nearest.GlobalPosition - GlobalPosition).Normalized();
        FireInDirection(baseDir);

        if (_hasExtraProjectile)
        {
            FireInDirection(baseDir.Rotated(Mathf.DegToRad(25)));
            FireInDirection(baseDir.Rotated(Mathf.DegToRad(-25)));
        }

        if (ExtraFiringLines > 0)
        {
            Vector2 perpendicular = baseDir.Rotated(Mathf.Pi / 2f);
            for (int i = 1; i <= ExtraFiringLines; i++)
            {
                float side = (i % 2 == 1) ? 1f : -1f;
                int rank = (i + 1) / 2;
                Vector2 offset = perpendicular * SideShotSpacing * rank * side;
                FireInDirection(baseDir, offset);
            }
        }
    }

    private readonly Random _critRng = new();
    private const float CritMultiplier = 2f;

    private void FireInDirection(Vector2 dir, Vector2 positionOffset = default)
    {
        if (BulletScene == null || _bulletsContainer == null) return;

        var bullet = BulletScene.Instantiate<Bullet>();
        bullet.GlobalPosition = GlobalPosition + positionOffset;
        bullet.Direction = dir;
        bullet.Speed = BulletSpeed;
        bullet.Pierce = BulletPierce;
        bullet.Knockback = BulletKnockback;

        if (BurnLevel > 0)
        {
            var (burnDps, burnDuration) = BurnTiers[BurnLevel - 1];
            bullet.BurnDps = burnDps;
            bullet.BurnDuration = burnDuration;
        }

        // Rolled per bullet, not per volley, so a Twin/Side Shot spread can crit on some lines and
        // not others — more visible feedback than an all-or-nothing volley.
        bool isCrit = CritChance > 0f && _critRng.NextDouble() * 100.0 < CritChance;
        bullet.Damage = isCrit ? Mathf.RoundToInt(BulletDamage * CritMultiplier) : BulletDamage;
        if (isCrit) bullet.Modulate = new Color(1f, 0.85f, 0.2f);

        _bulletsContainer.AddChild(bullet);
    }

    public void ApplyUpgrade(UpgradeData upgrade)
    {
        switch (upgrade.Type)
        {
            case UpgradeType.FireRange:
                FireRange = Mathf.Min(_baseFireRange + Mathf.Min(ApplyTieredStack(upgrade), MaxFireRangeBonus), MaxFireRange);
                RebuildFireRangeIndicator();
                break;
            case UpgradeType.ExtraProjectile:
                _hasExtraProjectile = true;
                break;
            case UpgradeType.OrbitShield:
                OrbitCount = Mathf.Max(OrbitCount, (int)upgrade.Value);
                RefreshOrbitBlades();
                break;
            case UpgradeType.FireRate:
                FireRate = Mathf.Min(_baseFireRate + ApplyTieredStack(upgrade), MaxFireRate);
                _fireCooldown.WaitTime = 1f / FireRate;
                break;
            case UpgradeType.BulletDamage:
                BulletDamage = Mathf.RoundToInt(_baseBulletDamage + Mathf.Min(ApplyTieredStack(upgrade), MaxBulletDamageBonus));
                break;
            // Heart is a single Legendary tier now, so the old "max tracks the best tier ever
            // picked" rule is meaningless (every pick is the same tier) — it's straightforwardly
            // additive instead, +1 max per purchase up to the cap, and always a full heal.
            case UpgradeType.Heart:
                MaxLives = Mathf.Min(MaxLives + (int)upgrade.Value, MaxLivesCap);
                CurrentLives = MaxLives;
                LivesChanged?.Invoke(CurrentLives, MaxLives);
                break;
            case UpgradeType.HitShield:
                MaxShieldCharges = Mathf.Min(Mathf.Max(MaxShieldCharges, (int)upgrade.Value), MaxShieldChargesCap);
                CurrentShieldCharges = Mathf.Min(CurrentShieldCharges + (int)upgrade.Value, MaxShieldCharges);
                RaiseShieldChanged();
                break;
            case UpgradeType.Companion:
                CompanionStatPercent = Mathf.Max(CompanionStatPercent, upgrade.Value / 100f);
                EnsureCompanion();
                break;
            case UpgradeType.SideShot:
                ExtraFiringLines = Mathf.Min(ExtraFiringLines + (int)upgrade.Value, MaxExtraFiringLinesCap);
                break;
            case UpgradeType.Laser:
                LaserLevel = Mathf.Max(LaserLevel, (int)upgrade.Value);
                EnsureLaserTimer();
                break;
            case UpgradeType.Missile:
                MissileLevel = Mathf.Max(MissileLevel, (int)upgrade.Value);
                EnsureMissileTimer();
                break;
            case UpgradeType.Burn:
                BurnLevel = Mathf.Max(BurnLevel, (int)upgrade.Value);
                break;
            case UpgradeType.Ultimate:
                EquippedUltimate = upgrade.Ultimate;
                UltimateProgress = 0f;
                UltimateChargeChanged?.Invoke(UltimateProgress, UltimateChargeTarget);
                break;

            // Counts and percentages accumulate flatly up to a cap (the SideShot model) — tier
            // bucketing would be strange for "how many enemies does a bullet pass through".
            case UpgradeType.Pierce:
                BulletPierce = Mathf.Min(BulletPierce + (int)upgrade.Value, MaxPierceCap);
                break;
            case UpgradeType.CritChance:
                CritChance = Mathf.Min(CritChance + upgrade.Value, MaxCritChance);
                break;
            case UpgradeType.CoinBonus:
                CoinBonusPercent = Mathf.Min(CoinBonusPercent + upgrade.Value, MaxCoinBonusPercent);
                break;

            // Magnitude stats use the same tier-bucketing as FireRange/FireRate/BulletDamage.
            case UpgradeType.BulletSpeed:
                BulletSpeed = _baseBulletSpeed + Mathf.Min(ApplyTieredStack(upgrade), MaxBulletSpeedBonus);
                break;
            case UpgradeType.BulletKnockback:
                BulletKnockback = Mathf.Min(ApplyTieredStack(upgrade), MaxBulletKnockbackBonus);
                break;

            case UpgradeType.Revive:
                ReviveCharges = Mathf.Min(ReviveCharges + (int)upgrade.Value, MaxReviveCharges);
                break;
            case UpgradeType.ShieldRegen:
                ShieldRegenPerMinute = Mathf.Max(ShieldRegenPerMinute, upgrade.Value);
                EnsureShieldRegenTimer();
                break;
        }
    }

    // Shield regen (shop-only Regeneración). Stored as charges-per-minute so "higher is better"
    // stays uniform with every other take-the-best reward; the timer interval is derived from it.
    private void EnsureShieldRegenTimer()
    {
        if (ShieldRegenPerMinute <= 0f) return;

        if (_shieldRegenTimer == null)
        {
            _shieldRegenTimer = new Timer();
            AddChild(_shieldRegenTimer);
            _shieldRegenTimer.Timeout += OnShieldRegenTimeout;
        }
        _shieldRegenTimer.WaitTime = 60f / ShieldRegenPerMinute;
        _shieldRegenTimer.Start();
    }

    private void OnShieldRegenTimeout()
    {
        if (MaxShieldCharges <= 0 || CurrentShieldCharges >= MaxShieldCharges) return;

        CurrentShieldCharges++;
        RaiseShieldChanged();
    }

    // True when actually picking this exact offer right now would improve the player's current
    // state at all. Value-based (simulates the real result), not just a tier-number comparison —
    // e.g. a Common Fire Range boost after two stacked Rares is correctly "no" even though Rare > Common
    // nominally, because the Rare bucket already wins. Drives both the glow highlight and the
    // "se suma" / "no suma" wording below.
    public bool IsUpgradeOverCurrent(UpgradeData upgrade)
    {
        switch (upgrade.Type)
        {
            case UpgradeType.FireRange:
                return Mathf.Min(_baseFireRange + Mathf.Min(PreviewTieredBonus(upgrade), MaxFireRangeBonus), MaxFireRange) > FireRange;
            case UpgradeType.BulletDamage:
                return Mathf.Min(PreviewTieredBonus(upgrade), MaxBulletDamageBonus) > (BulletDamage - _baseBulletDamage);
            case UpgradeType.FireRate:
                return Mathf.Min(PreviewTieredBonus(upgrade), MaxFireRate - _baseFireRate) > (FireRate - _baseFireRate);
            // Helps if it can still raise the ceiling, or if there's any damage for its full heal
            // to undo — only a player at the cap *and* on full lives gains nothing.
            case UpgradeType.Heart:
                return MaxLives < MaxLivesCap || CurrentLives < MaxLives;
            case UpgradeType.HitShield:
            {
                int newMax = Mathf.Min(Mathf.Max(MaxShieldCharges, (int)upgrade.Value), MaxShieldChargesCap);
                int newCurrent = Mathf.Min(CurrentShieldCharges + (int)upgrade.Value, newMax);
                return newMax > MaxShieldCharges || newCurrent > CurrentShieldCharges;
            }
            case UpgradeType.SideShot:
                return ExtraFiringLines < MaxExtraFiringLinesCap;
            case UpgradeType.OrbitShield:
                return (int)upgrade.Value > OrbitCount;
            case UpgradeType.Companion:
                return upgrade.Value / 100f > CompanionStatPercent;
            case UpgradeType.Laser:
                return (int)upgrade.Value > LaserLevel;
            case UpgradeType.Missile:
                return (int)upgrade.Value > MissileLevel;
            case UpgradeType.Burn:
                return (int)upgrade.Value > BurnLevel;
            case UpgradeType.Ultimate:
                return EquippedUltimate != upgrade.Ultimate;
            case UpgradeType.Pierce:
                return BulletPierce < MaxPierceCap;
            case UpgradeType.CritChance:
                return CritChance < MaxCritChance;
            case UpgradeType.CoinBonus:
                return CoinBonusPercent < MaxCoinBonusPercent;
            case UpgradeType.BulletSpeed:
                return Mathf.Min(PreviewTieredBonus(upgrade), MaxBulletSpeedBonus) > (BulletSpeed - _baseBulletSpeed);
            case UpgradeType.BulletKnockback:
                return Mathf.Min(PreviewTieredBonus(upgrade), MaxBulletKnockbackBonus) > BulletKnockback;
            case UpgradeType.Revive:
                return ReviveCharges < MaxReviveCharges;
            case UpgradeType.ShieldRegen:
                return upgrade.Value > ShieldRegenPerMinute;
            default:
                return false;
        }
    }

    // True when picking this specific offer right now would do absolutely nothing — used to
    // disable/grey out the option (in both the shop and the free level-up picker) so the player
    // never spends a purchase or a pick on something with zero effect. The offer stays visible;
    // only its button is disabled.
    //
    // For the tiered-stacking types (FireRange/FireRate/BulletDamage) merely being a *losing tier*
    // is NOT useless — picking one still feeds that tier's bucket, which is a legitimate long-game
    // "stack the same tier" strategy, and those cases are flagged via GetStackInfoText's wording
    // instead. But once the stat has hit its hard cap, feeding the bucket provably changes nothing,
    // so at-cap is genuinely useless and does get blocked.
    public bool IsRewardUseless(UpgradeData upgrade)
    {
        switch (upgrade.Type)
        {
            case UpgradeType.ExtraProjectile:
                return HasExtraProjectile;
            case UpgradeType.OrbitShield:
            case UpgradeType.Companion:
            case UpgradeType.SideShot:
            case UpgradeType.Heart:
            case UpgradeType.HitShield:
            case UpgradeType.Laser:
            case UpgradeType.Missile:
            case UpgradeType.Burn:
            case UpgradeType.Ultimate:
            case UpgradeType.Pierce:
            case UpgradeType.CritChance:
            case UpgradeType.CoinBonus:
            case UpgradeType.Revive:
            case UpgradeType.ShieldRegen:
                return !IsUpgradeOverCurrent(upgrade);
            case UpgradeType.FireRange:
                return FireRange >= MaxFireRange;
            case UpgradeType.BulletDamage:
                return BulletDamage - _baseBulletDamage >= MaxBulletDamageBonus;
            case UpgradeType.FireRate:
                return FireRate >= MaxFireRate;
            default:
                return false;
        }
    }

    // Wording for a disabled offer's button, shared by the shop and the level-up picker so they
    // can't drift: "Adquirido" reads right for a one-off you already own, but not for a stat
    // that's simply maxed out.
    public string GetUnavailableLabel(UpgradeData upgrade)
    {
        switch (upgrade.Type)
        {
            case UpgradeType.FireRange:
            case UpgradeType.BulletDamage:
            case UpgradeType.FireRate:
            case UpgradeType.SideShot:
            case UpgradeType.Heart:
            case UpgradeType.HitShield:
            case UpgradeType.Pierce:
            case UpgradeType.CritChance:
            case UpgradeType.CoinBonus:
            case UpgradeType.Revive:
                return "Al tope";
            default:
                return "Adquirido";
        }
    }

    // Which of the currently-offered rewards should get the "this is the one to take" glow.
    //
    // IsUpgradeOverCurrent alone can't answer this: it's a per-offer question ("does this beat what
    // I already have?"), so with a tier-1 stat owned and tier 2 + tier 3 both on the table, both
    // pass and the strongest isn't distinguished. This adds the missing cross-offer pass — among
    // the offers that do improve on the player's current state, only the single best of each
    // reward family wins. Shared by the shop and the level-up picker so the two can't disagree.
    //
    // Returns a parallel bool[] indexed the same as `offers`.
    public bool[] GetHighlightFlags(List<UpgradeData> offers)
    {
        var flags = new bool[offers.Count];
        var bestByType = new Dictionary<UpgradeType, int>();

        for (int i = 0; i < offers.Count; i++)
        {
            if (!IsUpgradeOverCurrent(offers[i])) continue;

            if (!bestByType.TryGetValue(offers[i].Type, out int bestIndex) || IsStronger(offers[i], offers[bestIndex]))
                bestByType[offers[i].Type] = i;
        }

        foreach (int index in bestByType.Values)
            flags[index] = true;

        return flags;
    }

    // Value is the primary comparison (it's the actual magnitude of the effect); Tier breaks ties
    // for families where two tiers share a value, like Heart's Epic/Legendary +3.
    private static bool IsStronger(UpgradeData candidate, UpgradeData current)
    {
        if (candidate.Value != current.Value) return candidate.Value > current.Value;
        return candidate.Tier > current.Tier;
    }

    // Always-shown "here's where you stand" text for every offer except the one-time Twin Shot
    // toggle (which has nothing to show beyond the shop's own "Adquirido" state).
    public string GetStackInfoText(UpgradeData upgrade)
    {
        bool helps = IsUpgradeOverCurrent(upgrade);
        switch (upgrade.Type)
        {
            case UpgradeType.FireRange:
                return $"Tenés: {FireRange:0} de rango (tope {MaxFireRange:0}) — {(helps ? "se suma" : "no suma (ya estás en el tope)")}";
            case UpgradeType.BulletDamage:
                return $"Tenés: +{BulletDamage - Mathf.RoundToInt(_baseBulletDamage)} (tope +{(int)MaxBulletDamageBonus}) — {(helps ? "se suma" : "no suma (ya tenés un tier mejor)")}";
            case UpgradeType.FireRate:
                return $"Tenés: {FireRate:0.0}/s (tope {MaxFireRate:0}/s) — {(helps ? "se suma" : "no suma (ya tenés un tier mejor)")}";
            case UpgradeType.Heart:
                return $"Tenés: {CurrentLives}/{MaxLives} vidas (tope {MaxLivesCap}) — {(helps ? "+1 al máximo y cura total" : "no suma (estás al tope y a full)")}";
            case UpgradeType.HitShield:
                return $"Tenés: {MaxShieldCharges} cargas (tope {MaxShieldChargesCap}) — {(helps ? "se suma" : "no suma (ya estás a full)")}";
            case UpgradeType.SideShot:
                return $"Tenés: {ExtraFiringLines} líneas (tope {MaxExtraFiringLinesCap}) — {(helps ? "se suma" : "ya estás en el tope")}";
            case UpgradeType.OrbitShield:
                return $"Tenés: {OrbitCount} cuchillas (tope 4) — {(helps ? "mejora" : "no mejora (ya tenés igual o mejor)")}";
            case UpgradeType.Companion:
                return $"Tenés: {CompanionStatPercent * 100:0}% stats (tope 50%) — {(helps ? "mejora" : "no mejora (ya tenés igual o mejor)")}";
            case UpgradeType.Laser:
                return $"Tenés: Láser Nv{LaserLevel} (tope Nv4) — {(helps ? "mejora" : "no mejora (ya tenés igual o mejor)")}";
            case UpgradeType.Missile:
                return MissileLevel > 0
                    ? $"Tenés: Misil Nv{MissileLevel} cada {MissileTiers[MissileLevel - 1].Interval:0.#}s — {(helps ? "mejora" : "no mejora (ya tenés igual o mejor)")}"
                    : "No tenés misiles todavía";
            case UpgradeType.Burn:
                return BurnLevel > 0
                    ? $"Tenés: Incendiario Nv{BurnLevel} ({BurnTiers[BurnLevel - 1].Dps:0}/seg) — {(helps ? "mejora" : "no mejora (ya tenés igual o mejor)")}"
                    : "No tenés quemadura todavía";
            case UpgradeType.Ultimate:
            {
                string current = EquippedUltimate == null ? "ninguna" : EquippedUltimate.Value.ToString();
                return $"Ultimate equipada: {current} — {(helps ? "la reemplaza" : "ya es esta")}";
            }
            case UpgradeType.Pierce:
                return $"Tenés: atraviesa {BulletPierce} (tope {MaxPierceCap}) — {(helps ? "se suma" : "ya estás en el tope")}";
            case UpgradeType.CritChance:
                return $"Tenés: {CritChance:0}% crítico (tope {MaxCritChance:0}%) — {(helps ? "se suma" : "ya estás en el tope")}";
            case UpgradeType.CoinBonus:
                return $"Tenés: +{CoinBonusPercent:0}% monedas (tope {MaxCoinBonusPercent:0}%) — {(helps ? "se suma" : "ya estás en el tope")}";
            case UpgradeType.BulletSpeed:
                return $"Tenés: {BulletSpeed:0} vel. de bala (tope {_baseBulletSpeed + MaxBulletSpeedBonus:0}) — {(helps ? "se suma" : "no suma (ya tenés un tier mejor)")}";
            case UpgradeType.BulletKnockback:
                return $"Tenés: +{BulletKnockback:0} empuje (tope {MaxBulletKnockbackBonus:0}) — {(helps ? "se suma" : "no suma (ya tenés un tier mejor)")}";
            case UpgradeType.Revive:
                return $"Tenés: {ReviveCharges} revividas (tope {MaxReviveCharges}) — {(helps ? "se suma" : "ya estás en el tope")}";
            case UpgradeType.ShieldRegen:
                return ShieldRegenPerMinute > 0f
                    ? $"Tenés: 1 carga cada {60f / ShieldRegenPerMinute:0.#}s — {(helps ? "mejora" : "no mejora (ya tenés igual o mejor)")}"
                    : "No tenés regeneración de escudo todavía";
            default:
                return null;
        }
    }

    // Non-mutating version of ApplyTieredStack: what would the winning bucket total become if this
    // pick were applied, without actually recording it. Used for the "would this help" preview.
    private float PreviewTieredBonus(UpgradeData upgrade)
    {
        float best = upgrade.Value;
        if (_tierStackTotals.TryGetValue(upgrade.Type, out var tierTotals))
        {
            best = tierTotals.GetValueOrDefault(upgrade.Tier, 0f) + upgrade.Value;
            foreach (var kv in tierTotals)
                if (kv.Key != upgrade.Tier && kv.Value > best) best = kv.Value;
        }
        return best;
    }

    // Adds this pick's Value to its (Type, Tier) bucket, then returns the highest bucket total
    // across all tiers seen so far for that Type — same-tier picks stack, cross-tier ones don't.
    private float ApplyTieredStack(UpgradeData upgrade)
    {
        if (!_tierStackTotals.TryGetValue(upgrade.Type, out var tierTotals))
        {
            tierTotals = new Dictionary<RewardTier, float>();
            _tierStackTotals[upgrade.Type] = tierTotals;
        }

        tierTotals[upgrade.Tier] = tierTotals.GetValueOrDefault(upgrade.Tier, 0f) + upgrade.Value;

        float best = 0f;
        foreach (var total in tierTotals.Values)
            if (total > best) best = total;

        return best;
    }

    private void RefreshOrbitBlades()
    {
        if (OrbitBladeScene == null) return;

        while (_orbitBlades.Count < OrbitCount)
        {
            var blade = OrbitBladeScene.Instantiate<OrbitShield>();
            blade.AngleOffset = _orbitBlades.Count / (float)OrbitCount * Mathf.Tau;
            AddChild(blade);
            _orbitBlades.Add(blade);
        }
    }

    private void EnsureCompanion()
    {
        if (CompanionScene == null) return;

        if (_companion == null)
        {
            _companion = CompanionScene.Instantiate<Companion>();
            _companion.OwnerPlayer = this;
            _companion.BulletScene = BulletScene;
            _companion.Position = new Vector2(-36f, -36f);
            AddChild(_companion);
        }

        _companion.StatPercent = CompanionStatPercent;
    }
}
