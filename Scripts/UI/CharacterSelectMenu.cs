namespace ShooterLoop;

// Opened from MainMenu's "Comenzar" button instead of it changing scene directly. A carousel over
// CharacterCatalog: arrow buttons step through the cast one at a time, the preview
// image/name/description update to match, and "Elegir" confirms whichever one is currently framed.
//
// The cast is not fixed — the player can add their own (CharacterCreator) and delete them again —
// so the order is rebuilt on every Open rather than cached once.
public partial class CharacterSelectMenu : Control
{
    private string[] _order = System.Array.Empty<string>();
    private int _index;

    private TextureRect _previewTexture;

    // The description area is sized to whatever the current description actually needs (see
    // FitDescriptionHeight) instead of being one fixed height for everybody — the lengths range from
    // eight words to a couple hundred, so a single height left a big dead gap under the short ones.
    //
    // It stays a ScrollContainer purely as a ceiling, and the ceiling is not decorative: it is what
    // stops a player-created description of 600 pressed Enters (the creator's field is a multi-line
    // TextEdit) from pushing Elegir and Cancelar off-screen, with no Escape handler to close the menu.
    // Real descriptions land well under it, so no scrollbar ever appears for actual content.
    //
    // The budget it has to fit inside is the LANDSCAPE viewport — 1152x648 logical, i.e. only 648
    // tall, far tighter than portrait's 1152. Everything else in this panel adds up to ~440px, so
    // this is what's left.
    private ScrollContainer _descScroll;
    private const float MaxDescriptionHeight = 190f;

    // The panel's fixed 600px minus the stylebox's 18px content margins. Hardcoded on purpose: the
    // height below is measured against a known width instead of asking the label how it wrapped,
    // because Label.GetLineCount() reports against its *current* width — which is still 0 on the
    // layout pass this runs in, so it returned a wild line count and pinned every description to the
    // ceiling regardless of length. Measuring against the width we already control has no such
    // ordering problem, and removes the need to defer this a frame at all.
    private const float DescriptionTextWidth = 564f;

    private PanelContainer _perkPanel;
    private Label _perkLabel;

    private Label _nameLabel;
    private Label _descLabel;
    private Button _confirmButton;
    private Button _unlockButton;
    private Button _deleteButton;
    private Label _nucleosLabel;
    private CharacterCreator _creator;
    private PanelContainer _panel;
    private Control _carouselPreview;
    private VBoxContainer _identity;

    // Guards against a rapid-fire double-press queuing a second crossfade on top of one still in
    // flight — the two would fight over the same nodes' Position/Modulate.
    private bool _isTransitioning;

    public void Open()
    {
        RebuildOrder(GameManager.Instance.SelectedCharacter);
        Juice.ModalIn(_panel);
        Visible = true;
    }

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("CenterContainer/Panel");
        _carouselPreview = GetNode<Control>("CenterContainer/Panel/Box/Carousel/Preview");
        _previewTexture = GetNode<TextureRect>("CenterContainer/Panel/Box/Carousel/Preview/Swatch/PreviewTexture");
        _identity = GetNode<VBoxContainer>("CenterContainer/Panel/Box/Identity");
        _nameLabel = GetNode<Label>("CenterContainer/Panel/Box/Identity/NameLabel");
        _descScroll = GetNode<ScrollContainer>("CenterContainer/Panel/Box/Identity/DescScroll");
        _descLabel = GetNode<Label>("CenterContainer/Panel/Box/Identity/DescScroll/DescLabel");
        _perkPanel = GetNode<PanelContainer>("CenterContainer/Panel/Box/PerkPanel");
        _perkLabel = GetNode<Label>("CenterContainer/Panel/Box/PerkPanel/PerkLabel");
        _confirmButton = GetNode<Button>("CenterContainer/Panel/Box/ConfirmButton");
        _unlockButton = GetNode<Button>("CenterContainer/Panel/Box/UnlockButton");
        _deleteButton = GetNode<Button>("CenterContainer/Panel/Box/Actions/DeleteButton");
        _nucleosLabel = GetNode<Label>("CenterContainer/Panel/Box/NucleosLabel");
        _creator = GetNode<CharacterCreator>("CharacterCreator");

        var leftButton = GetNode<Button>("CenterContainer/Panel/Box/Carousel/LeftButton");
        var rightButton = GetNode<Button>("CenterContainer/Panel/Box/Carousel/RightButton");
        var createButton = GetNode<Button>("CenterContainer/Panel/Box/Actions/CreateButton");
        var cancelButton = GetNode<Button>("CenterContainer/Panel/Box/CancelButton");

        leftButton.Pressed += () => Step(-1);
        rightButton.Pressed += () => Step(1);
        createButton.Pressed += () => _creator.Open();
        _deleteButton.Pressed += DeleteFramed;
        _confirmButton.Pressed += Confirm;
        _unlockButton.Pressed += UnlockFramed;
        cancelButton.Pressed += () => Juice.ModalOut(_panel, () => Visible = false);

        foreach (var b in new[] { leftButton, rightButton, createButton, _deleteButton, _confirmButton, _unlockButton, cancelButton })
            Juice.WireButtonFeedback(b);

