namespace ShooterLoop;

using Godot;

// "Crear piloto": pick an image off the device, type a name and a description, and it becomes a
// selectable character stored locally (CustomCharacterStore). Opened from CharacterSelectMenu and
// layered over it.
//
// The image is processed on save, not here — this screen only remembers which file was picked and
// shows it raw as a preview, so a photo that turns out to be wrong can be re-picked for free.
public partial class CharacterCreator : Control
{
    [Signal]
    public delegate void CharacterCreatedEventHandler(string slug);

    private LineEdit _nameEdit;
    private TextEdit _descEdit;
    private TextureRect _preview;
    private Label _errorLabel;
    private FileDialog _fileDialog;
    private Button _saveButton;

    private string _pickedImagePath = "";
    private Timer _galleryPollTimer;

    public override void _Ready()
    {
        _nameEdit = GetNode<LineEdit>("CenterContainer/Panel/Box/NameEdit");
        _descEdit = GetNode<TextEdit>("CenterContainer/Panel/Box/DescEdit");
        _preview = GetNode<TextureRect>("CenterContainer/Panel/Box/Top/Swatch/PreviewTexture");
        _errorLabel = GetNode<Label>("CenterContainer/Panel/Box/ErrorLabel");
        _fileDialog = GetNode<FileDialog>("FileDialog");
        _saveButton = GetNode<Button>("CenterContainer/Panel/Box/SaveButton");

        GetNode<Button>("CenterContainer/Panel/Box/Top/Side/ImageButton").Pressed += OnImageButtonPressed;
        _saveButton.Pressed += Save;
        GetNode<Button>("CenterContainer/Panel/Box/CancelButton").Pressed += () => Visible = false;

        _fileDialog.FileSelected += OnFileSelected;

        _nameEdit.MaxLength = CustomCharacterStore.MaxNameLength;

        if (OS.HasFeature("android"))
        {
            _galleryPollTimer = new Timer();
            _galleryPollTimer.WaitTime = 0.15;
            _galleryPollTimer.OneShot = true;
            _galleryPollTimer.Timeout += OnGalleryPoll;
            AddChild(_galleryPollTimer);
        }
    }

    public override void _ExitTree()
    {
        if (_galleryPollTimer != null)
        {
            _galleryPollTimer.Timeout -= OnGalleryPoll;
            _galleryPollTimer.QueueFree();
            _galleryPollTimer = null;
        }
    }

    public void Open()
    {
        _nameEdit.Text = "";
        _descEdit.Text = "";
        _pickedImagePath = "";
        _preview.Texture = null;
        ShowError("");
        Visible = true;
        _nameEdit.GrabFocus();
    }

    private void OnImageButtonPressed()
    {
        if (OS.HasFeature("android"))
        {
            RequestNativeGallery();
        }
        else
        {
            _fileDialog.PopupCentered();
        }
    }

    private void RequestNativeGallery()
    {
        using var flag = FileAccess.Open("user://.gallery_request", FileAccess.ModeFlags.Write);
        if (flag == null)
        {
            ShowError("No pude abrir la galería.");
            return;
        }
        _galleryPollTimer.Start();
    }

    private void OnGalleryPoll()
    {
        using var ready = FileAccess.Open("user://.gallery_ready", FileAccess.ModeFlags.Read);
        if (ready == null)
        {
            _galleryPollTimer.Start();
            return;
        }

        string status = ready.GetAsText().Trim();
        ready.Close();
        using (var dir = DirAccess.Open("user://"))
            dir?.Remove(".gallery_ready");

        if (status == "cancelled")
            return;

        if (status != "ok")
        {
            ShowError("No pude cargar la imagen.");
            return;
        }

        const string tempPath = "user://gallery_pick.png";
        var image = Image.LoadFromFile(tempPath);
        if (image == null)
        {
            ShowError("No pude leer esa imagen.");
            return;
        }

        _pickedImagePath = tempPath;
        _preview.Texture = ImageTexture.CreateFromImage(image);
        ShowError("");
    }

    private void OnFileSelected(string path)
    {
        // Shown as-is (uncropped) so the player sees the file they actually picked. Load it here
        // rather than at save time so an unreadable file is caught immediately, next to the button
        // that caused it.
        var image = Image.LoadFromFile(path);
        if (image == null)
        {
            ShowError("No pude leer esa imagen.");
            return;
        }

        _pickedImagePath = path;
        _preview.Texture = ImageTexture.CreateFromImage(image);
        ShowError("");
    }

    private void Save()
    {
        // Newlines and tabs are collapsed: both values are stored as single-line ConfigFile entries,
        // and a pasted multi-line description would otherwise corrupt the file it's written to.
        string name = CustomCharacterStore.Sanitise(_nameEdit.Text);
        string description = CustomCharacterStore.Sanitise(_descEdit.Text);

        string slug = CustomCharacterStore.Create(name, description, _pickedImagePath, out string error);
        if (slug == null)
        {
            ShowError(error);
            return;
        }

        Visible = false;
        EmitSignal(SignalName.CharacterCreated, slug);
    }

    private void ShowError(string message)
    {
        _errorLabel.Text = message;
        _errorLabel.Visible = message.Length > 0;
    }
}
