namespace ShooterLoop;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        // Character select, Options and Builds are overlay *children* of this scene rather than
        // separate scenes, so this runs once per actual visit to the menu — opening a submenu won't
        // restart the track. (PlayMusic also no-ops when the requested track is already playing.)
        AudioManager.Instance?.PlayMusic(AudioManager.MusicTrack.Menu);

        var startButton = GetNode<Button>("VBoxContainer/ButtonsRow/StartButton");
        var optionsButton = GetNode<Button>("VBoxContainer/ButtonsRow/OptionsButton");
        var buildsButton = GetNode<Button>("VBoxContainer/ButtonsRow/BuildsButton");
        var tiendaButton = GetNode<Button>("VBoxContainer/ButtonsRow/TiendaButton");
        var characterSelect = GetNode<CharacterSelectMenu>("CharacterSelectMenu");
        startButton.Pressed += characterSelect.Open;
        startButton.GrabFocus();

        Juice.WireButtonFeedback(startButton);
        Juice.WireButtonFeedback(optionsButton);
        Juice.WireButtonFeedback(buildsButton);
        Juice.WireButtonFeedback(tiendaButton);

        var highScoreLabel = GetNode<Label>("VBoxContainer/HighScoreLabel");
        highScoreLabel.Text = $"Mejor puntaje: {GameManager.LoadHighScore()}";

        // Read from the live instance, not a static file re-read like the high score above — Libras
        // is already loaded into GameManager.Instance at boot. CharacterSelectMenu is an overlay
        // *child* of this menu, not a scene swap, so MainMenu's own Visible never toggles while it's
        // open — the refresh has to hook the overlay's visibility instead, so spending Libras in
        // there and hitting Cancel updates the balance shown underneath.
        var librasLabel = GetNode<Label>("VBoxContainer/LibrasLabel");
        var accountLevelLabel = GetNode<Label>("VBoxContainer/AccountLevelLabel");
        var accountLevelBar = GetNode<ProgressBar>("VBoxContainer/AccountLevelBarRow/AccountLevelBar");
        RefreshMetaLabels(librasLabel, accountLevelLabel, accountLevelBar);
        characterSelect.VisibilityChanged += () =>
        {
            if (!characterSelect.Visible) RefreshMetaLabels(librasLabel, accountLevelLabel, accountLevelBar);
        };

        var options = GetNode<OptionsMenu>("OptionsMenu");
        optionsButton.Pressed += options.Open;

        var builds = GetNode<BuildsMenu>("BuildsMenu");
        buildsButton.Pressed += builds.Open;

        // Same refresh-on-close hook as CharacterSelectMenu above — the shop is the other place Libras
        // can be spent, and this is the label that has to notice.
        var cosmeticsShop = GetNode<CosmeticsShopMenu>("CosmeticsShopMenu");
        tiendaButton.Pressed += cosmeticsShop.Open;
        cosmeticsShop.VisibilityChanged += () =>
        {
            if (!cosmeticsShop.Visible) RefreshMetaLabels(librasLabel, accountLevelLabel, accountLevelBar);
        };

        // Same refresh-on-close hook — achievement/mission payouts also spend into the same Libras
        // balance shown here.
        var achievementsButton = GetNode<Button>("AchievementsButton");
        var achievementsMenu = GetNode<AchievementsMenu>("AchievementsMenu");
        achievementsButton.Pressed += achievementsMenu.Open;
        Juice.WireButtonFeedback(achievementsButton);
        achievementsMenu.VisibilityChanged += () =>
        {
            if (!achievementsMenu.Visible) RefreshMetaLabels(librasLabel, accountLevelLabel, accountLevelBar);
        };

        // Split out of the combined Logros screen into its own button/screen, immediately to the
        // left of it — same refresh-on-close reasoning (missions also pay Libras).
        var missionsButton = GetNode<Button>("MissionsButton");
        var missionsMenu = GetNode<MissionsMenu>("MissionsMenu");
        missionsButton.Pressed += missionsMenu.Open;
        Juice.WireButtonFeedback(missionsButton);
        missionsMenu.VisibilityChanged += () =>
        {
            if (!missionsMenu.Visible) RefreshMetaLabels(librasLabel, accountLevelLabel, accountLevelBar);
        };

        // Read-only — no VisibilityChanged refresh hook needed, nothing here spends or earns Libras.
        var statsButton = GetNode<Button>("StatsButton");
        var statsMenu = GetNode<StatsMenu>("StatsMenu");
        statsButton.Pressed += statsMenu.Open;
        Juice.WireButtonFeedback(statsButton);

        AnimateTitle();
        PopulateRecords();
        PlayEntranceAnimation(highScoreLabel);
    }

    // The title bobs letter by letter and breathes between cyan and white, which is the attract-mode
    // look the screen was missing.
    //
    // Godot's built-in RichTextLabel effects do the per-letter part -- [wave] offsets each glyph on
    // its own phase, which a Label can't do at all without being split into one node per character.
    // That's the whole reason Title is a RichTextLabel now. The colour breath stays a Tween, because
    // [rainbow] is the only built-in colour effect and it would throw away the game's palette.
    //
    // Both are skipped under reduced motion: a title that never stops moving is exactly what that
    // setting exists to turn off.
    private void AnimateTitle()
    {
        var title = GetNode<RichTextLabel>("VBoxContainer/Title");

        if (DangerLevel.Reduced)
        {
            title.Text = "[center]INFINITIX[/center]";
            return;
        }

        // Godot divides amp by 10 internally (offset = sin(...) * amp/10), so 90 is a +/-9px bob on
        // a 40px title -- enough to read as movement across the room. The default 5.0 freq reads as a
        // glitch at this size; 2.6 reads as a sign swaying.
        title.Text = "[center][wave amp=90.0 freq=2.6]INFINITIX[/wave][/center]";

        Juice.Shimmer(title, "theme_override_colors/default_color",
            new Color(0.3f, 1f, 1f), new Color(0.85f, 1f, 1f), 2.2f);
    }

    // The title screen's own "arrival" — the first thing a player sees, so it fades+scales up as a
    // whole rather than snapping into place, and the high score/records callout (the part most
    // worth a second look) settles in a beat after the rest.
    private void PlayEntranceAnimation(Label highScoreLabel)
    {
        var vbox = GetNode<Control>("VBoxContainer");
        var recordsPanel = GetNode<Control>("VBoxContainer/RecordsRow/RecordsPanel");

        highScoreLabel.Modulate = new Color(1f, 1f, 1f, 0f);
        recordsPanel.Modulate = new Color(1f, 1f, 1f, 0f);

        Juice.ModalIn(vbox, 0.35f, 0.92f);

        var timer = GetTree().CreateTimer(0.2f);
        timer.Timeout += () =>
        {
            highScoreLabel.CreateTween().TweenProperty(highScoreLabel, "modulate:a", 1f, 0.25f);
        };

        var timer2 = GetTree().CreateTimer(0.32f);
        timer2.Timeout += () =>
        {
            recordsPanel.CreateTween().TweenProperty(recordsPanel, "modulate:a", 1f, 0.25f);
        };
    }

    private void RefreshMetaLabels(Label librasLabel, Label accountLevelLabel, ProgressBar accountLevelBar)
    {
        var gm = GameManager.Instance;
        librasLabel.Text = $"Libras: {gm.Libras}";
        accountLevelLabel.Text = $"Nivel de cuenta: {gm.AccountLevel}";
        accountLevelBar.MaxValue = gm.AccountXpToNextLevel;
        accountLevelBar.Value = gm.AccountXp;
    }

    // The full top-10 the save file keeps. This panel has the room for all of it, unlike character
    // select's, which is squeezed in beside a portrait.
    private const int MaxRecordsShown = 10;

    // How many monospaced characters fit across RecordsPanel: 340px wide, less 14px content margin
    // and a 2px border on each side, leaves 308px. PixelFont's advance is 0.6 em (6 blocks of 10 to
    // the em, see tools/gen_font.py), so at font_size 16 that's 9.6px per character and about 32 fit.
    // Held at 25 deliberately -- the budget only decides whether the column gap is one space or two,
    // and leaving headroom means a longer date format or a wider score can't start wrapping rows.
    private const int CharBudget = 25;

    private void PopulateRecords()
    {
        var list = GetNode<RichTextLabel>("VBoxContainer/RecordsRow/RecordsPanel/RecordsBox/RecordsList");
        list.Text = RecordTable.Build(GameManager.LoadRecords(), MaxRecordsShown, CharBudget,
            "Todavía no hay récords", animateFirst: true);
    }
}
