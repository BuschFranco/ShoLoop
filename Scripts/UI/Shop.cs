namespace ShooterLoop;

public partial class Shop : Control
{
    private static readonly PackedScene CardScene = GD.Load<PackedScene>("res://Scenes/UI/RewardCard.tscn");

    // Built in code rather than left in the .tscn so it reads from Palette.ShopPanelBorder instead of a
    // hand-synced duplicate of the same hex — the scene used to carry its own copy, which meant the
    // Palette constant was dead code that could silently drift from what shipped.
    private static readonly StyleBoxFlat PanelStyle = UIUtil.CreatePanelStyle(Palette.ShopPanelBorder);

    private Label _coinsLabel;
    private VBoxContainer _cardsContainer;
    private Button _continueButton;
    private readonly List<RewardCard> _cards = new();
    private List<UpgradeData> _items;
    private bool[] _resolved;
    private Tween _coinsTween;

    // Guards the auto-close path from firing twice (it would call StartNextRound twice).
    private bool _closing;

    // Gates auto-advance behind the player having actually bought something this visit — see
    // CheckAutoAdvance for why opening into a dead end must NOT close itself.
    private bool _purchasedThisVisit;

    public override void _Ready()
    {
        AddToGroup("shop");
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;

        GetNode<PanelContainer>("CenterContainer/Panel").AddThemeStyleboxOverride("panel", PanelStyle);
        _coinsLabel = GetNode<Label>("CenterContainer/Panel/VBoxContainer/CoinsRow/CoinsLabel");
        _cardsContainer = GetNode<VBoxContainer>("CenterContainer/Panel/VBoxContainer/CardsContainer");
        _continueButton = GetNode<Button>("CenterContainer/Panel/VBoxContainer/ContinueButton");

        _continueButton.Pressed += OnContinuePressed;
        GameManager.Instance.CoinsChanged += OnCoinsChanged;
    }

    public override void _ExitTree()
    {
        // Same reasoning as HUD._ExitTree(): GameManager outlives this scene-scoped node.
        if (GameManager.Instance == null) return;
        GameManager.Instance.CoinsChanged -= OnCoinsChanged;
    }

    public void Open()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        _items = UpgradeData.PickRandomTiered(3, GameManager.Instance.RoundNumber, RewardSource.Shop, isUseless: player != null ? player.IsRewardUseless : null, fortuneBonus: player?.FortuneBonus ?? 0f);
        _resolved = new bool[_items.Count];
        _closing = false;
        _purchasedThisVisit = false;
        for (int i = 0; i < _items.Count; i++)
            _resolved[i] = player != null && player.IsRewardUseless(_items[i]);

        // Cards are instantiated per open rather than being three fixed slots in the scene. PickRandomTiered
        // can legitimately return fewer than 3 offers when the pool runs dry, and the old fixed slots left
        // the leftovers on screen as empty cards.
        BuildCards();
        RefreshCards();
        UpdateCoinsLabel(GameManager.Instance.Coins);
        Visible = true;

        for (int i = 0; i < _cards.Count; i++)
            _cards[i].PlayAppearFlare(i * 0.08f);

