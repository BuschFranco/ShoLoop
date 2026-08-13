namespace ShooterLoop;

public partial class VirtualJoystick : Control
{
    [Export] public float MaxRadius = 90f;

    private const float BoxSize = 210f;

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

        // Anchors reset to top-left so GlobalPosition in HandlePress can place the joystick
        // freely anywhere on screen — the .tscn's own fixed-bottom layout is irrelevant now.
        AnchorLeft = 0f;
        AnchorTop = 0f;
        AnchorRight = 0f;
        AnchorBottom = 0f;

        // Floating joystick: hidden until a press in the lower half of the screen places it.
        Visible = false;

        // Read here rather than pushed from the options screen: this scene only exists during a run, and
        // the setting can't change mid-run without going back through the menu. Modulate propagates to
        // both Polygon2D children and multiplies their own alphas, so the base/knob contrast survives.
        Modulate = new Color(1f, 1f, 1f, GameManager.Instance?.JoystickOpacity ?? 1f);
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
            Visible = false;
        }
    }

    private void HandlePress(int index, bool pressed, Vector2 position)
    {
        if (pressed && _touchIndex == -1 && IsEligiblePress(position))
        {
            _touchIndex = index;
            _origin = position;
            _knobOffset = Vector2.Zero;

            // Center the joystick on the touch point.
            GlobalPosition = position - new Vector2(BoxSize / 2f, BoxSize / 2f);
            Visible = true;
            UpdateKnob();
        }
        else if (!pressed && index == _touchIndex)
        {
            _touchIndex = -1;
            _knobOffset = Vector2.Zero;
            UpdateKnob();
            Visible = false;
        }
    }

    // A press only becomes a joystick grip when it lands in the bottom half of the screen AND
    // isn't on top of a UI control (the Ultimate button lives down there). GuiPick isn't exposed
    // in the C# bindings, so this walks the tree instead: any visible Control with
    // mouse_filter != Ignore under the finger (the joystick itself aside) wins the touch.
    private bool IsEligiblePress(Vector2 position)
    {
        if (position.Y < GetViewportRect().Size.Y / 2f) return false;
        return !HasInteractiveControlUnder(GetTree().Root, position);
    }

    private bool HasInteractiveControlUnder(Node root, Vector2 position)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is not Control control) continue;
            if (control != this
                && control.IsVisibleInTree()
                && control.MouseFilter != Control.MouseFilterEnum.Ignore
                && control.GetGlobalRect().HasPoint(position))
                return true;

            if (HasInteractiveControlUnder(control, position)) return true;
        }
        return false;
    }

    private void HandleDrag(int index, Vector2 position)
    {
        if (index != _touchIndex) return;

        Vector2 delta = position - _origin;

        // Drag-follow: once the finger outruns MaxRadius the whole joystick re-centers along
        // with it (keeping the knob pinned to the rim) so the player can keep steering without
        // lifting and re-tapping.
        if (delta.Length() > MaxRadius)
        {
            Vector2 clamped = delta.LimitLength(MaxRadius);
            Vector2 excess = delta - clamped;
            _origin += excess;
            GlobalPosition += excess;
            delta = clamped;
        }

        _knobOffset = delta;
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
