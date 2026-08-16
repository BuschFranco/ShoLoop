namespace ShooterLoop;

public partial class Player : CharacterBody2D
{
    [Export] public float MoveSpeed = 290f;
    [Export] public int MaxLives = 3;
    [Export] public float FireRate = 3f;
    [Export] public int BulletDamage = 10;
    [Export] public float FireRange = 220f;

    // Owned here rather than left to Bullet.tscn's own default so every shot the player fires gets
    // it from one place. Fixed stat — there's no reward that raises it.
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
    private const float MaxFireRangeBonus = 230f;

    // Absolute ceiling on FireRange, separate from the bonus cap above. Without it the only limit
    // was "base + 500", which isn't a stated ceiling so much as a side effect — and a range that
    // outgrows the visible screen stops being a meaningful stat, since you can't see what you're
    // shooting at anyway. This is the number that actually binds.
    private const float MaxFireRange = 450f;
    // 6 rather than 10: the HUD now shows lives as a row of icons, and ten of them is more width than a
    // portrait screen has to spare. Reaching 10 also meant buying the Legendary shop-only Corazón seven
    // separate times, which no real run did.
    private const int MaxLivesCap = 6;

    // Was 8, which was unreachable dead code: Barrier tiers are worth 1/2/3/4 and apply as
    // Max(current, value) rather than summing, so 4 was always the real ceiling. Now the constant says
    // what the game actually does — which matters more than before, since the shield row is on screen.
    private const int MaxShieldChargesCap = 4;
    public const int MaxExtraFiringLinesCap = 5;
    private const float SideShotSpacing = 14f;

    private const int MaxPierceCap = 5;
    private const float MaxCritChance = 60f;        // percent
    private const float MaxBulletKnockbackBonus = 400f;
    private const float MaxCoinBonusPercent = 150f;
    private const float MaxXpBonusPercent = 100f;

    public int CurrentLives;
    public int MaxShieldCharges = 0;
    public int CurrentShieldCharges = 0;
    public int OrbitCount = 0;
    public float CompanionStatPercent = 0f;
    public int ExtraFiringLines = 0;
    public int LaserLevel = 0;
    public int MissileLevel = 0;
    public int OndaLevel = 0;
    public int VendavalLevel = 0;
    public int BurnLevel = 0;
    public int BulletPierce = 0;
    public float CritChance = 0f;            // percent, 0-60
    public float BulletKnockback = 0f;
    public float CoinBonusPercent = 0f;      // percent, 0-150
    public float XpBonusPercent = 0f;        // percent, 0-100
    public float ShieldRegenPerMinute = 0f;
    public float DodgeChance = 0f;           // percent, 0-25
    public float FortuneBonus = 0f;          // percent added to Rare/Epic/Legendary odds
    public int RicochetCount = 0;            // number of additional bounces
    public float ThornsDamage = 0f;          // damage dealt to enemies on hit
    private float _thornsCooldown;
    public bool HasExtraProjectile => _hasExtraProjectile;
    public bool HasOrbitShield => OrbitCount > 0;

    // Coin payout multiplier applied in GameManager.RegisterKill (Botín reward).
    public float CoinMultiplier => 1f + CoinBonusPercent / 100f;

    // XP payout multiplier (Sabiduría reward). Applied to XP only — see RegisterKill for why Score
    // deliberately stays on the unmultiplied value.
    public float XpMultiplier => 1f + XpBonusPercent / 100f;

    public UltimateKind? EquippedUltimate = null;

    // Time-based cooldown, not a Score-driven charge meter — usable immediately on first pickup
    // (starts at 0, i.e. ready), then a flat wait after every use.
    //
    // Deliberately FLAT at 10s now. It used to shrink with RoundNumber down to a 3.5s floor by round 11,
    // and combined with the cooldown running *concurrently* with the effect (see TriggerUltimate) that
    // gave an 80% uptime on a 2.8s slow/damage-doubling — an Ultimate that's up four fifths of the time
    // stops being a panic button and just becomes the player's baseline state.
    public float UltimateCooldownRemaining { get; private set; } = 0f;
    public float UltimateCooldownDuration => UltimateCooldownCurve.Evaluate(GameManager.Instance?.RoundNumber ?? 1);
    private static readonly RoundCurve UltimateCooldownCurve = new(10f, 0f, 10f, 10f);

    public event Action<int, int> LivesChanged;
    public event Action<int, int> ShieldChanged;

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

