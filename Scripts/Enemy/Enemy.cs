namespace ShooterLoop;

// Demon is appended rather than inserted: every enemy .tscn stores Category as an integer, so
// renumbering the existing members would silently reclassify all of them.
public enum EnemyCategory { Common, Rare, Special, Hidden, Boss, Demon }

public enum EliteModifier { None, Vampiric, Shielded, Explosive, Fast, Regenerating }

public partial class Enemy : CharacterBody2D
{
    [Export] public float MoveSpeed = 90f;
    [Export] public int MaxHp = 30;
    [Export] public int XpReward = 1;
    [Export] public int CoinsReward = 1;
    [Export] public int ContactDamage = 10;
    [Export] public EnemyCategory Category = EnemyCategory.Common;

    // Elite system: certain enemies spawn with bonus stats and a modifier.
    public bool IsElite;
    public EliteModifier EliteModifier = EliteModifier.None;
    private float _regenTimer;
    private int _shieldCharges;

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

    // Elite system: apply bonus stats and visual indicator.
    public void MakeElite(EliteModifier modifier)
    {
        IsElite = true;
        EliteModifier = modifier;
        MaxHp = Mathf.RoundToInt(MaxHp * 1.5f);
        CurrentHp = MaxHp;
        MoveSpeed *= 1.2f;
        _shieldCharges = modifier == EliteModifier.Shielded ? 1 : 0;

        // Ensure health bar exists for elites.
        if (_healthBarFill == null)
            CreateHealthBar();

        // The glow is deliberately NOT built here. EnemySpawner calls this before adding the enemy
        // to the tree, so _Ready hasn't run and "Visual" cannot be resolved yet — the glow used to
        // sit behind an `if (_visual != null)` guard right here, which meant it never passed and
        // elites silently rendered with no glow at all. _Ready builds it instead, off IsElite.
    }

    // A tinted copy of the enemy's own silhouette sitting just behind it, so an elite reads as the
    // same creature with an aura rather than as a different enemy. Scaled off VisualBaseScale, not
    // Vector2.One: the sprite's on-screen size lives in the Visual's scale, so a hardcoded 1.15
    // would blow the halo up to the texture's full resolution and detach it from the enemy.
    private void CreateEliteGlow()
    {
        if (_visual == null) return;

        var glow = new Sprite2D();
        glow.Texture = _visual.Texture;
        glow.Scale = _visualBaseScale * EliteGlowScale;
        glow.Modulate = GetEliteColor(EliteModifier);
        glow.ZIndex = -1;
        AddChild(glow);

        CreateEliteMarker();
    }

    // A glyph over the enemy naming its modifier, because the aura's colour was the ONLY thing
    // distinguishing the five kinds — and Explosive (orange) versus Fast (yellow) are nearly the same
    // hue under common colour-vision deficiency, made worse by the WorldEnvironment's bloom washing
    // both toward white. Explosive means "killing this next to you hurts you", so guessing wrong on
    // that one costs a life. Now the colour is reinforcement rather than the whole signal.
    private void CreateEliteMarker()
    {
        var marker = new Label();
        marker.Text = GetEliteMarker(EliteModifier);
        marker.AddThemeColorOverride("font_color", Colors.White);
        marker.AddThemeColorOverride("font_outline_color", Colors.Black);
        marker.AddThemeConstantOverride("outline_size", 4);
        marker.AddThemeFontSizeOverride("font_size", 16);
        marker.ZIndex = 12;

        // Offset off HealthBarOffset like the damage numbers do, so it clears each enemy's own visual
        // radius rather than sitting on top of a big one and floating away from a small one.
        marker.Position = new Vector2(-6f, HealthBarOffset - 20f);
        AddChild(marker);
    }

    private static string GetEliteMarker(EliteModifier modifier) => modifier switch
    {
        EliteModifier.Vampiric => "V",
        EliteModifier.Shielded => "S",
        EliteModifier.Explosive => "!",   // the one that punishes killing it up close
        EliteModifier.Fast => "»",
        EliteModifier.Regenerating => "+",
        _ => "*",
    };

    private const float EliteGlowScale = 1.15f;

    private static Color GetEliteColor(EliteModifier modifier) => modifier switch
    {
        EliteModifier.Vampiric => new Color(1f, 0.2f, 0.2f, 0.7f),
        EliteModifier.Shielded => new Color(0.3f, 0.5f, 1f, 0.7f),
        EliteModifier.Explosive => new Color(1f, 0.6f, 0f, 0.7f),
        EliteModifier.Fast => new Color(1f, 1f, 0.2f, 0.7f),
        EliteModifier.Regenerating => new Color(0.2f, 1f, 0.3f, 0.7f),
        _ => new Color(1f, 1f, 1f, 0.5f),
    };

