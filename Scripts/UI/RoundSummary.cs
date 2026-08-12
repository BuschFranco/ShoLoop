namespace ShooterLoop;

// End-of-round recap, shown from the moment a round ends until the next one actually starts.
//
// It deliberately stays up *through* the whole interstitial chain — level-up picks, the first-boss
// Ultimate choice, and the shop — so the numbers stay on screen while you're deciding what to
// spend them on. GameManager.BeginRoundEnd() shows it and StartNextRound() hides it, which lines
// its lifetime up exactly with the queue drained by ResolveNextInterstitial().
public partial class RoundSummary : Control
{
    private PanelContainer _panel;
    private Label _title;
    private Label _stats;

    public override void _Ready()
    {
        AddToGroup("round_summary");
        Visible = false;

        // Rendered while the tree is paused, same as the shop and pause menu.
        ProcessMode = ProcessModeEnum.Always;

        // Ignoring mouse input entirely keeps it purely decorative — it's meant to read as an
        // extension of the HUD's own stats, not a menu, and never swallows clicks meant for
        // whichever modal (level-up/Ultimate/shop) happens to be open on top of it.
        MouseFilter = MouseFilterEnum.Ignore;

        _panel = GetNode<PanelContainer>("Panel");
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

        // The .tscn's own offset_bottom is a safe non-zero fallback, same trick as HUD's
        // TopBarPanel — this shrinks the box to exactly fit however many stat lines actually
        // showed up (4 or 5), so it can never overflow into whatever modal is centered below it.
        _panel.ResetSize();
    }

    public void HideSummary() => Visible = false;
}
