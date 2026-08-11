namespace ShooterLoop;

public partial class PauseMenu : Control
{
    private Label _statsLabel;
    private Button _resumeButton;

    public override void _Ready()
    {
        AddToGroup("pause_menu");
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;

        _statsLabel = GetNode<Label>("Panel/VBoxContainer/StatsLabel");
        _resumeButton = GetNode<Button>("Panel/VBoxContainer/ResumeButton");
        _resumeButton.Pressed += OnResumePressed;
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
        Visible = true;
    }

    private void OnResumePressed()
    {
        Visible = false;
        GameManager.Instance.Resume();
    }
}
