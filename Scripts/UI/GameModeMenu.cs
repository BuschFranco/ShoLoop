namespace ShooterLoop;

// Sits between MainMenu's "Comenzar" and CharacterSelectMenu: pick Clásico or Hardcore before
// picking a pilot. A static two-option layout (unlike AchievementsMenu/MissionsMenu's rebuilt row
// lists) since there are exactly two modes and neither needs a dynamic list — the modes themselves
// live on GameManager.GameMode, this screen just picks one.
public partial class GameModeMenu : Control
{
    private PanelContainer _panel;
    private PanelContainer _classicPanel;
    private PanelContainer _hardcorePanel;
    private Button _classicButton;
    private Button _hardcoreButton;
    private Button _cancelButton;
    private CharacterSelectMenu _characterSelect;

    public override void _Ready()
    {
        Visible = false;

        _panel = GetNode<PanelContainer>("CenterContainer/Panel");
        _classicPanel = GetNode<PanelContainer>("CenterContainer/Panel/Box/ClassicPanel");
        _hardcorePanel = GetNode<PanelContainer>("CenterContainer/Panel/Box/HardcorePanel");
        _classicButton = GetNode<Button>("CenterContainer/Panel/Box/ClassicPanel/ClassicBox/ClassicButton");
        _hardcoreButton = GetNode<Button>("CenterContainer/Panel/Box/HardcorePanel/HardcoreBox/HardcoreButton");
        _cancelButton = GetNode<Button>("CenterContainer/Panel/Box/CancelButton");
        _characterSelect = GetNode<CharacterSelectMenu>("../CharacterSelectMenu");

        _panel.AddThemeStyleboxOverride("panel", UIUtil.CreatePanelStyle(Palette.Player));

        _classicButton.Pressed += () => SelectMode(GameManager.GameMode.Classic);
        _hardcoreButton.Pressed += () => SelectMode(GameManager.GameMode.Hardcore);
        _cancelButton.Pressed += () => Juice.ModalOut(_panel, () => Visible = false);

        foreach (var b in new[] { _classicButton, _hardcoreButton, _cancelButton })
            Juice.WireButtonFeedback(b);
    }

    private void SelectMode(GameManager.GameMode mode)
    {
        GameManager.Instance.SetGameMode(mode);
        Juice.ModalOut(_panel, () =>
        {
            Visible = false;
            _characterSelect.Open();
        });
    }

    // The last-picked mode gets a brighter border so returning players see at a glance what they're
    // about to start with again, instead of the choice always looking blank.
    private void RefreshHighlight()
    {
        bool hardcore = GameManager.Instance.CurrentGameMode == GameManager.GameMode.Hardcore;
        StylePanel(_classicPanel, Palette.Player, active: !hardcore);
        StylePanel(_hardcorePanel, Palette.Warning, active: hardcore);
    }

    private static void StylePanel(PanelContainer panel, Color accent, bool active)
    {
        var style = new StyleBoxFlat
        {
            BgColor = active ? new Color(accent, 0.22f) : new Color(0.102f, 0.0588f, 0.1686f, 0.75f),
            BorderColor = accent,
        };
        style.SetBorderWidthAll(active ? 3 : 1);
        style.SetContentMarginAll(12f);
        panel.AddThemeStyleboxOverride("panel", style);
    }

    public void Open()
    {
        RefreshHighlight();
        Visible = true;
        Juice.ModalIn(_panel);
    }
}