    // Level → (fire interval, damage, blast radius). Onda de Choque detonates in place around the
    // player rather than traveling, so it needs no aim and hits everything adjacent for free —
    // deliberately weaker per-tick than Missile and capped at a small radius (well under FireRange)
    // to stay a close-range panic button, not a second Missile.
    // Radii were originally 45/55/65/75, deliberately kept well under Missile's on the grounds that Onda
    // costs nothing to aim. In play that made it useless against exactly the threat it should answer: a
    // Speedy at 205 base speed covers ~315 px/s by round 10, so a 45px ring caught it only after it had
    // already closed to contact range and landed its hit. These are ~45% wider — still short enough to be
    // a close-range panic button rather than a second Missile, but wide enough to catch a rusher on the
    // way in.
    //
    // What makes the extra reach actually pay off is TriggerOnda holding the cooldown at 0 while nothing
    // is in range: the pulse is always armed and waiting, so a wider ring means it fires *earlier* on the
    // approach rather than just covering more ground at the same moment.
    private static readonly (float Interval, int Damage, float Radius)[] OndaTiers =
    {
        (6.0f, 20, 65f),
        (5.0f, 35, 78f),
        (4.0f, 55, 92f),
        (3.2f, 80, 105f),
    };

    // Level → (fire interval, damage, range, half-angle in degrees, knockback force). Vendaval is
    // shop-exclusive and only ever offered at Epic/Legendary (see UpgradeData), so it only needs 2
    // tiers. A directional cone in front of the player (not a full ring like Onda de Choque) that
    // hits hard AND shoves survivors out of the cone — the "clears you a path" effect the reward
    // is built around, so the knockback matters as much as the damage.
    private static readonly (float Interval, int Damage, float Range, float HalfAngleDegrees, float Knockback)[] VendavalTiers =
    {
        (4.5f, 90, 260f, 32f, 380f),
        (3.6f, 140, 320f, 36f, 460f),
    };

    // Level → (damage per second, duration). Incendiario is a modifier applied by every source of
    // player damage — basic shots, the Laser, Orbit Blades, and Missiles all read these two
    // properties rather than each keeping their own copy of the tier lookup.
    public float CurrentBurnDps => BurnLevel > 0 ? BurnTiers[BurnLevel - 1].Dps : 0f;
    public float CurrentBurnDuration => BurnLevel > 0 ? BurnTiers[BurnLevel - 1].Duration : 0f;

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
    // True for the invuln window opened by a hit a shield charge absorbed — blinks the shield aura
    // instead of the ship itself, so a hit that cost you a charge doesn't read identically to one
    // that cost you a life.
    private bool _blinkShieldInstead = false;
    private const float InvulnDuration = 2f;
    private float _blinkTimer = 0f;
    private const float BlinkInterval = 0.1f;
    private Polygon2D _visual;
    private Vector2 _knockbackVelocity = Vector2.Zero;
    private const float KnockbackForce = 220f;
    private const float KnockbackDecay = 900f;

    // How fast the Visual triangle turns to face the movement direction, as an exponential
    // catch-up rate (bigger = snappier). Kept separate from MoveAcceleration/Deceleration below —
    // rotation and translation reading as loosely coupled, rather than locked in lockstep, is what
    // makes the turn look like banking instead of the whole ship instantly snapping to face a
    // direction change.
    private const float FacingTurnRate = 16f;

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
    private Timer _shieldRegenTimer;
    private Line2D _fireRangeRing;
    private Timer _frenzyTimer;

    // Read by HUD's cooldown icons. 1 = just fired at an enemy (icon fully covered), decaying
    // to 0 and HOLDING there. Laser/Missile aren't driven by a repeating Timer at all (see
    // TryFireLaser/TryFireMissile, polled every physics frame instead) — a Timer fires on its own
    // schedule regardless of whether anything is in range, which both made the icon flicker
    // uncovered for a single frame on every empty retry, AND meant an enemy walking into range
    // right after an empty tick had to wait out a whole extra interval before actually firing.
    public float LaserCooldownFraction { get; private set; }
    public float MissileCooldownFraction { get; private set; }
    public float OndaCooldownFraction { get; private set; }
    public float VendavalCooldownFraction { get; private set; }

    public float ShieldRegenCooldownFraction =>
        _shieldRegenTimer == null || _shieldRegenTimer.IsStopped() || _shieldRegenTimer.WaitTime <= 0f
            ? 0f : (float)(_shieldRegenTimer.TimeLeft / _shieldRegenTimer.WaitTime);

    public override void _Ready()
    {
        AddToGroup("player");
        CurrentLives = MaxLives;

        _baseFireRate = FireRate;
        _baseBulletDamage = BulletDamage;
        _baseFireRange = FireRange;

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
        StartIdleAnimations();
    }

