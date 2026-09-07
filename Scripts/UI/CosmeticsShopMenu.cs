namespace ShooterLoop;

using System.Collections.Generic;

// Where Libras' second sink lives: swap purely cosmetic colors (bullets, ship trail, character
// outline, map accent) for a currency whose only prior use was the three "secreto" pilot slots.
//
// Every category shares the same CosmeticCatalog.Options list — a color is a color, only where it's
// applied differs — so the swatch rows are built identically for all four, just pointed at a
// different CosmeticCategory and a different GameManager Equipped* property.
public partial class CosmeticsShopMenu : Control
{
    private PanelContainer _panel;
    private ScrollContainer _scroll;
    private Label _librasLabel;
    private Button _closeButton;
    private Tween _librasTween;

    private readonly Dictionary<CosmeticCategory, HBoxContainer> _rows = new();
    private readonly List<(Button Button, CosmeticCategory Category, string Id)> _swatches = new();

    // Same bound-the-ScrollContainer fix OptionsMenu/PauseMenu already use for the same reason: a
    // CenterContainer never bounds a ScrollContainer's height on its own, so without this the panel
    // would grow past the screen in landscape once all four rows are on screen at once.
    private const float ScrollHeightLandscape = 560f;
    private const float ScrollHeightPortrait = 1000f;

    private const float SwatchSize = 44f;

    // Locked (not-yet-bought) swatches dim to this — same "disabled" convention RewardCard and
    // CharacterSelectMenu already use, so a locked color reads the same way a locked pilot does.
    private const float LockedAlpha = 0.55f;

    public override void _Ready()
    {
        Visible = false;

        _panel = GetNode<PanelContainer>("CenterContainer/Panel");
        _scroll = GetNode<ScrollContainer>("CenterContainer/Panel/Scroll");
        _librasLabel = GetNode<Label>("CenterContainer/Panel/Scroll/Box/LibrasLabel");
        _closeButton = GetNode<Button>("CenterContainer/Panel/Scroll/Box/CloseButton");

        _panel.AddThemeStyleboxOverride("panel", UIUtil.CreatePanelStyle(Palette.Player));

        _rows[CosmeticCategory.Bullet] = GetNode<HBoxContainer>("CenterContainer/Panel/Scroll/Box/BulletRow");
        _rows[CosmeticCategory.Trail] = GetNode<HBoxContainer>("CenterContainer/Panel/Scroll/Box/TrailRow");
        _rows[CosmeticCategory.Outline] = GetNode<HBoxContainer>("CenterContainer/Panel/Scroll/Box/OutlineRow");
        _rows[CosmeticCategory.Arena] = GetNode<HBoxContainer>("CenterContainer/Panel/Scroll/Box/ArenaRow");

        foreach (var (category, row) in _rows)
            BuildSwatchRow(category, row);

        _closeButton.Pressed += Close;
        Juice.WireButtonFeedback(_closeButton);

        FitToOrientation();
    }

    // Built once, in this fixed order — the catalog is static, so there's nothing to rebuild on
    // reopen, only swatch *state* (owned/equipped/affordable), refreshed by RefreshSwatches().
    private void BuildSwatchRow(CosmeticCategory category, HBoxContainer row)
    {
        foreach (var option in CosmeticCatalog.Options)
        {
            var button = new Button
            {
                CustomMinimumSize = new Vector2(SwatchSize, SwatchSize),
                ToggleMode = false,
                TooltipText = option.Cost > 0 ? $"{option.Name} ({option.Cost} Libras)" : option.Name,
            };
            row.AddChild(button);
            Juice.WireButtonFeedback(button);

            string id = option.Id; // local copy for the closure — option is a foreach loop variable
            button.Pressed += () => OnSwatchPressed(category, id, option.Cost);

            _swatches.Add((button, category, id));
        }
    }

    private void OnSwatchPressed(CosmeticCategory category, string id, int cost)
    {
        var gm = GameManager.Instance;
        if (!gm.IsCosmeticOwned(category, id))
        {
            if (!gm.TryBuyCosmetic(category, id, cost))
            {
                // The one branch a tap on an unlocked-looking swatch can still fail on: not enough
                // Libras. Owned-already and already-equipped never reach TryBuyCosmetic at all.
                AudioManager.Instance?.Play(AudioManager.Sfx.UiDenied);
                // Shakes and flashes the balance itself red — the number that's actually short is
                // what should read as the problem, not just an anonymous "denied" beep.
                Juice.Shake(_librasLabel, flashColor: Palette.Warning);
                return;
            }

            AudioManager.Instance?.Play(AudioManager.Sfx.UiBuy);
            PulseLibras(cost);
        }

        gm.EquipCosmetic(category, id);
        RefreshSwatches();
    }

