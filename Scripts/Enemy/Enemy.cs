namespace ShooterLoop;

public enum EnemyCategory { Common, Rare, Special, Hidden, Boss }

public partial class Enemy : CharacterBody2D
{
    [Export] public float MoveSpeed = 90f;
    [Export] public int MaxHp = 30;
    [Export] public int XpReward = 1;
    [Export] public int CoinsReward = 1;
    [Export] public int ContactDamage = 10;
    [Export] public EnemyCategory Category = EnemyCategory.Common;

    [Export] public bool CanSplit = false;
    [Export] public int SplitCount = 2;
    [Export] public int SplitGeneration = 0;
    [Export] public int MaxSplitGenerations = 3;
    [Export] public float SplitHpScale = 0.5f;
    [Export] public float SplitVisualScale = 0.75f;

    // Only Boss/Special enemies show a health bar + floating damage numbers — commons/rares die
    // fast enough that it'd just be visual noise. Offset tuned per-scene to roughly clear each
    // enemy's own visual radius (bigger enemies need a bigger offset).
    [Export] public float HealthBarOffset = -30f;

    // "Magnet-like" repulsion radius — any other enemy closer than this gets pushed directly away
    // from its own center, so a crowd converging on the player doesn't just stack on one point.
    [Export] public float SeparationRadius = 24f;
    private const float SeparationStrength = 80f;

    public int CurrentHp;

    // Set by whoever instantiates this enemy (EnemySpawner or a parent Split()), so a
    // splitting enemy can spawn more copies of itself without the scene referencing itself.
    public PackedScene SelfScene;

    private Node2D _player;
    private readonly Random _rng = new();
    private Polygon2D _healthBarFill;
    private const float HealthBarWidth = 30f;
    private const float HealthBarHeight = 4f;

    public override void _Ready()
    {
        AddToGroup("enemies");
        CurrentHp = MaxHp;
        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;

        if (Category == EnemyCategory.Special || Category == EnemyCategory.Boss)
            CreateHealthBar();
    }

    private void CreateHealthBar()
    {
        var barPos = new Vector2(-HealthBarWidth / 2f, HealthBarOffset);

        var background = new Polygon2D();
        background.Polygon = RectPoints(HealthBarWidth, HealthBarHeight);
        background.Color = new Color(0f, 0f, 0f, 0.6f);
        background.Position = barPos;
        AddChild(background);

        _healthBarFill = new Polygon2D();
        _healthBarFill.Polygon = RectPoints(HealthBarWidth, HealthBarHeight);
        _healthBarFill.Color = new Color(1f, 0.2f, 0.2f, 0.95f);
        _healthBarFill.Position = barPos;
        AddChild(_healthBarFill);
    }

    private static Vector2[] RectPoints(float width, float height) => new[]
    {
        Vector2.Zero,
        new Vector2(width, 0f),
        new Vector2(width, height),
        new Vector2(0f, height),
    };

    private void UpdateHealthBar()
    {
        if (_healthBarFill == null) return;
        float pct = Mathf.Clamp((float)CurrentHp / MaxHp, 0f, 1f);
        _healthBarFill.Polygon = RectPoints(HealthBarWidth * pct, HealthBarHeight);
    }

    private void SpawnDamageNumber(int amount)
    {
        var parent = GetParent();
        if (parent == null) return;

        var label = new Label();
        label.Text = $"-{amount}";
        label.AddThemeColorOverride("font_color", new Color(1f, 0.35f, 0.35f));
        label.AddThemeFontSizeOverride("font_size", 14);
        label.ZIndex = 10;
        parent.AddChild(label);
        label.GlobalPosition = GlobalPosition + new Vector2(-8f, HealthBarOffset - 14f);

        var tween = label.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", label.Position + new Vector2(0f, -20f), 0.5f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 0.4f).SetDelay(0.1f);
        tween.Chain().TweenCallback(Callable.From(() => label.QueueFree()));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            if (_player == null) return;
        }

        float speedMult = GameManager.Instance?.EnemySpeedMultiplier ?? 1f;
        Vector2 chaseDir = (_player.GlobalPosition - GlobalPosition).Normalized();
        Vector2 separation = ComputeSeparation();

