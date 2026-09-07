namespace ShooterLoop;

// "Ver Stats" from the Game Over screen — read-only, no Reanudar/Abandonar/settings (the run is
// already over), just the same COMBATE/DEFENSAS/PODERES readout PauseMenu shows
// (Player.BuildCombatStatsLines, shared so the two can't drift apart) plus the same LoadoutMenu
// panel (equipped items + build progress) side by side, same two-panel layout PauseMenu already
// uses. GameOverScreen keeps its own summary (round reached, score, coins, Libras) — this only adds
// what that summary doesn't cover.
public partial class GameOverStatsMenu : Control
{
    private Label _statsLabel;
    private Button _closeButton;
    private PanelContainer _panel;
    private ScrollContainer _scroll;
    private LoadoutMenu _loadoutMenu;
    private Control _hbox;

    // Same numbers PauseMenu uses for the same reason — see PauseMenu.cs for the full rationale
    // (a CenterContainer never bounds a ScrollContainer's height on its own).
    private const float PortraitPanelWidth = 250f;
    private const float PortraitScrollHeight = 940f;
    private const float PortraitLoadoutWidth = 360f;
    private const float LandscapePanelWidth = 340f;
    private const float LandscapeScrollHeight = 520f;
    private const float LandscapeLoadoutWidth = 460f;

    public override void _Ready()
    {
        AddToGroup("game_over_stats_menu");
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;

        _statsLabel = GetNode<Label>("CenterContainer/HBox/Panel/Scroll/VBoxContainer/StatsLabel");
        _closeButton = GetNode<Button>("CenterContainer/HBox/Panel/Scroll/VBoxContainer/CloseButton");
        _panel = GetNode<PanelContainer>("CenterContainer/HBox/Panel");
        _scroll = GetNode<ScrollContainer>("CenterContainer/HBox/Panel/Scroll");
        _loadoutMenu = GetNode<LoadoutMenu>("CenterContainer/HBox/LoadoutMenu");
        _hbox = GetNode<Control>("CenterContainer/HBox");

        _closeButton.Pressed += Close;
        Juice.WireButtonFeedback(_closeButton);

        bool portrait = GameManager.Instance?.CurrentOrientation == GameManager.ScreenOrientation.Portrait;
        if (portrait)
        {
            _panel.CustomMinimumSize = new Vector2(PortraitPanelWidth, 0f);
            _scroll.CustomMinimumSize = new Vector2(0f, PortraitScrollHeight);
            _loadoutMenu.CustomMinimumSize = new Vector2(PortraitLoadoutWidth, PortraitScrollHeight);
        }
        else
        {
            _panel.CustomMinimumSize = new Vector2(LandscapePanelWidth, 0f);
            _scroll.CustomMinimumSize = new Vector2(0f, LandscapeScrollHeight);
            _loadoutMenu.CustomMinimumSize = new Vector2(LandscapeLoadoutWidth, LandscapeScrollHeight);
        }
    }

    public void Open()
    {
        // The player is still in the tree at this point — GameOverScreen.Open() already relies on
        // the same lookup succeeding (it reads live stats into its own run summary), and nothing
        // frees the player until the user actually leaves this screen (Jugar de nuevo/Menú/Salir).
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        _statsLabel.Text = player != null ? string.Join("\n", player.BuildCombatStatsLines()) : "";
        _loadoutMenu.Refresh(player);

        Visible = true;
        Juice.ModalIn(_hbox);
    }

    private void Close() => Juice.ModalOut(_hbox, () => Visible = false);
}
