namespace ShooterLoop;

public partial class PauseMenu : Control
{
    private Label _statsLabel;
    private Button _resumeButton;
    private Button _menuButton;
    private PanelContainer _pausePanel;
    private LoadoutMenu _loadoutMenu;

    public override void _Ready()
    {
        AddToGroup("pause_menu");
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;

        _statsLabel = GetNode<Label>("CenterContainer/HBox/Panel/VBoxContainer/StatsLabel");
        _resumeButton = GetNode<Button>("CenterContainer/HBox/Panel/VBoxContainer/ResumeButton");
        _menuButton = GetNode<Button>("CenterContainer/HBox/Panel/VBoxContainer/MenuButton");
        _pausePanel = GetNode<PanelContainer>("CenterContainer/HBox/Panel");
        _loadoutMenu = GetNode<LoadoutMenu>("CenterContainer/HBox/LoadoutMenu");
        _resumeButton.Pressed += OnResumePressed;
        _menuButton.Pressed += OnMenuPressed;

        // Portrait (648px wide) can't fit both panels at landscape widths — shrink both so the
        // HBox still fits side by side with a comfortable margin.
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

            // Adaptive-difficulty readout — see DifficultyBalancer. Surfaced so the auto-scaling
            // is visible while playtesting instead of silently changing enemy stats.
            var spawner = GetTree().GetFirstNodeInGroup("enemy_spawner") as EnemySpawner;
            if (spawner != null)
                lines.Add($"Poder: {player.GetOffensivePower():0.0}x   Ajuste enemigo: {spawner.CatchUpMultiplier:0.00}x");
        }

        _statsLabel.Text = string.Join("\n", lines);
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
        // Abandon the run entirely: same path as the Game Over screen, so no run state carries
        // back into the main menu (and the next run starts from a clean ResetRun).
        GameManager.Instance.ResetRun();
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
    }
}
