namespace ShooterLoop;

// Orchestrates the whole "the game is getting dangerous" presentation layer: arena tone, alarm bars,
// threat callouts, boss-arrival punctuation. Reads every number from DangerLevel and pushes it at the
// nodes that own each visual, resolving them by group the way everything else in this project does
// cross-tree lookups.
//
// Deliberately the ONLY subscriber to GameManager.RoundChanged for this feature. GameManager is a
// persistent autoload while these scene nodes are freed on ReloadCurrentScene(), so every subscription
// is a potential dangling-delegate crash (see the comment in HUD._ExitTree — this bit the project
// before). One subscriber means one place that has to get the unsubscribe right.
public partial class DangerDirector : Node
{
    private Tween _backdropTween;
    private Tween _boundsTween;

    public override void _Ready()
    {
        AddToGroup("danger_director");

        if (GameManager.Instance == null) return;

        GameManager.Instance.RoundChanged += Apply;

        // Applied synchronously here rather than waiting for the next RoundChanged: on a mid-run
        // ReloadCurrentScene() (or any scene rebuild) the round number is already high, and without
        // this the arena would come back wearing round-1 colours until the following round ticked over.
        Apply(GameManager.Instance.RoundNumber);
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.RoundChanged -= Apply;
    }

    private void Apply(int round)
    {
        float danger = DangerLevel.ForRound(round);

        ApplyBackdrop(danger);
        ApplyBoundsTint(danger);

        if (GetTree().GetFirstNodeInGroup("danger_overlay") is DangerOverlay overlay)
            overlay.SetDanger(danger);
    }

    // Called from GameManager while the pre-round countdown runs, so the alarm screams before a boss
    // round rather than only reacting once the boss is already on screen.
    public void SetBossAlert(bool active)
    {
        if (GetTree().GetFirstNodeInGroup("danger_overlay") is DangerOverlay overlay)
            overlay.SetBossAlert(active);
    }

    public void AnnounceThreat(string text)
    {
        if (GetTree().GetFirstNodeInGroup("danger_overlay") is DangerOverlay overlay)
            overlay.AnnounceThreat(text);
    }

    private void ApplyBackdrop(float danger)
    {
        // Every tween is killed before being rebuilt — Godot leaves prior tweens on the same property
        // running, and two colour tweens fighting over the backdrop is a visible flicker.
        _backdropTween?.Kill();
        _backdropTween = null;

        if (GetTree().GetFirstNodeInGroup("backdrop") is not ColorRect backdrop) return;

        Color target = DangerLevel.BackdropColor(danger);
        _backdropTween = CreateTween();

        // At max danger the floor stops holding a flat colour and breathes instead, so the late game
        // never settles into looking static. This is its own two-leg loop rather than something chained
        // onto the entry lerp: SetLoops() replays the *whole* tween, so a chained version would replay
        // the entry lerp every cycle and stall the heartbeat with a dead 1.5s hold each beat. The first
        // leg doubles as the transition in from whatever colour the backdrop currently holds.
        // Reduced motion holds the flat danger colour instead of breathing. This is a full-viewport
        // loop with nowhere to look away to, so it's skipped entirely rather than slowed — the
        // backdrop still reaches its max-danger tone via the plain lerp below, so the escalation
        // still reads, it just stops moving.
        if (DangerLevel.HeartbeatActive(danger) && !DangerLevel.Reduced)
        {
            _backdropTween.SetLoops();
            _backdropTween.TweenProperty(backdrop, "color", DangerLevel.BackdropHeartbeatColor(), DangerLevel.HeartbeatLegDuration)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            _backdropTween.TweenProperty(backdrop, "color", target, DangerLevel.HeartbeatLegDuration)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            return;
        }

        // A plain colour lerp, never a sequenced tween with TweenInterval: RoundChanged fires from
        // GameManager.StartNextRound, which only calls Resume() on its very last line, so anything
        // created in this handler is born while the tree is still paused.
        _backdropTween.TweenProperty(backdrop, "color", target, DangerLevel.TintTweenDuration)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    // The arena wall, its grid children (which inherit the parent's Modulate, so one property covers
    // the whole grid), and every obstacle, all shifted by the same amount so the map reads as one
    // space rather than a red floor with untouched furniture on it.
    private void ApplyBoundsTint(float danger)
    {
        _boundsTween?.Kill();
        _boundsTween = null;

        Color tint = DangerLevel.BoundsModulate(danger);
        _boundsTween = CreateTween();
        _boundsTween.SetParallel(true);

        bool any = false;

        if (GetTree().GetFirstNodeInGroup("arena_bounds") is CanvasItem bounds)
        {
            _boundsTween.TweenProperty(bounds, "modulate", tint, DangerLevel.TintTweenDuration)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            any = true;
        }

        foreach (var node in GetTree().GetNodesInGroup("obstacles"))
        {
            if (node is not CanvasItem obstacle || !IsInstanceValid(obstacle)) continue;
            _boundsTween.TweenProperty(obstacle, "modulate", tint, DangerLevel.TintTweenDuration)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            any = true;
        }

        // An empty Tween never finishes and would leak; kill it rather than leave it parked.
        if (!any)
        {
            _boundsTween.Kill();
            _boundsTween = null;
        }
    }
}
