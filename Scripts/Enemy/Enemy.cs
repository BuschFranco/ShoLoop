namespace ShooterLoop;

public enum EnemyCategory { Common, Rare, Special, Hidden, Boss }

public partial class Enemy : CharacterBody2D
{
    [Export] public float MoveSpeed = 90f;
    [Export] public int MaxHp = 30;
    [Export] public int XpReward = 1;
    [Export] public int CoinsReward = 1;
    [Export] public int ContactDamage = 10;
    [Export] public EnemyCategory Category = EnemyCategory.Common;

    [Export] public bool CanSplit = false;
    [Export] public int SplitCount = 2;
    [Export] public int SplitGeneration = 0;
    [Export] public int MaxSplitGenerations = 3;
    [Export] public float SplitHpScale = 0.5f;
    [Export] public float SplitVisualScale = 0.75f;

    // Only Boss/Special enemies get an actual health bar — commons/rares die fast enough that a
    // permanent bar would just be visual noise. This offset does double duty though: it's also
    // where the floating damage number spawns on *every* enemy, health bar or not, so it stays
    // exported (not folded into CreateHealthBar) and tuned per-scene to roughly clear each enemy's
    // own visual radius (bigger enemies need a bigger offset).
    [Export] public float HealthBarOffset = -30f;

    // "Magnet-like" repulsion radius — any other enemy closer than this gets pushed directly away
    // from its own center, so a crowd converging on the player doesn't just stack on one point.
    // Both numbers were roughly doubled after the original values lost outright to the chase force:
    // 24px is barely more than touching for the bigger enemies, and a strength of 80 against move
    // speeds of 90-190 meant a herded crowd still collapsed into a ball.
    [Export] public float SeparationRadius = 46f;
    private const float SeparationStrength = 170f;

    // Persistent per-enemy heading offset (degrees, randomised once in _Ready). Without it every
    // enemy computes the *identical* chase vector and the pack travels in one straight column;
    // a few degrees of variation makes them converge along slightly different arcs instead.
    // Persistent rather than per-frame so it reads as a different approach line, not as jitter.
    // Small enough to still converge — a constant angular offset spirals inward, not around.
    [Export] public float MaxChaseAngleOffset = 14f;
    private float _chaseAngleOffset;

    // How far ahead an enemy looks for obstacles when steering around them. Bigger/faster enemies
    // want more warning, hence the per-scene override.
    [Export] public float AvoidanceProbeDistance = 70f;

    // Detour angles tried in order, nearest-to-straight first, so an enemy deviates as little as
    // it can get away with. Only layer 8 (obstacles) is probed — see docs/enemies.md.
    private static readonly float[] AvoidanceAngles = { 20f, 40f, 60f, 80f, 100f, 125f };
    private const uint ObstacleCollisionMask = 8;

    // Which way this enemy has committed to going around its current obstacle (-1 left, +1 right,
    // 0 = not avoiding). Held across frames because re-deciding every frame makes enemies jitter
    // in place at a wall instead of actually committing to a way around it.
    private int _avoidanceSide = 0;

    // Transient shove applied on top of normal movement (currently from orbit blade hits), decaying
    // back to zero. Same shape as the player's own knockback in Player.cs.
    private Vector2 _knockbackVelocity = Vector2.Zero;
    private const float KnockbackDecay = 700f;

    public void ApplyKnockback(Vector2 impulse)
    {
        // Bosses hold their ground — getting shoved around by every blade/bullet hit reads as
        // flinching rather than as a boss, and it disrupts their own attack patterns.
        if (Category == EnemyCategory.Boss) return;
        _knockbackVelocity = impulse;
    }

    // Burn (Incendiario reward): damage over time applied by the player's bullets.
    private float _burnDps;
    private float _burnRemaining;
    private float _burnTickAccumulator;
    private bool _isBurning;
    private const float BurnTickInterval = 0.25f;
    private static readonly Color BurnTint = Palette.EnemyBurnTint;

