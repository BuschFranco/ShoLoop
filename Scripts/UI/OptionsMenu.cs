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

    public override void _Ready()
    {
        Visible = false;

        _joystickLabel = GetNode<Label>("CenterContainer/Panel/Box/JoystickLabel");
        _joystickSlider = GetNode<HSlider>("CenterContainer/Panel/Box/JoystickSlider");
        _ultimateButtonLabel = GetNode<Label>("CenterContainer/Panel/Box/UltimateButtonLabel");
        _ultimateButtonSlider = GetNode<HSlider>("CenterContainer/Panel/Box/UltimateButtonSlider");
        _cameraDistanceLabel = GetNode<Label>("CenterContainer/Panel/Box/CameraDistanceLabel");
        _cameraDistanceSlider = GetNode<HSlider>("CenterContainer/Panel/Box/CameraDistanceSlider");
        _landscapeButton = GetNode<Button>("CenterContainer/Panel/Box/OrientationRow/LandscapeButton");
        _portraitButton = GetNode<Button>("CenterContainer/Panel/Box/OrientationRow/PortraitButton");
        _closeButton = GetNode<Button>("CenterContainer/Panel/Box/CloseButton");

        _joystickSlider.ValueChanged += OnJoystickOpacityChanged;
        _ultimateButtonSlider.ValueChanged += OnUltimateButtonOpacityChanged;
        _cameraDistanceSlider.ValueChanged += OnCameraDistanceChanged;
        _closeButton.Pressed += () => Visible = false;

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

        Visible = true;
    }

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
