namespace ShooterLoop;

public enum AchievementCategory { Progress, Combat, Builds }

// Same shape as Player.BuildRequirement: a threshold plus a way to read the current value, so
// evaluating a whole category is just "iterate and compare" with no per-achievement special case.
public readonly record struct AchievementDef(
    string Id, string Name, string Description, AchievementCategory Category,
    int RewardLibras, Func<GameManager, float> Current, float Needed);

public static class AchievementCatalog
{
    // First-guess numbers, pending playtesting — same spirit as the rest of the reward/economy
    // catalogs in this project. Not a closed list; more get added later with the same shape. Each
    // tiered family (Exterminador, Cazajefes, Puntería...) climbs into genuinely grindy endgame
    // territory at its higher tiers — those are meant to take a committed player weeks, not one
    // good run. Round-based Progress achievements are deliberately capped at ronda 50 (both the
    // single-run "reach round N" ladder and the lifetime "rounds cleared" one) — beyond that the
    // ask stops being a milestone and starts being a grind for its own sake.
    public static readonly AchievementDef[] All =
    {
        // --- Progreso ---
        new("survivor", "Sobreviviente", "Llegar a la ronda 10", AchievementCategory.Progress,
            20, gm => gm.RoundNumber, 10),
        new("hardened", "Curtido", "Llegar a la ronda 20", AchievementCategory.Progress,
            40, gm => gm.RoundNumber, 20),
        new("unstoppable", "Imparable", "Llegar a la ronda 30", AchievementCategory.Progress,
            60, gm => gm.RoundNumber, 30),
        new("legend", "Leyenda", "Llegar a la ronda 50", AchievementCategory.Progress,
            100, gm => gm.RoundNumber, 50),
        new("veteran", "Veterano", "Superar 50 rondas", AchievementCategory.Progress,
            30, gm => gm.TotalRoundsCleared, 50),
        new("account_10", "Cuenta nivel 10", "Alcanzar el nivel de cuenta 10", AchievementCategory.Progress,
            30, gm => gm.AccountLevel, 10),
        new("account_25", "Cuenta nivel 25", "Alcanzar el nivel de cuenta 25", AchievementCategory.Progress,
            60, gm => gm.AccountLevel, 25),
        new("account_50", "Cuenta nivel 50", "Alcanzar el nivel de cuenta 50", AchievementCategory.Progress,
            120, gm => gm.AccountLevel, 50),

        // --- Combate ---
        new("exterminator_1", "Exterminador", "100 bajas", AchievementCategory.Combat,
            15, gm => gm.TotalEnemiesKilled, 100),
        new("exterminator_2", "Exterminador II", "1000 bajas", AchievementCategory.Combat,
            40, gm => gm.TotalEnemiesKilled, 1000),
        new("exterminator_3", "Exterminador III", "5000 bajas", AchievementCategory.Combat,
            80, gm => gm.TotalEnemiesKilled, 5000),
        new("exterminator_4", "Exterminador IV", "20000 bajas", AchievementCategory.Combat,
            150, gm => gm.TotalEnemiesKilled, 20000),
        new("boss_hunter_1", "Cazajefes", "5 jefes derrotados", AchievementCategory.Combat,
            25, gm => gm.TotalBossesKilled, 5),
        new("boss_hunter_2", "Cazajefes II", "20 jefes derrotados", AchievementCategory.Combat,
            60, gm => gm.TotalBossesKilled, 20),
        new("boss_hunter_3", "Cazajefes III", "50 jefes derrotados", AchievementCategory.Combat,
            120, gm => gm.TotalBossesKilled, 50),
        new("marksman_1", "Puntería", "100 críticos", AchievementCategory.Combat,
            20, gm => gm.TotalCritsLanded, 100),
        new("marksman_2", "Puntería II", "500 críticos", AchievementCategory.Combat,
            60, gm => gm.TotalCritsLanded, 500),
        new("marksman_3", "Puntería III", "2000 críticos", AchievementCategory.Combat,
            120, gm => gm.TotalCritsLanded, 2000),

        // --- Builds ---
        new("first_build", "Primera build", "Completar cualquier build al menos una vez", AchievementCategory.Builds,
            20, gm => gm.EverCompletedBuilds.Count > 0 ? 1 : 0, 1),
        new("specialist", "Especialista", "Completar 3 builds distintas al menos una vez cada una", AchievementCategory.Builds,
            35, gm => gm.EverCompletedBuilds.Count, 3),
        new("completionist", "Completista", "Completar las 7 builds al menos una vez cada una", AchievementCategory.Builds,
            60, gm => gm.EverCompletedBuilds.Count, 7),
        new("collector", "Coleccionista", "Conseguir una Legendaria de cualquier tipo", AchievementCategory.Builds,
            15, gm => gm.EverGotLegendary.Count > 0 ? 1 : 0, 1),
        new("legendary_arsenal", "Arsenal legendario", "Conseguir 5 Legendarias distintas", AchievementCategory.Builds,
            60, gm => gm.EverGotLegendary.Count, 5),
        new("full_arsenal", "Arsenal completo", "Conseguir 15 Legendarias distintas", AchievementCategory.Builds,
            120, gm => gm.EverGotLegendary.Count, 15),
    };

    public static bool IsUnlocked(AchievementDef def, GameManager gm) => def.Current(gm) >= def.Needed;
}
