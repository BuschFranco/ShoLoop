namespace ShooterLoop;

public enum UpgradeType
{
    FireRange,
    ExtraProjectile,
    OrbitShield,
    FireRate,
    BulletDamage,
    Heart,
    HitShield,
    Companion,
    SideShot,
    Laser,
    Ultimate,
    Pierce,
    CritChance,
    BulletKnockback,
    CoinBonus,
    ShieldRegen,
    Missile,
    Burn,
    XpBonus,
    ShockwaveAura,
    Vendaval
}

public enum RewardTier
{
    Common,
    Rare,
    Epic,
    Legendary
}

// The 3 Ultimates a player can find in the shop. See Player.TriggerUltimate for what each does.
public enum UltimateKind
{
    Nova,
    TimeSlow,
    Frenzy
}

public static class UltimateKindNames
{
    // The UI used to print the enum member directly, so the player read "TimeSlow" while the catalog
    // that sold it to them called it "Zona Lenta". These are the catalog's own names, minus the
    // "Ultimate: " prefix the shop rows carry.
    public static string Display(UltimateKind kind) => kind switch
    {
        UltimateKind.Nova => "Pulso Nova",
        UltimateKind.TimeSlow => "Zona Lenta",
        UltimateKind.Frenzy => "Sobrecarga",
        _ => kind.ToString(),
    };
}

// Where a reward is allowed to show up. This replaced a pair of ad-hoc `includeHearts` /
// `includeUltimates` bools threaded through the catalog builder: with more than a couple of
// exclusives that approach needs a new flag per exclusive and encodes the rule at the *call site*
// instead of on the reward itself. Now each entry states its own availability, and the two pools
// — free level-up picks vs. paid shop stock — are a real, readable distinction.
[Flags]
public enum RewardSource
{
    LevelUp = 1,
    Shop = 2,
    Both = LevelUp | Shop,
}

public class UpgradeData
{
    public string Name;
    public string Description;
    public UpgradeType Type;
    public float Value;
    public int Cost;
    public RewardTier Tier;
    public RewardSource Source;
    public UltimateKind Ultimate;

    public UpgradeData(string name, string description, UpgradeType type, RewardTier tier, float value = 0f, int cost = 0, RewardSource source = RewardSource.Both, UltimateKind ultimate = UltimateKind.Nova)
    {
        Name = name;
        Description = description;
        Type = type;
        Tier = tier;
        Value = value;
        Cost = cost;
        Source = source;
        Ultimate = ultimate;
    }

    private static readonly Random Rng = new();

