namespace ShooterLoop;

// World drop from an enemy kill (see Enemy.TryDropPickup). Heals the player 1 life on contact —
// a mid-run top-up, distinct from the Corazón Legendario reward which raises MaxLives itself.
public partial class HeartPickup : Area2D
{
    private const float Lifetime = 10f;
    private const float PulseSpeed = 4f;

    private Polygon2D _visual;
    private float _age;

    public override void _Ready()
    {
        AddToGroup("pickups");
        _visual = GetNodeOrNull<Polygon2D>("Visual");
        BodyEntered += OnBodyEntered;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }

        // Gentle pulse so a stationary drop still reads as "alive" and worth noticing.
        if (_visual != null)
            _visual.Scale = Vector2.One * (1f + Mathf.Sin(_age * PulseSpeed) * 0.12f);

        if (_age > Lifetime - 1f)
            Modulate = new Color(1f, 1f, 1f, Lifetime - _age);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player player) return;
        player.AddLife(1);
        SpawnPickupLabel("+1 vida", Palette.HeartPickup);
        QueueFree();
    }

    private void SpawnPickupLabel(string text, Color color)
    {
        var parent = GetParent();
        if (parent == null) return;

        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 2);
        label.AddThemeFontSizeOverride("font_size", 16);
        label.ZIndex = 10;
        parent.AddChild(label);
        label.GlobalPosition = GlobalPosition - new Vector2(20f, 10f);

        var tween = label.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", label.Position + new Vector2(0f, -30f), 0.7f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 0.5f).SetDelay(0.3f);
        tween.Chain().TweenCallback(Callable.From(() => label.QueueFree()));
    }
}
