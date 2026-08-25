namespace ShooterLoop;

// Shared animation ("juice") helpers. UIUtil.cs answers "what does it look like" (StyleBoxFlats);
// this answers "how does it move". Extracted from patterns that used to be hand-duplicated per
// screen — HUD.OnLevelsGained, Shop.SpawnSpentLabel and DangerOverlay.AnnounceThreat each built
// their own near-identical "floating label" tween, and HUD.PulseRoundTimer/UpdateKillStreak each
// hand-rolled the same scale-punch. New call sites should reach for a method here before writing
// another one-off Tween.
public static class Juice
{
    // Tracks the one active tween per target so a repeated call (e.g. two quick coin gains) kills
    // the previous tween instead of fighting it for the same property. Keyed by instance id rather
    // than holding a live reference, so a freed target just leaves a harmless stale entry — mirrors
    // the project's existing "_streakPulseTween?.Kill()" convention, generalized across callers.
    private static readonly Dictionary<ulong, Tween> _activeTweens = new();

    private static Tween StartTween(GodotObject target, Node tweenSource)
    {
        ulong id = target.GetInstanceId();
        if (_activeTweens.TryGetValue(id, out var existing) && GodotObject.IsInstanceValid(existing))
            existing.Kill();

        var tween = tweenSource.CreateTween();
        _activeTweens[id] = tween;
        return tween;
    }

    // Reduced motion degrades every helper below rather than disabling it: the movement goes away,
    // the information does not. A modal still appears, a changed number still gets a beat of
    // attention, a bar still ends up at the right value — they just stop scaling, overshooting,
    // drifting and shaking. Removing the feedback outright would trade an accessibility problem for
    // a discoverability one.
    //
    // Read through a property rather than captured, so toggling the setting takes effect immediately
    // on the next animation instead of at the next scene load.
    private static bool Reduced => DangerLevel.Reduced;

    // --- Modal open/close ---

