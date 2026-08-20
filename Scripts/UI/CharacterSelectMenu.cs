namespace ShooterLoop;

// Opened from MainMenu's "Comenzar" button instead of it changing scene directly. A carousel over
// the fixed CharacterCatalog: arrow buttons step through the 3 characters one at a time, the preview
// swatch/name/description update to match, and "Elegir" confirms whichever one is currently framed.
public partial class CharacterSelectMenu : Control
{
    private static readonly CharacterId[] Order =
    {
        CharacterId.Equilibrado, CharacterId.Centella, CharacterId.Coloso,
    };

    private int _index;
    private Panel _swatch;
    private Label _nameLabel;
    private Label _descLabel;
    private Button _confirmButton;

    public void Open()
    {
        _index = System.Array.IndexOf(Order, GameManager.Instance.SelectedCharacter);
        if (_index < 0) _index = 0;
        RefreshPreview();
        Visible = true;
    }

    public override void _Ready()
    {
        _swatch = GetNode<Panel>("CenterContainer/Panel/Box/Carousel/Preview/Swatch");
        _nameLabel = GetNode<Label>("CenterContainer/Panel/Box/Carousel/Preview/NameLabel");
        _descLabel = GetNode<Label>("CenterContainer/Panel/Box/DescLabel");
        _confirmButton = GetNode<Button>("CenterContainer/Panel/Box/ConfirmButton");

        GetNode<Button>("CenterContainer/Panel/Box/Carousel/LeftButton").Pressed += () => Step(-1);
        GetNode<Button>("CenterContainer/Panel/Box/Carousel/RightButton").Pressed += () => Step(1);
        _confirmButton.Pressed += Confirm;
        GetNode<Button>("CenterContainer/Panel/Box/CancelButton").Pressed += () => Visible = false;

        RefreshPreview();
    }

    private void Step(int direction)
    {
        _index = (_index + direction + Order.Length) % Order.Length;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        var info = CharacterCatalog.Get(Order[_index]);
        _swatch.SelfModulate = info.Color;
        _nameLabel.Text = info.Name;
        _descLabel.Text = info.Description;
        _confirmButton.Text = $"Elegir a {info.Name}";
    }

    private void Confirm()
    {
        GameManager.Instance.SetSelectedCharacter(Order[_index]);
        GetTree().ChangeSceneToFile("res://Scenes/Arena.tscn");
    }
}