    // Every reward in the game, keyed by tier. Callers filter by RewardSource rather than getting
    // a pre-trimmed list, so there's exactly one place to read the full catalog from.
    private static Dictionary<RewardTier, List<UpgradeData>> BuildFullCatalog() => new()
    {
        [RewardTier.Common] = new List<UpgradeData>
        {
            new("Alcance de Disparo I", "+70 rango de disparo automático", UpgradeType.FireRange, RewardTier.Common, 70f, cost: 8),
            new("Fuego Rápido I", "+0.5 disparos/seg", UpgradeType.FireRate, RewardTier.Common, 0.5f, cost: 8),
            new("Balas Afiladas I", "+3 daño de bala", UpgradeType.BulletDamage, RewardTier.Common, 3f, cost: 8),
            new("Cuchilla Orbital I", "1 cuchilla giratoria daña a los enemigos al contacto", UpgradeType.OrbitShield, RewardTier.Common, 1f, cost: 20),
            new("Barrera I", "Bloquea el próximo 1 golpe", UpgradeType.HitShield, RewardTier.Common, 1f, cost: 14),
            new("Dron I", "Un mini compañero dispara al 30% de tus estadísticas", UpgradeType.Companion, RewardTier.Common, 30f, cost: 12),
            new("Láser I", "Dispara un láser cada 4s a hasta 5 enemigos dentro de tu rango (12 daño)", UpgradeType.Laser, RewardTier.Common, 1f, cost: 20),
            new("Perforación I", "Tus balas atraviesan 1 enemigo más", UpgradeType.Pierce, RewardTier.Common, 1f, cost: 18),
            new("Crítico I", "+8% de probabilidad de daño crítico (x2)", UpgradeType.CritChance, RewardTier.Common, 8f, cost: 12),
            new("Retroceso I", "Tus balas empujan a los enemigos (+60)", UpgradeType.BulletKnockback, RewardTier.Common, 60f, cost: 10),
            new("Botín I", "+15% de monedas por enemigo", UpgradeType.CoinBonus, RewardTier.Common, 15f, cost: 14),
            new("Misil I", "Lanza un misil cada 4.5s que explota en área (30 daño, radio 70)", UpgradeType.Missile, RewardTier.Common, 1f, cost: 24),
            new("Incendiario I", "Tus balas queman: 6 daño/seg por 3s", UpgradeType.Burn, RewardTier.Common, 1f, cost: 20),
            new("Sabiduría I", "+20% de experiencia por enemigo", UpgradeType.XpBonus, RewardTier.Common, 20f, cost: 18),
            new("Onda de Choque I", "Cada 6s liberás una explosión a tu alrededor (20 daño, radio 65)", UpgradeType.ShockwaveAura, RewardTier.Common, 1f, cost: 20),
        },
        [RewardTier.Rare] = new List<UpgradeData>
        {
            new("Alcance de Disparo II", "+130 rango de disparo automático", UpgradeType.FireRange, RewardTier.Rare, 130f, cost: 16),
            new("Fuego Rápido II", "+1 disparo/seg", UpgradeType.FireRate, RewardTier.Rare, 1f, cost: 16),
            new("Balas Afiladas II", "+5 daño de bala", UpgradeType.BulletDamage, RewardTier.Rare, 5f, cost: 16),
            new("Cuchilla Orbital II", "2 cuchillas giratorias dañan a los enemigos al contacto", UpgradeType.OrbitShield, RewardTier.Rare, 2f, cost: 32),
            new("Barrera II", "Bloquea los próximos 2 golpes", UpgradeType.HitShield, RewardTier.Rare, 2f, cost: 24),
            new("Dron II", "Un mini compañero dispara al 35% de tus estadísticas", UpgradeType.Companion, RewardTier.Rare, 35f, cost: 22),
            new("Disparo Lateral", "+1 línea de tiro en paralelo (máx. 5)", UpgradeType.SideShot, RewardTier.Rare, 1f, cost: 22),
            new("Láser II", "Dispara un láser cada 3.2s a todos los enemigos dentro de tu rango (20 daño)", UpgradeType.Laser, RewardTier.Rare, 2f, cost: 34),
            new("Perforación II", "Tus balas atraviesan 1 enemigo más", UpgradeType.Pierce, RewardTier.Rare, 1f, cost: 26),
            new("Crítico II", "+12% de probabilidad de daño crítico (x2)", UpgradeType.CritChance, RewardTier.Rare, 12f, cost: 20),
            new("Retroceso II", "Tus balas empujan a los enemigos (+110)", UpgradeType.BulletKnockback, RewardTier.Rare, 110f, cost: 18),
            new("Botín II", "+25% de monedas por enemigo", UpgradeType.CoinBonus, RewardTier.Rare, 25f, cost: 24),
            new("Misil II", "Lanza un misil cada 3.8s que explota en área (50 daño, radio 85)", UpgradeType.Missile, RewardTier.Rare, 2f, cost: 38),
            new("Incendiario II", "Tus balas queman: 10 daño/seg por 3s", UpgradeType.Burn, RewardTier.Rare, 2f, cost: 32),
            new("Onda de Choque II", "Cada 5s liberás una explosión a tu alrededor (35 daño, radio 78)", UpgradeType.ShockwaveAura, RewardTier.Rare, 2f, cost: 32),
        },
        [RewardTier.Epic] = new List<UpgradeData>
        {
            new("Alcance de Disparo III", "+200 rango de disparo automático", UpgradeType.FireRange, RewardTier.Epic, 200f, cost: 28),
            new("Fuego Rápido III", "+1.8 disparos/seg", UpgradeType.FireRate, RewardTier.Epic, 1.8f, cost: 28),
            new("Balas Afiladas III", "+9 daño de bala", UpgradeType.BulletDamage, RewardTier.Epic, 9f, cost: 28),
            new("Disparo Doble", "Dispara dos proyectiles extra en diagonal", UpgradeType.ExtraProjectile, RewardTier.Epic, cost: 30),
            new("Cuchilla Orbital III", "3 cuchillas giratorias dañan a los enemigos al contacto", UpgradeType.OrbitShield, RewardTier.Epic, 3f, cost: 46),
            new("Barrera III", "Bloquea los próximos 3 golpes", UpgradeType.HitShield, RewardTier.Epic, 3f, cost: 36),
            new("Dron III", "Un mini compañero dispara al 45% de tus estadísticas", UpgradeType.Companion, RewardTier.Epic, 45f, cost: 34),
            new("Láser III", "Dispara un láser cada 2.5s a todos los enemigos dentro de tu rango (31 daño)", UpgradeType.Laser, RewardTier.Epic, 3f, cost: 50),
            new("Perforación III", "Tus balas atraviesan 2 enemigos más", UpgradeType.Pierce, RewardTier.Epic, 2f, cost: 38),
            new("Crítico III", "+18% de probabilidad de daño crítico (x2)", UpgradeType.CritChance, RewardTier.Epic, 18f, cost: 32),
            new("Retroceso III", "Tus balas empujan a los enemigos (+170)", UpgradeType.BulletKnockback, RewardTier.Epic, 170f, cost: 28),
            new("Botín III", "+40% de monedas por enemigo", UpgradeType.CoinBonus, RewardTier.Epic, 40f, cost: 36),
            new("Misil III", "Lanza un misil cada 3.2s que explota en área (75 daño, radio 100)", UpgradeType.Missile, RewardTier.Epic, 3f, cost: 54),
            new("Incendiario III", "Tus balas queman: 16 daño/seg por 3.5s", UpgradeType.Burn, RewardTier.Epic, 3f, cost: 46),
            new("Sabiduría II", "+50% de experiencia por enemigo", UpgradeType.XpBonus, RewardTier.Epic, 50f, cost: 44),
            new("Onda de Choque III", "Cada 4s liberás una explosión a tu alrededor (55 daño, radio 92)", UpgradeType.ShockwaveAura, RewardTier.Epic, 3f, cost: 46),

            // --- Shop-exclusive from here ---
            new("Ultimate: Pulso Nova", "Daña fuerte a todos los enemigos cercanos de una vez", UpgradeType.Ultimate, RewardTier.Epic, cost: 80, source: RewardSource.Shop, ultimate: UltimateKind.Nova),
            new("Ultimate: Zona Lenta", "Ralentiza a todos los enemigos por varios segundos", UpgradeType.Ultimate, RewardTier.Epic, cost: 80, source: RewardSource.Shop, ultimate: UltimateKind.TimeSlow),
            new("Ultimate: Sobrecarga", "Duplica tu cadencia y daño por varios segundos", UpgradeType.Ultimate, RewardTier.Epic, cost: 80, source: RewardSource.Shop, ultimate: UltimateKind.Frenzy),
            new("Regeneración", "Recuperás 1 carga de escudo cada 12s", UpgradeType.ShieldRegen, RewardTier.Epic, 5f, cost: 55, source: RewardSource.Shop),
            new("Vendaval I", "Cada 4.5s liberás una ráfaga frente tuyo que daña y empuja a los enemigos (90 daño, alcance 260)", UpgradeType.Vendaval, RewardTier.Epic, 1f, cost: 75, source: RewardSource.Shop),
        },
        [RewardTier.Legendary] = new List<UpgradeData>
        {
            new("Alcance de Disparo IV", "+300 rango de disparo automático", UpgradeType.FireRange, RewardTier.Legendary, 300f, cost: 45),
            new("Fuego Rápido IV", "+3 disparos/seg", UpgradeType.FireRate, RewardTier.Legendary, 3f, cost: 45),
            new("Balas Afiladas IV", "+15 daño de bala", UpgradeType.BulletDamage, RewardTier.Legendary, 15f, cost: 45),
            new("Cuchilla Orbital IV", "4 cuchillas giratorias dañan a los enemigos al contacto", UpgradeType.OrbitShield, RewardTier.Legendary, 4f, cost: 60),
            new("Barrera IV", "Bloquea los próximos 4 golpes", UpgradeType.HitShield, RewardTier.Legendary, 4f, cost: 48),
            new("Dron IV", "Un mini compañero dispara al 50% de tus estadísticas", UpgradeType.Companion, RewardTier.Legendary, 50f, cost: 50),
            new("Disparo Lateral", "+2 líneas de tiro en paralelo (máx. 5)", UpgradeType.SideShot, RewardTier.Legendary, 2f, cost: 55),
            new("Láser IV", "Dispara un láser cada 1.8s a todos los enemigos dentro de tu rango (50 daño)", UpgradeType.Laser, RewardTier.Legendary, 4f, cost: 65),
            new("Perforación IV", "Tus balas atraviesan 3 enemigos más", UpgradeType.Pierce, RewardTier.Legendary, 3f, cost: 55),
            new("Crítico IV", "+25% de probabilidad de daño crítico (x2)", UpgradeType.CritChance, RewardTier.Legendary, 25f, cost: 48),
            new("Retroceso IV", "Tus balas empujan a los enemigos (+240)", UpgradeType.BulletKnockback, RewardTier.Legendary, 240f, cost: 42),
            new("Botín IV", "+60% de monedas por enemigo", UpgradeType.CoinBonus, RewardTier.Legendary, 60f, cost: 52),
            new("Misil IV", "Lanza un misil cada 2.6s que explota en área (110 daño, radio 120)", UpgradeType.Missile, RewardTier.Legendary, 4f, cost: 72),
            new("Incendiario IV", "Tus balas queman: 24 daño/seg por 4s", UpgradeType.Burn, RewardTier.Legendary, 4f, cost: 62),
            new("Onda de Choque IV", "Cada 3.2s liberás una explosión a tu alrededor (80 daño, radio 105)", UpgradeType.ShockwaveAura, RewardTier.Legendary, 4f, cost: 62),

            // --- Shop-exclusive from here ---
            // A single Legendary Heart rather than one per tier: max lives is the most swingy stat
            // in the game (it's literally how many mistakes you're allowed), so it shouldn't be
            // something a cheap Common roll hands out repeatedly.
            new("Corazón", "+1 vida máxima y cura toda tu vida", UpgradeType.Heart, RewardTier.Legendary, 1f, cost: 60, source: RewardSource.Shop),
            new("Regeneración+", "Recuperás 1 carga de escudo cada 7.5s", UpgradeType.ShieldRegen, RewardTier.Legendary, 8f, cost: 75, source: RewardSource.Shop),
            new("Vendaval II", "Cada 3.6s liberás una ráfaga más ancha y fuerte frente tuyo (140 daño, alcance 320)", UpgradeType.Vendaval, RewardTier.Legendary, 2f, cost: 115, source: RewardSource.Shop),
        },
    };