    // Refreshes rather than stacks: a second hit takes the stronger DPS and restarts the clock at
    // whichever duration is longer. Stacking instances would make sustained fire scale burn
    // quadratically with fire rate, which dwarfs every other damage source almost immediately.
    public void ApplyBurn(float dps, float duration)
    {
        _burnDps = Mathf.Max(_burnDps, dps);
        _burnRemaining = Mathf.Max(_burnRemaining, duration);
    }

    private void TickBurn(float delta)
    {
        if (_burnRemaining <= 0f)
        {
            if (_isBurning)
            {
                _isBurning = false;
                Modulate = Colors.White;
            }
            return;
        }

        if (!_isBurning)
        {
            _isBurning = true;
            // Tints via Modulate, not the Visual's Color — the hit flash owns that property, and
            // the two would fight over it. Modulate multiplies on top, so they compose instead.
            Modulate = BurnTint;
        }

        _burnRemaining -= delta;
        _burnTickAccumulator += delta;
        if (_burnTickAccumulator < BurnTickInterval) return;

        int damage = Mathf.RoundToInt(_burnDps * _burnTickAccumulator);
        _burnTickAccumulator = 0f;

        // showFlash: false only — the flash+punch would strobe at 4 ticks/second, but the number
        // is exactly what tells the player Incendiario is doing anything, so it stays on.
        if (damage > 0) TakeDamage(damage, showFlash: false);
    }

    public int CurrentHp;

    // Set by whoever instantiates this enemy (EnemySpawner or a parent Split()), so a
    // splitting enemy can spawn more copies of itself without the scene referencing itself.
    public PackedScene SelfScene;

    private Node2D _player;
    private readonly Random _rng = new();

    // World drops, rolled independently on every kill. Loaded once via GD.Load rather than an
    // [Export] field so every enemy scene picks these up automatically, without hand-wiring the
    // reference into all eight enemy .tscn files.
    private static readonly PackedScene HeartPickupScene = GD.Load<PackedScene>("res://Scenes/Pickup/HeartPickup.tscn");
    private static readonly PackedScene ShieldPickupScene = GD.Load<PackedScene>("res://Scenes/Pickup/ShieldPickup.tscn");
    private const float HeartDropChance = 0.01f;
    private const float ShieldDropChance = 0.01f;

    // From round 11 on, both drops fall to a quarter of their early-game rate — by then the
    // player has usually accumulated enough Barrier/lives sources that the early rate would flood
    // the field with pickups.
    private const int LateDropRound = 11;
    private const float LateDropChance = 0.0015f;
    private Polygon2D _healthBarFill;
    private const float HealthBarWidth = 30f;
    private const float HealthBarHeight = 4f;

    // Hit-reaction state. The flash restores this exact colour rather than assuming one, since
    // every enemy scene picks its own.
    private Polygon2D _visual;
    private Color _visualBaseColor = Colors.White;
    private Tween _hitTween;
    private const float HitFlashDuration = 0.14f;
    private const float HitPunchScale = 1.3f;

    public override void _Ready()
    {
        AddToGroup("enemies");
        CurrentHp = MaxHp;
        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;

        _visual = GetNodeOrNull<Polygon2D>("Visual");
        if (_visual != null) _visualBaseColor = _visual.Color;

        _chaseAngleOffset = Mathf.DegToRad((float)(_rng.NextDouble() * 2.0 - 1.0) * MaxChaseAngleOffset);

        if (Category == EnemyCategory.Special || Category == EnemyCategory.Boss)
            CreateHealthBar();

        PlaySpawnAnimation();
    }

