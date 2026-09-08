namespace ShooterLoop;

// A slow-drifting dust layer sitting between the backdrop and the arena grid. It is pure depth cue
// and touches nothing gameplay-visible: because it tracks the camera at a fraction of the world's
// rate, panning separates it from the floor, and a flat scene starts reading as having distance in it.
// Motion is what sells the effect — a static field of dots would just be more decoration.
//
// Drawn in a single _Draw pass rather than as N child nodes. The dots never move relative to each
// other, so one canvas item covers the whole field instead of ninety nodes for the scene tree to walk
// every frame, which matters on the Mobile renderer this project targets.
public partial class DustField : Node2D
{
    // 0 would pin the field to the world (no parallax at all); 1 would weld it to the camera so it
    // never appears to move. Between those, lower reads as further away.
    [Export] public float ParallaxFactor = 0.45f;
    [Export] public int DotCount = 90;

    // Spread over more than the arena so the field still covers the viewport once parallax has slid
    // it out from under the camera at the far corners.
    private const float FieldMargin = 1.7f;

    private const float MinRadius = 1.2f;
    private const float MaxRadius = 3.4f;
    private const float MinAlpha = 0.10f;
    private const float MaxAlpha = 0.34f;

    // Dimmer and bluer than the grid, so the two layers separate by tone as well as by motion.
    private static readonly Color DustColor = new("8f6ad0");

    private readonly Random _rng = new();
    private Vector2[] _positions = System.Array.Empty<Vector2>();
    private float[] _radii = System.Array.Empty<float>();
    private float[] _alphas = System.Array.Empty<float>();

    public override void _Ready()
    {
        // Behind the grid (-3) and the arena outline (-2), in front of the Backdrop — which is on its
        // own CanvasLayer at -10 and therefore not orderable against this by ZIndex at all.
        ZIndex = -4;

        // Read off the player the same way ArenaBounds does, so the arena's size stays one number to
        // change rather than two that can silently disagree.
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        Vector2 extents = (player?.ArenaHalfExtents ?? new Vector2(2200f, 1400f)) * FieldMargin;

        _positions = new Vector2[DotCount];
        _radii = new float[DotCount];
        _alphas = new float[DotCount];

        for (int i = 0; i < DotCount; i++)
        {
            _positions[i] = new Vector2(
                (float)(_rng.NextDouble() * 2.0 - 1.0) * extents.X,
                (float)(_rng.NextDouble() * 2.0 - 1.0) * extents.Y);
            _radii[i] = Mathf.Lerp(MinRadius, MaxRadius, (float)_rng.NextDouble());

            // Alpha correlates with size: bigger dots read as nearer, so making them brighter too
            // gives the field a little internal depth of its own rather than one flat scatter.
            float t = (_radii[i] - MinRadius) / (MaxRadius - MinRadius);
            _alphas[i] = Mathf.Lerp(MinAlpha, MaxAlpha, t);
        }

        QueueRedraw();
    }

    // _Process, not _PhysicsProcess: this chases the camera, which Godot's own position smoothing
    // interpolates per *frame*. Updating it on the physics tick instead would make the dust step in
    // place at 60Hz while everything else it's parallaxing against moves smoothly.
    public override void _Process(double delta)
    {
        var camera = GetViewport()?.GetCamera2D();
        if (camera == null) return;

        // Moving the field WITH the camera by (1 - factor) leaves it apparently moving at `factor` of
        // the rate of anything actually anchored in the world.
        GlobalPosition = camera.GlobalPosition * (1f - ParallaxFactor);
    }

    public override void _Draw()
    {
        for (int i = 0; i < _positions.Length; i++)
        {
            var color = DustColor;
            color.A = _alphas[i];
            // A square rather than a disc, and sized by its own radius rather than snapped to the
            // block grid -- these are 1-3px across, so snapping would flatten every mote to one size.
            float d = _radii[i] * 2f;
            DrawRect(new Rect2(_positions[i] - Vector2.One * _radii[i], new Vector2(d, d)), color);
        }
    }
}
