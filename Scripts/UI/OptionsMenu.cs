namespace ShooterLoop;

// Settings overlay opened from the main menu. Currently: joystick and Ultimate-button opacity, the
// three volumes (general / effects / music), camera distance (zoom), screen orientation and reduced
// motion.
//
// The three volume rows are laid out as a group on purpose: General carries the explanatory hint and
// a full-height separator, while Effects and Music sit under it with tighter 14px gaps and no hints
// of their own. Three consecutive hint paragraphs would be noise, and the indentation-by-spacing is
// what says "these two are inside that one" without needing to write it.
public partial class OptionsMenu : Control
{
    private Label _joystickLabel;
    private HSlider _joystickSlider;
    private Label _ultimateButtonLabel;
    private HSlider _ultimateButtonSlider;
    private Label _masterVolumeLabel;
    private HSlider _masterVolumeSlider;
    private Label _volumeLabel;
    private HSlider _volumeSlider;
    private Label _musicVolumeLabel;
    private HSlider _musicVolumeSlider;
    private Control _musicSeparator;
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
        _masterVolumeLabel = GetNode<Label>("CenterContainer/Panel/Box/MasterVolumeLabel");
        _masterVolumeSlider = GetNode<HSlider>("CenterContainer/Panel/Box/MasterVolumeSlider");
        _volumeLabel = GetNode<Label>("CenterContainer/Panel/Box/VolumeLabel");
        _volumeSlider = GetNode<HSlider>("CenterContainer/Panel/Box/VolumeSlider");
        _musicVolumeLabel = GetNode<Label>("CenterContainer/Panel/Box/MusicVolumeLabel");
        _musicVolumeSlider = GetNode<HSlider>("CenterContainer/Panel/Box/MusicVolumeSlider");
        _musicSeparator = GetNode<Control>("CenterContainer/Panel/Box/SepSfx");
        _cameraDistanceLabel = GetNode<Label>("CenterContainer/Panel/Box/CameraDistanceLabel");
        _cameraDistanceSlider = GetNode<HSlider>("CenterContainer/Panel/Box/CameraDistanceSlider");
        _landscapeButton = GetNode<Button>("CenterContainer/Panel/Box/OrientationRow/LandscapeButton");
        _portraitButton = GetNode<Button>("CenterContainer/Panel/Box/OrientationRow/PortraitButton");
        _closeButton = GetNode<Button>("CenterContainer/Panel/Box/CloseButton");
        _reducedMotionButton = GetNode<Button>("CenterContainer/Panel/Box/ReducedMotionButton");

        _joystickSlider.ValueChanged += OnJoystickOpacityChanged;
        _ultimateButtonSlider.ValueChanged += OnUltimateButtonOpacityChanged;
        _masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
        _volumeSlider.ValueChanged += OnVolumeChanged;
        _musicVolumeSlider.ValueChanged += OnMusicVolumeChanged;
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

        float masterVolume = GameManager.Instance?.MasterVolume ?? 1f;
        _masterVolumeSlider.SetValueNoSignal(Mathf.Round(masterVolume * 100f));
        UpdateMasterVolumeLabel(_masterVolumeSlider.Value);

        float volume = GameManager.Instance?.SfxVolume ?? 1f;
        _volumeSlider.SetValueNoSignal(Mathf.Round(volume * 100f));
        UpdateVolumeLabel(_volumeSlider.Value);

        // Hidden while the game ships no music — a slider that provably controls nothing is worse
        // than an absent one. Its separator goes with it, or the two remaining rows sit in a gap
        // twice the size of the one above them. Drop a music track into Assets/Audio and the row
        // comes back on its own; the setting keeps its saved value in the meantime.
        bool hasMusic = AudioManager.Instance?.HasMusic ?? false;
        _musicVolumeLabel.Visible = hasMusic;
        _musicVolumeSlider.Visible = hasMusic;
        _musicSeparator.Visible = hasMusic;

        float musicVolume = GameManager.Instance?.MusicVolume ?? 0.7f;
        _musicVolumeSlider.SetValueNoSignal(Mathf.Round(musicVolume * 100f));
        UpdateMusicVolumeLabel(_musicVolumeSlider.Value);

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

    // No separate mute buttons: a slider that reaches 0 already is the mute, same as the two opacity
    // settings above, and each setter drives its audio bus directly so dragging is audible live.
    private void OnMasterVolumeChanged(double value)
    {
        GameManager.Instance?.SetMasterVolume((float)value / 100f);
        UpdateMasterVolumeLabel(value);
    }

    private void UpdateMasterVolumeLabel(double value) =>
        _masterVolumeLabel.Text = $"Volumen general: {value:0}%";

    private void OnVolumeChanged(double value)
    {
        GameManager.Instance?.SetSfxVolume((float)value / 100f);
        UpdateVolumeLabel(value);
    }

    private void UpdateVolumeLabel(double value) =>
        _volumeLabel.Text = $"Efectos: {value:0}%";

    private void OnMusicVolumeChanged(double value)
    {
        GameManager.Instance?.SetMusicVolume((float)value / 100f);
        UpdateMusicVolumeLabel(value);
    }

    private void UpdateMusicVolumeLabel(double value) =>
        _musicVolumeLabel.Text = $"Música: {value:0}%";

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
