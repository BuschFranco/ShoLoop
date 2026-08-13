namespace ShooterLoop;

// Shared behaviour for every world drop (Heart/Shield/Xp/Coin): pulse, fade-out-then-expire, and
// the "magnet" pull toward the player once it wanders within MagnetRadius. Subclasses only need to
// supply their own visual/collision (via the scene) and OnCollected.
public abstract partial class PickupBase : Area2D
{
    private const float Lifetime = 10f;
    private const float PulseSpeed = 4f;

    // Once the player gets this close, the pickup accelerates toward them instead of waiting to be
    // walked into — reads as a magnet grabbing loot, not the player having to path over every gem.
    private const float MagnetRadius = 140f;
    private const float MagnetAcceleration = 1400f;
    private const float MagnetMaxSpeed = 640f;

    private Polygon2D _visual;
    private float _age;
    private float _magnetSpeed;
    private Player _player;

    public override void _Ready()
    {
        AddToGroup("pickups");
        _visual = GetNodeOrNull<Polygon2D>("Visual");
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        BodyEntered += OnBodyEntered;
    }

    // Called by GameManager.StartNextRound on everything that survived the round boundary. Resets the
    // Modulate too, not just the age — the last-second fade below may have already partially applied,
    // and a pickup that carried over shouldn't spend its new lifetime stuck half-transparent.
    public void RefreshLifetime()
    {
        _age = 0f;
        Modulate = Colors.White;
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

        ApplyMagnet((float)delta);
    }

    // Accelerates in rather than snapping to the player's position, so a gem that enters the radius
    // still reads as "flying toward you" instead of teleporting.
    private void ApplyMagnet(float delta)
    {
        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Player;
            if (_player == null) return;
        }

        float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);
        if (distance > MagnetRadius)
        {
            _magnetSpeed = 0f;
            return;
        }

        _magnetSpeed = Mathf.Min(_magnetSpeed + MagnetAcceleration * delta, MagnetMaxSpeed);
        GlobalPosition = GlobalPosition.MoveToward(_player.GlobalPosition, _magnetSpeed * delta);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player player) return;
        OnCollected(player);
        QueueFree();
    }

    protected abstract void OnCollected(Player player);

    protected void SpawnPickupLabel(string text, Color color)
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
