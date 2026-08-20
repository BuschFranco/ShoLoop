namespace ShooterLoop;

public enum CharacterId
{
    Equilibrado,
    Centella,
    Coloso,
}

public readonly struct CharacterInfo
{
    public CharacterId Id { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }
    public float MoveSpeedMultiplier { get; init; }
    public float BulletDamageMultiplier { get; init; }

    // Tints the ship (Player/Visual) and the carousel preview swatch, so each pick reads as a
    // visually distinct character on top of the stat difference, not just a name.
    public Color Color { get; init; }

    // Placeholder sprite until real character art exists — swapping it later means overwriting
    // this file in place, no code change needed.
    public string SpritePath { get; init; }
}

// Static catalog of playable characters, picked at the character-select screen (CharacterSelectMenu)
// before a run starts. Deliberately only varies MoveSpeed/BulletDamage — the two base stats with no
// absolute ceiling of their own (see Player.MaxFireRate/MaxFireRange/MaxLivesCap) — so a character
// choice is a playstyle preference, not a shortcut past the game's balance caps.
public static class CharacterCatalog
{
    public static readonly CharacterInfo[] All =
    {
        new()
        {
            Id = CharacterId.Equilibrado,
            Name = "Equilibrado",
            Description = "Estadísticas base, sin ventajas ni desventajas.",
            MoveSpeedMultiplier = 1f,
            BulletDamageMultiplier = 1f,
            Color = new Color(0.49f, 0.99f, 1f, 1f), // cyan — the ship's original default color
            SpritePath = "res://Assets/Sprites/Characters/equilibrado.png",
        },
        new()
        {
            Id = CharacterId.Centella,
            Name = "Centella",
            Description = "+15% velocidad, -10% daño de bala.",
            MoveSpeedMultiplier = 1.15f,
            BulletDamageMultiplier = 0.9f,
            Color = new Color(0.75f, 1f, 0.3f, 1f), // yellow-green — reads as "fast"
            SpritePath = "res://Assets/Sprites/Characters/centella.png",
        },
        new()
        {
            Id = CharacterId.Coloso,
            Name = "Coloso",
            Description = "-10% velocidad, +20% daño de bala.",
            MoveSpeedMultiplier = 0.9f,
            BulletDamageMultiplier = 1.2f,
            Color = new Color(1f, 0.45f, 0.2f, 1f), // orange — reads as "heavy hitter"
            SpritePath = "res://Assets/Sprites/Characters/coloso.png",
        },
    };

    public static CharacterInfo Get(CharacterId id)
    {
        foreach (var info in All)
        {
            if (info.Id == id) return info;
        }
        return All[0];
    }
}