    // Enemies pop into existence rather than appearing on a frame boundary. Scales the Visual (not
    // the enemy) for the same reason PlayHitFeedback does: the enemy's own Scale carries the
    // Splitter's per-generation shrink.
    private void PlaySpawnAnimation()
    {
        if (_visual == null) return;

        _visual.Scale = Vector2.Zero;
        _visual.Rotation = -Mathf.Pi / 4f;

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_visual, "scale", Vector2.One, 0.28f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_visual, "rotation", 0f, 0.28f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private void CreateHealthBar()
    {
        var barPos = new Vector2(-HealthBarWidth / 2f, HealthBarOffset);

        var background = new Polygon2D();
        background.Polygon = RectPoints(HealthBarWidth, HealthBarHeight);
        background.Color = Palette.HealthBarBg;
        background.Position = barPos;
        AddChild(background);

        _healthBarFill = new Polygon2D();
        _healthBarFill.Polygon = RectPoints(HealthBarWidth, HealthBarHeight);
        _healthBarFill.Color = Palette.HealthBarFill;
        _healthBarFill.Position = barPos;
        AddChild(_healthBarFill);
    }

    private static Vector2[] RectPoints(float width, float height) => new[]
    {
        Vector2.Zero,
        new Vector2(width, 0f),
        new Vector2(width, height),
        new Vector2(0f, height),
    };

    private void UpdateHealthBar()
    {
        if (_healthBarFill == null) return;
        float pct = Mathf.Clamp((float)CurrentHp / MaxHp, 0f, 1f);
        _healthBarFill.Polygon = RectPoints(HealthBarWidth * pct, HealthBarHeight);
    }

    private void SpawnDamageNumber(int amount)
    {
        var parent = GetParent();
        if (parent == null) return;

        var label = new Label();
        label.Text = $"-{amount}";
        label.AddThemeColorOverride("font_color", Palette.DamageNumber);
        label.AddThemeFontSizeOverride("font_size", 14);
        label.ZIndex = 10;
        parent.AddChild(label);
        label.GlobalPosition = GlobalPosition + new Vector2(-8f, HealthBarOffset - 14f);

        var tween = label.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", label.Position + new Vector2(0f, -20f), 0.5f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 0.4f).SetDelay(0.1f);
        tween.Chain().TweenCallback(Callable.From(() => label.QueueFree()));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            if (_player == null) return;
        }

        // Ticked before movement so a burn kill skips the rest of this frame's steering work —
        // TakeDamage can QueueFree us from in here.
        TickBurn((float)delta);
        if (CurrentHp <= 0) return;

        float speedMult = GameManager.Instance?.EnemySpeedMultiplier ?? 1f;
        Vector2 chaseDir = (_player.GlobalPosition - GlobalPosition).Normalized().Rotated(_chaseAngleOffset);

        // Steer around obstacles rather than into them. MoveAndSlide alone isn't enough: it only
        // slides when the velocity has a tangential component, so an enemy meeting a wall face
        // head-on resolves to ~zero slide and just grinds against it forever.
        Vector2 moveDir = ComputeAvoidanceDirection(chaseDir);
        Vector2 separation = ComputeSeparation();

        _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, KnockbackDecay * (float)delta);