        Velocity = (chaseDir * MoveSpeed + separation) * speedMult;
        MoveAndSlide();
    }

    private Vector2 ComputeSeparation()
    {
        Vector2 push = Vector2.Zero;
        foreach (var n in GetTree().GetNodesInGroup("enemies"))
        {
            if (n == this || n is not Enemy other || !IsInstanceValid(other)) continue;

            Vector2 offset = GlobalPosition - other.GlobalPosition;
            float distSq = offset.LengthSquared();
            float minDist = SeparationRadius;
            if (distSq > 0f && distSq < minDist * minDist)
            {
                float dist = Mathf.Sqrt(distSq);
                push += offset / dist * (minDist - dist) / minDist;
            }
        }
        return push * SeparationStrength;
    }

    public void TakeDamage(int amount)
    {
        if (_healthBarFill != null)
        {
            int actualDamage = Mathf.Min(amount, Mathf.Max(CurrentHp, 0));
            if (actualDamage > 0)
                SpawnDamageNumber(actualDamage);
        }

        CurrentHp -= amount;
        UpdateHealthBar();

        if (CurrentHp <= 0)
        {
            bool willSplit = CanSplit && SplitGeneration < MaxSplitGenerations && SelfScene != null;
            if (willSplit)
                Split();

            // Killing a split that spawns more copies doesn't finish anything yet — only the
            // final generation (the one that doesn't split further) grants XP. Score is synced to
            // XP so it's withheld the same way; Coins still pay out on every kill along the way.
            int xpToGive = willSplit ? 0 : XpReward;
            GameManager.Instance?.RegisterKill(xpToGive, CoinsReward, Category);

            // Score is synced 1:1 with XP now, so a mid-chain Splitter kill (xpToGive == 0 until
            // the final generation) correctly shows no popup either — only the terminal kill does.
            if (xpToGive > 0)
                SpawnScorePopup(xpToGive);

            QueueFree();
        }
    }

    private void SpawnScorePopup(int amount)
    {
        var parent = GetParent();
        if (parent == null) return;

        var label = new Label();
        label.Text = $"+{amount}";
        label.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.15f));
        label.AddThemeFontSizeOverride("font_size", 20);
        label.ZIndex = 10;
        parent.AddChild(label);
        label.GlobalPosition = GlobalPosition - new Vector2(10f, 10f);

        var tween = label.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", label.Position + new Vector2(0f, -40f), 0.8f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 0.6f).SetDelay(0.2f);
        tween.Chain().TweenCallback(Callable.From(() => label.QueueFree()));
    }

    private void Split()
    {
        // Evenly spaced around a random starting angle (rather than each child rolling its own
        // independent random angle) so two children can never land close together by bad luck —
        // they always end up visibly separated, not stacked on the parent's spot.
        float baseAngle = (float)(_rng.NextDouble() * Mathf.Tau);
        float angleStep = Mathf.Tau / SplitCount;

        for (int i = 0; i < SplitCount; i++)
        {
            var child = SelfScene.Instantiate<Enemy>();
            child.SelfScene = SelfScene;
            child.CanSplit = CanSplit;
            child.SplitCount = SplitCount;
            child.SplitGeneration = SplitGeneration + 1;
            child.MaxSplitGenerations = MaxSplitGenerations;
            child.SplitHpScale = SplitHpScale;
            child.SplitVisualScale = SplitVisualScale;
            child.Category = Category;
            child.HealthBarOffset = HealthBarOffset;

            child.MaxHp = Mathf.Max(1, Mathf.RoundToInt(MaxHp * SplitHpScale));
            child.ContactDamage = ContactDamage;
            child.MoveSpeed = MoveSpeed;
            child.XpReward = Mathf.Max(1, XpReward / 2);
            child.CoinsReward = Mathf.Max(1, CoinsReward / 2);
            child.Scale = Scale * SplitVisualScale;

            float angle = baseAngle + angleStep * i;
            child.GlobalPosition = GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 22f;

            GetParent().AddChild(child);
        }
    }
}
