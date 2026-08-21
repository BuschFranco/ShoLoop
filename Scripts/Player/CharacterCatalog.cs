using System.Collections.Generic;

namespace ShooterLoop;

public readonly struct CharacterInfo
{
    // Stable identity, and the only thing persisted. A string rather than an enum because
    // player-created characters have no compile-time identity to enumerate — see
    // CustomCharacterStore. For portrait characters the slug doubles as the sprite's filename, so
    // there's one name to keep straight instead of two.
    public string Slug { get; init; }

    public string Name { get; init; }
    public string Description { get; init; }

    // The character's mechanical effect, in one player-facing sentence, kept OUT of Description
    // so the select screen can show it in its own bordered box instead of burying it at the end
    // of a paragraph of flavour. Null/empty means "no perk" and the box hides itself —
    // CharacterInfo is a struct, so an entry that doesn't set this gets null, not "".
    public string PerkText { get; init; }
    public float MoveSpeedMultiplier { get; init; }
    public float BulletDamageMultiplier { get; init; }

    // Added to the coin pickup's drop chance (Enemy.TryDropPickup), in absolute probability, not as
    // a multiplier. Worth knowing why that scale is the fair one: from round 5 on a coin pickup is
    // worth the same CoinsReward the kill already pays out directly, so raising a 2% drop chance to
    // 17% raises total coin income by about the same 15% — the number reads as a percentage bonus to
    // income, which is what a player assumes it means.
    public float CoinDropBonus { get; init; }

    // Effects a character has on the *enemies* rather than on the ship. All four are read once by
    // Player._Ready and re-exposed there, so Enemy/EnemySpawner never have to know the catalog
    // exists — the character's whole footprint on a run stays applied in one place.

    // Scales every spawned enemy's MaxHp (Juli). 1.0 = untouched.
    public float EnemyHpMultiplier { get; init; }

    // Overrides every enemy's identity colour (Juli). Null leaves each scene's own colour alone.
    public Color? EnemyTint { get; init; }

    // Per-enemy chance, rolled once when that enemy first closes on the player, that it gets hacked
    // and goes inoperable for a second (Maxi) or simply dies on the spot (Nico L).
    //
    // Rolled once per enemy rather than per tick, deliberately: a repeating roll would scale with how
    // long an enemy loiters in range AND with how many are alive at once, so at a late-round cap of
    // ~50 enemies a "2%" would fire several times a second. Once per enemy ties the rate to enemies
    // *spawned*, which is what the percentage reads as. The cost is that it's subtle — expect only a
    // handful of triggers per round, out at the edge of fire range. See docs/characters.md.
    public float EnemyHackChance { get; init; }
    public float EnemySuicideChance { get; init; }

    // Applied as the sprite's Modulate, so each pick reads as a visually distinct character on top
    // of the stat difference, not just a name.
    //
    // Set this to White for a sprite that already carries its own colours — a photo portrait, say.
    // Modulating by white is the identity operation, so "white" *is* how a character opts out of
    // tinting; there's no separate flag to keep in sync.
    public Color Color { get; init; }

    // The three original characters share ship.svg and are told apart by Color alone, which is how
    // the ship looked before the character system existed; portrait characters each point at their
    // own PNG. Any resolution works — Player._Ready derives the on-screen scale from the texture's
    // own dimensions, so the whole cast renders at one size regardless of source size.
    //
    // For a custom character this is a user:// path, which cannot be GD.Load'ed (nothing under
    // user:// goes through the import pipeline). Always go through CharacterCatalog.Texture(info)
    // rather than loading this yourself.
    public string SpritePath { get; init; }

    // Player-created, therefore deletable and loaded from disk at runtime rather than compiled in.
    public bool IsCustom { get; init; }
}

// The playable cast: a fixed built-in list plus whatever the player has created, picked at the
// character-select screen before a run starts.
//
// The built-ins deliberately only vary MoveSpeed/BulletDamage — the two base stats with no absolute
// ceiling of their own (see Player.MaxFireRate/MaxFireRange/MaxLivesCap) — so a character choice is
// a playstyle preference, not a shortcut past the game's balance caps. Custom characters are always
// 1.0/1.0: letting players type their own multipliers would be handing them a cheat menu.
public static class CharacterCatalog
{
    public const string DefaultSlug = "equilibrado";

