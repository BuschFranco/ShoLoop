namespace ShooterLoop;

// Settings overlay opened from the main menu. Currently: joystick opacity, screen orientation,
// and camera distance (zoom).
public partial class OptionsMenu : Control
{
    private Label _joystickLabel;
    private HSlider _joystickSlider;
    private Label _ultimateButtonLabel;
    private HSlider _ultimateButtonSlider;
    private Label _cameraDistanceLabel;
    private HSlider _cameraDistanceSlider;
    private Button _landscapeButton;
    private Button _portraitButton;
    private Button _closeButton;
    private Button _reducedMotionButton;
    private PanelContainer _panel;

    public override void _Ready()
    {
        Visible = false;

        _panel = GetNode<PanelContainer>("CenterContainer/Panel");
        _joystickLabel = GetNode<Label>("CenterContainer/Panel/Box/JoystickLabel");
        _joystickSlider = GetNode<HSlider>("CenterContainer/Panel/Box/JoystickSlider");
        _ultimateButtonLabel = GetNode<Label>("CenterContainer/Panel/Box/UltimateButtonLabel");
        _ultimateButtonSlider = GetNode<HSlider>("CenterContainer/Panel/Box/UltimateButtonSlider");
        _cameraDistanceLabel = GetNode<Label>("CenterContainer/Panel/Box/CameraDistanceLabel");
        _cameraDistanceSlider = GetNode<HSlider>("CenterContainer/Panel/Box/CameraDistanceSlider");
        _landscapeButton = GetNode<Button>("CenterContainer/Panel/Box/OrientationRow/LandscapeButton");
        _portraitButton = GetNode<Button>("CenterContainer/Panel/Box/OrientationRow/PortraitButton");
        _closeButton = GetNode<Button>("CenterContainer/Panel/Box/CloseButton");
        _reducedMotionButton = GetNode<Button>("CenterContainer/Panel/Box/ReducedMotionButton");

        _joystickSlider.ValueChanged += OnJoystickOpacityChanged;
        _ultimateButtonSlider.ValueChanged += OnUltimateButtonOpacityChanged;
        _cameraDistanceSlider.ValueChanged += OnCameraDistanceChanged;
        _closeButton.Pressed += Close;
        Juice.WireButtonFeedback(_closeButton);
        Juice.WireButtonFeedback(_landscapeButton);
        Juice.WireButtonFeedback(_portraitButton);
        Juice.WireButtonFeedback(_reducedMotionButton);

        _reducedMotionButton.Toggled += OnReducedMotionToggled;

        _landscapeButton.Toggled += pressed =>
        {
            if (pressed) GameManager.Instance?.SetOrientation(GameManager.ScreenOrientation.Landscape);
        };
        _portraitButton.Toggled += pressed =>
        {
            if (pressed) GameManager.Instance?.SetOrientation(GameManager.ScreenOrientation.Portrait);
        };
    }

    public void Open()
    {
        float opacity = GameManager.Instance?.JoystickOpacity ?? 1f;
        _joystickSlider.SetValueNoSignal(Mathf.Round(opacity * 100f));
        UpdateJoystickLabel(_joystickSlider.Value);

        float ultimateOpacity = GameManager.Instance?.UltimateButtonOpacity ?? 1f;
        _ultimateButtonSlider.SetValueNoSignal(Mathf.Round(ultimateOpacity * 100f));
        UpdateUltimateButtonLabel(_ultimateButtonSlider.Value);

        float camDist = GameManager.Instance?.CameraDistance ?? 2000f;
        _cameraDistanceSlider.SetValueNoSignal(camDist);
        UpdateCameraDistanceLabel(camDist);

        bool isPortrait = GameManager.Instance?.CurrentOrientation != GameManager.ScreenOrientation.Landscape;
        _landscapeButton.SetPressedNoSignal(!isPortrait);
        _portraitButton.SetPressedNoSignal(isPortrait);

        bool reduced = GameManager.Instance?.ReducedMotion ?? false;
        _reducedMotionButton.SetPressedNoSignal(reduced);
        UpdateReducedMotionLabel(reduced);

        Visible = true;
        Juice.ModalIn(_panel);
    }

    private void Close() => Juice.ModalOut(_panel, () => Visible = false);

    // A toggle rather than a slider: it's a binary preference, and toggle_mode gives it a pressed
    // StyleBox that reads as "on" without needing a separate checkbox widget the project doesn't
    // otherwise use. The label states the current state in words too, so "on" doesn't rest purely
    // on the button's fill colour.
    private void OnReducedMotionToggled(bool pressed)
    {
        GameManager.Instance?.SetReducedMotion(pressed);
        UpdateReducedMotionLabel(pressed);
    }

    private void UpdateReducedMotionLabel(bool enabled) =>
        _reducedMotionButton.Text = enabled ? "Movimiento reducido: SÍ" : "Movimiento reducido: NO";

    private void OnJoystickOpacityChanged(double value)
    {
        GameManager.Instance?.SetJoystickOpacity((float)value / 100f);
        UpdateJoystickLabel(value);
    }

    private void UpdateJoystickLabel(double value) =>
        _joystickLabel.Text = $"Opacidad del joystick: {value:0}%";

    private void OnUltimateButtonOpacityChanged(double value)
    {
        GameManager.Instance?.SetUltimateButtonOpacity((float)value / 100f);
        UpdateUltimateButtonLabel(value);
    }

    private void UpdateUltimateButtonLabel(double value) =>
        _ultimateButtonLabel.Text = $"Opacidad del botón Ultimate: {value:0}%";

    private void OnCameraDistanceChanged(double value)
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.CameraDistance = (float)value;
        GameManager.Instance.UpdateCameraExtents();
        UpdateCameraDistanceLabel(value);
    }

    private void UpdateCameraDistanceLabel(double value)
    {
        float pct = 2000f / (float)value * 100f;
        _cameraDistanceLabel.Text = $"Zoom de cámara: {pct:0}%";
    }
}
