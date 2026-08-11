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
    Ultimate
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
    Frenzy,
    Nuke
}

public class UpgradeData
{
    public string Name;
    public string Description;
    public UpgradeType Type;
    public float Value;
    public int Cost;
    public RewardTier Tier;
    public UltimateKind Ultimate;

    public UpgradeData(string name, string description, UpgradeType type, RewardTier tier, float value = 0f, int cost = 0, UltimateKind ultimate = UltimateKind.Nova)
    {
        Name = name;
        Description = description;
        Type = type;
        Tier = tier;
        Value = value;
        Cost = cost;
        Ultimate = ultimate;
    }

    private static readonly Random Rng = new();

    public static Dictionary<RewardTier, List<UpgradeData>> BuildTieredCatalog(bool includeHearts, bool includeUltimates = false)
    {
        var catalog = new Dictionary<RewardTier, List<UpgradeData>>
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
            },
        };

        if (includeHearts)
        {
            catalog[RewardTier.Common].Add(new UpgradeData("Corazón", "+1 vida, cura por completo", UpgradeType.Heart, RewardTier.Common, 1f, cost: 14));
            catalog[RewardTier.Rare].Add(new UpgradeData("Corazón", "+2 vidas, cura por completo", UpgradeType.Heart, RewardTier.Rare, 2f, cost: 24));
            catalog[RewardTier.Epic].Add(new UpgradeData("Corazón", "+3 vidas, cura por completo", UpgradeType.Heart, RewardTier.Epic, 3f, cost: 36));
            catalog[RewardTier.Legendary].Add(new UpgradeData("Corazón", "+3 vidas, cura por completo", UpgradeType.Heart, RewardTier.Legendary, 3f, cost: 48));
        }

        if (includeUltimates)
        {
            catalog[RewardTier.Epic].Add(new UpgradeData("Ultimate: Pulso Nova", "Daña fuerte a todos los enemigos cercanos de una vez", UpgradeType.Ultimate, RewardTier.Epic, cost: 80, ultimate: UltimateKind.Nova));
            catalog[RewardTier.Epic].Add(new UpgradeData("Ultimate: Zona Lenta", "Ralentiza a todos los enemigos por varios segundos", UpgradeType.Ultimate, RewardTier.Epic, cost: 80, ultimate: UltimateKind.TimeSlow));
            catalog[RewardTier.Epic].Add(new UpgradeData("Ultimate: Sobrecarga", "Duplica tu cadencia y daño por varios segundos", UpgradeType.Ultimate, RewardTier.Epic, cost: 80, ultimate: UltimateKind.Frenzy));
            catalog[RewardTier.Epic].Add(new UpgradeData("Ultimate: Nuke", "Elimina a todos los enemigos en una explosión (a los jefes les quita 20% de su vida máxima)", UpgradeType.Ultimate, RewardTier.Epic, cost: 100, ultimate: UltimateKind.Nuke));
        }

        return catalog;
    }

    // isUseless: optional filter (typically Player.IsRewardUseless) so a reward that would have
    // zero effect right now — e.g. a Common Barrier while already topped off on a better tier —
    // is avoided when there's any alternative, instead of wasting one of the offered slots (or,
    // worse, leaving the free level-up picker with all 3 choices disabled and no way to proceed).
    public static List<UpgradeData> PickRandomTiered(int count, int round, bool includeHearts = false, bool includeUltimates = false, Func<UpgradeData, bool> isUseless = null)
    {
        var catalog = BuildTieredCatalog(includeHearts, includeUltimates);
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
        var all = BuildTieredCatalog(includeHearts: false, includeUltimates: true)[RewardTier.Epic]
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