    private static readonly CharacterInfo[] BuiltIns =
    {
        // The three originals share ship.svg and are told apart by Color alone, which is how the ship
        // looked before the character system existed. Their whole identity is mechanical, so the stat
        // line lives in PerkText and they carry no flavour Description — Equilibrado is the mirror
        // image, all Description and no perk.
        new()
        {
            Slug = "equilibrado",
            Name = "Equilibrado",
            Description = "Estadísticas base, sin ventajas ni desventajas.",
            MoveSpeedMultiplier = 1f,
            BulletDamageMultiplier = 1f,
            Color = new Color(0.49f, 0.99f, 1f, 1f), // cyan — the ship's original default color
            SpritePath = "res://Assets/Sprites/Characters/ship.svg",
        },
        new()
        {
            Slug = "centella",
            Name = "Centella",
            PerkText = "+15% de velocidad, -10% de daño de bala.",
            MoveSpeedMultiplier = 1.15f,
            BulletDamageMultiplier = 0.9f,
            Color = new Color(0.75f, 1f, 0.3f, 1f), // yellow-green — reads as "fast"
            SpritePath = "res://Assets/Sprites/Characters/ship.svg",
        },
        new()
        {
            Slug = "coloso",
            Name = "Coloso",
            PerkText = "-10% de velocidad, +20% de daño de bala.",
            MoveSpeedMultiplier = 0.9f,
            BulletDamageMultiplier = 1.2f,
            Color = new Color(1f, 0.45f, 0.2f, 1f), // orange — reads as "heavy hitter"
            SpritePath = "res://Assets/Sprites/Characters/ship.svg",
        },

        // Portrait characters — the joke roster, expected to rotate. Cosmetic by default: every
        // multiplier defaults to 1.0, so a portrait with no perk arguments is mechanically identical
        // to Equilibrado. Color is White on all of them because their sprites carry their own colours
        // and must not be modulated. See docs/characters.md for how to add, edit or remove one.
        Portrait("maxi", "Maxi",
            "Antes del primer destello de luz, solo existía el vacío y Él: el Coloso primordial, el "
            + "Señor de la Lógica, el Mente Maestra que osó enseñarle a pensar al caos. Con las manos "
            + "desnudas forjó los cimientos del código, traduciendo el orden del universo en algoritmos "
            + "eternos y tallando la realidad línea por línea. Programó la realidad que hoy vivimos.",
            perkText: "Tiene 2% de probabilidad de hackear la nave de los enemigos dentro de su rango "
            + "y dejarlos inoperantes por 1 segundo.",
            enemyHackChance: 0.02f),

        Portrait("conrado", "Conrado",
            "El abuelo de la oficina (Jubilado). Utilizarlo te hace más lento y te mea el mapa pero "
            + "como te bancamos conra jaja.",
            perkText: "-50% de velocidad.",
            moveSpeed: 0.5f),

        Portrait("manu", "Manu",
            "Nacido del barro y la soberbia, desafió los cielos, desgarró la garganta del Creador con "
            + "sus propias manos y ahogó la creación en sangre divina. No buscaba un trono Celestial, "
            + "sino la esencia de la vida eterna; añejó el fluido sagrado en las tinieblas y transformó "
            + "la sangre de Dios en el vino más embriagador que jamás haya tocado labios mortales. De "
            + "esa cosecha sacrílega erigió Vinitus, un imperio de púrpura, hierro y opulencia, "
            + "sostenido por la copa imperial donde cada trago es el recuerdo amargo de un cielo "
            + "destronado.",
            perkText: "Es medio turro, asi que seguro es mano larga: sus víctimas tienen +15% de "
            + "dropear monedas.",
            coinDropBonus: 0.15f),

        Portrait("juan", "Juan",
            "Juan con pelo jaja, que capo, carrea solo."),

        Portrait("nico_l", "Nico L",
            "Nadie sabe cuándo ni cómo acecha. A veces se desvanece en el vacío, ocultando su "
            + "inmensidad tras la piel de un therian: una bestia ancestral que ruge con la esencia pura "
            + "de la naturaleza indomable en el barrio chino. No busca devotos ni coronas; su sola "
            + "existencia es una demostración de aura absoluto, un depredador de vibraciones ante el "
            + "cual hasta los dioses bajan la mirada.",
            perkText: "Los enemigos tienen 1% de probabilidad de suicidarse al entrar en su rango por "
            + "quedar humillados ante semejante aura.",
            enemySuicideChance: 0.01f),

        Portrait("juli", "Juli",
            "No hace nada. Si no la pongo seguro se enoja",
            perkText: "Su presencia convierte a los enemigos en vegetarianos: se tornan verdes y "
            + "-10% de salud.",
            enemyHp: 0.9f, enemyTint: new Color("9bff4d")),
    };

