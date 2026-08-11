namespace ShooterLoop;

public partial class EnemySpawner : Node2D
{
    [Export] public PackedScene EnemyScene;
    [Export] public PackedScene TankScene;
    [Export] public PackedScene SpeedyScene;
    [Export] public PackedScene SplitterScene;
    [Export] public PackedScene ShooterScene;
    [Export] public PackedScene RareScene;
    [Export] public PackedScene HiddenScene;
    [Export] public PackedScene BossScene;
    // Must stay outside the visible area or enemies pop in on screen. At the 1152x648 viewport
    // with the camera's 0.85 zoom the visible half-diagonal is ~780px, so this leaves a margin.
    [Export] public float SpawnRadius = 900f;

    // All difficulty knobs are percentage curves off round 1 baseline: Evaluate(round) = Base + (round-1)*PerRound, clamped [Min, Max].
    // Tune these to change pacing without touching per-enemy nominal stats anywhere else.
    private static readonly RoundCurve SpawnIntervalCurve = new(0.6f, -0.035f, 0.12f, 0.6f);   // seconds between spawn ticks (lower = faster)
    private static readonly RoundCurve BurstCountCurve = new(1f, 0.35f, 1f, 8f);                // enemies spawned per tick
    private static readonly RoundCurve MaxConcurrentCurve = new(25f, 4f, 25f, 200f);           // cap on enemies alive at once

    // Late-game crowd bump on top of MaxConcurrentCurve: flat 1.0x through round 10, then a linear
    // ramp reaching 1.2x at round 13 and holding there. The odd-looking Base of 0.4 is what makes
    // the clamps land on exactly those rounds — Evaluate is Base + (round-1)*PerRound clamped to
    // [Min, Max], so it reads 1.0 at round 10, 1.067 at 11, 1.133 at 12, and pins to 1.2 from 13 on.
    private static readonly RoundCurve LateCrowdMultCurve = new(0.4f, 0.0667f, 1f, 1.2f);
    private static readonly RoundCurve SpecialChanceCurve = new(0.05f, 0.05f, 0.05f, 0.6f);    // chance a spawn is a special enemy
    private static readonly RoundCurve RareChanceCurve = new(0.15f, 0.02f, 0.15f, 0.35f);      // chance a spawn (that isn't hidden/special) is rare
    private const float HiddenChance = 0.05f;                                                   // flat 5% per user request, not round-scaled
    // A linear RoundCurve can't express "extra gentle for the first couple of rounds, then rejoin
    // the normal ramp" — that shape is piecewise by definition, and re-sloping SpawnIntervalCurve
    // to soften the opening would drag every later round along with it. So the opening rounds get
    // an explicit multiplier on the spawn interval instead, tapering back to 1x by round 3 where
    // the normal curve takes over untouched. Purely an onboarding cushion for new players.
    private static readonly float[] EarlyRoundIntervalMult = { 1.6f, 1.25f };

    // Mirror of the above at the other end of the run: from this round on the concurrent cap gets a
    // flat bump, so the mid/late game keeps a denser screen than MaxConcurrentCurve's linear growth
    // alone provides. Applied *after* the curve's own clamp, so it lifts the eventual 200 ceiling to
    // 240 too rather than quietly doing nothing once the curve saturates.
    private const int LateRoundStart = 11;
    private const float LateRoundConcurrentMult = 1.2f;

    private static readonly RoundCurve HpMultCurve = new(1f, 0.25f, 1f, 10f);
    private static readonly RoundCurve DmgMultCurve = new(1f, 0.22f, 1f, 10f);
    private static readonly RoundCurve SpeedMultCurve = new(1f, 0.06f, 1f, 2.2f);
    private static readonly RoundCurve RewardMultCurve = new(1f, 0.25f, 1f, 12f);

    private Timer _spawnTimer;
    private Node2D _enemiesContainer;
    private Node2D _player;
    private readonly Random _rng = new();

    private float _specialChance;
    private float _rareChance;
    private int _maxConcurrentEnemies;
    private int _burstCount = 1;
    private float _hpMult = 1f;
    private float _dmgMult = 1f;
    private float _speedMult = 1f;
    private float _rewardMult = 1f;

    // Last evaluated adaptive-difficulty multiplier — surfaced for the pause menu's stat readout
    // so the system is inspectable rather than invisible.
    public float CatchUpMultiplier => _catchUpMult;
    private float _catchUpMult = 1f;

    public override void _Ready()
    {
        AddToGroup("enemy_spawner");

        _spawnTimer = GetNode<Timer>("SpawnTimer");
        _spawnTimer.Timeout += OnSpawnTimeout;

        _enemiesContainer = GetTree().CurrentScene.GetNode<Node2D>("Enemies");
        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;

        ConfigureForRound(1);
    }

    // A Timer only pauses (freezes its remaining time) when the tree pauses — it doesn't reset.
    // Left running, it can have almost no time left by the time the round-end shop closes and the
    // tree unpauses, firing an immediate spawn burst that ignores the round-start countdown
    // entirely. GameManager.EndRound() calls this so spawning stays off until the countdown
    // elapses and ConfigureForRound/ConfigureForBossRound explicitly restarts it.
    public void StopSpawning() => _spawnTimer.Stop();

    public void ConfigureForRound(int round)
    {
        _spawnTimer.WaitTime = SpawnIntervalCurve.Evaluate(round) * GetEarlyRoundIntervalMult(round);
        _burstCount = Mathf.RoundToInt(BurstCountCurve.Evaluate(round));
        _specialChance = SpecialChanceCurve.Evaluate(round);
        _rareChance = RareChanceCurve.Evaluate(round);
        _maxConcurrentEnemies = Mathf.RoundToInt(MaxConcurrentCurve.Evaluate(round) * LateCrowdMultCurve.Evaluate(round));

        EvaluateStatCurves(round);

        _spawnTimer.Start();
    }

