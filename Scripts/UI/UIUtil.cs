namespace ShooterLoop;

using Godot;

public static class UIUtil
{
    public static StyleBoxFlat CreatePanelStyle(Color borderColor)
    {
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.043f, 0.024f, 0.078f, 0.94f);
        style.BorderColor = new Color(borderColor, 0.85f);
        style.SetBorderWidthAll(3);
        // Square, like every other edge in the game. This one line is the whole art direction for
        // five different panels -- shop, reward picker, confirm dialog, cosmetics.
        style.SetCornerRadiusAll(0);
        style.SetContentMarginAll(16f);
        style.ContentMarginTop = 14f;
        style.ContentMarginBottom = 14f;
        return style;
    }
}