    public static Dictionary<RewardTier, List<UpgradeData>> BuildCatalog(RewardSource source)
    {
        var full = BuildFullCatalog();
        var filtered = new Dictionary<RewardTier, List<UpgradeData>>();
        foreach (var kv in full)
            filtered[kv.Key] = kv.Value.FindAll(u => (u.Source & source) != 0);
        return filtered;
    }

    // isUseless: optional filter (typically Player.IsRewardUseless) so a reward that would have
    // zero effect right now — e.g. a Common Barrier while already topped off on a better tier —
    // is avoided when there's any alternative, instead of wasting one of the offered slots (or,
    // worse, leaving the free level-up picker with all 3 choices disabled and no way to proceed).
    public static List<UpgradeData> PickRandomTiered(int count, int round, RewardSource source, Func<UpgradeData, bool> isUseless = null)
    {
        var catalog = BuildCatalog(source);
        var weights = RewardTierRoller.GetWeights(round);
        var usedNames = new HashSet<string>();
        var result = new List<UpgradeData>();

        for (int i = 0; i < count; i++)
        {
            var tier = RewardTierRoller.RollTier(weights, Rng);
            var pick = PickFromTier(catalog, tier, usedNames, isUseless) ?? PickFromAnyTier(catalog, usedNames, isUseless);
            if (pick == null) break;

            usedNames.Add(pick.Name);
            result.Add(pick);
        }

        return result;
    }