        // Knockback sits outside the speedMult so a Zona Lenta ultimate doesn't also weaken the
        // shove — it's an impulse from the player's weapon, not part of the enemy's own movement.
        Velocity = (moveDir * MoveSpeed + separation) * speedMult + _knockbackVelocity;
        MoveAndSlide();
    }

    // Straight at the player when the way is clear; otherwise the smallest detour that isn't
    // blocked, committed to one side so the path around reads as deliberate.
    private Vector2 ComputeAvoidanceDirection(Vector2 desired)
    {
        if (!IsPathBlocked(desired))
        {
            _avoidanceSide = 0;
            return desired;
        }

        if (_avoidanceSide == 0)
            _avoidanceSide = PickAvoidanceSide(desired);

        foreach (float degrees in AvoidanceAngles)
        {
            Vector2 candidate = desired.Rotated(Mathf.DegToRad(degrees) * _avoidanceSide);
            if (!IsPathBlocked(candidate)) return candidate;
        }

        // Every detour on this side is blocked (inside a corner, or wedged between obstacles).
        // Flip the commitment so the next frame explores the other way out, and meanwhile slide
        // straight along the wall instead of pushing into it.
        _avoidanceSide = -_avoidanceSide;
        return desired.Rotated(Mathf.Pi / 2f * _avoidanceSide);
    }

    private int PickAvoidanceSide(Vector2 desired)
    {
        bool leftClear = !IsPathBlocked(desired.Rotated(-Mathf.Pi / 2f));
        bool rightClear = !IsPathBlocked(desired.Rotated(Mathf.Pi / 2f));

        if (leftClear && !rightClear) return -1;
        if (rightClear && !leftClear) return 1;
        return _rng.NextDouble() < 0.5 ? -1 : 1;
    }

    private bool IsPathBlocked(Vector2 direction)
    {
        var query = PhysicsRayQueryParameters2D.Create(
            GlobalPosition,
            GlobalPosition + direction.Normalized() * AvoidanceProbeDistance,
            ObstacleCollisionMask);

        return GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0;
    }

    private Vector2 ComputeSeparation()
    {
        Vector2 push = Vector2.Zero;
        foreach (var n in GetTree().GetNodesInGroup("enemies"))
        {
            if (n == this || n is not Enemy other || !IsInstanceValid(other)) continue;

            Vector2 offset = GlobalPosition - other.GlobalPosition;
            float distSq = offset.LengthSquared();
            float minDist = SeparationRadius;
            if (distSq > 0f && distSq < minDist * minDist)
            {
                float dist = Mathf.Sqrt(distSq);
                push += offset / dist * (minDist - dist) / minDist;
            }
        }

        // Clamped before scaling: push accumulates one term per crowding neighbour, so deep inside
        // a pack the raw sum can get large enough to fling an enemy across the arena. Capping the
        // direction vector at unit length keeps the shove at exactly SeparationStrength at most,
        // which is comparable to move speed — firm, but never launching.
        return push.LimitLength(1f) * SeparationStrength;
    }

    // Split into two independent flags rather than one showFeedback bool: burn wants the damage
    // number (it's real, useful information — how else would you know Incendiario is doing
    // anything) but NOT the white flash + scale punch, which at 4 ticks/second would strobe rather
    // than read as a hit landing. Burn was passing showFeedback: false for both together, which
    // silently meant burn damage never showed a number at all.
    //
    // The damage number shows on every enemy, not just Special/Boss — that gate used to exist
    // because it doubled as the health bar's own reveal condition, but "does this enemy have a
    // health bar" and "should this hit show a number" are different questions, and leaving
    // Common/Rare hits silent was the main reason damage feedback read as invisible or, worse,
    // got mistaken for something happening to the player's own stats instead of the enemy's.
    public void TakeDamage(int amount, bool showFlash = true, bool showNumber = true)
    {
        if (showNumber)
        {
            int actualDamage = Mathf.Min(amount, Mathf.Max(CurrentHp, 0));
            if (actualDamage > 0)
                SpawnDamageNumber(actualDamage);
        }

        CurrentHp -= amount;
        UpdateHealthBar();

        if (CurrentHp <= 0)
        {
            bool willSplit = CanSplit && SplitGeneration < MaxSplitGenerations && SelfScene != null;
            if (willSplit)
                Split();

            // Killing a split that spawns more copies doesn't finish anything yet — only the
            // final generation (the one that doesn't split further) grants XP. Score is synced to
            // XP so it's withheld the same way; Coins still pay out on every kill along the way.
            int xpToGive = willSplit ? 0 : XpReward;
            GameManager.Instance?.RegisterKill(xpToGive, CoinsReward, Category);

            // Score is synced 1:1 with XP now, so a mid-chain Splitter kill (xpToGive == 0 until
            // the final generation) correctly shows no popup either — only the terminal kill does.
            if (xpToGive > 0)
                SpawnScorePopup(xpToGive);

            TryDropPickup();
            QueueFree();
        }
        else if (showFlash)
        {
            PlayHitFeedback();
        }
    }

    // Brief white flash + scale punch so a hit that doesn't kill still reads as having landed.
    // Skipped entirely on the killing blow — the enemy is freed on the same frame, so there'd be
    // nothing left to animate.
    private void PlayHitFeedback()
    {
        if (_visual == null) return;

        // Rapid hits (a laser volley, a blade sweep) would otherwise stack tweens that fight over
        // the same two properties and can leave the enemy stuck mid-flash or mid-punch.
        if (_hitTween != null && _hitTween.IsValid()) _hitTween.Kill();

        _visual.Color = Colors.White;
        _visual.Scale = Vector2.One * HitPunchScale;

        // Tweens the Visual child, not the enemy itself — the enemy's own Scale carries the
        // Splitter's per-generation shrink and must not be clobbered.
        _hitTween = CreateTween();
        _hitTween.SetParallel(true);
        _hitTween.TweenProperty(_visual, "color", _visualBaseColor, HitFlashDuration);
        _hitTween.TweenProperty(_visual, "scale", Vector2.One, HitFlashDuration)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void SpawnScorePopup(int amount)
    {
        var parent = GetParent();
        if (parent == null) return;

        var label = new Label();
        label.Text = $"+{amount}";
        label.AddThemeColorOverride("font_color", Palette.ScorePopup);
        label.AddThemeFontSizeOverride("font_size", 20);
        label.ZIndex = 10;
        parent.AddChild(label);
        label.GlobalPosition = GlobalPosition - new Vector2(10f, 10f);

        var tween = label.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", label.Position + new Vector2(0f, -40f), 0.8f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 0.6f).SetDelay(0.2f);
        tween.Chain().TweenCallback(Callable.From(() => label.QueueFree()));
    }

    // Rolled independently for every kill, split generations included (same as Coins already
    // paying out on every generation) — simplest reading of "enemies drop this", not just bosses
    // or only final-generation kills.
    private void TryDropPickup()
    {
        var parent = GetParent();
        if (parent == null) return;

        bool isLateRound = (GameManager.Instance?.RoundNumber ?? 1) >= LateDropRound;
        float heartChance = isLateRound ? LateDropChance : HeartDropChance;
        float shieldChance = isLateRound ? LateDropChance : ShieldDropChance;

        if (HeartPickupScene != null && _rng.NextDouble() < heartChance)
        {
            var heart = HeartPickupScene.Instantiate<Node2D>();
            parent.AddChild(heart);
            heart.GlobalPosition = GlobalPosition;
        }

        // Shield drops only matter to a player who's actually bought a Barrier — otherwise it'd
        // land in the world with nothing to do.
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (ShieldPickupScene != null && player != null && player.MaxShieldCharges > 0
            && _rng.NextDouble() < shieldChance)
        {
            var shield = ShieldPickupScene.Instantiate<Node2D>();
            parent.AddChild(shield);
            shield.GlobalPosition = GlobalPosition;
        }
    }

    private void Split()
    {
        // Evenly spaced around a random starting angle (rather than each child rolling its own
        // independent random angle) so two children can never land close together by bad luck —
        // they always end up visibly separated, not stacked on the parent's spot.
        float baseAngle = (float)(_rng.NextDouble() * Mathf.Tau);
        float angleStep = Mathf.Tau / SplitCount;

        for (int i = 0; i < SplitCount; i++)
        {
            var child = SelfScene.Instantiate<Enemy>();
            child.SelfScene = SelfScene;
            child.CanSplit = CanSplit;
            child.SplitCount = SplitCount;
            child.SplitGeneration = SplitGeneration + 1;
            child.MaxSplitGenerations = MaxSplitGenerations;
            child.SplitHpScale = SplitHpScale;
            child.SplitVisualScale = SplitVisualScale;
            child.Category = Category;
            child.HealthBarOffset = HealthBarOffset;
            child.AvoidanceProbeDistance = AvoidanceProbeDistance;

            child.MaxHp = Mathf.Max(1, Mathf.RoundToInt(MaxHp * SplitHpScale));
            child.ContactDamage = ContactDamage;
            child.MoveSpeed = MoveSpeed;
            child.XpReward = Mathf.Max(1, XpReward / 2);
            child.CoinsReward = Mathf.Max(1, CoinsReward / 2);
            child.Scale = Scale * SplitVisualScale;

            float angle = baseAngle + angleStep * i;
            child.GlobalPosition = GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 22f;

            GetParent().AddChild(child);
        }
    }
}
