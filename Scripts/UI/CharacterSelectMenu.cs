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
    // It stays a ScrollContainer purely as a ceiling: past MaxDescriptionHeight the panel would grow
    // taller than the screen and push the Confirm button out of reach, so the longest couple of
    // descriptions scroll instead of expanding.
    private ScrollContainer _descScroll;
    private const float MaxDescriptionHeight = 200f;

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
        _descLabel.Text = info.Description;
        _confirmButton.Text = $"Elegir a {info.Name}";

        // Only the player's own characters can be removed; the built-in cast is part of the game.
        _deleteButton.Visible = info.IsCustom;

        // Deferred because the line count is only known once the label has re-wrapped the new text
        // at its current width, which happens during layout — reading it in this same call would
        // still return the *previous* description's line count.
        CallDeferred(nameof(FitDescriptionHeight));
    }

    // Shrinks the scroll area to exactly the wrapped text, capped so the panel can't outgrow the
    // screen. GetLineCount counts wrapped lines, not newlines, which is what makes this work for
    // autowrapped text; the +1 line of slack keeps a descender from being clipped mid-glyph.
    private void FitDescriptionHeight()
    {
        float needed = (_descLabel.GetLineCount() + 1) * _descLabel.GetLineHeight();
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
