namespace ShooterLoop;

// The game's pink/neon colour scheme in one place.
//
// Scope note: this covers everything drawn *procedurally* from code (rings, beams, blasts, floating
// labels, obstacles, arena edge). Visuals authored as nodes in a .tscn keep their colour in the
// scene file, because `Enemy` reads its own `Visual.Color` back as the hit-flash restore target —
// moving those here would give the same value two owners. The scene values are kept in step with
// the constants below by hand; the table in docs/visuals.md lists both sides.
//
// Values are deliberately bright (several channels at or near 1.0) because the arena's
// WorldEnvironment applies glow above a brightness threshold — dimmer colours simply won't bloom.
public static class Palette
{
    // --- Player ---
    // Electric cyan, the one cold colour in the scheme. The player has to stay instantly findable
    // in a crowd of pink enemies, which a pink ship wouldn't.
    public static readonly Color Player = new("7dfdfe");
    public static readonly Color PlayerBullet = new("ff4fd8");
    public static readonly Color ShieldAura = new("4fa8ff", 0.35f);
    public static readonly Color FireRangeRing = new("ff4fd8", 0.16f);

    // --- Player weapons ---
    public static readonly Color LaserBeam = new("ff2fb9", 0.85f);
    public static readonly Color OrbitBlade = new("f9c2ff");
    public static readonly Color Missile = new("ff8ae2");
    public static readonly Color MissileBlast = new("ff6ec7", 0.5f);
    public static readonly Color LevelUpNova = new("ffa8f0", 0.5f);
    public static readonly Color UltimateNova = new("ff2fb9", 0.4f);

    // --- Enemies ---
    public static readonly Color EnemyBullet = new("ff3860");
    public static readonly Color EnemyBurnTint = new("ff8a5c");

    // --- World ---
    public static readonly Color ObstacleFill = new("1a0f2b");
    public static readonly Color ObstacleOutline = new("ff4fd8", 0.9f);
    public static readonly Color ArenaBounds = new("ff4fd8", 0.45f);

    // Warm gold, kept deliberately outside the pink family so a crit still reads as "that one was
    // different" instead of blending into the standard magenta bullets.
    public static readonly Color CritBullet = new("ffe066");

    // --- UI / feedback ---
    public static readonly Color ScorePopup = new("ffa8f0");
    public static readonly Color DamageNumber = new("ff6b8a");
    public static readonly Color HealthBarBg = new("1a0f2b", 0.75f);
    public static readonly Color HealthBarFill = new("ff2e88");
    public static readonly Color GlowHighlight = new("ff9ff3");
    public static readonly Color Accent = new("7dfdfe");
    public static readonly Color Warning = new("ff2e88");
}