    private static float GetEarlyRoundIntervalMult(int round)
    {
        int index = round - 1;
        return index >= 0 && index < EarlyRoundIntervalMult.Length ? EarlyRoundIntervalMult[index] : 1f;
    }

    // The round-indexed HP/damage curves assume the player's power grows on a predictable
    // schedule; DifficultyBalancer measures whether it actually did and scales those two (only)
    // up if the player has run far ahead. Speed and reward payout are deliberately left alone —
    // an over-powered player shouldn't earn less per kill for the same work. Evaluated once per
    // round here, alongside every other curve, rather than per-spawn.
    private void EvaluateStatCurves(int round)
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        _catchUpMult = player != null
            ? DifficultyBalancer.GetCatchUpMultiplier(player.GetOffensivePower(), round)
            : 1f;

        _hpMult = HpMultCurve.Evaluate(round) * _catchUpMult;
        _dmgMult = DmgMultCurve.Evaluate(round) * _catchUpMult;
        _speedMult = SpeedMultCurve.Evaluate(round);
        _rewardMult = RewardMultCurve.Evaluate(round);
    }

    public void ConfigureForBossRound(int round)
    {
        _spawnTimer.Stop();

        foreach (Node child in _enemiesContainer.GetChildren())
            child.QueueFree();

        EvaluateStatCurves(round);

        SpawnOne(BossScene);
    }

    private void OnSpawnTimeout()
    {
        if (EnemyScene == null) return;

        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            if (_player == null) return;
        }

        // Count the "enemies" group, NOT _enemiesContainer.GetChildCount(). Enemies parent their
        // score popups, damage numbers, and fired bullets into that same container, so the child
        // count silently includes transient VFX — which made the cap bind far earlier than its
        // number suggests, worst exactly in the busy late rounds where it matters most.
        // Read once per tick rather than per burst item, then tracked locally.
        int aliveEnemies = GetTree().GetNodesInGroup("enemies").Count;

        for (int i = 0; i < _burstCount; i++)
        {
            if (aliveEnemies >= _maxConcurrentEnemies) return;
            SpawnOne(ChooseEnemyScene());
            aliveEnemies++;
        }
    }

    // Enemies must never appear inside the player's own fire-range ring — that reads as them
    // materializing on top of you. The ring grows with Fire Range upgrades, so the clearance is
    // derived from it at spawn time rather than trusting SpawnRadius to always be the larger of
    // the two.
    private const float SpawnClearanceMargin = 120f;
    private const int SpawnPositionAttempts = 8;

    private Vector2 ChooseSpawnPosition()
    {
        float distance = SpawnRadius;
        if (_player is Player player)
            distance = Mathf.Max(distance, player.FireRange + SpawnClearanceMargin);

        // Prefer a spot inside the arena: near an edge, a random angle can easily land outside it,
        // leaving the enemy with a long walk in from off-map. A handful of re-rolls almost always
        // finds an in-bounds angle; if none does (player cornered), fall back to the first pick and
        // let it walk in rather than skipping the spawn.
        Vector2 fallback = Vector2.Zero;
        for (int attempt = 0; attempt < SpawnPositionAttempts; attempt++)
        {
            float angle = (float)(_rng.NextDouble() * Mathf.Tau);
            Vector2 candidate = _player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            if (attempt == 0) fallback = candidate;
            if (IsInsideArena(candidate)) return candidate;
        }
        return fallback;
    }

    private bool IsInsideArena(Vector2 position)
    {
        if (_player is not Player player) return true;

        var extents = player.ArenaHalfExtents;
        return Mathf.Abs(position.X) <= extents.X && Mathf.Abs(position.Y) <= extents.Y;
    }

    private void SpawnOne(PackedScene scene)
    {
        if (scene == null || _player == null) return;

        Vector2 spawnPos = ChooseSpawnPosition();

        var enemy = scene.Instantiate<Enemy>();
        enemy.SelfScene = scene;
        enemy.MaxHp = Mathf.RoundToInt(enemy.MaxHp * _hpMult);
        enemy.ContactDamage = Mathf.RoundToInt(enemy.ContactDamage * _dmgMult);
        enemy.MoveSpeed *= _speedMult;
        enemy.XpReward = Mathf.RoundToInt(enemy.XpReward * _rewardMult);
        enemy.CoinsReward = Mathf.RoundToInt(enemy.CoinsReward * _rewardMult);
        enemy.GlobalPosition = spawnPos;

        _enemiesContainer.AddChild(enemy);
    }

    // Sequential independent rolls, rarest first: Hidden is a flat 5% regardless of anything
    // else, then Special, then Rare, else Common. Not a normalized single distribution — simple
    // to tune per-tier without the categories needing to sum to 100%.
    private PackedScene ChooseEnemyScene()
    {
        if (HiddenScene != null && _rng.NextDouble() < HiddenChance)
            return HiddenScene;

        if (_rng.NextDouble() < _specialChance)
        {
            var specials = new List<PackedScene>();
            if (TankScene != null) specials.Add(TankScene);
            if (SpeedyScene != null) specials.Add(SpeedyScene);
            if (SplitterScene != null) specials.Add(SplitterScene);
            if (ShooterScene != null) specials.Add(ShooterScene);
            if (specials.Count > 0) return specials[_rng.Next(specials.Count)];
        }

        if (RareScene != null && _rng.NextDouble() < _rareChance)
            return RareScene;

        return EnemyScene;
    }
}
