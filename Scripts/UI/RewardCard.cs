namespace ShooterLoop;

// One reward offer, shared by the shop and the level-up/Ultimate picker.
//
// Both modals used to build their own card inline, duplicating the tier text, the glow stylebox
// (byte-identical in both files), the whole-card tap detection and the useless/disabled handling. That
// was survivable while a card was one multi-line Label; it isn't now that each piece of information has
// its own node. Everything presentational lives here; both modals just hand over an offer and listen for
// Activated.
//
// The card queries Player itself for the introspection it needs (stack info, whether the offer improves
// anything, the disabled wording) rather than having each modal pass those in — same group-lookup pattern
// the rest of the project uses, and it keeps the two callers thin.
public partial class RewardCard : PanelContainer
{
    [Signal]
    public delegate void ActivatedEventHandler();

    private Label _tierChip;
    private Label _bestBadge;
    private Label _nameLabel;
    private Label _descLabel;
    private Label _stackLabel;
    private Label _verdictChip;
    private Label _priceLabel;
    private Button _actionButton;
    private ColorRect _flare;

    private Tween _flareTween;
    private Tween _badgeTween;
    private Color _tierColor = Colors.White;
    private bool _interactive;

    // Tier-coloured borders, built once per tier and shared. The border is what makes a card read as a
    // card at all — before this, a non-highlighted offer had no border and no background, so three
    // offers looked like one run-on block of text.
    private static readonly Dictionary<RewardTier, StyleBoxFlat> TierStyles = new();

    private static readonly Color VerdictImproves = new("7dfdfe");
    private static readonly Color VerdictNeutral = new(0.55f, 0.58f, 0.66f);
    private static readonly Color PriceAffordable = Palette.CoinPickup;
    private static readonly Color PriceTooExpensive = Palette.Warning;
    private static readonly Color FreeColor = new("7dfdfe");

    public override void _Ready()
    {
        _tierChip = GetNode<Label>("Body/TopRow/TierChip");
        _bestBadge = GetNode<Label>("Body/TopRow/BestBadge");
        _nameLabel = GetNode<Label>("Body/NameLabel");
        _descLabel = GetNode<Label>("Body/DescBox/DescLabel");
        _stackLabel = GetNode<Label>("Body/StackBox/StackRow/StackLabel");
        _verdictChip = GetNode<Label>("Body/StackBox/StackRow/VerdictChip");
        _priceLabel = GetNode<Label>("Body/BottomRow/PriceBox/PriceLabel");
        _actionButton = GetNode<Button>("Body/BottomRow/ActionButton");
        _flare = GetNode<ColorRect>("Flare");

        _flare.Modulate = new Color(1f, 1f, 1f, 0f);

        // Read from Palette rather than left as a literal in the .tscn: the same hand-synced-duplicate
        // problem that had left Palette.ShopPanelBorder as dead code.
        _bestBadge.AddThemeColorOverride("font_color", Palette.GlowHighlight);

        _actionButton.Pressed += Activate;
        GuiInput += OnCardGuiInput;
        MouseEntered += OnCardHovered;

        // Scale tweens need a centred pivot or the card grows out of its top-left corner, and Size isn't
        // known until the parent container has laid this out — hence tracking it rather than setting it
        // once in _Ready.
        Resized += () => PivotOffset = Size / 2f;
    }

    // cost == null means the reward is free (the level-up picker), which shows a GRATIS chip where the
    // price would go. That distinction is load-bearing: the palette comments record that players used to
    // tap "Comprar" thinking a pick was free, which is also why the two modals keep different border
    // colours.
    // allowUseless exists for the level-up picker's dead-end case: when every offer would change nothing,
    // the cards must stay tappable or the modal has no exit at all (it has no continue button). The
    // "NO SUMA" chip still tells the player what they're getting, so nothing is hidden from them.
    public void Configure(UpgradeData data, int? cost, int surcharge, bool isBest, string verb, bool alreadyTaken,
        bool allowUseless = false)
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;

        _tierColor = RewardTierRoller.GetTierColor(data.Tier);
        AddThemeStyleboxOverride("panel", GetTierStyle(data.Tier));
        _flare.Color = _tierColor;

