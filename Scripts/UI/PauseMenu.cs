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
    private Control _hbox;

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
        _hbox = GetNode<Control>("CenterContainer/HBox");
        _resumeButton.Pressed += OnResumePressed;
        _menuButton.Pressed += OnMenuPressed;
        _cameraDistanceSlider.ValueChanged += OnCameraDistanceChanged;
        Juice.WireButtonFeedback(_resumeButton);
        Juice.WireButtonFeedback(_menuButton);

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
        lines.Add($"{Glossary.LevelPrefix} {gm.Level}   Monedas: {gm.Coins}   Puntaje: {gm.Score}");
        lines.Add($"{Glossary.Kills}: {gm.EnemiesKilled}   Especiales: {gm.SpecialEnemiesKilled}");

        if (player != null)
        {
            lines.Add("");
            lines.Add("── COMBATE ──");
            lines.Add($"{Glossary.Damage}: {player.BulletDamage}   {Glossary.FireRate}: {player.FireRate:0.0}/s   {Glossary.Crit}: {player.CritChance:0}%");
            lines.Add($"{Glossary.Range}: {player.FireRange:0}   {Glossary.Pierce}: {player.BulletPierce}   Rebote: {player.RicochetCount}");
            lines.Add($"Retroceso: {player.BulletKnockback:0}   {Glossary.Dodge}: {player.DodgeChance:0}%");
            lines.Add($"Disparo en Diagonal: {(player.HasExtraProjectile ? "Sí" : "No")}   Disparo Paralelo: {player.ExtraFiringLines}/{Player.MaxExtraFiringLinesCap}");
            lines.Add($"Cuchillas Orbitales: {player.OrbitCount}   Escudo Voltáico: {(player.ThornsDamage > 0 ? $"{player.ThornsDamage:0} daño" : "No")}");

            lines.Add("");
            lines.Add("── DEFENSAS ──");
            lines.Add($"Vidas: {player.CurrentLives}/{player.MaxLives}   Escudos: {player.CurrentShieldCharges}/{player.MaxShieldCharges}");
            lines.Add($"Regeneración: {(player.ShieldRegenPerMinute > 0 ? $"{player.ShieldRegenPerMinute:0.#}/min" : "No")}");

            lines.Add("");
            lines.Add("── PODERES ──");
            lines.Add($"Dron: {(player.CompanionStatPercent > 0 ? $"{player.CompanionStatPercent * 100:0}%" : "No")}");

            // "Nv" here too — this block used to be the one place in the game that said "Lv", three
            // lines below its own "Nv 5" in the status section above. Mina was also simply missing:
            // the loadout panel and the HUD both showed it, this didn't.
            if (player.LaserLevel > 0) lines.Add($"Láser: {Glossary.LevelPrefix}{player.LaserLevel}");
            if (player.MissileLevel > 0) lines.Add($"Misil: {Glossary.LevelPrefix}{player.MissileLevel}");
            if (player.MineLevel > 0) lines.Add($"Mina: {Glossary.LevelPrefix}{player.MineLevel}");
            if (player.BurnLevel > 0) lines.Add($"Incendiario: {Glossary.LevelPrefix}{player.BurnLevel}");
            if (player.OndaLevel > 0) lines.Add($"Onda de Choque: {Glossary.LevelPrefix}{player.OndaLevel}");
            if (player.VendavalLevel > 0) lines.Add($"Vendaval: {Glossary.LevelPrefix}{player.VendavalLevel}");
            if (player.EquippedUltimate != null) lines.Add($"Ultimate: {UltimateKindNames.Display(player.EquippedUltimate.Value)}");
        }

        _statsLabel.Text = string.Join("\n", lines);
        _cameraDistanceSlider.Value = gm.CameraDistance;
        UpdateCameraDistanceLabel(gm.CameraDistance);
        _loadoutMenu.Refresh(player);

        // Defensive reset: Visible only ever goes false once the countdown below completes, so this
        // shouldn't be reachable mid-countdown — but a fresh Open() is the right place to guarantee
        // the menu never reopens stuck disabled/mid-count regardless.
        _resuming = false;
        _resumeCountdownLabel.Visible = false;
        _resumeButton.Disabled = false;
        _menuButton.Disabled = false;

        Visible = true;
        Juice.ModalIn(_hbox);
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

    private int _lastCountdownSecond = -1;

    private void UpdateResumeCountdownLabel()
    {
        int secondsLeft = Mathf.CeilToInt(_resumeCountdownRemaining);
        if (secondsLeft != _lastCountdownSecond)
        {
            _resumeCountdownLabel.Text = $"Reanudando en...\n{secondsLeft}";
            Juice.ValuePop(_resumeCountdownLabel, 1.3f, 0.3f);
            _lastCountdownSecond = secondsLeft;
        }
    }

    // Doesn't resume immediately — counts down first so the player has a beat to get ready instead
    // of gameplay picking back up on the same frame they tapped the button. The pause/loadout panels
    // close first (rather than staying up underneath) so the countdown reads against the gameplay
    // you're about to drop back into, not a menu that's still on screen.
    private void OnResumePressed()
    {
        if (_resuming) return;

        _resuming = true;
        _resumeCountdownRemaining = ResumeCountdownDuration;
        _lastCountdownSecond = -1;
        _resumeButton.Disabled = true;
        _menuButton.Disabled = true;
        Juice.ModalOut(_hbox);
        _resumeCountdownLabel.Visible = true;
        UpdateResumeCountdownLabel();
    }

    // Abandoning a run is the most destructive thing the player can do outside of dying, and it used
    // to be one unconfirmed tap on a button labelled "Menu Principal" — which promises navigation,
    // not the end of the run. Worse, the score vanished with it: RegisterFinalScore only ran from
    // NotifyPlayerDied, so a 40-minute run abandoned at round 19 left no high score and no record row.
    //
    // Both halves are fixed here: it asks first, and GameManager.AbandonRun records the score on the
    // way out, so quitting early costs you the run but not the result.
    private void OnMenuPressed()
    {
        var dialog = GetTree().GetFirstNodeInGroup("confirm_dialog") as ConfirmDialog;
        if (dialog == null)
        {
            AbandonToMenu();
            return;
        }

        dialog.Ask(
            "¿Abandonar la partida?",
            $"Vas a volver al menú principal en la ronda {GameManager.Instance.RoundNumber}. " +
            $"Tu puntaje de {GameManager.Instance.Score} queda guardado, pero la partida termina acá.",
            "Abandonar",
            AbandonToMenu);
    }

    private void AbandonToMenu()
    {
        GameManager.Instance.AbandonRun();
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
    }

    private void OnCameraDistanceChanged(double value)
    {
        GameManager.Instance.CameraDistance = (float)value;
        GameManager.Instance.UpdateCameraExtents();
        UpdateCameraDistanceLabel((float)value);
    }

    // One name for one control. This label used to read "Zoom: N%" when the menu opened and rename
    // itself to "Distancia Cámara: N%" the moment you dragged the slider — the same value, two names,
    // and a third ("Zoom de cámara") on the options screen for the identical setting.
    private void UpdateCameraDistanceLabel(float distance)
    {
        float pct = 2000f / distance * 100f;
        _cameraDistanceLabel.Text = $"Zoom de cámara: {pct:0}%";
    }
}
