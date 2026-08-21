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
    private Button _deleteButton;
    private CharacterCreator _creator;

    public void Open()
    {
        RebuildOrder(GameManager.Instance.SelectedCharacter);
        Visible = true;
    }

    public override void _Ready()
    {
        _previewTexture = GetNode<TextureRect>("CenterContainer/Panel/Box/Carousel/Preview/Swatch/PreviewTexture");
        _nameLabel = GetNode<Label>("CenterContainer/Panel/Box/Identity/NameLabel");
        _descScroll = GetNode<ScrollContainer>("CenterContainer/Panel/Box/Identity/DescScroll");
        _descLabel = GetNode<Label>("CenterContainer/Panel/Box/Identity/DescScroll/DescLabel");
        _perkPanel = GetNode<PanelContainer>("CenterContainer/Panel/Box/PerkPanel");
        _perkLabel = GetNode<Label>("CenterContainer/Panel/Box/PerkPanel/PerkLabel");
        _confirmButton = GetNode<Button>("CenterContainer/Panel/Box/ConfirmButton");
        _deleteButton = GetNode<Button>("CenterContainer/Panel/Box/Actions/DeleteButton");
        _creator = GetNode<CharacterCreator>("CharacterCreator");

        GetNode<Button>("CenterContainer/Panel/Box/Carousel/LeftButton").Pressed += () => Step(-1);
        GetNode<Button>("CenterContainer/Panel/Box/Carousel/RightButton").Pressed += () => Step(1);
        GetNode<Button>("CenterContainer/Panel/Box/Actions/CreateButton").Pressed += () => _creator.Open();
        _deleteButton.Pressed += DeleteFramed;
        _confirmButton.Pressed += Confirm;
        GetNode<Button>("CenterContainer/Panel/Box/CancelButton").Pressed += () => Visible = false;

        // Land on the character that was just created rather than making the player hunt for it at
        // the end of the carousel.
        _creator.CharacterCreated += RebuildOrder;

        RebuildOrder(GameManager.Instance.SelectedCharacter);
    }

    private void RebuildOrder(string slugToFrame)
    {
        var all = CharacterCatalog.All;
        _order = new string[all.Length];
        for (int i = 0; i < all.Length; i++) _order[i] = all[i].Slug;

        _index = System.Array.IndexOf(_order, slugToFrame);
        if (_index < 0) _index = 0;
        RefreshPreview();
    }

    private void Step(int direction)
    {
        _index = (_index + direction + _order.Length) % _order.Length;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        var info = CharacterCatalog.Get(_order[_index]);

        // Same texture and same tint the arena will use, so the preview is the character rather than
        // a stand-in for it: a white silhouette picks up its Color, a portrait (Color = White) shows
        // its own photo untouched.
        _previewTexture.Texture = CharacterCatalog.Texture(info);
        _previewTexture.Modulate = info.Color;

        _nameLabel.Text = info.Name;
        _descLabel.Text = info.Description ?? "";
        _confirmButton.Text = $"Elegir a {info.Name}";

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

        FitDescriptionHeight(info.Description ?? "");
    }

    // Sizes the scroll area to exactly the text, capped so the panel can't outgrow the screen.
    // Measured with the font against DescriptionTextWidth rather than read off the label, so the
    // answer doesn't depend on whether layout has run yet.
    private void FitDescriptionHeight(string text)
    {
        if (text.Length == 0)
        {
            _descScroll.CustomMinimumSize = Vector2.Zero;
            return;
        }

        var font = _descLabel.GetThemeFont("font") ?? _descLabel.GetThemeDefaultFont();
        int fontSize = _descLabel.GetThemeFontSize("font_size");

        // One line of slack: GetMultilineStringSize measures glyph extents, and without it a
        // descender on the last line gets clipped by the scroll area's edge.
        float lineHeight = font.GetHeight(fontSize);
        float needed = font.GetMultilineStringSize(text, HorizontalAlignment.Left,
                           DescriptionTextWidth, fontSize).Y + lineHeight;

        _descScroll.CustomMinimumSize = new Vector2(0, Mathf.Min(needed, MaxDescriptionHeight));

        // Back to the top, or arriving from a long description leaves the next one already scrolled
        // past its own first line.
        _descScroll.ScrollVertical = 0;
    }

    private void DeleteFramed()
    {
        var info = CharacterCatalog.Get(_order[_index]);
        if (!info.IsCustom) return;

        CustomCharacterStore.Delete(info.Slug);

        // If the deleted character was the saved selection, the persisted slug now points at
        // nothing. Reset it rather than leaving a dangling value for Player._Ready to resolve.
        if (GameManager.Instance.SelectedCharacter == info.Slug)
            GameManager.Instance.SetSelectedCharacter(CharacterCatalog.DefaultSlug);

        RebuildOrder(GameManager.Instance.SelectedCharacter);
    }

    private void Confirm()
    {
        GameManager.Instance.SetSelectedCharacter(_order[_index]);
        GetTree().ChangeSceneToFile("res://Scenes/Arena.tscn");
    }
}