        CheckAutoAdvance();
    }

    private void BuildCards()
    {
        foreach (var card in _cards) card.QueueFree();
        _cards.Clear();

        for (int i = 0; i < _items.Count; i++)
        {
            int index = i;
            var card = CardScene.Instantiate<RewardCard>();
            _cardsContainer.AddChild(card);
            card.Activated += () => OnBuyPressed(index);
            _cards.Add(card);
        }
    }

    // Recomputed in full on every refresh: buying one offer changes the coin balance, can re-price a
    // sibling offer of the same UpgradeType via the repurchase surcharge, and can move which offer counts
    // as the strongest of its family.
    private void RefreshCards()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        bool[] highlight = player?.GetHighlightFlags(_items) ?? new bool[_items.Count];

        for (int i = 0; i < _cards.Count; i++)
        {
            int roundCost = GetRoundAdjustedCost(i);
            int finalCost = GetCost(i);
            _cards[i].Configure(_items[i], finalCost, finalCost - roundCost, highlight[i], "Comprar", _resolved[i]);
        }
    }

    private void OnCoinsChanged(int coins)
    {
        UpdateCoinsLabel(coins);
        if (Visible) RefreshCards();
    }

    // The balance is the single most-consulted number on this screen and used to be a default-size
    // uncoloured label. It's now large and in the same gold as the in-world coin pickup, and it reacts
    // when it changes — the HUD's own coin readout is on a lower CanvasLayer and therefore hidden behind
    // this modal, so this label is the player's only view of their wallet while shopping.
    private void UpdateCoinsLabel(int coins) => _coinsLabel.Text = $"{coins} monedas";

    private void PulseCoins(int spent)
    {
        _coinsTween?.Kill();
        _coinsLabel.PivotOffset = _coinsLabel.Size / 2f;
        _coinsLabel.Scale = Vector2.One * 1.25f;
        _coinsTween = _coinsLabel.CreateTween();
        _coinsTween.TweenProperty(_coinsLabel, "scale", Vector2.One, 0.3f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        SpawnSpentLabel(spent);
    }

    // Spawned as a sibling of the coins row, not a child: a child of a container gets laid out by it,
    // which is the same reason HUD.OnLevelsGained parents its popup where it does.
    private void SpawnSpentLabel(int spent)
    {
        var label = new Label();
        label.Text = $"−{spent}";
        label.AddThemeColorOverride("font_color", Palette.Warning);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);
        label.AddThemeFontSizeOverride("font_size", 20);
        label.MouseFilter = MouseFilterEnum.Ignore;
        label.ZIndex = 20;
        AddChild(label);
        label.GlobalPosition = _coinsLabel.GlobalPosition + new Vector2(_coinsLabel.Size.X + 10f, 0f);

        var tween = label.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", label.Position + new Vector2(0f, -26f), 0.8f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 0.55f).SetDelay(0.3f);
        tween.Chain().TweenCallback(Callable.From(() => label.QueueFree()));
    }

    private int GetRoundAdjustedCost(int index)
    {
        return Mathf.RoundToInt(_items[index].Cost * GameManager.Instance.UpgradeCostMultiplier);
    }

    private int GetCost(int index)
    {
        float surcharge = GameManager.Instance.GetShopSurchargeMultiplier(_items[index].Type);
        return Mathf.RoundToInt(GetRoundAdjustedCost(index) * surcharge);
    }

    private void OnBuyPressed(int index)
    {
        if (_resolved[index]) return;

        var item = _items[index];
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null && player.IsRewardUseless(item)) return;

        int cost = GetCost(index);
        if (GameManager.Instance.Coins < cost) return;

        // The purchase itself lands immediately — only the modal's *exit* waits for the flare, so the
        // confirmation animation never delays gameplay state.
        GameManager.Instance.SpendCoins(cost);
        GameManager.Instance.ApplyUpgrade(item);
        GameManager.Instance.RegisterShopPurchase(item.Type);
        _resolved[index] = true;
        _purchasedThisVisit = true;

        _cards[index].PlayConfirmFlare();
        PulseCoins(cost);

        RefreshCards();
        CheckAutoAdvance();
    }

    // Auto-advance is strictly a *reward* for having acted: it fires only after a real purchase, once
    // there's provably nothing left to buy. A shop that opens with everything already maxed or nothing
    // affordable deliberately stays put and waits for "Siguiente Ronda" — closing itself in the player's
    // face before they've read a single offer is worse than asking for one extra tap.
    private void CheckAutoAdvance()
    {
        if (_closing || !_purchasedThisVisit) return;

        bool allResolved = true;
        foreach (var resolved in _resolved)
            if (!resolved) { allResolved = false; break; }

        // Either everything is bought/maxed, or what's left is out of reach. Safe to decide here because
        // the tree is paused while the shop is open: no coins can come in, and buying only ever lowers
        // the balance (and raises that type's surcharge), so neither state can un-fire.
        if (allResolved || !AnyPendingAffordable())
        {
            _closing = true;
            OnContinuePressed();
        }
    }

    private bool AnyPendingAffordable()
    {
        int coins = GameManager.Instance.Coins;
        for (int i = 0; i < _items.Count; i++)
        {
            if (_resolved[i]) continue;
            if (coins >= GetCost(i)) return true;
        }
        return false;
    }


    private void OnContinuePressed()
    {
        if (!Visible) return;
        Visible = false;
        GameManager.Instance.StartNextRound();
    }
}
