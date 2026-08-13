namespace ShooterLoop;

// The screen-space half of the danger escalation: pulsing alarm bars along the four screen edges, the
// pre-boss alert burst, and the transient threat-level callouts. Driven entirely by DangerDirector —
// this node subscribes to nothing and holds no round state of its own.
//
// Lives on its own CanvasLayer (layer 2 in Arena.tscn): above every world Node2D at layer 0, below the
// HUD at layer 5. That matters for the pause interaction — every modal (shop/pause/upgrades/game over)
// sits at layer 8 and up, so when the tree pauses and these tweens freeze mid-pulse, the frozen bars
// are completely hidden behind whatever modal is open. Deliberately NOT ProcessMode.Always: an alarm
// that kept strobing behind a shop screen the player is reading would be both worse-looking and a
// photosensitivity liability.
//
// The bars are built in code rather than authored in the .tscn to match how every other visual in this
// project is made, and so their thicknesses/colour stay owned by DangerLevel instead of being a second
// copy in a scene file.
public partial class DangerOverlay : Control
{
    private Control _bars;
    private Tween _pulseTween;
    private float _danger;
    private bool _bossAlert;

    public override void _Ready()
    {
        AddToGroup("danger_overlay");
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);

        // Wrapped in their own container so the pulse can drive one Modulate for all four bars without
        // also tinting the threat callout labels, which are siblings of this container.
        _bars = new Control();
        _bars.MouseFilter = MouseFilterEnum.Ignore;
        _bars.SetAnchorsPreset(LayoutPreset.FullRect);
        _bars.Visible = false;
        AddChild(_bars);

        float side = DangerLevel.AlarmBarThicknessSide;
        float cap = DangerLevel.AlarmBarThicknessTopBottom;

        AddBar(0f, 0f, 1f, 0f, 0f, 0f, 0f, cap);        // top
        AddBar(0f, 1f, 1f, 1f, 0f, -cap, 0f, 0f);       // bottom
        AddBar(0f, 0f, 0f, 1f, 0f, 0f, side, 0f);       // left
        AddBar(1f, 0f, 1f, 1f, -side, 0f, 0f, 0f);      // right
    }

    // MouseFilter.Ignore on every bar is not optional: ColorRect defaults to Stop, and these are
    // full-width/full-height rects sitting exactly where the virtual joystick's touch area is on a
    // portrait screen — leaving the default would silently eat movement input.
    private void AddBar(float aL, float aT, float aR, float aB, float oL, float oT, float oR, float oB)
    {
        var bar = new ColorRect();
        bar.Color = DangerLevel.AlarmBarColor;
        bar.MouseFilter = MouseFilterEnum.Ignore;
        bar.AnchorLeft = aL;
        bar.AnchorTop = aT;
        bar.AnchorRight = aR;
        bar.AnchorBottom = aB;
        bar.OffsetLeft = oL;
        bar.OffsetTop = oT;
        bar.OffsetRight = oR;
        bar.OffsetBottom = oB;
        _bars.AddChild(bar);
    }

    public void SetDanger(float danger)
    {
        _danger = danger;
        RefreshAlarm();
    }

    // Forced full-intensity alarm regardless of round, used for the 3-second countdown before a boss
    // round. This is also what gives the round-5 boss an alarm at all — normal escalation hasn't
    // reached AlarmThreshold that early, so without this the first boss would arrive in silence.
    public void SetBossAlert(bool active)
    {
        _bossAlert = active;
        RefreshAlarm();
    }

    private void RefreshAlarm()
    {
        // Killed before every rebuild: Godot doesn't retire a previous tween targeting the same
        // property, so without this each round would stack another pulse on the same Modulate and the
        // alarm would visibly jitter by the mid teens.
        _pulseTween?.Kill();
        _pulseTween = null;

        float effective = _bossAlert ? 1f : _danger;
        bool visible = _bossAlert || _danger >= DangerLevel.AlarmThreshold;

        _bars.Visible = visible;
        if (!visible) return;

        float trough = DangerLevel.AlarmTrough(effective);
        float peak = DangerLevel.AlarmPeak(effective);

        if (DangerLevel.Reduced)
        {
            _bars.Modulate = Grey(trough);
            return;
        }

        _bars.Modulate = Grey(trough);

        float leg = DangerLevel.AlarmLegDuration(effective);
        _pulseTween = CreateTween();
        _pulseTween.SetLoops();
        _pulseTween.TweenProperty(_bars, "modulate", Grey(peak), leg)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _pulseTween.TweenProperty(_bars, "modulate", Grey(trough), leg)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    // Modulate multiplies, so a neutral grey scales the bars' own red brightness up and down. Alpha
    // stays at 1 throughout — see the comment on DangerLevel.AlarmBarColor for why pulsing alpha
    // instead would have killed the bloom entirely.
    private static Color Grey(float v) => new(v, v, v, 1f);

    // Fade-hold-fade rather than the drift-up-and-fade used by the small floating labels elsewhere:
    // this one is anchored (to stay centred at any resolution), and anchors recompute Position on every
    // layout pass, so tweening Position here would fight the layout rather than animate. BossBanner
    // solves the same problem the same way for the same reason.
    public void AnnounceThreat(string text)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", DangerLevel.AlarmBarColor);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 4);
        label.AddThemeFontSizeOverride("font_size", 30);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.MouseFilter = MouseFilterEnum.Ignore;

        // Sits above centre so it never lands on the pre-round countdown label, which owns the exact
        // middle of the screen.
        label.AnchorLeft = 0f;
        label.AnchorRight = 1f;
        label.AnchorTop = 0.34f;
        label.AnchorBottom = 0.34f;
        label.OffsetTop = -30f;
        label.OffsetBottom = 30f;
        label.Modulate = new Color(1f, 1f, 1f, 0f);
        AddChild(label);

        var tween = label.CreateTween();
        tween.TweenProperty(label, "modulate:a", 1f, 0.25f);
        tween.TweenInterval(1.4f);
        tween.TweenProperty(label, "modulate:a", 0f, 0.6f);
        tween.TweenCallback(Callable.From(() => label.QueueFree()));
    }
}
