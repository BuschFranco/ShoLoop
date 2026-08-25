namespace ShooterLoop;

// The game's pink/neon colour scheme in one place.
//
// Scope note: this covers everything drawn *procedurally* from code (rings, beams, blasts, floating
// labels, obstacles, arena edge). Visuals authored as nodes in a .tscn keep their colour in the
// scene file, because `Enemy` reads its own `Visual.Modulate` back as the hit-flash restore target —
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
    // Bright poison green — the player's whole weapon family is green so it can never be confused
    // with enemy fire (red). Every projectile visual below keys off this one family.
    public static readonly Color PlayerBullet = new("3dff8f");
    public static readonly Color ShieldAura = new("4fa8ff", 0.35f);
    public static readonly Color FireRangeRing = new("3dff8f", 0.3f);

    // --- Player weapons ---
    public static readonly Color LaserBeam = new("3dff8f", 0.85f);
    public static readonly Color OrbitBlade = new("a8ffc8");
    public static readonly Color Missile = new("3dff8f");
    // Player-only missile detonation; hazards (Mine/MissileZone) keep the pink MissileBlast
    // below so an enemy-area explosion stays unambiguously hostile.
    public static readonly Color PlayerBlast = new("3dff8f", 0.5f);
    public static readonly Color MissileBlast = new("ff6ec7", 0.5f);
    public static readonly Color LevelUpNova = new("ffa8f0", 0.5f);
    public static readonly Color UltimateNova = new("ff2fb9", 0.4f);
    // Violet, distinct from Missile/Nova's pink-magenta family, for the Onda de Choque reward.
    public static readonly Color OndaBlast = new("c65bff", 0.45f);
    // Fierce orange-red, distinct from every other blast colour, for Vendaval's directional shove.
    public static readonly Color VendavalBlast = new("ff4b2b", 0.5f);
    // Amber, distinct from Vendaval's redder orange and from CoinPickup's warmer gold — reads as a
    // hazard marker (arming/armed rings) as much as a blast colour, since a Mine sits visible on
    // the floor for a while before it goes off.
    public static readonly Color MineBlast = new("ffa72b", 0.55f);

    // --- Pickups ---
    // World drops from enemies, distinct from the reward-shop rewards of the same name.
    public static readonly Color HeartPickup = new("ff2e88");
    public static readonly Color ShieldPickupColor = new("4fa8ff");

    // Matches ScorePopup/Accent's cyan — same reasoning as that pair: an XP gem should read as
    // "feeds the same bar" at a glance, not as its own unrelated colour.
    public static readonly Color XpPickup = new("7dfdfe");
    // Warm gold, same family as CritBullet/UltimatePanelBorder — coins read as "valuable" without
    // borrowing the pink used by everything else in the scheme.
    public static readonly Color CoinPickup = new("ffe066");

    // --- Boss HUD bar ---
    // Gold, same family as CoinPickup/CritBullet/UltimatePanelBorder — reads as "rare/important"
    // and deliberately doesn't reuse HealthBarFill's magenta, which stays "generic enemy HP" (the
    // floating bar Special enemies still carry). Keeps the fixed boss bar visually distinct at a
    // glance rather than just bigger.
    public static readonly Color BossHealthBarFill = new("ffe066");
    public static readonly Color BossHealthBarBg = new("1a0f2b", 0.85f);
    public static readonly Color BossHealthBarBorder = new("ffe066", 0.9f);

    // --- Enemies ---
    public static readonly Color EnemyBullet = new("ff3860");
    public static readonly Color EnemyBurnTint = new("ff8a5c");

    // --- World ---
    // The backdrop is #0b0614. The obstacle fill has to sit clearly above that or blocks read as
    // holes in the floor rather than solid cover, so it's kept several steps lighter.
    public static readonly Color Backdrop = new("0b0614");
    public static readonly Color ObstacleFill = new("2a1745");
    public static readonly Color ObstacleOutline = new("ff4fd8");
    public static readonly Color ArenaBounds = new("ff4fd8", 0.7f);

    // Faint enough to never compete with enemies, present enough to give the eye something to
    // measure motion against — a plain dark field several screens wide reads as static.
    public static readonly Color GridLine = new("6b3fa0", 0.16f);

    // Warm gold, kept deliberately outside the pink family so a crit still reads as "that one was
    // different" instead of blending into the standard magenta bullets.
    public static readonly Color CritBullet = new("ffe066");

    // --- UI / feedback ---
    // These two used to be ff6b8a (damage) vs ffa8f0 (score) — both bright warm pinks, and once
    // bloom washes out the hue difference a "-5" over an enemy and a "+5" reward popup read as the
    // same kind of event. Damage now uses a hot orange, entirely outside the pink family (matching
    // how CritBullet below is deliberately non-pink for the same reason), while score is pulled onto
    // Accent/Player's cyan — it already IS the XP bar's colour, so a kill's payout visually ties back
    // to the same bar it's about to fill instead of competing with damage for the same warm hue.
    // Same hex as Accent/Player below — written as a literal rather than a reference to it because
    // C# initializes static fields in declaration order, and Accent isn't declared until further
    // down this class; referencing it here would read its (0,0,0,0) default instead.
    public static readonly Color ScorePopup = new("7dfdfe");
    public static readonly Color DamageNumber = new("ff7a1a");
    public static readonly Color HealthBarBg = new("1a0f2b", 0.75f);
    public static readonly Color HealthBarFill = new("ff2e88");
    public static readonly Color GlowHighlight = new("ff9ff3");

    // Same hue as the HUD's "Nv" label (see HUD.tscn) so the level-up popup reads as
    // "this is about the level number" purely from colour, at a glance.
    public static readonly Color LevelPopup = new("ff9ff3");
    public static readonly Color Accent = new("7dfdfe");
    public static readonly Color Warning = new("ff2e88");

    // HUD chrome: the panel the top-bar stats sit on, so labels read as one instrument cluster
    // instead of loose text floating over the arena. Deliberately low-alpha and dark — it has to
    // stay a backdrop, not compete with the neon gameplay it's overlaid on.
    public static readonly Color HudPanelBg = new(Backdrop, 0.62f);
    public static readonly Color HudPanelBorder = new(Accent, 0.35f);
    public static readonly Color HudBarBg = new("1a0f2b", 0.8f);

    // --- Modals ---
    // A distinct border colour per interstitial type, so the free level-up picker, the one-time
    // post-boss Ultimate choice, and the paid shop don't read as interchangeable copies of the
    // same screen — that's what made it easy to tap "Comprar" thinking it was a free pick, or
    // vice versa.
    public static readonly Color RewardPanelBorder = Accent;          // cyan — free level-up picks
    public static readonly Color ShopPanelBorder = new("ff4fd8");     // magenta — spending coins
    public static readonly Color UltimatePanelBorder = new("ffe066"); // gold — the rare post-boss pick

    // --- Typography ---
    // Five roles, picked from the sizes already scattered across the .tscn files (11-72px) rather
    // than invented from scratch — Number was already exactly 40 in a couple of places and Title
    // already 32-36 for modal/screen headers, so both just become the canonical value instead of
    // shifting anything. Each screen re-points its own labels to the nearest role as part of that
    // screen's own pass, not in one mass find-replace — see the UX pass plan.
    // Excluded on purpose: HUD's pre-round countdown digit (72px) — that's a full-screen digit, not
    // a label among labels, so it stays its own one-off rather than being forced into this scale.
    public static class FontSize
    {
        public const int Caption = 13;   // fine print, badges, tier chips, stack info
        public const int Body = 16;       // standard body text, descriptions, list rows
        public const int Subtitle = 20;   // section headers, character/reward names
        public const int Title = 32;      // screen/modal titles
        public const int Number = 40;     // the one "read this number" hero moment (e.g. final score)
    }
}
