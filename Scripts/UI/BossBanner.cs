namespace ShooterLoop;

public partial class BossBanner : Control
{
    private Label _label;
    private Control _stripes;

    // Hazard chevrons framing the announcement, built procedurally like everything else in the project.
    // Top edge only: in portrait a bottom row would land squarely on the virtual joystick's touch area.
    private const int StripeCount = 8;
    private const float StripeWidth = 44f;
    private const float StripeHeight = 22f;
    private const float StripeSlant = 26f;
    private static readonly Color StripeColor = new(1f, 0.63f, 0.04f, 0.85f);

    public override void _Ready()
    {
        AddToGroup("boss_banner");
        MouseFilter = MouseFilterEnum.Ignore;
        _label = GetNode<Label>("Label");
        BuildStripes();
        Modulate = new Color(1f, 1f, 1f, 0f);
    }

    // Children of this node, so they inherit its Modulate — the existing 2s-hold + 0.6s fade in
    // Announce covers them for free without a second animation to keep in sync.
    private void BuildStripes()
    {
        _stripes = new Control();
        _stripes.MouseFilter = MouseFilterEnum.Ignore;
        _stripes.AnchorLeft = 0f;
        _stripes.AnchorRight = 1f;
        _stripes.AnchorTop = 0f;
        _stripes.AnchorBottom = 0f;
        _stripes.OffsetTop = 0f;
        _stripes.OffsetBottom = StripeHeight;
        AddChild(_stripes);

        for (int i = 0; i < StripeCount; i++)
        {
            var stripe = new Polygon2D();
            stripe.Color = StripeColor;
            stripe.Polygon = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(StripeWidth, 0f),
                new Vector2(StripeWidth - StripeSlant, StripeHeight),
                new Vector2(-StripeSlant, StripeHeight),
            };
            stripe.Position = new Vector2(i * (StripeWidth * 2f), 0f);
            _stripes.AddChild(stripe);
        }
    }

    public void Announce(int round)
    {
        _label.Text = $"¡RONDA DE JEFE {round}!";
        Modulate = new Color(1f, 1f, 1f, 1f);

        // Slides in from the left as the banner appears, then rides the banner's own fade out.
        _stripes.Position = new Vector2(-120f, 0f);
        var slide = _stripes.CreateTween();
        slide.TweenProperty(_stripes, "position", Vector2.Zero, 0.35f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        var tween = CreateTween();
        tween.TweenInterval(2f);
        tween.TweenProperty(this, "modulate:a", 0f, 0.6f);
    }
}