    // Called by the damage system; Shielded elites block one hit before taking damage.
    public bool TryBlockWithEliteShield()
    {
        if (_shieldCharges > 0)
        {
            _shieldCharges--;
            return true;
        }
        return false;
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
    private const float EarlyXpCoinDropChance = 0.09f;

    // Round 1 gets a higher per-gem value (7 instead of 3) so the player starts with more buying
    // power — but not so much that every shop item is instantly affordable. Rounds 2-4 keep the
    // original flat value of 3 via EarlyCoinPickupValue below.
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

    // Part of the same late-round survivability nerf as MaxLivesCap/LateShieldRegenRound: shields
    // taper a second time from round 17 on, on top of the round-11 taper above, before drying up
    // entirely at NoShieldDropRound. Hearts are untouched — this is shield-specific.
    private const int VeryLateShieldDropRound = 17;
    private const float VeryLateShieldDropChance = LateDropChance * 0.5f;

    // Deliberately far higher than any other drop rate: it's the only thing unrewarded boss-round chaff
    // gives, and it exists so a long boss fight is winnable by attrition rather than a slow loss.
    private const float UnrewardedHeartDropChance = 0.07f;
    private Polygon2D _healthBarFill;
    private const float HealthBarWidth = 30f;
    private const float HealthBarHeight = 4f;

    // Hit-reaction state. The flash restores this exact colour rather than assuming one, since
    // every enemy scene picks its own.
    private Sprite2D _visual;

    // Read-only access for a subclass's own telegraph tweens (e.g. Boss's charge windup), plus the
    // two resting values every such tween has to return to.
    //
    // Both snapshots exist because the Visual node's own Modulate and Scale are load-bearing now:
    // the scene stores the enemy's identity colour in Modulate and its on-screen size in Scale.
    // Anything that treats Colors.White or Vector2.One as "the resting state" silently destroys one
    // of the two — which is exactly how the boss ended up permanently white after its first dash,
    // and how every enemy ended up rendering at its texture's full 128px.
    protected Sprite2D Visual => _visual;
    protected Color VisualBaseColor => _visualBaseColor;
    protected Vector2 VisualBaseScale => _visualBaseScale;
    private Color _visualBaseColor = Colors.White;

    // Size comes from the scene's Scale (0.25 for every entity, against a 4x-supersampled SVG),
    // not from vertex coordinates the way it did when Visual was a Polygon2D. Snapshotted in
    // _Ready *before* PlaySpawnAnimation runs, since that animation is what would overwrite it.
    private Vector2 _visualBaseScale = Vector2.One;
    private Tween _hitTween;
    private const float HitFlashDuration = 0.14f;
    private const float HitPunchScale = 1.3f;

    public override void _Ready()
    {
        AddToGroup("enemies");
        CurrentHp = MaxHp;
        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;

        _visual = GetNodeOrNull<Sprite2D>("Visual");
        if (_visual != null)
        {
            // Juli's perk repaints the whole roster. Applied *before* the snapshot so green becomes
            // this enemy's identity colour outright — otherwise the hit flash and the dash telegraphs
            // would keep restoring the scene's original colour and undo it on the first hit.
            if (_player is Player tinter && tinter.EnemyTint.HasValue)
                _visual.Modulate = tinter.EnemyTint.Value;

            _visualBaseColor = _visual.Modulate;
            _visualBaseScale = _visual.Scale;
        }

        _chaseAngleOffset = Mathf.DegToRad((float)(_rng.NextDouble() * 2.0 - 1.0) * MaxChaseAngleOffset);

        // Boss gets its own fixed bar on the HUD instead (see HUD.cs) — carrying this floating
        // sliver too would undercut the point of that bar being visually distinct. Demons are tanky
        // enough to need the same read Specials get.
        // The null check matters for elites: MakeElite already built a bar before this node entered
        // the tree, and this call had no guard, so an elite Special/Demon got a second overlapping
        // pair of bars whose orphaned fill never tracked damage again.
        if (_healthBarFill == null
            && (Category == EnemyCategory.Special || Category == EnemyCategory.Demon))
            CreateHealthBar();

        if (IsElite) CreateEliteGlow();

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
        tween.TweenProperty(_visual, "scale", _visualBaseScale, 0.28f)
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

    // Shared "text drifts up and fades" routine for the callouts that announce a state change: the
    // boss's pattern telegraphs and the hack tell. SpawnDamageNumber and SpawnScorePopup below stay
    // separate on purpose — their drift and fade timings differ from this one (damage numbers are
    // deliberately fast because burn fires them four times a second and slow ones would pile up), so
    // folding them in here would mean four more parameters and no gain.
    protected void SpawnFloatingLabel(string text, Color color, int fontSize, Vector2 offset, int zIndex = 15)
    {
        var parent = GetParent();
        if (parent == null) return;

        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.ZIndex = zIndex;
        parent.AddChild(label);
        label.GlobalPosition = GlobalPosition + offset;

        var tween = label.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", label.Position + new Vector2(0f, -24f), 0.9f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 0.6f).SetDelay(0.4f);
        tween.Chain().TweenCallback(Callable.From(() => label.QueueFree()));
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

        // Elite regenerating modifier: heal 1 HP/sec.
        if (IsElite && EliteModifier == EliteModifier.Regenerating && CurrentHp < MaxHp)
        {
            _regenTimer += (float)delta;
            if (_regenTimer >= 1f)
            {
                _regenTimer -= 1f;
                CurrentHp = Mathf.Min(CurrentHp + 1, MaxHp);
            }
        }

        TickPeriodicChecks((float)delta);
        if (CurrentHp <= 0) return;

        // ComputeMoveDirection is called even while hacked, and its result thrown away. DemonStalker
        // decrements its lunge/cooldown timers *inside* that method, so skipping the call freezes them
        // — it would then finish the remainder of a lunge in a now-stale direction when the hack ends,
        // which reads as a teleport-slide. The cost of calling it is one telegraphed lunge that never
        // happens; at one second, that's the cheaper of the two.
        Vector2 moveDir = ComputeMoveDirection(delta);
        FinishMovement(IsHacked ? Vector2.Zero : moveDir, delta);
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
    private const float PeriodicCheckInterval = 0.5f;
    private float _periodicAccumulator;

    // Separation is the single biggest cost in a crowded arena — iterating all enemies for every
    // enemy every frame is O(n^2). Throttling to ~9 frames apart cuts that cost by ~83% with no
    // visible difference (separation is a soft shove, not frame-critical).
    private const float SeparationInterval = 0.15f;
    private float _separationAccumulator;
    private Vector2 _cachedSeparation;

    // Both checks below share one accumulator: each is a distance test per enemy, neither needs
    // frame precision, and doing them every frame across a 60-enemy crowd is real cost.
    private void TickPeriodicChecks(float delta)
    {
        _periodicAccumulator += delta;
        if (_periodicAccumulator < PeriodicCheckInterval) return;
        _periodicAccumulator = 0f;

        TickProximityPerks();
        if (CurrentHp <= 0) return;   // a suicide roll just killed us; nothing left to reposition

        RepositionIfStraggler();
    }

    private void RepositionIfStraggler()
    {
        float limitSq = StragglerDistance * StragglerDistance;
        if (GlobalPosition.DistanceSquaredTo(_player.GlobalPosition) < limitSq) return;

        if (GetTree().GetFirstNodeInGroup("enemy_spawner") is EnemySpawner spawner)
            GlobalPosition = spawner.PickInterceptPosition();
    }

    // "Hackeado" (Maxi's perk): this enemy is inoperable — it can't steer and, if it's a ShooterEnemy,
    // can't fire. It can still be shoved and still separates from its neighbours, so a frozen enemy is
    // a harmless obstacle rather than a rigid pillar the crowd stacks on.
    private float _hackedTimer;
    private const float HackDuration = 1f;
    public bool IsHacked => _hackedTimer > 0f;

    public void ApplyHack(float duration)
    {
        // Bosses are exempt. Not just for balance — a boss never runs the code that would tick this
        // timer down (Boss overrides _PhysicsProcess without calling base), so a hacked boss would be
        // frozen and unable to fire for the rest of the fight. See the note in FinishMovement.
        if (Category == EnemyCategory.Boss) return;

        // Refreshes rather than stacks, so overlapping hacks can't compound into a long freeze.
        _hackedTimer = Mathf.Max(_hackedTimer, duration);
        SpawnFloatingLabel("HACKEADO", Palette.Warning, 14, new Vector2(-30f, HealthBarOffset - 14f), zIndex: 12);
    }

    // Character perks that fire on closing with the player: Maxi hacks their ships, Nico L's presence
    // makes them give up on the spot.
    //
    // Rolled ONCE per enemy, not on a schedule. A repeating roll would scale with how long an enemy
    // loiters in range and with how many are alive at once — at a late-round cap of 24 enemies,
    // "1%" every half second is a hack roughly every two seconds, which is a permanent stun, not a
    // rare event. One roll per enemy ties the rate to enemies *spawned* instead, which is the thing
    // the 1% reads as.
    private bool _proximityPerkRolled;

    private void TickProximityPerks()
    {
        if (_proximityPerkRolled || _player is not Player player) return;
        if (player.EnemyHackChance <= 0f && player.EnemySuicideChance <= 0f) return;

        float range = player.EffectiveFireRange;
        if (GlobalPosition.DistanceSquaredTo(player.GlobalPosition) > range * range) return;

        _proximityPerkRolled = true;

        // Death goes through the normal damage path rather than QueueFree, so a humiliated enemy
        // still pays out its XP, score, coins and drops exactly like any other kill.
        if (player.EnemySuicideChance > 0f && _rng.NextDouble() < player.EnemySuicideChance)
        {
            TakeDamage(CurrentHp);
            return;
        }

        if (player.EnemyHackChance > 0f && _rng.NextDouble() < player.EnemyHackChance)
            ApplyHack(HackDuration);
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
        // Ticked here rather than in _PhysicsProcess because this is the one seam every subclass
        // funnels through — Boss overrides _PhysicsProcess without calling base, so a timer ticked
        // there would never run for a boss and a hacked one would stay frozen forever. ApplyHack
        // refuses bosses outright, but keeping the tick here means that invariant isn't the only
        // thing standing between a future change and an unwinnable encounter.
        if (_hackedTimer > 0f) _hackedTimer -= (float)delta;

        float speedMult = GameManager.Instance?.EnemySpeedMultiplier ?? 1f;

        _separationAccumulator += (float)delta;
        if (_separationAccumulator >= SeparationInterval)
        {
            _separationAccumulator = 0f;
            _cachedSeparation = ComputeSeparation();
        }

        _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, KnockbackDecay * (float)delta);

        float chaseSpeed = MoveSpeed * speedMult;
        if (_player is Player player)
            chaseSpeed = Mathf.Min(chaseSpeed, player.MoveSpeed * MaxChaseSpeedFraction);

        // Knockback sits outside the speedMult so a Zona Lenta ultimate doesn't also weaken the
        // shove — it's an impulse from the player's weapon, not part of the enemy's own movement.
        Velocity = moveDir * chaseSpeed + _cachedSeparation * speedMult + _knockbackVelocity;
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
            int finalXp = GameManager.Instance?.RegisterKill(xpToGive, coinsToGive, Category) ?? xpToGive;

            // Score is synced 1:1 with XP now, so a mid-chain Splitter kill (xpToGive == 0 until
            // the final generation) correctly shows no popup either — only the terminal kill does.
            // Shows the actual XP gained after streak/wisdom multipliers.
            if (finalXp > 0)
                SpawnScorePopup(finalXp);

            TryDropPickup();

            // Explosive elite: damage nearby enemies and the player on death.
            if (IsElite && EliteModifier == EliteModifier.Explosive)
            {
                const float explosionRadius = 80f;
                var entities = GetTree().GetNodesInGroup("enemies");
                foreach (var node in entities)
                {
                    if (node is Enemy e && e != this && !e.IsQueuedForDeletion())
                    {
                        float dist = GlobalPosition.DistanceTo(e.GlobalPosition);
                        if (dist <= explosionRadius)
                            e.TakeDamage(20);
                    }
                }
                // Damage the player too if in range.
                var player = GetTree().GetFirstNodeInGroup("player") as Player;
                if (player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= explosionRadius)
                    player.TakeHit(GlobalPosition);
            }

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

        _visual.Modulate = Colors.White;
        _visual.Scale = _visualBaseScale * HitPunchScale;

        // Tweens the Visual child, not the enemy itself — the enemy's own Scale carries the
        // Splitter's per-generation shrink and must not be clobbered.
        _hitTween = CreateTween();
        _hitTween.SetParallel(true);
        _hitTween.TweenProperty(_visual, "modulate", _visualBaseColor, HitFlashDuration);
        _hitTween.TweenProperty(_visual, "scale", _visualBaseScale, HitFlashDuration)
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
            : round >= VeryLateShieldDropRound
                ? VeryLateShieldDropChance
                : (isLateRound ? LateDropChance : ShieldDropChance);

        bool isEarlyRound = round < EarlyDropRound;
        float xpChance = isEarlyRound ? EarlyXpCoinDropChance : XpPickupDropChance;
        float coinChance = isEarlyRound ? EarlyXpCoinDropChance : CoinPickupDropChance;

        // A character perk (Manu's "mano larga") stacks straight onto the chance rather than scaling
        // it, so it stays worth the same in every round instead of being swallowed by the early-round
        // boost above. Clamped because chance and certainty are not the same thing.
        if (_player is Player thief)
            coinChance = Mathf.Min(coinChance + thief.CoinDropBonus, 1f);

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
