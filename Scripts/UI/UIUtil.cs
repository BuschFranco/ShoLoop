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

    // --- Keeping modal panels on screen -------------------------------------------------------
    //
    // A ScrollContainer inside a CenterContainer has no height of its own to speak of: it reports a
    // near-zero minimum for the axis it scrolls, which is what lets it scroll in the first place. So
    // its custom_minimum_size doesn't merely raise a floor there, it *is* the height — and until one
    // is set, the panel around it grows to whatever its content wants and runs off the screen.
    //
    // Several screens set that height to a hardcoded number measured against the 648px landscape
    // viewport. Those numbers were correct when they were written and silently wrong the moment
    // anything changed: swapping the project font for one with a taller line box grew every label in
    // the game and pushed panels off the bottom of the screen, with nothing in the code to notice.
    // Deriving the height from the live viewport instead means the panel is bounded by construction.

    /// <summary>
    /// The tallest a scroll area may be and still leave room for the rest of its panel. Use this when
    /// the caller wants to animate toward the value; <see cref="FitScrollToViewport"/> assigns it.
    /// </summary>
    public static float AvailableScrollHeight(Control panel, Control scroll,
        float fraction = 0.92f, float minHeight = 80f)
    {
        if (panel == null || scroll == null) return minHeight;

        // The scroll contributes exactly its custom_minimum_size to the panel's minimum (see above),
        // so taking that back out leaves the chrome — titles, buttons, margins — it has to share the
        // panel with. Reading a stale value is harmless: it's the same one being subtracted.
        float chrome = panel.GetCombinedMinimumSize().Y - scroll.CustomMinimumSize.Y;
        return Mathf.Max(minHeight, scroll.GetViewportRect().Size.Y * fraction - chrome);
    }

    /// <summary>
    /// Sizes a scroll area to its content, capped so the panel around it fits on screen. Short menus
    /// stay short — the cap is a ceiling, not a target.
    /// </summary>
    public static void FitScrollToViewport(ScrollContainer scroll, Control panel,
        float fraction = 0.92f, float minHeight = 120f)
    {
        if (scroll == null || panel == null) return;

        var content = scroll.GetChildCount() > 0 ? scroll.GetChild(0) as Control : null;
        float needed = content?.GetCombinedMinimumSize().Y ?? 0f;
        float available = AvailableScrollHeight(panel, scroll, fraction, minHeight);

        scroll.CustomMinimumSize = new Vector2(
            scroll.CustomMinimumSize.X, Mathf.Max(minHeight, Mathf.Min(needed, available)));
    }
}