    // The one-time reward for killing the first boss: a straight choice between Ultimates, outside
    // the tier system entirely (Ultimates have no tiers — they all sit in the Epic bucket purely so
    // the normal shop roll can surface them). Prefers ones the player doesn't already have
    // equipped, falling back to the full set if that leaves too few to fill the picker.
    public static List<UpgradeData> PickUltimateChoices(int count, Func<UpgradeData, bool> isUseless = null)
    {
        var all = BuildCatalog(RewardSource.Shop)[RewardTier.Epic]
            .FindAll(u => u.Type == UpgradeType.Ultimate);

        var preferred = isUseless == null ? all : all.FindAll(u => !isUseless(u));
        var pool = new List<UpgradeData>(preferred.Count >= count ? preferred : all);

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool.GetRange(0, Mathf.Min(count, pool.Count));
    }

    private static UpgradeData PickFromTier(Dictionary<RewardTier, List<UpgradeData>> catalog, RewardTier tier, HashSet<string> usedNames, Func<UpgradeData, bool> isUseless)
    {
        var pool = catalog[tier].FindAll(u => !usedNames.Contains(u.Name) && (isUseless == null || !isUseless(u)));
        if (pool.Count == 0)
            pool = catalog[tier].FindAll(u => !usedNames.Contains(u.Name)); // nothing useful left — fall back rather than skip the slot
        if (pool.Count == 0) return null;
        return pool[Rng.Next(pool.Count)];
    }

    private static UpgradeData PickFromAnyTier(Dictionary<RewardTier, List<UpgradeData>> catalog, HashSet<string> usedNames, Func<UpgradeData, bool> isUseless)
    {
        foreach (var tier in catalog.Keys)
        {
            var pick = PickFromTier(catalog, tier, usedNames, isUseless);
            if (pick != null) return pick;
        }
        return null;
    }
}
