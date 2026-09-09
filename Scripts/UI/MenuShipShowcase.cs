namespace ShooterLoop;

using System.Collections.Generic;
using Godot;

// A handful of decorative ships drifting behind the main menu; whichever one happens to end up with
// the wandering enemy in its sights and in range fires on it -- pure flavour, no gameplay behind it.
// Deliberately a shot from range rather than the ships closing in and colliding with it: a convoy
// veering off its patrol to go ram something reads as broken AI, a quick shot while it's already
// lined up reads as an alert patrol. Sibling of MenuFireworks (same reasoning: sits between Background
// and Fireworks in the tree so it draws over the backdrop and under every Control, no ZIndex needed)
// but uses real Sprite2D children rather than a single _Draw call, since it needs the same rotating,
// textured look the arena's ships/enemies have rather than a swarm of tiny primitives.
public partial class MenuShipShowcase : Node2D
{
    private class Actor
    {
        public Sprite2D Visual;
        public Vector2 Velocity;
        public Vector2 Target;
        public float Speed;
        public bool IsEnemy;
    }

    // Ship.png points right (angle 0) undrawn, same convention Player.UpdateFacing relies on. Grunt's
    // art points up, so its facing needs the same -Pi/2 offset Enemy.VisualForwardAngle defaults to.
    private const float ShipForwardAngle = 0f;
    private const float EnemyForwardAngle = -Mathf.Pi / 2f;
    private const float TurnRate = 4f;

    private const float ShipSpeedMin = 22f;
    private const float ShipSpeedMax = 40f;
    private const float EnemySpeed = 30f;

    // How close the enemy needs to wander before a ship reacts to it.
    private const float FireRange = 130f;
    private const float AimDuration = 0.25f;       // brief turn-to-aim before firing
    private const float ShotTravelTime = 0.12f;    // how long the laser is visibly in flight
    private const float ShotCooldownMin = 1.5f;
    private const float ShotCooldownMax = 4f;
    private const float BeamWidth = 2f;

    private readonly List<Actor> _ships = new();
    private Actor _enemy;
    private readonly RandomNumberGenerator _rng = new();
    private float _shotCooldown;
    private bool _shotInFlight;
    // Ship mid-turn-to-aim, null the rest of the time. A ship's rotation otherwise always points
    // toward its own patrol waypoint (see StepActor), which is unrelated to where the enemy happens
    // to be -- checking that rotation against the enemy's bearing (an earlier version of this) meant
    // a ship could fly right past or over the enemy without ever having it "in its sights" by pure
    // coincidence of patrol heading. Proximity alone now triggers the reaction; this is what makes
    // the ship actually swivel toward it for a beat before firing, instead of shooting from whatever
    // direction it happened to be facing.
    private Actor _aimingShip;
    private float _aimTimer;

    public override void _Ready()
    {
        _rng.Randomize();

        // Same call as the rest of the menu's decoration under reduced motion: nothing to degrade
        // this to, so it simply doesn't run.
        if (DangerLevel.Reduced)
        {
            SetProcess(false);
            return;
        }

        var shipTexture = GD.Load<Texture2D>("res://Assets/Sprites/Characters/ship.png");
        var enemyTexture = GD.Load<Texture2D>("res://Assets/Sprites/Enemies/grunt.png");

        // Three of the original ship.png colours, pulled from the real catalog rather than invented
        // -- the same "belongs to this game's palette" reasoning MenuFireworks already follows for
        // its burst colours.
        string[] slugs = { "equilibrado", "centella", "coloso" };
        foreach (string slug in slugs)
        {
            var ship = new Sprite2D
            {
                Texture = shipTexture,
                Scale = new Vector2(2f, 2f),
                Modulate = CharacterCatalog.Get(slug).Color,
            };
            AddChild(ship);
            var actor = new Actor
            {
                Visual = ship,
                Speed = _rng.RandfRange(ShipSpeedMin, ShipSpeedMax),
            };
            // Enters from off-screen on whichever side its first wander point sits on, rather than
            // popping into existence already mid-scene -- same "arrives, doesn't just appear" feel
            // the enemy already had via RandomEdgePoint (used below, and on every respawn).
            Vector2 firstTarget = RandomWanderPoint();
            actor.Target = firstTarget;
            ship.Position = RandomSideEdgePoint(firstTarget.X < GetViewportRect().Size.X / 2f);
            _ships.Add(actor);
        }

        var enemyVisual = new Sprite2D
        {
            Texture = enemyTexture,
            Scale = new Vector2(2f, 2f),
            Modulate = new Color(1f, 0.18f, 0.53f, 1f), // same magenta EnemyGrunt.tscn uses
        };
        AddChild(enemyVisual);
        _enemy = new Actor { Visual = enemyVisual, Speed = EnemySpeed, IsEnemy = true };
        _enemy.Target = RandomWanderPoint();
        enemyVisual.Position = RandomEdgePoint();

        _shotCooldown = _rng.RandfRange(ShotCooldownMin, ShotCooldownMax);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        foreach (var ship in _ships)
            StepActor(ship, dt, aiming: ship == _aimingShip);

        StepActor(_enemy, dt);

        if (_shotInFlight) return;

        if (_aimingShip != null)
        {
            _aimTimer -= dt;
            if (_aimTimer <= 0f)
            {
                var ship = _aimingShip;
                _aimingShip = null;
                FireShot(ship);
            }
            return;
        }

        _shotCooldown -= dt;
        if (_shotCooldown > 0f) return;

        // Ships keep patrolling their own route the whole time -- nothing here changes course to "go
        // fight". Whichever one happens to end up close enough starts the aim-then-fire sequence.
        foreach (var ship in _ships)
        {
            if (ship.Visual.Position.DistanceTo(_enemy.Visual.Position) <= FireRange)
            {
                _aimingShip = ship;
                _aimTimer = AimDuration;
                break;
            }
        }
    }