    // The portrait entries differ only in their text, their slug and (for a few) one perk value, so
    // spelling out nine near-identical initialisers would just be nine places for a stat to get
    // typo'd into being non-cosmetic. The defaults live here once, and only here.
    private static CharacterInfo Portrait(
        string slug, string name, string description, string perkText = null,
        float moveSpeed = 1f, float bulletDamage = 1f, float coinDropBonus = 0f,
        float enemyHp = 1f, Color? enemyTint = null,
        float enemyHackChance = 0f, float enemySuicideChance = 0f) => new()
    {
        Slug = slug,
        Name = name,
        Description = description,
        PerkText = perkText,
        MoveSpeedMultiplier = moveSpeed,
        BulletDamageMultiplier = bulletDamage,
        CoinDropBonus = coinDropBonus,
        EnemyHpMultiplier = enemyHp,
        EnemyTint = enemyTint,
        EnemyHackChance = enemyHackChance,
        EnemySuicideChance = enemySuicideChance,
        Color = Colors.White,
        SpritePath = $"res://Assets/Sprites/Characters/{slug}.png",
    };

    // Built-ins first, then the player's own, so a saved selection index never points at a
    // different built-in just because a custom character was added or deleted.
    private static CharacterInfo[] _all;

    public static CharacterInfo[] All
    {
        get
        {
            if (_all != null) return _all;

            var list = new List<CharacterInfo>(BuiltIns);
            list.AddRange(CustomCharacterStore.LoadAll());
            _all = list.ToArray();
            return _all;
        }
    }

    // Call after creating or deleting a custom character so the next read rebuilds from disk.
    public static void Invalidate()
    {
        _all = null;
        _textures.Clear();
    }

    public static CharacterInfo Get(string slug)
    {
        foreach (var info in All)
            if (info.Slug == slug) return info;

        // A saved slug can legitimately vanish — the player deleted that custom character since.
        return BuiltIns[0];
    }

    // The selection used to be persisted as an index into the built-in list. Kept so an existing
    // settings.cfg doesn't silently reset everyone to Equilibrado on upgrade.
    public static string SlugForLegacyIndex(int index) =>
        index >= 0 && index < BuiltIns.Length ? BuiltIns[index].Slug : DefaultSlug;

    // Custom portraits live under user://, which never goes through Godot's import pipeline, so
    // GD.Load cannot see them — they have to be decoded into an ImageTexture by hand. Cached
    // because the arena, the HUD and the carousel all ask for the same texture.
    private static readonly Dictionary<string, Texture2D> _textures = new();

    public static Texture2D Texture(CharacterInfo info)
    {
        if (_textures.TryGetValue(info.Slug, out var cached) && cached != null) return cached;

        Texture2D texture = info.IsCustom
            ? CustomCharacterStore.LoadTexture(info.SpritePath)
            : GD.Load<Texture2D>(info.SpritePath);

        if (texture != null) _textures[info.Slug] = texture;
        return texture;
    }
}
