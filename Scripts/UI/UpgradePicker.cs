namespace ShooterLoop;

public partial class UpgradePicker : Control
{
    private static readonly PackedScene CardScene = GD.Load<PackedScene>("res://Scenes/UI/RewardCard.tscn");

    private static readonly StyleBoxFlat RewardPanelStyle = CreatePanelStyle(Palette.RewardPanelBorder);
    private static readonly StyleBoxFlat UltimatePanelStyle = CreatePanelStyle(Palette.UltimatePanelBorder);

    public const string LevelUpTitle = "¡SUBISTE DE NIVEL! Elige una mejora";
    public const string UltimateTitle = "¡JEFE DERROTADO! Elige tu Ultimate";

    private PanelContainer _panel;
    private Label _title;
    private Label _freeHint;
    private VBoxContainer _cardsContainer;
    private readonly List<RewardCard> _cards = new();
    private List<UpgradeData> _currentChoices;
    private bool _resolving;

    public override void _Ready()
    {
        AddToGroup("upgrade_picker");
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;

        _panel = GetNode<PanelContainer>("CenterContainer/Panel");
        _title = GetNode<Label>("CenterContainer/Panel/VBoxContainer/Title");
        _freeHint = GetNode<Label>("CenterContainer/Panel/VBoxContainer/FreeHint");
        _cardsContainer = GetNode<VBoxContainer>("CenterContainer/Panel/VBoxContainer/CardsContainer");
    }

    // The same picker serves both level-up rewards and the one-time post-boss Ultimate choice — the
    // heading is passed in rather than baked into the scene, and isUltimate swaps the panel's
    // border/title colour (gold vs. the level-up picker's cyan) so the two can't be mistaken for each
    // other at a glance, the way two identically-styled modals easily could. Both stay visibly distinct
    // from the shop, which is magenta and shows prices where these show GRATIS.
    public void Open(List<UpgradeData> choices, string title = LevelUpTitle, bool isUltimate = false)
    {
        // Nothing to offer at all shouldn't happen (the catalog is never empty), but opening an empty
        // picker would be an unexitable modal, so it's refused outright rather than half-shown.
        if (choices == null || choices.Count == 0) return;

        _currentChoices = choices;
        _resolving = false;
        _title.Text = title;

        var panelStyle = isUltimate ? UltimatePanelStyle : RewardPanelStyle;
        var accent = isUltimate ? Palette.UltimatePanelBorder : Palette.RewardPanelBorder;
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        _title.AddThemeColorOverride("font_color", accent);
        _freeHint.AddThemeColorOverride("font_color", accent);

        var player = GetTree().GetFirstNodeInGroup("player") as Player;

        // Computed across all 3 offers at once so only the strongest of each reward family is flagged,
        // rather than every offer that merely beats the player's current state.
        bool[] highlight = player?.GetHighlightFlags(choices) ?? new bool[choices.Count];

        // Softlock guard. A pick is free, so there's normally always something takeable — but a player
        // with everything maxed can be offered three useless rewards (PickRandomTiered only falls back to
        // useless picks when nothing useful is left), and this modal has no continue button, so all three
        // cards would be disabled with no way out.
        //
        // Rather than resolving it automatically, the cards are left tappable: the player still decides
        // and still acts, they just can't gain anything from it. Each card's "NO SUMA" chip says so
        // outright, so it isn't a hidden dud.
        bool allUseless = AllChoicesUseless(player, choices);

        BuildCards(choices, highlight, allUseless);
        Visible = true;

        for (int i = 0; i < _cards.Count; i++)
            _cards[i].PlayAppearFlare(i * 0.08f);
    }

    private static bool AllChoicesUseless(Player player, List<UpgradeData> choices)
    {
        if (player == null) return false;
        foreach (var choice in choices)
            if (!player.IsRewardUseless(choice)) return false;
        return true;
    }

    private void BuildCards(List<UpgradeData> choices, bool[] highlight, bool allowUseless)
    {
        foreach (var card in _cards) card.QueueFree();
        _cards.Clear();

        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;
            var card = CardScene.Instantiate<RewardCard>();
            _cardsContainer.AddChild(card);
            // cost: null is what makes the card show GRATIS instead of a price.
            card.Configure(choices[i], cost: null, surcharge: 0, highlight[i], "Elegir", alreadyTaken: false,
                allowUseless: allowUseless);
            card.Activated += () => OnChoicePressed(index);
            _cards.Add(card);
        }
    }

    private void OnChoicePressed(int index)
    {
        // A single guard covers both input paths (the button and the whole-card tap) — unlike the shop's
        // buy handler, this one has nothing to re-validate against, so without it a fast double tap could
        // apply two upgrades from one level-up.
        if (_resolving) return;
        _resolving = true;

        var chosen = _currentChoices[index];
        Visible = false;

        // Deferred by one frame rather than called straight from the signal handler. This used to wait
        // out the confirm flare, which read as the game hanging on the click — but it can't be a plain
        // synchronous call either: ApplyUpgradeAndResume may immediately reopen this picker for a queued
        // level-up, and Open() QueueFrees the cards, including the very card whose Activated signal is
        // still on the stack. One deferred frame is imperceptible and sidesteps that entirely.
        Callable.From(() => GameManager.Instance.ApplyUpgradeAndResume(chosen)).CallDeferred();
    }

    private static StyleBoxFlat CreatePanelStyle(Color borderColor)
    {
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.043f, 0.024f, 0.078f, 0.94f);
        style.BorderColor = new Color(borderColor, 0.85f);
        style.SetBorderWidthAll(3);
        style.SetCornerRadiusAll(12);
        style.SetContentMarginAll(16f);
        style.ContentMarginTop = 14f;
        style.ContentMarginBottom = 14f;
        return style;
    }
}
