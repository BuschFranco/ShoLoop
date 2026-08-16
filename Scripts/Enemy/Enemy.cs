namespace ShooterLoop;

// Demon is appended rather than inserted: every enemy .tscn stores Category as an integer, so
// renumbering the existing members would silently reclassify all of them.
public enum EnemyCategory { Common, Rare, Special, Hidden, Boss, Demon }

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

    protected void TickBurn(float delta)
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

    // False for boss-round chaff past its rewarded quota: the enemy is still a full threat and still has
    // to be dealt with, it just pays nothing — no XP, no Score, no coins, and no XP/coin drops. Hearts
    // are the sole exception (see TryDropPickup), which turns the endless chaff from an XP farm into a
    // sustain source for a long boss fight.
    public bool GrantsRewards = true;

    private Node2D _player;
    private readonly Random _rng = new();

    // Read-only view of the same player reference _PhysicsProcess re-fetches every frame, plus the
    // re-fetch itself as a guard a subclass can call from its own overridden _PhysicsProcess (Boss
    // does, since it replaces this method's body entirely rather than calling base).
    protected Node2D PlayerNode => _player;

    protected bool EnsurePlayer()
    {
        if (_player == null || !IsInstanceValid(_player))
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        return _player != null;
    }

    // World drops, rolled independently on every kill. Loaded once via GD.Load rather than an
    // [Export] field so every enemy scene picks these up automatically, without hand-wiring the
    // reference into all eight enemy .tscn files.
    private static readonly PackedScene HeartPickupScene = GD.Load<PackedScene>("res://Scenes/Pickup/HeartPickup.tscn");
    private static readonly PackedScene ShieldPickupScene = GD.Load<PackedScene>("res://Scenes/Pickup/ShieldPickup.tscn");
    private static readonly PackedScene XpPickupScene = GD.Load<PackedScene>("res://Scenes/Pickup/XpPickup.tscn");
    private static readonly PackedScene CoinPickupScene = GD.Load<PackedScene>("res://Scenes/Pickup/CoinPickup.tscn");
    private const float HeartDropChance = 0.01f;
    private const float ShieldDropChance = 0.01f;

    // Flat, not scaled down in late rounds like Heart/Shield above — these are a bonus on top of
    // the XP/Coins an enemy already grants on death, not a survival tool that needs rationing.
    private const float XpPickupDropChance = 0.02f;
    private const float CoinPickupDropChance = 0.02f;

    // Rounds 1-2 get a boosted rate instead: per-kill payout is at its lowest there (RewardMultCurve
    // is still ~1x), so at the flat rate the player reaches the first shop with almost nothing to
    // spend. Mirrors the late-round taper below, in the opposite direction.
    private const int EarlyDropRound = 3;   // exclusive — rounds 1 and 2 get the boost
    private const float EarlyXpCoinDropChance = 0.07f;

    // Round 1 gets a higher per-gem value (7 instead of 3) so the player starts with more buying
    // power. Rounds 2-4 keep the original flat value of 3 via EarlyCoinPickupValue below.
    private const int Round1CoinValueRound = 2;   // exclusive — only round 1
    private const int Round1CoinPickupValue = 7;

    // Rounds 1-4 get a flat coin value instead of the enemy's CoinsReward (which is still ~1 that
    // early since RewardMultCurve barely moves). Purely a per-gem value bump.
    private const int EarlyCoinValueRound = 5;   // exclusive — rounds 1-4 get the flat value
    private const int EarlyCoinPickupValue = 3;

    // Shields stop dropping entirely this late. By then a run either has a Barrier stack that makes
    // them redundant or has no Barrier at all (in which case the drop was already suppressed), and the
    // late game is meant to stop handing out defensive freebies.
    private const int NoShieldDropRound = 21;

    // From round 11 on, both drops fall to a quarter of their early-game rate — by then the
    // player has usually accumulated enough Barrier/lives sources that the early rate would flood
    // the field with pickups.
    private const int LateDropRound = 11;
    private const float LateDropChance = 0.0015f;

    // Deliberately far higher than any other drop rate: it's the only thing unrewarded boss-round chaff
    // gives, and it exists so a long boss fight is winnable by attrition rather than a slow loss.
    private const float UnrewardedHeartDropChance = 0.07f;
    private Polygon2D _healthBarFill;
    private const float HealthBarWidth = 30f;
    private const float HealthBarHeight = 4f;

    // Hit-reaction state. The flash restores this exact colour rather than assuming one, since
    // every enemy scene picks its own.
    private Polygon2D _visual;

    // Read-only access for a subclass's own telegraph tweens (e.g. Boss's charge windup), which
    // must animate Modulate rather than Color — Color is owned by the hit-flash tween above and
    // the two would fight over it if a subclass also drove Color.
    protected Polygon2D Visual => _visual;
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

        // Boss gets its own fixed bar on the HUD instead (see HUD.cs) — carrying this floating
        // sliver too would undercut the point of that bar being visually distinct. Demons are tanky
        // enough to need the same read Specials get.
        if (Category == EnemyCategory.Special || Category == EnemyCategory.Demon)
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
        if (!EnsurePlayer()) return;

        // Ticked before movement so a burn kill skips the rest of this frame's steering work —
        // TakeDamage can QueueFree us from in here.
        TickBurn((float)delta);
        if (CurrentHp <= 0) return;

        TickStragglerReposition((float)delta);

        FinishMovement(ComputeMoveDirection(delta), delta);
    }

    // Steer around obstacles rather than into them. MoveAndSlide alone isn't enough: it only slides when
    // the velocity has a tangential component, so an enemy meeting a wall face head-on resolves to ~zero
    // slide and just grinds against it forever.
    //
    // Virtual so a subclass can substitute its own steering (DemonStalker's lunge) without
    // re-implementing _PhysicsProcess and its burn/straggler guards. Boss predates this and overrides
    // _PhysicsProcess wholesale instead, which is why it doesn't get the straggler check — a boss
    // teleporting would be wrong anyway.
    protected virtual Vector2 ComputeMoveDirection(double delta) =>
        ComputeAvoidanceDirection(ChaseDirection());

    // Enemies that fall hopelessly behind used to trail the player forever, which turned the game into
    // an endless retreat: you could simply outrun the pack and never fight. Instead of deleting them (and
    // handing out free breathing room) they're moved back into the player's path, keeping every scrap of
    // their state — only GlobalPosition changes, so HP, burn, split generation and round scaling all
    // survive exactly as they were.
    //
    // 1100px is chosen to be well outside the camera: at 0.85 zoom the visible half-diagonal is ~780px,
    // so the relocation is never witnessed. Checked on an accumulator rather than every frame because a
    // group lookup plus a distance test per enemy per frame is real cost in a 60-enemy crowd.
    private const float StragglerDistance = 1100f;
    private const float StragglerCheckInterval = 0.5f;
    private float _stragglerAccumulator;

    private void TickStragglerReposition(float delta)
    {
        _stragglerAccumulator += delta;
        if (_stragglerAccumulator < StragglerCheckInterval) return;
        _stragglerAccumulator = 0f;

        float limitSq = StragglerDistance * StragglerDistance;
        if (GlobalPosition.DistanceSquaredTo(_player.GlobalPosition) < limitSq) return;

        if (GetTree().GetFirstNodeInGroup("enemy_spawner") is EnemySpawner spawner)
            GlobalPosition = spawner.PickInterceptPosition();
    }

    // Direction toward the player, plus this instance's persistent heading offset. Exposed so a
    // subclass (Boss) can fall back to normal chasing during its idle/cooldown sub-phases without
    // duplicating the offset/normalize logic.
    protected Vector2 ChaseDirection() =>
        (_player.GlobalPosition - GlobalPosition).Normalized().Rotated(_chaseAngleOffset);

    // Tail end of the movement step shared by every enemy: separation from neighbours, knockback
    // decay, and the final velocity assignment. Split out of _PhysicsProcess so Boss can override
    // the whole method (to swap in a dash/orbit direction for moveDir) while still funneling
    // through the exact same knockback/separation handling as everyone else.
    // No enemy may out-run the player in a *sustained* chase. A pursuer that is strictly faster and
    // homes in a straight line doesn't make contact likely, it makes it certain: no input avoids it.
    // That was the real state of things from round 8 on — Speedy's 205 base crosses the player's 290
    // as soon as SpeedMultCurve passes 1.41 — and Frenesí's ×1.5 dragged the crossing down to round 3,
    // so every Frenesí round shipped an enemy that could not be dodged, only out-damaged.
    //
    // Three details carry the fix:
    //   - It's a fraction of the player's *current* MoveSpeed, not a constant, so buying Move Speed
    //     widens the gap instead of leaving a stale hardcoded number behind.
    //   - It's applied *after* speedMult, which is the whole point: Frenesí can still speed up anything
    //     below the cap (a round-21 Grunt goes 198 -> 261) but can no longer lift anything past it.
    //     A Zona Lenta slow still lands, because Min() keeps whichever value is lower.
    //   - It caps the chase speed, NOT the final Velocity. moveDir arrives with its length encoding a
    //     committed dash (Boss's charge, DasherEnemy's burst), and that burst is deliberately the one
    //     thing allowed past the cap — briefly, and only after a telegraph. Clamping Velocity instead
    //     would silently neuter every dash in the game.
    //
    // Enemies whose base speed stays under the cap are untouched: the Boss at 55 base peaks at 121.
    private const float MaxChaseSpeedFraction = 0.9f;

    protected void FinishMovement(Vector2 moveDir, double delta)
    {
        float speedMult = GameManager.Instance?.EnemySpeedMultiplier ?? 1f;
        Vector2 separation = ComputeSeparation();

        _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, KnockbackDecay * (float)delta);

        float chaseSpeed = MoveSpeed * speedMult;
        if (_player is Player player)
            chaseSpeed = Mathf.Min(chaseSpeed, player.MoveSpeed * MaxChaseSpeedFraction);

        // Knockback sits outside the speedMult so a Zona Lenta ultimate doesn't also weaken the
        // shove — it's an impulse from the player's weapon, not part of the enemy's own movement.
        Velocity = moveDir * chaseSpeed + separation * speedMult + _knockbackVelocity;
        MoveAndSlide();
    }

    // Straight at the player when the way is clear; otherwise the smallest detour that isn't
    // blocked, committed to one side so the path around reads as deliberate.
    protected Vector2 ComputeAvoidanceDirection(Vector2 desired)
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

    protected bool IsPathBlocked(Vector2 direction) => IsPathBlocked(direction, AvoidanceProbeDistance);

    // The explicit-distance overload exists for Boss's dashes, which travel much further than
    // AvoidanceProbeDistance. Probing only the steering distance would clear a dash that then plows
    // straight through an obstacle in its second half — and raising AvoidanceProbeDistance instead
    // would make the boss's ordinary movement start swerving around walls from far too far away.
    protected bool IsPathBlocked(Vector2 direction, float distance)
    {
        var query = PhysicsRayQueryParameters2D.Create(
            GlobalPosition,
            GlobalPosition + direction.Normalized() * distance,
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
            // Unrewarded chaff still counts as a kill (it happened, and the HUD's total should say so) —
            // it just contributes no XP, Score or coins.
            int xpToGive = willSplit || !GrantsRewards ? 0 : XpReward;
            int coinsToGive = GrantsRewards ? CoinsReward : 0;
            GameManager.Instance?.RegisterKill(xpToGive, coinsToGive, Category);

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

        int round = GameManager.Instance?.RoundNumber ?? 1;

        // Unrewarded chaff drops hearts and nothing else. The rate is its own constant rather than the
        // normal one because the late-round taper below cuts hearts to 0.15%, and boss rounds are all
        // late — at that rate "only hearts" would have meant "nothing at all" in practice. This is what
        // makes a drawn-out boss fight survivable instead of just attritional.
        if (!GrantsRewards)
        {
            if (HeartPickupScene != null && _rng.NextDouble() < UnrewardedHeartDropChance)
            {
                var heartOnly = HeartPickupScene.Instantiate<Node2D>();
                parent.AddChild(heartOnly);
                heartOnly.GlobalPosition = GlobalPosition;
            }
            return;
        }

        bool isLateRound = round >= LateDropRound;
        float heartChance = isLateRound ? LateDropChance : HeartDropChance;
        float shieldChance = round >= NoShieldDropRound
            ? 0f
            : (isLateRound ? LateDropChance : ShieldDropChance);

        bool isEarlyRound = round < EarlyDropRound;
        float xpChance = isEarlyRound ? EarlyXpCoinDropChance : XpPickupDropChance;
        float coinChance = isEarlyRound ? EarlyXpCoinDropChance : CoinPickupDropChance;

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

        if (XpPickupScene != null && _rng.NextDouble() < xpChance)
        {
            var xp = XpPickupScene.Instantiate<XpPickup>();
            xp.XpAmount = Mathf.Max(1, XpReward);
            parent.AddChild(xp);
            xp.GlobalPosition = GlobalPosition;
        }

        if (CoinPickupScene != null && _rng.NextDouble() < coinChance)
        {
            var coin = CoinPickupScene.Instantiate<CoinPickup>();
            if (round < Round1CoinValueRound)
                coin.CoinAmount = Round1CoinPickupValue;
            else if (round < EarlyCoinValueRound)
                coin.CoinAmount = EarlyCoinPickupValue;
            else
                coin.CoinAmount = Mathf.Max(1, CoinsReward);
            parent.AddChild(coin);
            coin.GlobalPosition = GlobalPosition;
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
            child.GrantsRewards = GrantsRewards;   // an unrewarded parent can't launder XP through its children
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
