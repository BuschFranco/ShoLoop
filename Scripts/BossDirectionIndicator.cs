namespace ShooterLoop;

// During boss rounds, points toward the boss whenever it's off camera -- there was previously no
// way to tell which direction to go looking for it. Lives entirely in world space (a sibling of the
// camera, not a HUD Control): its own position is just "the point on the camera's visible rect
// closest to the boss", so no screen<->world conversion is needed anywhere in this file.
//
// The shape doubles as both "a red arrow" and "a red glow coming from that direction" at once: it's
// a plain hard-edged triangle (no blur/gradient asset, matching docs/visuals.md), but its colour is
// pushed past 1.0 and pulsed via Juice.Shimmer so Arena's WorldEnvironment actually blooms it --
// same HDR-boost idiom already used for the menu title and the HUD's kill-streak badge.
public partial class BossDirectionIndicator : Node2D
{
    private const float EdgeMargin = 48f;       // keeps the arrow fully on screen, not clipped at the rim
    private const float ArrowLength = 22f;
    private const float ArrowHalfWidth = 11f;
    private const float PulsePeriod = 0.7f;
    private const float GlowBoost = 1.9f;

    private Polygon2D _arrow;
    private Tween _pulseTween;
    private Boss _boss;
    private bool _visibleLastFrame;

    public override void _Ready()
    {
        _arrow = new Polygon2D
        {
            Polygon = new[]
            {
                new Vector2(ArrowLength, 0f),
                new Vector2(-ArrowLength * 0.6f, -ArrowHalfWidth),
                new Vector2(-ArrowLength * 0.6f, ArrowHalfWidth),
            },
            Color = DangerLevel.AlarmBarColor,
            ZIndex = 50,
        };
        AddChild(_arrow);
        Visible = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!TryFindTarget(out Vector2 targetPosition) || !(GetTree().GetFirstNodeInGroup("camera_rig") is Camera2D camera))
        {
            SetIndicatorVisible(false);
            return;
        }

        Vector2 halfExtents = GetViewportRect().Size * 0.5f / camera.Zoom;
        Vector2 offset = targetPosition - camera.GlobalPosition;

        bool onScreen = Mathf.Abs(offset.X) <= halfExtents.X && Mathf.Abs(offset.Y) <= halfExtents.Y;
        if (onScreen)
        {
            SetIndicatorVisible(false);
            return;
        }

        Vector2 dir = offset.Normalized();
        Vector2 inset = halfExtents - new Vector2(EdgeMargin, EdgeMargin);
        float tx = Mathf.Abs(dir.X) > 0.0001f ? inset.X / Mathf.Abs(dir.X) : float.PositiveInfinity;
        float ty = Mathf.Abs(dir.Y) > 0.0001f ? inset.Y / Mathf.Abs(dir.Y) : float.PositiveInfinity;
        float t = Mathf.Min(tx, ty);

        GlobalPosition = camera.GlobalPosition + dir * t;
        Rotation = dir.Angle();
        SetIndicatorVisible(true);
    }

    // Only true while a live Boss exists and the round is actually a boss round -- the "boss" out
    // parameter is its position, so the caller never has to re-check IsInstanceValid itself.
    private bool TryFindTarget(out Vector2 position)
    {
        position = Vector2.Zero;
        if (GameManager.Instance == null || !GameManager.Instance.IsBossRound) return false;

        if (_boss == null || !IsInstanceValid(_boss))
        {
            _boss = null;
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
            {
                if (node is Boss boss) { _boss = boss; break; }
            }
        }

        if (_boss == null) return false;
        position = _boss.GlobalPosition;
        return true;
    }

    private void SetIndicatorVisible(bool value)
    {
        Visible = value;
        if (value == _visibleLastFrame) return;
        _visibleLastFrame = value;

        if (value)
        {
            var boosted = new Color(DangerLevel.AlarmBarColor.R * GlowBoost, DangerLevel.AlarmBarColor.G * GlowBoost, DangerLevel.AlarmBarColor.B * GlowBoost, DangerLevel.AlarmBarColor.A);
            _pulseTween = Juice.Shimmer(this, _arrow, "color", DangerLevel.AlarmBarColor, boosted, PulsePeriod);
        }
        else
        {
            _pulseTween?.Kill();
        }
    }
}