        // Land on the character that was just created rather than making the player hunt for it at
        // the end of the carousel.
        _creator.CharacterCreated += slug => RebuildOrder(slug, animate: true);

        RebuildOrder(GameManager.Instance.SelectedCharacter);
    }

    private void RebuildOrder(string slugToFrame) => RebuildOrder(slugToFrame, animate: false);

    private void RebuildOrder(string slugToFrame, bool animate)
    {
        var all = CharacterCatalog.All;
        _order = new string[all.Length];
        for (int i = 0; i < all.Length; i++) _order[i] = all[i].Slug;

        _index = System.Array.IndexOf(_order, slugToFrame);
        if (_index < 0) _index = 0;

        if (animate)
            CrossfadeSwap(RefreshPreview, 1);
        else
            RefreshPreview();
    }

    private void Step(int direction)
    {
        if (_isTransitioning) return;
        _index = (_index + direction + _order.Length) % _order.Length;
        CrossfadeSwap(RefreshPreview, direction);
    }

    // The actual "carousel" motion: the currently framed character's preview/name/description/perk
    // slide+fade out in the direction of travel, the data swap happens while nothing is visible, and
    // the new character's content slides+fades in from the opposite side.
    //
    // _perkPanel is deliberately fade-only, no position/slide: its Y sits below _identity in the
    // same VBoxContainer, and FitDescriptionHeight (called by swap() below) starts its own tween
    // resizing the description box — which, for a much longer/shorter description than the outgoing
    // character's, moves _perkPanel's true rest Y over the following ~0.15s. A position tween here
    // targeting the Y captured *before* that resize would fight it and could win the race, freezing
    // the perk box at a stale height's position — overlapping the description for exactly the
    // "some characters" case this was reported for. Preview/Identity don't have this problem: nothing
    // above either of them ever resizes, so their rest Y never moves.
    private void CrossfadeSwap(System.Action swap, int direction)
    {
        _isTransitioning = true;
        var slideTargets = new[] { _carouselPreview, (Control)_identity };
        float slide = 36f * (direction < 0 ? -1f : 1f);

        // Captured once, before anything moves. The out-tween leaves each target sitting at
        // restPos + slide, not back at restPos — reading .Position again afterward (an earlier bug)
        // returned that departed value, so the in-tween's "final" target quietly baked in one
        // slide's worth of permanent offset every single step, compounding further each time the
        // carousel was stepped again.
        var restPositions = new Vector2[slideTargets.Length];
        for (int i = 0; i < slideTargets.Length; i++) restPositions[i] = slideTargets[i].Position;

        var outTween = CreateTween();
        outTween.SetParallel(true);
        for (int i = 0; i < slideTargets.Length; i++)
        {
            var t = slideTargets[i];
            outTween.TweenProperty(t, "position", restPositions[i] + new Vector2(slide, 0f), 0.11f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            outTween.TweenProperty(t, "modulate:a", 0f, 0.11f);
        }
        outTween.TweenProperty(_perkPanel, "modulate:a", 0f, 0.11f);

        outTween.Chain().TweenCallback(Callable.From(() =>
        {
            swap();

            var inTween = CreateTween();
            inTween.SetParallel(true);
            for (int i = 0; i < slideTargets.Length; i++)
            {
                var t = slideTargets[i];
                t.Position = restPositions[i] - new Vector2(slide, 0f);
                t.Modulate = new Color(1f, 1f, 1f, 0f);
                inTween.TweenProperty(t, "position", restPositions[i], 0.16f)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                inTween.TweenProperty(t, "modulate:a", 1f, 0.16f);
            }
            _perkPanel.Modulate = new Color(1f, 1f, 1f, 0f);
            inTween.TweenProperty(_perkPanel, "modulate:a", 1f, 0.16f);
            inTween.Chain().TweenCallback(Callable.From(() => _isTransitioning = false));
        }));
    }

    private void RefreshPreview()
    {
        var info = CharacterCatalog.Get(_order[_index]);
        bool locked = !CharacterCatalog.IsUnlocked(info);

        // Same texture and same tint the arena will use, so the preview is the character rather than
        // a stand-in for it: a white silhouette picks up its Color, a portrait (Color = White) shows
        // its own photo untouched. Locked ones dim to 55% alpha — same "disabled" convention the
        // RewardCard uses — rather than hiding the portrait outright, since seeing who you're saving
        // up for is part of the motivation to keep playing.
        _previewTexture.Texture = CharacterCatalog.Texture(info);
        var tint = info.Color;
        _previewTexture.Modulate = locked ? new Color(tint.R, tint.G, tint.B, 0.55f) : tint;

        // Marks the pilot the run would currently start with. The carousel silently *opened* on this
        // one, but nothing said so — step once and there was no way back to "which was I using"
        // except memory, and the confirm button read identically either way.
        bool isEquipped = info.Slug == GameManager.Instance.SelectedCharacter;
        _nameLabel.Text = isEquipped ? $"{info.Name}  ·  ACTUAL" : info.Name;
        _descLabel.Text = info.Description ?? "";
        _confirmButton.Text = isEquipped ? $"Jugar con {info.Name}" : $"Elegir a {info.Name}";

        // IsNullOrWhiteSpace, not != "": CharacterInfo is a struct, so a character that never sets
        // PerkText arrives with null — which is every custom character, since CustomCharacterStore
        // deliberately leaves perks off them.
        _perkLabel.Text = info.PerkText ?? "";
        _perkPanel.Visible = !string.IsNullOrWhiteSpace(info.PerkText);

        // Equilibrado and the three stat characters use one box or the other, never both, so the
        // description box hides itself too rather than leaving an empty gap above the perk.
        _descScroll.Visible = !string.IsNullOrWhiteSpace(info.Description);

        // Only the player's own characters can be removed; the built-in cast is part of the game.
        _deleteButton.Visible = info.IsCustom;

        // Confirm and Unlock are mutually exclusive states for the framed character, never both.
        _confirmButton.Visible = !locked;
        _unlockButton.Visible = locked;
        if (locked)
        {
            _unlockButton.Text = $"Desbloquear ({info.UnlockCost} Núcleos)";
            _unlockButton.Disabled = GameManager.Instance.MetaCurrency < info.UnlockCost;
        }

        _nucleosLabel.Text = $"Núcleos: {GameManager.Instance.MetaCurrency}";

        FitDescriptionHeight(info.Description ?? "");
    }

    // Spends and unlocks in place — no auto-select, no closing the carousel — so the player can
    // immediately see the character they just unlocked (Confirm swaps in for Unlock right here)
    // instead of being dropped back into a run before they've even looked at what they bought.
    private void UnlockFramed()
    {
        var info = CharacterCatalog.Get(_order[_index]);
        if (!GameManager.Instance.TryUnlockCharacter(info.Slug, info.UnlockCost)) return;

        RefreshPreview();
    }

    // Sizes the scroll area to exactly the text, capped so the panel can't outgrow the screen.
    // Measured with the font against DescriptionTextWidth rather than read off the label, so the
    // answer doesn't depend on whether layout has run yet.
    private void FitDescriptionHeight(string text)
    {
        Vector2 target;
        if (text.Length == 0)
        {
            target = Vector2.Zero;
        }
        else
        {
            var font = _descLabel.GetThemeFont("font") ?? _descLabel.GetThemeDefaultFont();
            int fontSize = _descLabel.GetThemeFontSize("font_size");

            // One line of slack: GetMultilineStringSize measures glyph extents, and without it a
            // descender on the last line gets clipped by the scroll area's edge.
            float lineHeight = font.GetHeight(fontSize);
            float needed = font.GetMultilineStringSize(text, HorizontalAlignment.Left,
                               DescriptionTextWidth, fontSize).Y + lineHeight;
            target = new Vector2(0, Mathf.Min(needed, MaxDescriptionHeight));
        }

        // Eased instead of snapped, so switching between a short and a long description doesn't
        // jump the panel's height in one frame — mostly invisible in practice since this runs while
        // the identity block is faded out mid-crossfade, but still smooth for any caller that isn't.
        _descScroll.CreateTween().TweenProperty(_descScroll, "custom_minimum_size", target, 0.15f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        // Back to the top, or arriving from a long description leaves the next one already scrolled
        // past its own first line.
        _descScroll.ScrollVertical = 0;
    }

    // Confirmed because CustomCharacterStore.Delete doesn't just drop a config entry — it also
    // RemoveAbsolute's the portrait file, which for a custom pilot is a photo the player imported
    // from their own gallery. Unrecoverable, and the button sat unguarded in the same row as the
    // benign "+ Crear piloto".
    private void DeleteFramed()
    {
        var info = CharacterCatalog.Get(_order[_index]);
        if (!info.IsCustom) return;

        var dialog = GetTree().GetFirstNodeInGroup("confirm_dialog") as ConfirmDialog;
        if (dialog == null)
        {
            PerformDelete(info.Slug);
            return;
        }

        dialog.Ask(
            $"¿Borrar a {info.Name}?",
            "Se borra el piloto y también la imagen que elegiste para él. No se puede deshacer.",
            "Borrar",
            () => PerformDelete(info.Slug));
    }

    private void PerformDelete(string slug)
    {
        var info = CharacterCatalog.Get(slug);
        if (!info.IsCustom) return;

        CustomCharacterStore.Delete(info.Slug);

        // If the deleted character was the saved selection, the persisted slug now points at
        // nothing. Reset it rather than leaving a dangling value for Player._Ready to resolve.
        if (GameManager.Instance.SelectedCharacter == info.Slug)
            GameManager.Instance.SetSelectedCharacter(CharacterCatalog.DefaultSlug);

        RebuildOrder(GameManager.Instance.SelectedCharacter, animate: true);
    }

    private void Confirm()
    {
        var info = CharacterCatalog.Get(_order[_index]);
        if (!CharacterCatalog.IsUnlocked(info)) return;   // Confirm is hidden while locked; this is just defense in depth

        GameManager.Instance.SetSelectedCharacter(info.Slug);
        GetTree().ChangeSceneToFile("res://Scenes/Arena.tscn");
    }
}
