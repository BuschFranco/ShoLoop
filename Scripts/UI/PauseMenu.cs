namespace ShooterLoop;

public partial class PauseMenu : Control
{
    private Label _statsLabel;
    private Button _resumeButton;
    private Button _menuButton;
    private PanelContainer _pausePanel;
    private LoadoutMenu _loadoutMenu;
    private HSlider _cameraDistanceSlider;
    private Label _cameraDistanceLabel;
    private Label _resumeCountdownLabel;

    // Gives the player a beat to get their thumbs back on the joystick instead of gameplay resuming
    // the instant they tap Reanudar — the same reasoning as the round-start "Preparate..." countdown
    // in HUD/GameManager, just scoped to this menu since resuming from pause has no round-start event
    // of its own to piggyback on.
    private bool _resuming;
    private float _resumeCountdownRemaining;
    private const float ResumeCountdownDuration = 3f;

    public override void _Ready()
    {
        AddToGroup("pause_menu");
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;

        _statsLabel = GetNode<Label>("CenterContainer/HBox/Panel/VBoxContainer/StatsLabel");
        _resumeButton = GetNode<Button>("CenterContainer/HBox/Panel/VBoxContainer/ResumeButton");
        _menuButton = GetNode<Button>("CenterContainer/HBox/Panel/VBoxContainer/MenuButton");
        _cameraDistanceSlider = GetNode<HSlider>("CenterContainer/HBox/Panel/VBoxContainer/CameraDistanceSlider");
        _cameraDistanceLabel = GetNode<Label>("CenterContainer/HBox/Panel/VBoxContainer/CameraDistanceLabel");
        _pausePanel = GetNode<PanelContainer>("CenterContainer/HBox/Panel");
        _loadoutMenu = GetNode<LoadoutMenu>("CenterContainer/HBox/LoadoutMenu");
        _resumeCountdownLabel = GetNode<Label>("ResumeCountdownLabel");
        _resumeButton.Pressed += OnResumePressed;
        _menuButton.Pressed += OnMenuPressed;
        _cameraDistanceSlider.ValueChanged += OnCameraDistanceChanged;

        bool portrait = GameManager.Instance?.CurrentOrientation == GameManager.ScreenOrientation.Portrait;
        if (portrait)
        {
            _pausePanel.CustomMinimumSize = new Vector2(250f, 0f);
            _loadoutMenu.CustomMinimumSize = new Vector2(360f, 940f);
        }
        else
        {
            _pausePanel.CustomMinimumSize = new Vector2(340f, 0f);
            _loadoutMenu.CustomMinimumSize = new Vector2(460f, 520f);
        }
    }