    // Looping ambient motion so the ship and its range ring never sit perfectly static. Both run on
    // the Visual/ring nodes rather than the player itself, so they can't interfere with movement,
    // the invuln blink (which toggles _visual.Visible, not its scale), or the arena clamp.
    private void StartIdleAnimations()
    {
        var breathe = CreateTween();
        breathe.SetLoops();
        breathe.TweenProperty(_visual, "scale", Vector2.One * 1.06f, 0.9f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        breathe.TweenProperty(_visual, "scale", Vector2.One, 0.9f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        // The ring slowly counter-rotates — a static circle reads as UI, a drifting one as a field.
        var spin = CreateTween();
        spin.SetLoops();
        spin.TweenProperty(_fireRangeRing, "rotation", Mathf.Tau, 24f);
        spin.TweenCallback(Callable.From(() => _fireRangeRing.Rotation = 0f));
    }

    // A faint ring showing FireRange — the player only auto-fires at enemies inside it, so it
    // needs to be visible rather than an invisible gameplay number. Rebuilt whenever the Fire
    // Range reward raises FireRange, since a Line2D's points are baked at the radius they were
    // drawn at, not tied live to the FireRange field.
    private void SpawnFireRangeIndicator()
    {
        _fireRangeRing = new Line2D();
        _fireRangeRing.Width = 2f;
        _fireRangeRing.DefaultColor = Palette.FireRangeRing;
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

        // Laser/Missile fire from here directly (no repeating Timer) — see the property comments
        // on LaserCooldownFraction above for why. Decay first so a tick that just cleared the
        // cooldown can also fire immediately in the same frame, rather than firing next frame.
        if (LaserLevel > 0)
        {
            if (LaserCooldownFraction > 0f)
                LaserCooldownFraction = Mathf.Max(0f, LaserCooldownFraction - (float)delta / GetLaserInterval());
            if (LaserCooldownFraction <= 0f)
                TryFireLaser();
        }
        if (MissileLevel > 0)
        {
            if (MissileCooldownFraction > 0f)
                MissileCooldownFraction = Mathf.Max(0f, MissileCooldownFraction - (float)delta / MissileTiers[MissileLevel - 1].Interval);
            if (MissileCooldownFraction <= 0f)
                TryFireMissile();
        }

        // Onda de Choque/Vendaval are always centered on/in front of the player, so unlike
        // Laser/Missile there's no "nothing in range" case to bail on — they simply always fire
        // once the cooldown clears.
        if (OndaLevel > 0)
        {
            if (OndaCooldownFraction > 0f)
                OndaCooldownFraction = Mathf.Max(0f, OndaCooldownFraction - (float)delta / OndaTiers[OndaLevel - 1].Interval);
            if (OndaCooldownFraction <= 0f)
                TriggerOnda();
        }

        if (VendavalLevel > 0)
        {
            if (VendavalCooldownFraction > 0f)
                VendavalCooldownFraction = Mathf.Max(0f, VendavalCooldownFraction - (float)delta / VendavalTiers[VendavalLevel - 1].Interval);
            if (VendavalCooldownFraction <= 0f)
                TriggerVendaval();
        }

        if (UltimateCooldownRemaining > 0f)
            UltimateCooldownRemaining = Mathf.Max(0f, UltimateCooldownRemaining - (float)delta);

        Velocity = _moveVelocity + _knockbackVelocity;
        MoveAndSlide();

        UpdateFacing(dir, (float)delta);

        GlobalPosition = new Vector2(
            Mathf.Clamp(GlobalPosition.X, -ArenaHalfExtents.X, ArenaHalfExtents.X),
            Mathf.Clamp(GlobalPosition.Y, -ArenaHalfExtents.Y, ArenaHalfExtents.Y)
        );

        if (_invulnTimer > 0f)
        {
            _invulnTimer -= (float)delta;
            _blinkTimer += (float)delta;
            bool blinkOn = ((int)(_blinkTimer / BlinkInterval)) % 2 == 0;

            if (_blinkShieldInstead)
                _shieldAura.Visible = blinkOn && CurrentShieldCharges > 0;
            else
                _visual.Visible = blinkOn;

            if (_invulnTimer <= 0f)
            {
                _visual.Visible = true;
                _shieldAura.Visible = CurrentShieldCharges > 0;
            }
        }

        if (_thornsCooldown > 0f)
            _thornsCooldown -= (float)delta;
    }

    // Turns the triangle to face the direction the player is actually moving in — it used to be
    // rigidly fixed pointing right regardless of input. Prefers raw input direction (the player's
    // immediate intent) and falls back to the smoothed velocity while decelerating with no input
    // held, so the ship keeps facing forward as it coasts to a stop rather than snapping to face
    // whatever residual coast direction. Holds its last facing entirely when both are ~zero,
    // instead of resetting to some default — a stationary ship has no "forward" to snap back to.
    private void UpdateFacing(Vector2 inputDir, float delta)
    {
        Vector2 facing = inputDir != Vector2.Zero ? inputDir
            : (_moveVelocity.LengthSquared() > 25f ? _moveVelocity : Vector2.Zero);
        if (facing == Vector2.Zero) return;

        // Exponential catch-up rather than an instant snap, so a sharp direction change reads as
        // the triangle banking through the turn instead of teleporting to face the new heading.
        float weight = 1f - Mathf.Exp(-FacingTurnRate * delta);
        _visual.Rotation = Mathf.LerpAngle(_visual.Rotation, facing.Angle(), weight);
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

        // Dodge: chance to avoid the hit entirely
        if (DodgeChance > 0f && _dodgeRng.NextDouble() < DodgeChance / 100f)
        {
            _invulnTimer = InvulnDuration * 0.5f; // shorter invuln on dodge
            return;
        }

        // Thorns: damage enemies when hit
        if (ThornsDamage > 0f && _thornsCooldown <= 0f)
        {
            _thornsCooldown = 8f;
            var enemies = GetTree().GetNodesInGroup("enemies");
            int hit = 0;
            foreach (var node in enemies)
            {
                if (node is Enemy e && !e.IsQueuedForDeletion())
                {
                    e.TakeDamage(Mathf.RoundToInt(ThornsDamage));
                    hit++;
                    if (hit >= 15) break;
                }
            }
        }

        if (CurrentShieldCharges > 0)
        {
            CurrentShieldCharges--;
            RaiseShieldChanged();
            _blinkShieldInstead = true;
        }
        else
        {
            _blinkShieldInstead = false;
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

    // Heals 1 life, capped at MaxLives — used by the Heart pickup an enemy can drop. A mid-run
    // top-up, distinct from the Corazón Legendario reward which raises MaxLives itself.
    public void AddLife(int amount = 1)
    {
        if (CurrentLives >= MaxLives) return;
        CurrentLives = Mathf.Min(CurrentLives + amount, MaxLives);
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
        visual.Color = Palette.LevelUpNova;
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

    // Called every physics frame while LaserLevel > 0 and the cooldown has fully cleared — NOT a
    // repeating Timer. A Timer fires on its own schedule regardless of whether a target is in
    // range, so an enemy walking into range right after an empty tick had to wait out a whole
    // extra interval before the laser could actually go off. Polling every frame means it fires
    // the instant both conditions are true, with no dead time in between.
    private void TryFireLaser()
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

        if (inRange.Count > 0)
            LaserCooldownFraction = 1f;

        foreach (var enemy in inRange)
        {
            if (CurrentBurnDps > 0f)
                enemy.ApplyBurn(CurrentBurnDps, CurrentBurnDuration);

            int finalDamage = ApplyCrit(Mathf.RoundToInt(damage), out bool isCrit);
            enemy.TakeDamage(finalDamage);
            SpawnLaserBeam(enemy.GlobalPosition, isCrit);
        }
    }

    // Called every physics frame while MissileLevel > 0 and the cooldown has fully cleared — same
    // reasoning as TryFireLaser above.
    private void TryFireMissile()
    {
        if (MissileScene == null || _bulletsContainer == null) return;

        var target = FindNearestEnemyInRange();
        if (target == null) return;   // nothing to shoot at; cooldown stays at 0, retried next frame

        MissileCooldownFraction = 1f;
        var (_, damage, radius) = MissileTiers[MissileLevel - 1];

        var missile = MissileScene.Instantiate<Missile>();
        missile.GlobalPosition = GlobalPosition;

        // Snapshot of where the target is *now*. Missile.cs never re-reads it, which is what makes
        // the shot dodgeable rather than homing.
        missile.TargetPosition = target.GlobalPosition;
        missile.Damage = ApplyCrit(damage, out bool isCrit);
        if (isCrit) missile.Modulate = Palette.CritBullet;
        missile.ExplosionRadius = radius;
        missile.BurnDps = CurrentBurnDps;
        missile.BurnDuration = CurrentBurnDuration;

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

    private void SpawnLaserBeam(Vector2 targetGlobalPos, bool isCrit = false)
    {
        var beam = new Line2D();
        beam.AddPoint(Vector2.Zero);
        beam.AddPoint(ToLocal(targetGlobalPos));
        beam.Width = 3f;
        beam.DefaultColor = isCrit ? Palette.CritBullet : Palette.LaserBeam;
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

        float laserDps = LaserLevel > 0 ? GetLaserDamage() / GetLaserInterval() * critMult : 0f;

        // Both AoE, so their nominal single-target DPS understates them — a modest multiplier keeps
        // a missile/burn build from reading as weaker than it plays and slipping past the balancer.
        float missileDps = 0f;
        if (MissileLevel > 0)
        {
            var (interval, damage, _) = MissileTiers[MissileLevel - 1];
            missileDps = damage / interval * 1.5f * critMult;
        }

        // Sustained fire keeps burn permanently refreshed, so its DPS is effectively always on.
        // Applies once here even though it's now layered onto every weapon (bullets/laser/blades/
        // missiles) — modeling per-weapon burn uptime precisely isn't worth the complexity for an
        // estimate that only needs to catch order-of-magnitude runaway builds.
        float burnDps = CurrentBurnDps;

        float orbitDps = OrbitCount * OrbitBladeDamage * OrbitHitsPerSecondEstimate * critMult;

        float ondaDps = 0f;
        if (OndaLevel > 0)
        {
            var (interval, damage, _) = OndaTiers[OndaLevel - 1];
            ondaDps = damage / interval * critMult;
        }

        float vendavalDps = 0f;
        if (VendavalLevel > 0)
        {
            var (interval, damage, _, _, _) = VendavalTiers[VendavalLevel - 1];
            vendavalDps = damage / interval * critMult;
        }

        // Ultimates bypass every stat term above entirely — Nova/Zona Lenta have no persistent
        // stat to read, and even Sobrecarga's temporary FireRate/BulletDamage doubling would only
        // get caught here if this happened to be sampled mid-buff. Modeled explicitly instead, as
        // an average over one full cycle — this is what makes a build that leans entirely on spamming
        // its Ultimate read as the load-bearing power source it actually is, instead of silently
        // reading as "doing nothing" and dodging the adaptive difficulty correction altogether (see
        // docs/difficulty-scaling.md — this was a real exploit, not hypothetical).
        float ultimateDps = 0f;
        if (EquippedUltimate != null)
        {
            // A full cycle is now effect-then-cooldown, not cooldown-with-effect-inside, so the
            // denominator has to be the sum. Dividing by the cooldown alone (as this did while the two
            // overlapped) would over-report uptime and hand the player a difficulty penalty they
            // haven't earned.
            float active = GetUltimateActiveDuration(EquippedUltimate.Value);
            float cooldown = Mathf.Max(UltimateCooldownDuration, 0.1f);
            float cycle = active + cooldown;
            float uptimeFraction = Mathf.Clamp(active / cycle, 0f, 1f);

            ultimateDps = EquippedUltimate.Value switch
            {
                // A flat burst every cycle — genuine average DPS, no approximation needed.
                UltimateKind.Nova => UltimateNovaDamage / cycle,

                // No direct damage number to convert — models "enemies can't catch you" as extra
                // effective throughput roughly on par with the player's whole raw kit, since a
                // permanent slow is close to permanent safety. Same "order of magnitude, not exact
                // combat simulation" approximation this whole method already leans on.
                UltimateKind.TimeSlow => baselineDps * uptimeFraction,

                // Already doubles gunDps live; averaged over the cycle so a snapshot taken between
                // uses doesn't read Sobrecarga as contributing nothing.
                UltimateKind.Frenzy => gunDps * uptimeFraction,

                _ => 0f,
            };
        }

        // The companion re-derives its own output from the player's live stats, so it's a flat
        // percentage bonus on top of everything rather than its own independent term.
        float total = (gunDps + laserDps + missileDps + burnDps + orbitDps + ondaDps + vendavalDps) * (1f + CompanionStatPercent) + ultimateDps;
        return total / baselineDps;
    }

    // Nominal duration for the two time-based Ultimates before the anti-permanent-uptime cap in
    // GetUltimateEffectDuration() below.
    private const float UltimateEffectDurationBase = 6f;

    // Once UltimateCooldownDuration floors out at 3.5s (round 11+), a flat 6s effect would overlap
    // itself and grant up to 100% uptime — enemies permanently slowed to 30% speed, or gun damage
    // permanently doubled, forever, rather than an occasional panic button. Capping the live
    // duration to a fraction of the CURRENT cooldown guarantees a real downtime window no matter
    // how short the cooldown gets, while leaving early rounds (where the cooldown is already long
    // enough that this never binds) completely unaffected.
    private const float UltimateUptimeCap = 0.8f;

    private float GetUltimateEffectDuration() =>
        Mathf.Min(UltimateEffectDurationBase, UltimateCooldownDuration * UltimateUptimeCap);

    // Mirrors OrbitShield.Damage's default. Kept in sync manually rather than read off a live
    // blade so the power estimate still works before any blade has been instantiated.
    private const int OrbitBladeDamage = 7;

    public void TriggerUltimate()
    {
        if (EquippedUltimate == null || UltimateCooldownRemaining > 0f) return;

        switch (EquippedUltimate.Value)
        {
            case UltimateKind.Nova:
                TriggerUltimateNova();
                break;
            case UltimateKind.TimeSlow:
                GameManager.Instance?.ApplyTemporarySlow(0.3f, GetUltimateEffectDuration());
                break;
            case UltimateKind.Frenzy:
                TriggerUltimateFrenzy();
                break;
        }

        // The cooldown starts counting only once the effect has finished, rather than running
        // concurrently with it the way it used to. Modelled by simply adding the active duration on
        // top — no extra state or callback needed, and the HUD's readout stays honest because it shows
        // the whole remaining lock either way. Nova is instantaneous, so it gets the bare cooldown.
        UltimateCooldownRemaining = GetUltimateActiveDuration(EquippedUltimate.Value) + UltimateCooldownDuration;
    }

    // Nova detonates on the frame it's triggered; the other two run for a while. Only the timed ones
    // push their cooldown back.
    private float GetUltimateActiveDuration(UltimateKind kind) =>
        kind == UltimateKind.Nova ? 0f : GetUltimateEffectDuration();

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
        visual.Color = Palette.UltimateNova;
        visual.Scale = Vector2.Zero;
        AddChild(visual);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(visual, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(visual, "modulate:a", 0f, 0.5f);
        tween.Chain().TweenCallback(Callable.From(() => visual.QueueFree()));
    }

    // Onda de Choque: the periodic small AoE reward. Same enemies-in-radius loop as
    // TriggerUltimateNova above, just centered on the player continuously rather than on demand.
    private void TriggerOnda()
    {
        var (_, damage, radius) = OndaTiers[OndaLevel - 1];

        var targets = new List<Enemy>();
        foreach (var n in GetTree().GetNodesInGroup("enemies"))
        {
            if (n is not Enemy enemy || !IsInstanceValid(enemy)) continue;
            if (GlobalPosition.DistanceTo(enemy.GlobalPosition) <= radius) targets.Add(enemy);
        }

        // Nothing in range yet — leave the cooldown at 0 rather than spending it on an empty pop,
        // so it fires the instant an enemy actually wanders into radius instead of on a fixed
        // schedule that can whiff entirely. Same "stays at 0, retried next frame" idea as
        // TryFireMissile's own no-target bail above.
        if (targets.Count == 0) return;

        OndaCooldownFraction = 1f;
        int dmg = ApplyCrit(damage, out bool isCrit);

        foreach (var enemy in targets)
        {
            if (CurrentBurnDps > 0f) enemy.ApplyBurn(CurrentBurnDps, CurrentBurnDuration);
            enemy.TakeDamage(dmg);
        }

        SpawnOndaVisual(radius, isCrit);
    }

    private void SpawnOndaVisual(float radius, bool isCrit)
    {
        var visual = new Polygon2D();
        var points = new Vector2[24];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.Tau;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        visual.Polygon = points;
        visual.Color = isCrit ? Palette.CritBullet : Palette.OndaBlast;
        visual.Scale = Vector2.Zero;
        AddChild(visual);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(visual, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(visual, "modulate:a", 0f, 0.4f);
        tween.Chain().TweenCallback(Callable.From(() => visual.QueueFree()));
    }

    // Vendaval: shop-exclusive Epic/Legendary reward. A directional cone in front of the player
    // (facing direction, same angle the player's own triangle sprite is already turned to — see
    // UpdateFacing) that hits hard and shoves survivors out of the cone, clearing a path rather
    // than just clearing HP.
    private void TriggerVendaval()
    {
        VendavalCooldownFraction = 1f;
        var (_, damage, range, halfAngleDegrees, knockback) = VendavalTiers[VendavalLevel - 1];
        int dmg = ApplyCrit(damage, out bool isCrit);

        Vector2 facing = Vector2.Right.Rotated(_visual.Rotation);
        float halfAngleRad = Mathf.DegToRad(halfAngleDegrees);

        foreach (var n in GetTree().GetNodesInGroup("enemies"))
        {
            if (n is not Enemy enemy || !IsInstanceValid(enemy)) continue;

            Vector2 toEnemy = enemy.GlobalPosition - GlobalPosition;
            float dist = toEnemy.Length();
            if (dist > range || dist <= 0f) continue;
            if (Mathf.Abs(facing.AngleTo(toEnemy)) > halfAngleRad) continue;

            if (CurrentBurnDps > 0f) enemy.ApplyBurn(CurrentBurnDps, CurrentBurnDuration);
            enemy.TakeDamage(dmg);
            enemy.ApplyKnockback(toEnemy / dist * knockback);
        }

        SpawnVendavalVisual(range, halfAngleDegrees, facing, isCrit);
    }

    private void SpawnVendavalVisual(float range, float halfAngleDegrees, Vector2 facing, bool isCrit)
    {
        const int segments = 14;
        var points = new Vector2[segments + 2];
        points[0] = Vector2.Zero;

        float halfAngleRad = Mathf.DegToRad(halfAngleDegrees);
        float startAngle = facing.Angle() - halfAngleRad;
        float endAngle = facing.Angle() + halfAngleRad;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(startAngle, endAngle, i / (float)segments);
            points[i + 1] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * range;
        }

        var visual = new Polygon2D();
        visual.Polygon = points;
        visual.Color = isCrit ? Palette.CritBullet : Palette.VendavalBlast;
        AddChild(visual);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(visual, "modulate:a", 0f, 0.3f);
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
        _frenzyTimer.WaitTime = GetUltimateEffectDuration();
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
    private readonly Random _dodgeRng = new();
    private const float CritMultiplier = 2f;

    // Rolled per hit, not per volley/tick — same reasoning as burn being applied per hit rather
    // than per pickup. Shared by every weapon (bullets, laser, blades, missiles, the drone) so a
    // Crítico build feels like it crits everywhere, not just on the basic gun.
    public int ApplyCrit(int baseDamage, out bool isCrit)
    {
        isCrit = CritChance > 0f && _critRng.NextDouble() * 100.0 < CritChance;
        return isCrit ? Mathf.RoundToInt(baseDamage * CritMultiplier) : baseDamage;
    }

    private void FireInDirection(Vector2 dir, Vector2 positionOffset = default)
    {
        if (BulletScene == null || _bulletsContainer == null) return;

        var bullet = BulletScene.Instantiate<Bullet>();
        bullet.GlobalPosition = GlobalPosition + positionOffset;
        bullet.Direction = dir;
        bullet.Speed = BulletSpeed;
        bullet.Pierce = BulletPierce;
        bullet.Knockback = BulletKnockback;
        bullet.Ricochet = RicochetCount;

        bullet.BurnDps = CurrentBurnDps;
        bullet.BurnDuration = CurrentBurnDuration;

        // Rolled per bullet, not per volley, so a Twin/Side Shot spread can crit on some lines and
        // not others — more visible feedback than an all-or-nothing volley.
        bullet.Damage = ApplyCrit(BulletDamage, out bool isCrit);
        if (isCrit) bullet.Modulate = Palette.CritBullet;

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
                break;
            case UpgradeType.Missile:
                MissileLevel = Mathf.Max(MissileLevel, (int)upgrade.Value);
                break;
            case UpgradeType.ShockwaveAura:
                OndaLevel = Mathf.Max(OndaLevel, (int)upgrade.Value);
                break;
            case UpgradeType.Vendaval:
                VendavalLevel = Mathf.Max(VendavalLevel, (int)upgrade.Value);
                break;
            case UpgradeType.Burn:
                BurnLevel = Mathf.Max(BurnLevel, (int)upgrade.Value);
                break;
            case UpgradeType.Ultimate:
                EquippedUltimate = upgrade.Ultimate;
                UltimateCooldownRemaining = 0f;
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
            case UpgradeType.XpBonus:
                XpBonusPercent = Mathf.Min(XpBonusPercent + upgrade.Value, MaxXpBonusPercent);
                break;

            // Magnitude stat, so it uses the same tier-bucketing as FireRange/FireRate/BulletDamage.
            case UpgradeType.BulletKnockback:
                BulletKnockback = Mathf.Min(ApplyTieredStack(upgrade), MaxBulletKnockbackBonus);
                break;

            case UpgradeType.ShieldRegen:
                ShieldRegenPerMinute = Mathf.Max(ShieldRegenPerMinute, upgrade.Value);
                EnsureShieldRegenTimer();
                break;
            case UpgradeType.Dodge:
                DodgeChance = Mathf.Min(DodgeChance + upgrade.Value, 25f);
                break;
            case UpgradeType.Fortune:
                FortuneBonus = Mathf.Min(FortuneBonus + upgrade.Value, 25f);
                break;
            case UpgradeType.Ricochet:
                RicochetCount = Mathf.Max(RicochetCount, (int)upgrade.Value);
                break;
            case UpgradeType.Thorns:
                ThornsDamage = Mathf.Max(ThornsDamage, upgrade.Value);
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
        AddShieldCharge();
    }

    // Adds one shield charge, capped at MaxShieldCharges. No-ops for a player with no Barrier
    // (MaxShieldCharges == 0). Shared by the passive Regeneración timer above and the Shield
    // pickup an enemy can drop.
    public void AddShieldCharge()
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
            // Was missing entirely, which meant Twin Shot always read as "improves nothing" and so
            // could never be flagged as the best offer even when unowned.
            case UpgradeType.ExtraProjectile:
                return !_hasExtraProjectile;
            case UpgradeType.OrbitShield:
                return (int)upgrade.Value > OrbitCount;
            case UpgradeType.Companion:
                return upgrade.Value / 100f > CompanionStatPercent;
            case UpgradeType.Laser:
                return (int)upgrade.Value > LaserLevel;
            case UpgradeType.Missile:
                return (int)upgrade.Value > MissileLevel;
            case UpgradeType.ShockwaveAura:
                return (int)upgrade.Value > OndaLevel;
            case UpgradeType.Vendaval:
                return (int)upgrade.Value > VendavalLevel;
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
            case UpgradeType.XpBonus:
                return XpBonusPercent < MaxXpBonusPercent;
            case UpgradeType.BulletKnockback:
                return Mathf.Min(PreviewTieredBonus(upgrade), MaxBulletKnockbackBonus) > BulletKnockback;
            case UpgradeType.ShieldRegen:
                return upgrade.Value > ShieldRegenPerMinute;
            case UpgradeType.Dodge:
                return upgrade.Value > DodgeChance;
            case UpgradeType.Fortune:
                return upgrade.Value > FortuneBonus;
            case UpgradeType.Ricochet:
                return (int)upgrade.Value > RicochetCount;
            case UpgradeType.Thorns:
                return upgrade.Value > ThornsDamage;
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
            case UpgradeType.ShockwaveAura:
            case UpgradeType.Vendaval:
            case UpgradeType.Burn:
            case UpgradeType.Ultimate:
            case UpgradeType.Pierce:
            case UpgradeType.CritChance:
            case UpgradeType.CoinBonus:
            case UpgradeType.XpBonus:
            case UpgradeType.ShieldRegen:
            case UpgradeType.Dodge:
            case UpgradeType.Fortune:
            case UpgradeType.Ricochet:
            case UpgradeType.Thorns:
                return !IsUpgradeOverCurrent(upgrade);
            case UpgradeType.FireRange:
                return FireRange >= MaxFireRange;
            case UpgradeType.BulletDamage:
                return BulletDamage - _baseBulletDamage >= MaxBulletDamageBonus;
            case UpgradeType.FireRate:
                return FireRate >= MaxFireRate;
            // Same hard-cap rule as the three above; it was absent, so a maxed-out Knockback offer
            // stayed enabled and could be bought for nothing.
            case UpgradeType.BulletKnockback:
                return BulletKnockback >= MaxBulletKnockbackBonus;
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
            case UpgradeType.XpBonus:
            case UpgradeType.BulletKnockback:
            case UpgradeType.Dodge:
            case UpgradeType.Fortune:
            case UpgradeType.Ricochet:
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
            case UpgradeType.ShockwaveAura:
                return OndaLevel > 0
                    ? $"Tenés: Onda de Choque Nv{OndaLevel} cada {OndaTiers[OndaLevel - 1].Interval:0.#}s — {(helps ? "mejora" : "no mejora (ya tenés igual o mejor)")}"
                    : "No tenés Onda de Choque todavía";
            case UpgradeType.Vendaval:
                return VendavalLevel > 0
                    ? $"Tenés: Vendaval Nv{VendavalLevel} cada {VendavalTiers[VendavalLevel - 1].Interval:0.#}s — {(helps ? "mejora" : "no mejora (ya tenés igual o mejor)")}"
                    : "No tenés Vendaval todavía";
            case UpgradeType.Burn:
                return BurnLevel > 0
                    ? $"Tenés: Incendiario Nv{BurnLevel} ({BurnTiers[BurnLevel - 1].Dps:0}/seg) — {(helps ? "mejora" : "no mejora (ya tenés igual o mejor)")}"
                    : "No tenés quemadura todavía";
            case UpgradeType.Ultimate:
            {
                string current = EquippedUltimate == null
                    ? "ninguna"
                    : UltimateKindNames.Display(EquippedUltimate.Value);
                return $"Ultimate equipada: {current} — {(helps ? "la reemplaza" : "ya es esta")}";
            }
            case UpgradeType.Pierce:
                return $"Tenés: atraviesa {BulletPierce} (tope {MaxPierceCap}) — {(helps ? "se suma" : "ya estás en el tope")}";
            case UpgradeType.CritChance:
                return $"Tenés: {CritChance:0}% crítico (tope {MaxCritChance:0}%) — {(helps ? "se suma" : "ya estás en el tope")}";
            case UpgradeType.CoinBonus:
                return $"Tenés: +{CoinBonusPercent:0}% monedas (tope {MaxCoinBonusPercent:0}%) — {(helps ? "se suma" : "ya estás en el tope")}";
            case UpgradeType.XpBonus:
                return $"Tenés: +{XpBonusPercent:0}% experiencia (tope {MaxXpBonusPercent:0}%) — {(helps ? "se suma" : "ya estás en el tope")}";
            case UpgradeType.BulletKnockback:
                return $"Tenés: +{BulletKnockback:0} empuje (tope {MaxBulletKnockbackBonus:0}) — {(helps ? "se suma" : "no suma (ya tenés un tier mejor)")}";
            case UpgradeType.ShieldRegen:
                return ShieldRegenPerMinute > 0f
                    ? $"Tenés: 1 carga cada {60f / ShieldRegenPerMinute:0.#}s — {(helps ? "mejora" : "no mejora (ya tenés igual o mejor)")}"
                    : "No tenés regeneración de escudo todavía";
            case UpgradeType.Dodge:
                return $"Tenés: {DodgeChance:0}% esquiva (tope 25%) — {(helps ? "se suma" : "ya estás en el tope")}";
            case UpgradeType.Fortune:
                return $"Tenés: +{FortuneBonus:0}% fortuna (tope 25%) — {(helps ? "se suma" : "ya estás en el tope")}";
            case UpgradeType.Ricochet:
                return $"Tenés: {RicochetCount} rebotes (tope 4) — {(helps ? "se suma" : "ya estás en el tope")}";
            case UpgradeType.Thorns:
                return ThornsDamage > 0f
                    ? $"Tenés: {ThornsDamage:0} daño de escudo voltáico — {(helps ? "mejora" : "no mejora (ya tenés igual o mejor)")}"
                    : "No tenés escudo voltáico todavía";
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
