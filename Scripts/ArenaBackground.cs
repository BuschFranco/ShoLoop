namespace ShooterLoop;

// Random scenery skin for the arena, picked once per run so consecutive rounds don't repeat one
// backdrop. Drawn as a sibling ON TOP of the Backdrop ColorRect (see DangerDirector/DangerLevel),
// never behind or instead of it: the danger-tint colour tween that ColorRect drives is what keeps
// the arena floor below WorldEnvironment's glow_hdr_threshold, and this node's own partial Modulate
// alpha only ever darkens the composited pixel relative to the image's own brightness -- it can't
// push the result over that threshold the way drawing it at full opacity might.
public partial class ArenaBackground : TextureRect
{
    private static readonly Texture2D[] Scenes =
    {
        GD.Load<Texture2D>("res://Assets/Sprites/Backgrounds/bg_arena_nebula.png"),
        GD.Load<Texture2D>("res://Assets/Sprites/Backgrounds/bg_boss_battle.png"),
        GD.Load<Texture2D>("res://Assets/Sprites/Backgrounds/bg_menu_synthgrid.png"),
        GD.Load<Texture2D>("res://Assets/Sprites/Backgrounds/bg_nebula_cyan_violet.png"),
        GD.Load<Texture2D>("res://Assets/Sprites/Backgrounds/bg_nebula_magenta_gold.png"),
        GD.Load<Texture2D>("res://Assets/Sprites/Backgrounds/bg_nebula_emerald_cyan.png"),
        GD.Load<Texture2D>("res://Assets/Sprites/Backgrounds/bg_galaxy_spiral_planets.png"),
        GD.Load<Texture2D>("res://Assets/Sprites/Backgrounds/bg_galaxy_ringed_planet.png"),
        GD.Load<Texture2D>("res://Assets/Sprites/Backgrounds/bg_galaxy_barred_spiral.png"),
        GD.Load<Texture2D>("res://Assets/Sprites/Backgrounds/bg_cyberpunk_cyanmagenta.png"),
        GD.Load<Texture2D>("res://Assets/Sprites/Backgrounds/bg_cyberpunk_violetgold.png"),
        GD.Load<Texture2D>("res://Assets/Sprites/Backgrounds/bg_cyberpunk_topdown.png"),
    };

    public override void _Ready()
    {
        Texture = Scenes[GD.Randi() % Scenes.Length];
    }
}
