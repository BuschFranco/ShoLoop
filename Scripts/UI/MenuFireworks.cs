namespace ShooterLoop;

using System.Collections.Generic;
using Godot;

// The main menu's backdrop: rockets climbing and bursting, drawn on the game's block grid.
//
// Everything is one Node2D with a single _Draw rather than a node per spark. A firework is ~40
// particles and several can be in the air at once, so a Polygon2D each would mean a few hundred nodes
// being created and freed continuously behind a menu -- for shapes that are literally one filled
// square. A list of structs and one draw call is both simpler and cheaper.
//
// The pixel-art read comes from two things, and neither is the particle's shape: positions are
// snapped to Juice.PixelSize before drawing, so sparks move in steps rather than gliding, and alpha
// is quantised so they fade in visible stages instead of dissolving.
public partial class MenuFireworks : Node2D
{
    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;          // counts down to 0
        public float MaxLife;
        public Color Color;
        public float Size;
        public bool IsRocket;       // bursts into Sparks when its life runs out
        public float TrailTimer;
    }

    // Gravity is far below real scale on purpose: at anything realistic the burst collapses before
    // it has finished expanding, and what reads as a firework is the slow hang at the top.
    private const float Gravity = 210f;
    private const float Drag = 0.72f;          // per second, applied to the burst so it settles

    private const float LaunchIntervalMin = 0.7f;
    private const float LaunchIntervalMax = 1.9f;

    private const int SparksMin = 26;
    private const int SparksMax = 40;
    private const float SparkSpeedMin = 90f;
    private const float SparkSpeedMax = 260f;

    // How many alpha steps a spark fades through. Continuous alpha reads as a soft glow; four steps
    // read as a sprite being swapped.
    private const int AlphaSteps = 4;

    // Bright, saturated, and drawn from the game's own palette family rather than invented, so the
    // menu still looks like this game. No HDR values here: the main menu has no WorldEnvironment, so
    // anything past 1.0 would simply clamp and look identical to a plain bright colour.
    private static readonly Color[] BurstColors =
    {
        new("ff4fd8"), new("4fa8ff"), new("9bff4d"), new("ffe066"),
        new("c65bff"), new("3dfff0"), new("ff5fa8"), new("ff8a3d"),
    };

    private readonly List<Particle> _particles = new();
    private readonly RandomNumberGenerator _rng = new();
    private float _nextLaunch;

    public override void _Ready()
    {
        _rng.Randomize();

        // No ZIndex here, deliberately. The obvious "ZIndex = -10 so it sits behind the menu" puts it
        // behind MainMenu's Background too -- which is a fully opaque ColorRect, so the whole effect
        // would render and never be seen. Tree order already does the right thing: this node is a
        // sibling placed after Background and before VBoxContainer, so it draws over the backdrop and
        // under every control.

        // A backdrop that never stops moving is exactly what reduced motion promises to remove, and
        // unlike the other effects there's nothing to degrade it to -- so it simply doesn't run.
        if (DangerLevel.Reduced)
        {
            SetProcess(false);
            return;
        }

        // Staggered, and the first one comes quickly: an empty sky for two seconds when the menu
        // opens makes the whole effect look broken rather than idle.
        _nextLaunch = _rng.RandfRange(0.15f, 0.5f);
        Launch();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        _nextLaunch -= dt;
        if (_nextLaunch <= 0f)
        {
            Launch();
            _nextLaunch = _rng.RandfRange(LaunchIntervalMin, LaunchIntervalMax);
        }

        // Iterated backwards so a removal doesn't skip the next particle.
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;

            if (p.Life <= 0f)
            {
                _particles.RemoveAt(i);
                if (p.IsRocket) Burst(p.Position, p.Color);
                continue;
            }

            p.Velocity.Y += Gravity * dt;
            if (!p.IsRocket) p.Velocity *= 1f - Drag * dt;
            p.Position += p.Velocity * dt;

            // The rocket's trail is spawned here rather than by the rocket itself so it inherits the
            // exact position it was at, and so a trail spark is an ordinary particle from then on.
            if (p.IsRocket)
            {
                p.TrailTimer -= dt;
                if (p.TrailTimer <= 0f)
                {
                    p.TrailTimer = 0.03f;
                    _particles.Add(new Particle
                    {
                        Position = p.Position,
                        Velocity = new Vector2(_rng.RandfRange(-12f, 12f), _rng.RandfRange(-6f, 18f)),
                        Life = 0.35f, MaxLife = 0.35f,
                        Color = new Color(p.Color, 0.6f),
                        Size = Juice.PixelSize,
                    });
                }
            }

            _particles[i] = p;
        }

        QueueRedraw();
    }

    private void Launch()
    {
        Vector2 view = GetViewportRect().Size;
        var color = BurstColors[_rng.RandiRange(0, BurstColors.Length - 1)];

        // Climbs from just below the bottom edge, so it enters the frame already moving rather than
        // appearing from nothing. Horizontal spread avoids the middle third, where the title and the
        // records panel sit -- a burst behind text makes both harder to read.
        float x = _rng.Randf() < 0.5f
            ? _rng.RandfRange(view.X * 0.04f, view.X * 0.30f)
            : _rng.RandfRange(view.X * 0.70f, view.X * 0.96f);

        // Life, not a target height, decides where it bursts: with gravity already acting on it that
        // gives a natural spread of burst heights without picking one.
        _particles.Add(new Particle
        {
            Position = new Vector2(x, view.Y + 10f),
            Velocity = new Vector2(_rng.RandfRange(-40f, 40f), _rng.RandfRange(-620f, -480f)),
            Life = _rng.RandfRange(0.75f, 1.05f),
            MaxLife = 1f,
            Color = color,
            Size = Juice.PixelSize,
            IsRocket = true,
            TrailTimer = 0f,
        });
    }

    private void Burst(Vector2 origin, Color color)
    {
        int count = _rng.RandiRange(SparksMin, SparksMax);

        // A ring plus jitter rather than fully random angles: evenly spaced sparks read as a firework,
        // where uniform randomness clumps and reads as debris.
        float step = Mathf.Tau / count;
        for (int i = 0; i < count; i++)
        {
            float angle = i * step + _rng.RandfRange(-step * 0.4f, step * 0.4f);
            float speed = _rng.RandfRange(SparkSpeedMin, SparkSpeedMax);

            // A handful of white sparks per burst. They're what make it read as hot rather than as a
            // coloured circle expanding.
            bool white = _rng.Randf() < 0.18f;

            float life = _rng.RandfRange(0.7f, 1.5f);
            _particles.Add(new Particle
            {
                Position = origin,
                Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                Life = life,
                MaxLife = life,
                Color = white ? new Color(1f, 0.97f, 0.9f) : color,
                // The bigger sparks are the minority, so a burst has some weight in it instead of
                // being uniform confetti.
                Size = Juice.PixelSize * (_rng.Randf() < 0.25f ? 2f : 1f),
            });
        }
    }

    public override void _Draw()
    {
        foreach (var p in _particles)
        {
            float t = p.MaxLife <= 0f ? 0f : p.Life / p.MaxLife;

            // Quantised: a spark should step down through a few brightnesses like a sprite swap, not
            // dissolve. Ceil keeps the last step visible instead of fading to nothing a frame early.
            float alpha = Mathf.Ceil(t * AlphaSteps) / AlphaSteps;
            if (alpha <= 0f) continue;

            // Snapping the position, not just the size, is what makes them move in steps. Without it
            // the squares are pixel-shaped but glide between pixels, which reads as smooth motion with
            // blocky sprites -- the exact mismatch the pixel-art pass exists to avoid.
            Vector2 pos = p.Position.Snapped(Vector2.One * Juice.PixelSize);
            DrawRect(new Rect2(pos, new Vector2(p.Size, p.Size)),
                new Color(p.Color, p.Color.A * alpha));
        }
    }
}