        _tierChip.Text = RewardTierRoller.GetTierName(data.Tier).ToUpperInvariant();
        _tierChip.AddThemeColorOverride("font_color", _tierColor);

        _nameLabel.Text = data.Name;
        _descLabel.Text = data.Description;

        string stackInfo = player?.GetStackInfoText(data);
        _stackLabel.Text = stackInfo ?? "";
        _stackLabel.Visible = stackInfo != null;

        bool improves = player != null && player.IsUpgradeOverCurrent(data);
        _verdictChip.Visible = player != null;
        _verdictChip.Text = improves ? "MEJORA" : "NO SUMA";
        _verdictChip.AddThemeColorOverride("font_color", improves ? VerdictImproves : VerdictNeutral);

        ConfigurePrice(cost, surcharge);
        ConfigureAction(data, cost, verb, alreadyTaken, player, allowUseless);
        ConfigureBestBadge(isBest);
    }

    private void ConfigurePrice(int? cost, int surcharge)
    {
        if (cost == null)
        {
            _priceLabel.Text = "GRATIS";
            _priceLabel.AddThemeColorOverride("font_color", FreeColor);
            return;
        }

        int coins = GameManager.Instance?.Coins ?? 0;
        bool affordable = coins >= cost.Value;

        // The surcharge is labelled, not just appended. It used to render as a bare "(+7)", which a
        // player has no way to tell apart from a tax, a rarity fee or a typo — the actual cause is
        // that they've already bought this family of reward before (GetShopSurchargeMultiplier), and
        // naming it is what turns a mystery number into a rule they can plan around.
        //
        // The wealth-inflation multiplier stays invisible, as before — it applies to every offer on
        // screen equally, so surfacing it wouldn't help anyone choose between them.
        string suffix = surcharge > 0 ? $" (+{surcharge} por repetir)" : "";
        _priceLabel.Text = $"{cost.Value}{suffix} monedas";
        _priceLabel.AddThemeColorOverride("font_color", affordable ? PriceAffordable : PriceTooExpensive);
    }

    private void ConfigureAction(UpgradeData data, int? cost, string verb, bool alreadyTaken, Player player,
        bool allowUseless)
    {
        if (alreadyTaken)
        {
            SetAction("Comprada", false);
            return;
        }

        if (player != null && player.IsRewardUseless(data) && !allowUseless)
        {
            SetAction(player.GetUnavailableLabel(data), false);
            return;
        }

        // "Faltan N" instead of just graying the button out: a disabled button says you can't, but not
        // why, or how close you are.
        int coins = GameManager.Instance?.Coins ?? 0;
        if (cost != null && coins < cost.Value)
        {
            SetAction($"Faltan {cost.Value - coins}", false);
            return;
        }

        SetAction(verb, true);
    }

    // How far an unavailable offer's decorative half is dimmed. Applied per-node rather than to the
    // whole card: Modulate cascades, so dimming the root took the action button's own text down with
    // it — and that text is precisely the string explaining *why* the card is unavailable ("Faltan
    // 37", "Al tope"). The card ended up hiding its own explanation, and at 0.55 over an already-muted
    // grey the stack line fell under 3:1 contrast.
    private static readonly Color UnavailableDim = new(1f, 1f, 1f, 0.5f);

    private void SetAction(string text, bool interactive)
    {
        _actionButton.Text = text;
        _actionButton.Disabled = !interactive;
        _interactive = interactive;

        // The card as a whole stays at full opacity; only the parts that carry no reason are dimmed,
        // so an unavailable offer still recedes next to the ones the player can actually take.
        Modulate = Colors.White;
        Color dim = interactive ? Colors.White : UnavailableDim;

        _tierChip.Modulate = dim;
        _nameLabel.Modulate = dim;
        _descLabel.Modulate = dim;
        _priceLabel.Modulate = dim;

        // Left at full: between them these say what's wrong and how far off the player is.
        _actionButton.Modulate = Colors.White;
        _stackLabel.Modulate = Colors.White;
        _verdictChip.Modulate = Colors.White;
    }

    private void ConfigureBestBadge(bool isBest)
    {
        _badgeTween?.Kill();
        _badgeTween = null;
        _bestBadge.Visible = isBest;
        if (!isBest) return;

        // The "best offer" signal used to own the card border, which now belongs to the tier. Moving it
        // to a badge plus a slow breathe gives each piece of information its own channel instead of
        // making them fight over one property.
        _bestBadge.Modulate = Colors.White;

        // Reduced motion: the badge stays, fully lit and static. It's a permanent loop sitting on
        // text the player is reading in order to decide, which makes it the worst-placed of the
        // game's looping animations even though it's also one of the gentlest.
        if (DangerLevel.Reduced) return;

        _badgeTween = _bestBadge.CreateTween();
        _badgeTween.SetLoops();
        _badgeTween.TweenProperty(_bestBadge, "modulate:a", 0.45f, 0.7f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _badgeTween.TweenProperty(_bestBadge, "modulate:a", 1f, 0.7f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    // --- Flare, in the tier's own colour ---

    // Staggered by the caller so the three cards cascade in rather than all flashing on the same frame.
    public void PlayAppearFlare(float delay)
    {
        _flareTween?.Kill();
        _flare.Modulate = new Color(1f, 1f, 1f, 0f);
        Scale = Vector2.One * 0.94f;

        _flareTween = CreateTween();
        _flareTween.TweenInterval(delay);
        _flareTween.SetParallel(true);
        _flareTween.TweenProperty(_flare, "modulate:a", 0.5f, 0.12f);
        _flareTween.TweenProperty(this, "scale", Vector2.One, 0.28f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _flareTween.Chain();
        _flareTween.TweenProperty(_flare, "modulate:a", 0f, 0.3f);
    }

    private void PlayTouchFlare()
    {
        if (!_interactive) return;
        _flareTween?.Kill();
        _flare.Modulate = new Color(1f, 1f, 1f, 0f);

        _flareTween = CreateTween();
        _flareTween.TweenProperty(_flare, "modulate:a", 0.26f, 0.08f);
        _flareTween.TweenProperty(_flare, "modulate:a", 0f, 0.22f);
    }

    // Bigger and slower than the other two — this one is the payoff for actually choosing. Returns its
    // duration so the caller can hold the modal open long enough for it to be seen.
    public const float ConfirmFlareDuration = 0.3f;

    public void PlayConfirmFlare()
    {
        _flareTween?.Kill();
        _flare.Modulate = new Color(1f, 1f, 1f, 0f);

        _flareTween = CreateTween();
        _flareTween.SetParallel(true);
        _flareTween.TweenProperty(_flare, "modulate:a", 0.75f, 0.1f);
        _flareTween.TweenProperty(this, "scale", Vector2.One * 1.04f, 0.1f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _flareTween.Chain();
        _flareTween.SetParallel(true);
        _flareTween.TweenProperty(_flare, "modulate:a", 0f, ConfirmFlareDuration - 0.1f);
        _flareTween.TweenProperty(this, "scale", Vector2.One, ConfirmFlareDuration - 0.1f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    // --- Input ---

    private void OnCardHovered() => PlayTouchFlare();

    // Whole-card tap: the inner button keeps its own default mouse_filter and claims its own clicks
    // first; everything else in the card is Ignore, so presses fall through to here.
    private void OnCardGuiInput(InputEvent @event)
    {
        bool pressed = (@event is InputEventScreenTouch touch && touch.Pressed)
            || (@event is InputEventMouseButton down && down.ButtonIndex == MouseButton.Left && down.Pressed);
        if (pressed)
        {
            PlayTouchFlare();
            return;
        }

        bool released = (@event is InputEventScreenTouch up && !up.Pressed)
            || (@event is InputEventMouseButton click && click.ButtonIndex == MouseButton.Left && !click.Pressed);
        if (released) Activate();
    }

    private void Activate()
    {
        if (!_interactive) return;
        EmitSignal(SignalName.Activated);
    }

    private static StyleBoxFlat GetTierStyle(RewardTier tier)
    {
        if (TierStyles.TryGetValue(tier, out var cached)) return cached;

        var color = RewardTierRoller.GetTierColor(tier);
        var style = new StyleBoxFlat();
        style.BgColor = new Color(color, 0.07f);
        style.BorderColor = new Color(color, 0.85f);
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(8);
        style.SetContentMarginAll(10);
        TierStyles[tier] = style;
        return style;
    }
}
