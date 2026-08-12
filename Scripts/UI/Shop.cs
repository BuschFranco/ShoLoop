namespace ShooterLoop;

public partial class Shop : Control
{
    private static readonly StyleBoxFlat GlowStyle = CreateGlowStyle();

    private Label _coinsLabel;

    // Each item's outer node is a PanelContainer (needed so it actually lays out its Label+Button
    // children — a plain Button isn't a Container and won't size/position arbitrary child nodes,
    // which is exactly what made every card's content overlap when this used to be a Button).
    // Tapping anywhere on it that isn't BuyButton itself — which keeps its own default
    // mouse_filter=Stop and so still claims/consumes its own clicks first — falls through
    // (Label/HBox children below are mouse_filter=Ignore) to this panel's own GuiInput.
    // OnBuyPressed already re-validates resolved/useless/afford on every call, so no extra guard
    // is needed here the way UpgradePicker needs one for its non-revalidating OnChoicePressed.
    private PanelContainer[] _itemPanels;
    private Label[] _itemLabels;
    private Button[] _buyButtons;
    private Button _continueButton;
    private List<UpgradeData> _items;
    private bool[] _resolved;
    private bool[] _highlight;

    public override void _Ready()
    {
        AddToGroup("shop");
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;

        _coinsLabel = GetNode<Label>("CenterContainer/Panel/VBoxContainer/PointsLabel");
        _itemPanels = new[]
        {
            GetNode<PanelContainer>("CenterContainer/Panel/VBoxContainer/Item1"),
            GetNode<PanelContainer>("CenterContainer/Panel/VBoxContainer/Item2"),
            GetNode<PanelContainer>("CenterContainer/Panel/VBoxContainer/Item3"),
        };
        _itemLabels = new[]
        {
            GetNode<Label>("CenterContainer/Panel/VBoxContainer/Item1/Content/Label"),
            GetNode<Label>("CenterContainer/Panel/VBoxContainer/Item2/Content/Label"),
            GetNode<Label>("CenterContainer/Panel/VBoxContainer/Item3/Content/Label"),
        };
        _buyButtons = new[]
        {
            GetNode<Button>("CenterContainer/Panel/VBoxContainer/Item1/Content/BuyButton"),
            GetNode<Button>("CenterContainer/Panel/VBoxContainer/Item2/Content/BuyButton"),
            GetNode<Button>("CenterContainer/Panel/VBoxContainer/Item3/Content/BuyButton"),
        };
        _continueButton = GetNode<Button>("CenterContainer/Panel/VBoxContainer/ContinueButton");

        for (int i = 0; i < _buyButtons.Length; i++)
        {
            int index = i;
            _buyButtons[i].Pressed += () => OnBuyPressed(index);
            _itemPanels[i].GuiInput += @event => OnItemGuiInput(@event, index);
        }
        _continueButton.Pressed += OnContinuePressed;

        GameManager.Instance.CoinsChanged += OnCoinsChanged;
    }

    private void OnItemGuiInput(InputEvent @event, int index)
    {
        bool released = (@event is InputEventScreenTouch touch && !touch.Pressed)
            || (@event is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left && !mouse.Pressed);
        if (released) OnBuyPressed(index);
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
        _items = UpgradeData.PickRandomTiered(3, GameManager.Instance.RoundNumber, RewardSource.Shop, isUseless: player != null ? player.IsRewardUseless : null);
        _resolved = new bool[_items.Count];
        for (int i = 0; i < _items.Count; i++)
            _resolved[i] = player != null && player.IsRewardUseless(_items[i]);

        RefreshAllItemLabels();
        RefreshButtons();
        _coinsLabel.Text = $"Monedas: {GameManager.Instance.Coins}";
        Visible = true;

        CheckAutoAdvance();
    }

    // The glow has to be decided across all 3 offers at once (only the strongest of each reward
    // family gets it), so the flags are computed here and then consumed per-item. Recomputed on
    // every refresh since buying something changes what "strongest vs. current" means.
    private void RefreshAllItemLabels()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        _highlight = player?.GetHighlightFlags(_items) ?? new bool[_items.Count];

        for (int i = 0; i < _items.Count; i++)
            UpdateItemLabel(i);
    }

    private void OnCoinsChanged(int coins)
    {
        _coinsLabel.Text = $"Monedas: {coins}";
        if (Visible) RefreshButtons();
    }

    private void UpdateItemLabel(int index)
    {
        var item = _items[index];
        int roundCost = GetRoundAdjustedCost(index);
        int finalCost = GetCost(index);
        int extra = finalCost - roundCost;
        string surchargeText = extra > 0 ? $" (+{extra})" : "";

        string text = $"[{RewardTierRoller.GetTierName(item.Tier)}] {item.Name} ({finalCost}{surchargeText} monedas)\n{item.Description}";

        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null)
        {
            string info = player.GetStackInfoText(item);
            if (info != null) text += $"\n{info}";
        }

        _itemLabels[index].Text = text;
        _itemLabels[index].AddThemeColorOverride("font_color", RewardTierRoller.GetTierColor(item.Tier));

        if (_highlight[index]) _itemPanels[index].AddThemeStyleboxOverride("panel", GlowStyle);
        else _itemPanels[index].RemoveThemeStyleboxOverride("panel");
    }

    private void RefreshButtons()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var player = GetTree().GetFirstNodeInGroup("player") as Player;
            if (player != null && player.IsRewardUseless(_items[i]))
            {
                _buyButtons[i].Disabled = true;
                _buyButtons[i].Text = player.GetUnavailableLabel(_items[i]);
            }
            else if (_resolved[i])
            {
                _buyButtons[i].Disabled = true;
                _buyButtons[i].Text = "Comprada";
            }
            else
            {
                _buyButtons[i].Disabled = GameManager.Instance.Coins < GetCost(i);
                _buyButtons[i].Text = "Comprar";
            }
        }
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

        GameManager.Instance.SpendCoins(cost);
        GameManager.Instance.ApplyUpgrade(item);
        GameManager.Instance.RegisterShopPurchase(item.Type);
        _resolved[index] = true;

        RefreshAllItemLabels();
        RefreshButtons();
        CheckAutoAdvance();
    }

    private void CheckAutoAdvance()
    {
        foreach (var resolved in _resolved)
            if (!resolved) return;

        OnContinuePressed();
    }

    private void OnContinuePressed()
    {
        Visible = false;
        GameManager.Instance.StartNextRound();
    }

    private static StyleBoxFlat CreateGlowStyle()
    {
        var style = new StyleBoxFlat();
        style.BgColor = new Color(Palette.GlowHighlight, 0.14f);
        style.BorderColor = Palette.GlowHighlight;
        style.SetBorderWidthAll(3);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(6);
        return style;
    }
}
