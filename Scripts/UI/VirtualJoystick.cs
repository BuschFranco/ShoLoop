namespace ShooterLoop;

public partial class VirtualJoystick : Control
{
    [Export] public float MaxRadius = 60f;

    private const int MouseTouchIndex = -100;

    private Polygon2D _knob;
    private Vector2 _knobCenter;
    private int _touchIndex = -1;
    private Vector2 _origin;
    private Vector2 _knobOffset = Vector2.Zero;

    public override void _Ready()
    {
        AddToGroup("virtual_joystick");
        _knob = GetNode<Polygon2D>("Knob");
        _knobCenter = _knob.Position;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventScreenTouch touch)
        {
            HandlePress(touch.Index, touch.Pressed, touch.Position);
        }
        else if (@event is InputEventScreenDrag drag)
        {
            HandleDrag(drag.Index, drag.Position);
        }
        else if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            HandlePress(MouseTouchIndex, mouseButton.Pressed, mouseButton.Position);
        }
        else if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleDrag(MouseTouchIndex, mouseMotion.Position);
        }
    }

    public override void _Process(double delta)
    {
        // Safety net: if the mouse button release event was missed (e.g. focus loss,
        // release outside the window), don't leave the joystick stuck engaged.
        if (_touchIndex == MouseTouchIndex && !Input.IsMouseButtonPressed(MouseButton.Left))
        {
            _touchIndex = -1;
            _knobOffset = Vector2.Zero;
            UpdateKnob();
        }
    }

    private void HandlePress(int index, bool pressed, Vector2 position)
    {
        if (pressed && _touchIndex == -1 && GetGlobalRect().HasPoint(position))
        {
            _touchIndex = index;
            _origin = position;
            _knobOffset = Vector2.Zero;
            UpdateKnob();
        }
        else if (!pressed && index == _touchIndex)
        {
            _touchIndex = -1;
            _knobOffset = Vector2.Zero;
            UpdateKnob();
        }
    }

    private void HandleDrag(int index, Vector2 position)
    {
        if (index != _touchIndex) return;

        Vector2 delta = position - _origin;
        _knobOffset = delta.LimitLength(MaxRadius);
        UpdateKnob();
    }

    private void UpdateKnob()
    {
        _knob.Position = _knobCenter + _knobOffset;
    }

    public Vector2 GetVector()
    {
        if (_touchIndex == -1 || MaxRadius <= 0f) return Vector2.Zero;
        return _knobOffset / MaxRadius;
    }
}