    // Fades+scales a modal in from a slightly shrunk, transparent state. Sets Visible = true first
    // so the tween is on screen from frame one. Pass fromScale = 1f to opt out of the scale
    // component (e.g. a fading-in health bar reads oddly scaling up; it just wants the fade).
    public static Tween ModalIn(Control modal, float duration = 0.22f, float fromScale = 0.9f)
    {
        // Reduced motion keeps a short fade (which carries the "this just appeared" information) but
        // drops the scale and its Back overshoot entirely.
        if (Reduced) fromScale = 1f;

        modal.PivotOffset = modal.Size / 2f;
        modal.Scale = Vector2.One * fromScale;
        modal.Modulate = new Color(1f, 1f, 1f, 0f);
        modal.Visible = true;

        var tween = StartTween(modal, modal);
        tween.SetParallel(true);
        if (!Reduced)
        {
            tween.TweenProperty(modal, "scale", Vector2.One, duration)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        tween.TweenProperty(modal, "modulate:a", 1f, duration * 0.8f);
        return tween;
    }

    // Fades+scales a modal out, then hides it and calls onDone — the caller's existing
    // "Visible = false" line (and anything that used to run right after it) moves into onDone.
    public static Tween ModalOut(Control modal, Action onDone = null, float duration = 0.16f, float toScale = 0.94f)
    {
        modal.PivotOffset = modal.Size / 2f;

        var tween = StartTween(modal, modal);
        tween.SetParallel(true);
        if (!Reduced)
        {
            tween.TweenProperty(modal, "scale", Vector2.One * toScale, duration)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        }
        tween.TweenProperty(modal, "modulate:a", 0f, duration);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            modal.Visible = false;
            modal.Scale = Vector2.One;
            modal.Modulate = Colors.White;
            onDone?.Invoke();
        }));
        return tween;
    }

    // --- Floating label ---

    // Spawns a label that scales in, drifts up by driftY, then fades out and frees itself — the
    // "+N" / "−N" popup pattern used for level-ups, coin spends and threat callouts. Spawned as a
    // child of `parent` at `globalPosition`; the caller still decides where that is (usually next
    // to the stat label the change refers to), same as every original call site did.
    public static void FloatingLabel(Node parent, string text, Vector2 globalPosition, Color color,
        int fontSize = 16, float driftY = -20f, float holdBeforeFade = 0.35f, float lifetime = 0.7f)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 2);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.ZIndex = 20;
        parent.AddChild(label);
        label.GlobalPosition = globalPosition;

        var tween = label.CreateTween();
        tween.SetParallel(true);

        // Reduced motion: the label still appears, holds and fades — it just doesn't pop in or drift.
        // The text is the point; the movement is decoration.
        if (!Reduced)
        {
            label.Scale = Vector2.One * 1.3f;
            tween.TweenProperty(label, "scale", Vector2.One, 0.2f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(label, "position", label.Position + new Vector2(0f, driftY), lifetime)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }

        tween.TweenProperty(label, "modulate:a", 0f, Mathf.Max(0.05f, lifetime - holdBeforeFade)).SetDelay(holdBeforeFade);
        tween.Chain().TweenCallback(Callable.From(() => label.QueueFree()));
    }

    // --- Value pop ---

    // Punches a Control's scale up then eases it back to normal — the "this number just changed"
    // feedback already hand-written for the kill-streak label and the round timer. Works on any
    // Control, not just Labels (e.g. a whole icon or row).
    public static void ValuePop(Control target, float punchScale = 1.4f, float duration = 0.25f)
    {
        // Reduced motion swaps the scale punch for a brief brightness lift: the "look here, this
        // changed" cue survives without anything moving or resizing on screen.
        if (Reduced)
        {
            target.Scale = Vector2.One;
            var flash = StartTween(target, target);
            flash.TweenProperty(target, "modulate", new Color(1.4f, 1.4f, 1.4f), duration * 0.35f);
            flash.TweenProperty(target, "modulate", Colors.White, duration * 0.65f);
            return;
        }

        target.PivotOffset = target.Size / 2f;
        target.Scale = Vector2.One * punchScale;

        var tween = StartTween(target, target);
        tween.TweenProperty(target, "scale", Vector2.One, duration)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    // --- Progress bar fill ---

    // Eases a ProgressBar's Value toward toValue instead of snapping it, for the XP bar / boss HP
    // bar — both currently just assign .Value directly.
    public static Tween BarFill(ProgressBar bar, double toValue, float duration = 0.3f)
    {
        var tween = StartTween(bar, bar);
        tween.TweenProperty(bar, "value", toValue, duration)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        return tween;
    }

    // --- Shake ---

    // A short horizontal shake plus an optional colour flash, for "that didn't work" feedback
    // (e.g. CharacterCreator's validation error) rather than a plain Visible toggle.
    public static void Shake(Control target, float strength = 8f, float duration = 0.35f, Color? flashColor = null)
    {
        // Reduced motion keeps the colour flash (which is what actually says "this failed" / "you
        // took a hit") and drops the positional shake — the part that's genuinely uncomfortable for
        // motion-sensitive players and the least informative half of the effect.
        if (!Reduced)
        {
            Vector2 restPosition = target.Position;
            var tween = StartTween(target, target);

            int bounces = 5;
            for (int i = 0; i < bounces; i++)
            {
                float t = 1f - (float)i / bounces;
                float offset = strength * t * (i % 2 == 0 ? 1f : -1f);
                tween.TweenProperty(target, "position", restPosition + new Vector2(offset, 0f), duration / bounces);
            }
            tween.TweenProperty(target, "position", restPosition, duration / bounces);
        }

        if (flashColor.HasValue)
        {
            var flashTween = target.CreateTween();
            Color baseColor = target.Modulate;
            target.Modulate = flashColor.Value;
            flashTween.TweenProperty(target, "modulate", baseColor, duration);
        }
    }

    // --- Button feedback ---

    // Wires a small hover/press scale-pop onto an otherwise-default Godot Button, in the spirit of
    // RewardCard.PlayTouchFlare but sized for a plain button. Call once per button, in its owning
    // screen's _Ready — safe to call multiple times per button (each call just adds another pair of
    // handlers), so callers don't need to guard against double-wiring themselves... though they
    // still should, since duplicate handlers would double the pop.
    public static void WireButtonFeedback(Button button)
    {
        button.MouseEntered += () =>
        {
            if (!button.Disabled) ValuePop(button, 1.06f, 0.15f);
        };
        button.ButtonDown += () =>
        {
            if (!button.Disabled) ValuePop(button, 0.94f, 0.1f);
        };
    }
}
