namespace ShooterLoop;

// Runs the optional per-round modifier. One event (or none) is rolled when a round is queued up, announced
// once the countdown clears, and torn down when the round ends.
//
// Deliberately split across those three moments, mirroring how DangerDirector already handles the boss
// alarm: the roll has to happen early so anything that wants to know before the round starts can ask, but
// the announcement has to wait for the countdown or it shares the screen with it.
public partial class RoundEventDirector : Node
{
    private readonly Random _rng = new();

    public RoundEventKind Active { get; private set; } = RoundEventKind.None;

    private Timer _missileTimer;
    private Node2D _hazardParent;

    private const float MissileInterval = 3.4f;
    private const int MissileZonesPerWave = 3;
    private const float MissileSpreadRadius = 620f;

    private const int MineCount = 26;
    private const float MineSpawnClearance = 320f;   // never seeded on top of where the player starts

    // Enemies move half again as fast; the payout doubles to make the round a genuine trade rather than a
    // pure punishment.
    private const float FrenzySpeedMultiplier = 1.5f;
    private const float FrenzyRewardMultiplier = 2f;

    public override void _Ready() => AddToGroup("round_event_director");

    // Called from GameManager.StartNextRound, i.e. as the countdown begins.
    public void RollForRound(int round, bool isBossRound)
    {
        Active = isBossRound ? RoundEventKind.None : RoundEvents.Roll(_rng, round);
    }

    // Called from GameManager.BeginRoundAfterCountdown once the round genuinely starts.
    public void BeginActiveEvent()
    {
        if (Active == RoundEventKind.None) return;

        _hazardParent = GetTree().CurrentScene as Node2D;
        if (_hazardParent == null) return;

        switch (Active)
        {
            case RoundEventKind.MissileStrike: StartMissileStrike(); break;
            case RoundEventKind.Fog: AddHazard(new FogOverlay()); break;
            case RoundEventKind.ShrinkingZone: AddHazard(new ShrinkingZone()); break;
            case RoundEventKind.Minefield: SeedMinefield(); break;
            case RoundEventKind.Frenzy: StartFrenzy(); break;
        }

        string announcement = RoundEvents.Announcement(Active);
        if (announcement != null && GetTree().GetFirstNodeInGroup("danger_director") is DangerDirector director)
            director.AnnounceThreat(announcement);
    }

    // Called from GameManager.EndRound. Everything an event created lives in one group, so teardown is a
    // single sweep no matter which event ran — and the two global multipliers get reset unconditionally
    // rather than only on the Frenzy path, so a mid-round crash or reload can't leave them stuck.
    public void EndActiveEvent()
    {
        _missileTimer?.Stop();

        foreach (var node in GetTree().GetNodesInGroup("round_event_hazards"))
            node.QueueFree();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.EnemySpeedMultiplier = 1f;
            GameManager.Instance.EventRewardMultiplier = 1f;
        }

        Active = RoundEventKind.None;
    }

    private void AddHazard(Node2D hazard)
    {
        hazard.AddToGroup("round_event_hazards");
        _hazardParent.AddChild(hazard);
    }

    private void StartFrenzy()
    {
        // Reuses the same field the Zona Lenta ultimate drives, which is what makes it apply to enemies
        // spawned mid-round too. Consequence worth knowing: firing that ultimate during a Frenzy round
        // cancels the speed-up for its duration, then its own timer restores the multiplier to 1 rather
        // than back to Frenzy's value — an acceptable trade for not duplicating the whole mechanism.
        GameManager.Instance.EnemySpeedMultiplier = FrenzySpeedMultiplier;
        GameManager.Instance.EventRewardMultiplier = FrenzyRewardMultiplier;
    }

    private void StartMissileStrike()
    {
        if (_missileTimer == null)
        {
            _missileTimer = new Timer { WaitTime = MissileInterval };
            AddChild(_missileTimer);
            _missileTimer.Timeout += SpawnMissileWave;
        }
        _missileTimer.WaitTime = MissileInterval;
        _missileTimer.Start();
    }

    // Aimed around the player rather than at them: landing directly on top would be undodgeable, while
    // scattering nearby forces a repositioning decision.
    private void SpawnMissileWave()
    {
        if (GetTree().GetFirstNodeInGroup("player") is not Node2D player) return;
        if (_hazardParent == null || !IsInstanceValid(_hazardParent)) return;

        for (int i = 0; i < MissileZonesPerWave; i++)
        {
            float angle = (float)(_rng.NextDouble() * Mathf.Tau);
            float distance = (float)_rng.NextDouble() * MissileSpreadRadius;

            var zone = new MissileZone();
            AddHazard(zone);
            zone.GlobalPosition = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }
    }

    private void SeedMinefield()
    {
        if (GetTree().GetFirstNodeInGroup("player") is not Node2D player) return;

        var extents = player is Player p ? p.ArenaHalfExtents : new Vector2(2200f, 1400f);
        float clearanceSq = MineSpawnClearance * MineSpawnClearance;

        for (int i = 0; i < MineCount; i++)
        {
            var position = new Vector2(
                (float)(_rng.NextDouble() * 2.0 - 1.0) * extents.X,
                (float)(_rng.NextDouble() * 2.0 - 1.0) * extents.Y);

            // Skipped rather than re-rolled: one fewer mine is invisible to the player, whereas a mine
            // seeded under their feet detonates before they've had any chance to react to the announcement.
            if (position.DistanceSquaredTo(player.GlobalPosition) < clearanceSq) continue;

            var mine = new Mine();
            AddHazard(mine);
            mine.GlobalPosition = position;
        }
    }
}
