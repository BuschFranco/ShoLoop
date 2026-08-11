namespace ShooterLoop;

// End-of-round recap, shown from the moment a round ends until the next one actually starts.
//
// It deliberately stays up *through* the whole interstitial chain — level-up picks, the first-boss
// Ultimate choice, and the shop — so the numbers stay on screen while you're deciding what to
// spend them on. GameManager.BeginRoundEnd() shows it and StartNextRound() hides it, which lines
// its lifetime up exactly with the queue drained by ResolveNextInterstitial().
public partial class RoundSummary : Control
{
    private Label _title;
    private Label _stats;

    public override void _Ready()
    {
        AddToGroup("round_summary");
        Visible = false;

        // Rendered while the tree is paused, same as the shop and pause menu.
        ProcessMode = ProcessModeEnum.Always;

        // It sits on a CanvasLayer *above* the shop so the shop's full-screen dim doesn't grey it
        // out — which means it would otherwise sit on top of the shop's buttons and swallow their
        // clicks. Ignoring mouse input entirely keeps it purely decorative.
        MouseFilter = MouseFilterEnum.Ignore;

        _title = GetNode<Label>("Panel/VBoxContainer/Title");
        _stats = GetNode<Label>("Panel/VBoxContainer/Stats");
    }

    public void ShowSummary(int round, int kills, int eliteKills, int coins, int score, int levels)
    {
        _title.Text = $"RONDA {round} COMPLETADA";

        var lines = new List<string>
        {
            $"Enemigos eliminados:  {kills}",
            $"De élite / jefes:  {eliteKills}",
            $"Monedas ganadas:  {coins}",
            $"Puntaje ganado:  {score}",
        };

        // Only worth a line when it actually happened — otherwise it reads as a permanent "0".
        if (levels > 0)
            lines.Add($"Niveles subidos:  {levels}");

        _stats.Text = string.Join("\n", lines);
        Visible = true;
    }

    public void HideSummary() => Visible = false;
}
