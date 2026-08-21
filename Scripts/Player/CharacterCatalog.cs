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
    public float MoveSpeedMultiplier { get; init; }
    public float BulletDamageMultiplier { get; init; }

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
            Description = "+15% velocidad, -10% daño de bala.",
            MoveSpeedMultiplier = 1.15f,
            BulletDamageMultiplier = 0.9f,
            Color = new Color(0.75f, 1f, 0.3f, 1f), // yellow-green — reads as "fast"
            SpritePath = "res://Assets/Sprites/Characters/ship.svg",
        },
        new()
        {
            Slug = "coloso",
            Name = "Coloso",
            Description = "-10% velocidad, +20% daño de bala.",
            MoveSpeedMultiplier = 0.9f,
            BulletDamageMultiplier = 1.2f,
            Color = new Color(1f, 0.45f, 0.2f, 1f), // orange — reads as "heavy hitter"
            SpritePath = "res://Assets/Sprites/Characters/ship.svg",
        },

        // Portrait characters. Cosmetic by default — every multiplier is 1.0, so picking one is
        // mechanically the same as picking Equilibrado — with Conrado as the single deliberate
        // exception. Color is White on all of them because their sprites carry their own colours
        // and must not be modulated.
        //
        // Swapping a photo is one command, and nothing else has to change:
        //   python tools/prep_character_sprite.py foto.jpg Assets/Sprites/Characters/maxi.png
        Portrait("maxi", "Maxi",
            "Antes del primer destello de luz, solo existía el vacío y Él: el Coloso primordial, el "
            + "Señor de la Lógica, el Mente Maestra que osó enseñarle a pensar al caos. Con las manos "
            + "desnudas forjó los cimientos del código, traduciendo el orden del universo en algoritmos "
            + "eternos y tallando la realidad línea por línea. Programó la realidad que hoy vivimos."),

        Portrait("conrado", "Conrado",
            "El abuelo de la oficina (Jubilado). Utilizarlo te hace más lento y te mea el mapa pero "
            + "como te bancamos conra jaja. -50% de velocidad.",
            moveSpeed: 0.5f),

        Portrait("manu", "Manu",
            "Nacido del barro y la soberbia, desafió los cielos, desgarró la garganta del Creador con "
            + "sus propias manos y ahogó la creación en sangre divina. No buscaba un trono Celestial, "
            + "sino la esencia de la vida eterna; añejó el fluido sagrado en las tinieblas y transformó "
            + "la sangre de Dios en el vino más embriagador que jamás haya tocado labios mortales. De "
            + "esa cosecha sacrílega erigió Vinitus, un imperio de púrpura, hierro y opulencia, "
            + "sostenido por la copa imperial donde cada trago es el recuerdo amargo de un cielo "
            + "destronado."),

        Portrait("juan", "Juan",
            "Juan con pelo jaja, que capo, carrea solo."),

        Portrait("nico_l", "Nico L",
            "Nadie sabe cuándo ni cómo acecha. A veces se desvanece en el vacío, ocultando su "
            + "inmensidad tras la piel de un therian: una bestia ancestral que ruge con la esencia pura "
            + "de la naturaleza indomable en el barrio chino. No busca devotos ni coronas; su sola "
            + "existencia es una demostración de aura absoluto, un depredador de vibraciones ante el "
            + "cual hasta los dioses bajan la mirada."),

        Portrait("juli", "Juli",
            "No hace nada. Si no la pongo seguro se enoja"),
    };

    // The portrait entries differ only in their text, their slug and (for Conrado) one multiplier,
    // so spelling out six near-identical initialisers would just be six places for a stat to get
    // typo'd into being non-cosmetic. The defaults live here once, and only here.
    private static CharacterInfo Portrait(
        string slug, string name, string description,
        float moveSpeed = 1f, float bulletDamage = 1f) => new()
    {
        Slug = slug,
        Name = name,
        Description = description,
        MoveSpeedMultiplier = moveSpeed,
        BulletDamageMultiplier = bulletDamage,
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