    // Menu-only bounds: the middle horizontal band holds the title and records table, so wander
    // targets (and the ships' own starting spots) stay in the side margins -- same avoid-the-middle
    // reasoning MenuFireworks already applies to its launch position.
    private Vector2 RandomWanderPoint()
    {
        Vector2 view = GetViewportRect().Size;
        float x = _rng.Randf() < 0.5f
            ? _rng.RandfRange(view.X * 0.04f, view.X * 0.24f)
            : _rng.RandfRange(view.X * 0.76f, view.X * 0.96f);
        float y = _rng.RandfRange(view.Y * 0.08f, view.Y * 0.92f);
        return new Vector2(x, y);
    }

    private Vector2 RandomEdgePoint()
    {
        Vector2 view = GetViewportRect().Size;
        return new Vector2(_rng.RandfRange(0f, view.X), _rng.Randf() < 0.5f ? -20f : view.Y + 20f);
    }

    // Ships wander in the left/right margins (see RandomWanderPoint), so they enter from whichever
    // side edge they'll end up patrolling near instead of dropping in from the top like the enemy.
    private Vector2 RandomSideEdgePoint(bool fromLeft)
    {
        Vector2 view = GetViewportRect().Size;
        float x = fromLeft ? -20f : view.X + 20f;
        float y = _rng.RandfRange(view.Y * 0.08f, view.Y * 0.92f);
        return new Vector2(x, y);
    }

    private void StepActor(Actor actor, float dt, bool aiming = false)
    {
        Vector2 toTarget = actor.Target - actor.Visual.Position;
        float distance = toTarget.Length();

        if (distance < 4f)
        {
            actor.Target = RandomWanderPoint();
            return;
        }

        Vector2 moveDir = toTarget / distance;
        actor.Velocity = moveDir * actor.Speed;
        actor.Visual.Position += actor.Velocity * dt;

        // Keeps flying its patrol line either way -- only which way it's *facing* changes while
        // aiming, swivelling toward the enemy instead of toward its own waypoint for the beat before
        // it fires.
        Vector2 facingDir = aiming ? (_enemy.Visual.Position - actor.Visual.Position).Normalized() : moveDir;
        float forwardOffset = actor.IsEnemy ? EnemyForwardAngle : ShipForwardAngle;
        float targetRotation = facingDir.Angle() - forwardOffset;
        float weight = 1f - Mathf.Exp(-TurnRate * dt);
        actor.Visual.Rotation = Mathf.LerpAngle(actor.Visual.Rotation, targetRotation, weight);
    }

    // A short, bright bolt from the ship straight to where the enemy is right now. Travel time is
    // brief enough (ShotTravelTime) that aiming at its current position rather than leading the shot
    // still reads as a hit, without needing real projectile physics for something this decorative.
    private void FireShot(Actor ship)
    {
        _shotInFlight = true;
        Vector2 from = ship.Visual.Position;
        Vector2 to = _enemy.Visual.Position;

        var beam = new Line2D();
        beam.Points = new[] { from, to };
        beam.Width = BeamWidth;
        beam.DefaultColor = Palette.PlayerBullet;
        beam.ZIndex = 3;
        AddChild(beam);

        var tween = beam.CreateTween();
        tween.TweenProperty(beam, "modulate:a", 0f, ShotTravelTime).SetDelay(ShotTravelTime * 0.4f);
        tween.Chain().TweenCallback(Callable.From(() => beam.QueueFree()));

        GetTree().CreateTimer(ShotTravelTime).Timeout += () => ResolveShot(to);
    }

    private void ResolveShot(Vector2 pos)
    {
        Juice.Blast(this, pos, 10f, Palette.PlayerBullet, growTime: 0.12f, fadeTime: 0.3f);
        Juice.Spark(this, pos, _enemy.Velocity.Normalized(), Colors.White, 8f);

        // The enemy "loses" and reappears elsewhere to be hunted down again later.
        _enemy.Visual.Position = RandomEdgePoint();
        _enemy.Target = RandomWanderPoint();
        _shotInFlight = false;
        _shotCooldown = _rng.RandfRange(ShotCooldownMin, ShotCooldownMax);
    }
}
