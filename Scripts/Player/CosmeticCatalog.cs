namespace ShooterLoop;

// Which visual system a cosmetic color applies to. Ownership is tracked per (category, id) pair —
// buying "Rojo" for Bullet doesn't grant it for free on Trail — even though the color list below is
// shared across all four.
public enum CosmeticCategory { Bullet, Trail, Outline, Arena }

public readonly struct CosmeticOption
{
    public string Id { get; init; }
    public string Name { get; init; }
    public Color Color { get; init; }
    public int Cost { get; init; }
}

// Presentation only, same role as CharacterCatalog: no gameplay logic lives here.
//
// One shared color list for all four categories — a color is a color, only where it gets applied
// differs — rather than four hand-authored palettes with no reason to differ. "Original" is free,
// always owned, and resolves to White/identity so equipping it reproduces exactly what the game
// looked like before this feature existed (see GameManager.IsCosmeticOwned).
public static class CosmeticCatalog
{
    public const string DefaultId = "original";

    public static readonly CosmeticOption[] Options =
    {
        new() { Id = DefaultId, Name = "Original", Color = Colors.White, Cost = 0 },
        new() { Id = "rojo", Name = "Rojo", Color = new Color("ff4b4b"), Cost = 10 },
        new() { Id = "azul", Name = "Azul", Color = new Color("4fa8ff"), Cost = 15 },
        new() { Id = "verde_lima", Name = "Verde Lima", Color = new Color("9bff4d"), Cost = 15 },
        new() { Id = "dorado", Name = "Dorado", Color = new Color("ffe066"), Cost = 20 },
        new() { Id = "violeta", Name = "Violeta", Color = new Color("c65bff"), Cost = 25 },
    };

    public static CosmeticOption Get(string id)
    {
        foreach (var option in Options)
            if (option.Id == id) return option;

        // An id can vanish if a color is ever removed from the catalog after being equipped —
        // fall back to Original rather than crash on a stale settings.cfg value.
        return Options[0];
    }

    public static Color ColorFor(string id) => Get(id).Color;

    // The ownership-tracking key: one entry per (category, id) pair in GameManager.OwnedCosmetics.
    public static string ItemKey(CosmeticCategory category, string id) => $"{category}:{id}";
}