    // Same pattern Shop.cs uses for its own coin balance: a scale-pop on the number itself plus a
    // "−N" label that floats up beside it and fades — so spending Libras here reads the same way
    // spending Coins already does in the round shop, not a silent number swap.
    private void PulseLibras(int spent)
    {
        _librasLabel.Text = $"Libras: {GameManager.Instance.Libras}";

        _librasTween?.Kill();
        _librasLabel.PivotOffset = _librasLabel.Size / 2f;
        _librasLabel.Scale = Vector2.One * 1.25f;
        _librasTween = _librasLabel.CreateTween();
        _librasTween.TweenProperty(_librasLabel, "scale", Vector2.One, 0.3f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        Juice.FloatingLabel(this, $"−{spent}", _librasLabel.GlobalPosition + new Vector2(_librasLabel.Size.X + 10f, 0f),
            Palette.Warning, Palette.FontSize.Subtitle, driftY: -26f, holdBeforeFade: 0.3f, lifetime: 0.8f);
    }

    private void RefreshSwatches()
    {
        var gm = GameManager.Instance;
        foreach (var (button, category, id) in _swatches)
        {
            var option = CosmeticCatalog.Get(id);
            bool owned = gm.IsCosmeticOwned(category, id);
            bool equipped = id == EquippedFor(category);

            // The catalog's own Color for "Original" is just White (identity — apply no tint), not
            // what the game actually looks like today. Show the real per-category default instead,
            // so the swatch reads as a preview of the look you'd get, not a literal Modulate value.
            Color previewColor = id == CosmeticCatalog.DefaultId ? OriginalPreviewColor(category) : option.Color;

            var style = new StyleBoxFlat
            {
                BgColor = owned ? previewColor : new Color(previewColor, LockedAlpha),
                CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            };
            style.SetBorderWidthAll(equipped ? 3 : owned ? 1 : 0);
            style.BorderColor = equipped ? Palette.Player : new Color(0f, 0f, 0f, 0.5f);

            button.AddThemeStyleboxOverride("normal", style);
            button.AddThemeStyleboxOverride("hover", style);
            button.AddThemeStyleboxOverride("pressed", style);
            button.Text = owned ? "" : option.Cost.ToString();
        }
    }

    // What "Original" actually looks like in each category today — a fixed color per category,
    // independent of whichever pilot is selected (see Player.SpawnThrusterPuff for Trail). Outline's
    // default is "no outline at all", shown hollow (fully transparent) rather than any solid color,
    // since there's no color to preview.
    private static Color OriginalPreviewColor(CosmeticCategory category) => category switch
    {
        CosmeticCategory.Bullet => Palette.PlayerBullet,
        CosmeticCategory.Trail => Palette.PlayerBullet,
        CosmeticCategory.Outline => new Color(0f, 0f, 0f, 0f),
        CosmeticCategory.Arena => Palette.ArenaBounds,
        _ => Colors.White,
    };

    private static string EquippedFor(CosmeticCategory category)
    {
        var gm = GameManager.Instance;
        return category switch
        {
            CosmeticCategory.Bullet => gm.EquippedBulletCosmetic,
            CosmeticCategory.Trail => gm.EquippedTrailCosmetic,
            CosmeticCategory.Outline => gm.EquippedOutlineCosmetic,
            CosmeticCategory.Arena => gm.EquippedArenaCosmetic,
            _ => CosmeticCatalog.DefaultId,
        };
    }

    private void FitToOrientation()
    {
        bool landscape = GameManager.Instance?.CurrentOrientation == GameManager.ScreenOrientation.Landscape;
        _scroll.CustomMinimumSize = new Vector2(0f, landscape ? ScrollHeightLandscape : ScrollHeightPortrait);
    }

    public void Open()
    {
        _librasLabel.Text = $"Libras: {GameManager.Instance.Libras}";
        RefreshSwatches();
        FitToOrientation();

        // Always reopen at the top — see OptionsMenu for why (a mid-scroll reopen reads as broken).
        _scroll.ScrollVertical = 0;

        Visible = true;
        Juice.ModalIn(_panel);
    }

    private void Close() => Juice.ModalOut(_panel, () => Visible = false);
}
