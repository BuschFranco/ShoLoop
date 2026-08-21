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

        var lines = new List<string>
        {
            $"Ronda {gm.RoundNumber}   Tiempo restante: {Mathf.CeilToInt(gm.RoundTimeRemaining)}s",
            $"Nivel {gm.Level}   Monedas: {gm.Coins}   Puntaje: {gm.Score}",
            $"Enemigos eliminados: {gm.EnemiesKilled}   Eliminaciones especiales: {gm.SpecialEnemiesKilled}",
        };

        if (player != null)
        {
            lines.Add($"Vidas: {player.CurrentLives}/{player.MaxLives}");
            lines.Add($"Escudo: {player.CurrentShieldCharges}/{player.MaxShieldCharges}");
            lines.Add($"Velocidad de movimiento: {player.MoveSpeed:0}");
            lines.Add($"Rango de disparo: {player.FireRange:0}");
            lines.Add($"Velocidad de ataque: {player.FireRate:0.0}/s");
            lines.Add($"Daño de bala: {player.BulletDamage}");
            lines.Add($"Disparo Doble: {(player.HasExtraProjectile ? "Sí" : "No")}");
            lines.Add($"Líneas de disparo lateral: {player.ExtraFiringLines}/{Player.MaxExtraFiringLinesCap}");
            lines.Add($"Cuchillas orbitales: {player.OrbitCount}");
            lines.Add($"Compañero: {(player.CompanionStatPercent > 0 ? $"{player.CompanionStatPercent * 100:0}% de tus estadísticas" : "No")}");

            var spawner = GetTree().GetFirstNodeInGroup("enemy_spawner") as EnemySpawner;
            if (spawner != null)
                lines.Add($"Poder: {player.GetOffensivePower():0.0}x   Ajuste enemigo: {spawner.CatchUpMultiplier:0.00}x");
        }

        _statsLabel.Text = string.Join("\n", lines);
        _cameraDistanceSlider.Value = gm.CameraDistance;
        float camPct = 2000f / gm.CameraDistance * 100f;
        _cameraDistanceLabel.Text = $"Distancia Cámara: {camPct:0}%";
        _loadoutMenu.Refresh(player);
        Visible = true;
    }

    private void OnResumePressed()
    {
        Visible = false;
        GameManager.Instance.Resume();
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
