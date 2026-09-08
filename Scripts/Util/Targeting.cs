namespace ShooterLoop;

// Shared line-of-sight raycast against obstacles (collision layer 8) — same primitive
// Enemy.IsPathBlocked already uses for steering, reused here so a shooter (the player's gun/missile,
// the Companion drone) doesn't waste a shot firing straight into a wall at whatever's nearest on the
// other side of it.
public static class Targeting
{
    private const uint ObstacleCollisionMask = 8;

    public static bool HasObstacleBetween(Node2D context, Vector2 from, Vector2 to)
    {
        var query = PhysicsRayQueryParameters2D.Create(from, to, ObstacleCollisionMask);
        return context.GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0;
    }
}