    public void Open()
    {
        var gm = GameManager.Instance;
        var player = GetTree().GetFirstNodeInGroup("player") as Player;

        var lines = new List<string>();

        lines.Add("── ESTADO ──");
        lines.Add($"Ronda {gm.RoundNumber}   Tiempo: {Mathf.CeilToInt(gm.RoundTimeRemaining)}s");
        lines.Add($"Nv {gm.Level}   Monedas: {gm.Coins}   Puntaje: {gm.Score}");
        lines.Add($"Eliminados: {gm.EnemiesKilled}   Especiales: {gm.SpecialEnemiesKilled}");

        if (player != null)
        {
            lines.Add("");
            lines.Add("── COMBATE ──");
            lines.Add($"Daño: {player.BulletDamage}   Cadencia: {player.FireRate:0.0}/s   Crítico: {player.CritChance:0}%");
            lines.Add($"Alcance: {player.FireRange:0}   Perforación: {player.BulletPierce}   Rebote: {player.RicochetCount}");
            lines.Add($"Retroceso: {player.BulletKnockback:0}   Esquiva: {player.DodgeChance:0}%");
            lines.Add($"Disparo Doble: {(player.HasExtraProjectile ? "Sí" : "No")}   Líneas laterales: {player.ExtraFiringLines}/{Player.MaxExtraFiringLinesCap}");
            lines.Add($"Cuchillas: {player.OrbitCount}   Escudo Voltáico: {(player.ThornsDamage > 0 ? $"{player.ThornsDamage:0} daño" : "No")}");

            lines.Add("");
            lines.Add("── DEFENSAS ──");
            lines.Add($"Vidas: {player.CurrentLives}/{player.MaxLives}   Escudo: {player.CurrentShieldCharges}/{player.MaxShieldCharges}");
            lines.Add($"Regeneración: {(player.ShieldRegenPerMinute > 0 ? $"{player.ShieldRegenPerMinute:0.#}/min" : "No")}");

            lines.Add("");
            lines.Add("── PODERES ──");
            lines.Add($"Dron: {(player.CompanionStatPercent > 0 ? $"{player.CompanionStatPercent * 100:0}%" : "No")}");
            if (player.LaserLevel > 0) lines.Add($"Láser: Lv{player.LaserLevel}");
            if (player.MissileLevel > 0) lines.Add($"Misil: Lv{player.MissileLevel}");
            if (player.BurnLevel > 0) lines.Add($"Incendiario: Lv{player.BurnLevel}");
            if (player.OndaLevel > 0) lines.Add($"Onda: Lv{player.OndaLevel}");
            if (player.VendavalLevel > 0) lines.Add($"Vendaval: Lv{player.VendavalLevel}");
            if (player.EquippedUltimate != null) lines.Add($"Ultimate: {UltimateKindNames.Display(player.EquippedUltimate.Value)}");
        }

        _statsLabel.Text = string.Join("\n", lines);
        _cameraDistanceSlider.Value = gm.CameraDistance;
        float camPct = 2000f / gm.CameraDistance * 100f;
        _cameraDistanceLabel.Text = $"Zoom: {camPct:0}%";
        _loadoutMenu.Refresh(player);

        // Defensive reset: Visible only ever goes false once the countdown below completes, so this
        // shouldn't be reachable mid-countdown — but a fresh Open() is the right place to guarantee
        // the menu never reopens stuck disabled/mid-count regardless.
        _resuming = false;
        _resumeCountdownLabel.Visible = false;
        _resumeButton.Disabled = false;
        _menuButton.Disabled = false;

        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!_resuming) return;

        _resumeCountdownRemaining -= (float)delta;
        if (_resumeCountdownRemaining <= 0f)
        {
            _resuming = false;
            _resumeCountdownLabel.Visible = false;
            _resumeButton.Disabled = false;
            _menuButton.Disabled = false;
            Visible = false;
            GameManager.Instance.Resume();
            return;
        }

        UpdateResumeCountdownLabel();
    }

    private void UpdateResumeCountdownLabel()
    {
        int secondsLeft = Mathf.CeilToInt(_resumeCountdownRemaining);
        _resumeCountdownLabel.Text = $"Reanudando en...\n{secondsLeft}";
    }

    // Doesn't resume immediately — counts down first so the player has a beat to get ready instead
    // of gameplay picking back up on the same frame they tapped the button. The pause/loadout panels
    // stay up underneath (only the buttons disable) so the countdown reads as "about to resume" rather
    // than replacing the whole menu.
    private void OnResumePressed()
    {
        if (_resuming) return;

        _resuming = true;
        _resumeCountdownRemaining = ResumeCountdownDuration;
        _resumeButton.Disabled = true;
        _menuButton.Disabled = true;
        _resumeCountdownLabel.Visible = true;
        UpdateResumeCountdownLabel();
    }

    private void OnMenuPressed()
    {
        GameManager.Instance.ResetRun();
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
    }

    private void OnCameraDistanceChanged(double value)
    {
        GameManager.Instance.CameraDistance = (float)value;
        GameManager.Instance.UpdateCameraExtents();
        float pct = 2000f / (float)value * 100f;
        _cameraDistanceLabel.Text = $"Distancia Cámara: {pct:0}%";
    }
}
