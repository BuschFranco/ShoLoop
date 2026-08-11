namespace ShooterLoop;

public partial class BossBanner : Control
{
    private Label _label;

    public override void _Ready()
    {
        AddToGroup("boss_banner");
        MouseFilter = MouseFilterEnum.Ignore;
        _label = GetNode<Label>("Label");
        Modulate = new Color(1f, 1f, 1f, 0f);
    }

    public void Announce(int round)
    {
        _label.Text = "¡RONDA DE JEFE!";
        Modulate = new Color(1f, 1f, 1f, 1f);

        var tween = CreateTween();
        tween.TweenInterval(2f);
        tween.TweenProperty(this, "modulate:a", 0f, 0.6f);
    }
}
