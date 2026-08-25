namespace ShooterLoop;

// One name per concept, for every screen that shows it.
//
// This exists because the same thing had up to four names depending on where you looked: fire rate
// was "CAD" in the HUD, "Cadencia" in the pause menu and "Fuego Rápido" in the shop; kills were
// "BAJAS", "Eliminados" and "Enemigos"; a level was "Nv" in four places and "Lv" in one. None of
// that was a typo — each screen was written on its own and picked its own wording, which is exactly
// the failure mode a shared constant prevents.
//
// The split this encodes: a STAT is a number the player watches (Daño, Cadencia, Alcance), a REWARD
// is a product they buy ("Balas Afiladas", "Fuego Rápido"). Those stay different vocabularies on
// purpose — the reward names carry the game's flavour and shouldn't be flattened into stat labels —
// but each side must be internally consistent, and each reward's description names the stat it
// moves so the player can connect the two.
//
// Same shape as Palette and BuildCatalog: presentation only, no logic, no scene-tree knowledge.
public static class Glossary
{
    // --- Stats ---
    // Deliberately unabbreviated. "CAD" saved six characters in the HUD and cost the player any
    // chance of connecting it to the "Fuego Rápido" they had just bought.
    public const string Damage = "Daño";
    public const string FireRate = "Cadencia";
    public const string Range = "Alcance";
    public const string Crit = "Crítico";
    public const string Kills = "Bajas";
    public const string Pierce = "Perforación";
    public const string Dodge = "Esquiva";

    // --- Shared vocabulary ---
    // "Nv" everywhere. The pause menu's powers block used "Lv" while every other screen — including
    // the pause menu's own status block, three lines above — used "Nv".
    public const string LevelPrefix = "Nv";

    // One phrasing for "you can't improve this any further". There were five: "Al tope",
    // "ya estás en el tope", "estás al tope y a full", "ya estás a full", and "(tope N)".
    public const string AtCap = "Al máximo";
    public const string AtCapSentence = "ya estás al máximo";
    public const string Owned = "Ya lo tenés";
    public const string OwnedBetter = "Ya tenés esto o mejor";

    // "tier" was internal jargon leaking into player-facing copy — the cards themselves never use
    // the word, they say COMÚN/RARO/ÉPICO/LEGENDARIO.
    public const string Rarity = "rareza";

    // --- Ability icons ---
    // The HUD identifies seven abilities by a single letter each, with no legend anywhere and no
    // hover to hang a tooltip off (this is a touch game). These are the letter→name pairs the pause
    // menu's legend prints, and they're the single source both it and the HUD scene agree on.
    public static readonly (string Glyph, string Name)[] AbilityLegend =
    {
        ("L", "Láser"),
        ("M", "Misil"),
        ("N", "Mina"),
        ("O", "Onda de Choque"),
        ("V", "Vendaval"),
        ("R", "Regeneración de escudo"),
        ("U", "Ultimate"),
    };
}
