namespace ShooterLoop;

// Settings overlay opened from the main menu. Currently: joystick opacity and screen orientation.
//
// The orientation picker used to sit inline in the main menu's own VBox. It's a setting, so it belongs
// here — and moving it is what freed the room on the menu for the records panel.
public partial class OptionsMenu : Control
{
    private Label _joystickLabel;
    private HSlider _joystickSlider;
    private Label _ultimateButtonLabel;
    private HSlider _ultimateButtonSlider;
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
        _landscapeButton = GetNode<Button>("CenterContainer/Panel/Box/OrientationRow/LandscapeButton");
        _portraitButton = GetNode<Button>("CenterContainer/Panel/Box/OrientationRow/PortraitButton");
        _closeButton = GetNode<Button>("CenterContainer/Panel/Box/CloseButton");

        _joystickSlider.ValueChanged += OnJoystickOpacityChanged;
        _ultimateButtonSlider.ValueChanged += OnUltimateButtonOpacityChanged;
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
        // Read fresh on every open rather than trusting the scene's defaults, so the controls always
        // reflect what's actually saved — including on a return trip from a finished run.
        float opacity = GameManager.Instance?.JoystickOpacity ?? 1f;

        // SetValueNoSignal, or assigning the slider would fire ValueChanged and immediately re-save the
        // value we just read.
        _joystickSlider.SetValueNoSignal(Mathf.Round(opacity * 100f));
        UpdateJoystickLabel(_joystickSlider.Value);

        float ultimateOpacity = GameManager.Instance?.UltimateButtonOpacity ?? 1f;
        _ultimateButtonSlider.SetValueNoSignal(Mathf.Round(ultimateOpacity * 100f));
        UpdateUltimateButtonLabel(_ultimateButtonSlider.Value);

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
}
