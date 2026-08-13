namespace ShooterLoop;

// World drop from an enemy kill (see Enemy.TryDropPickup). Heals the player 1 life on contact —
// a mid-run top-up, distinct from the Corazón Legendario reward which raises MaxLives itself.
public partial class HeartPickup : PickupBase
{
    protected override void OnCollected(Player player)
    {
        player.AddLife(1);
        SpawnPickupLabel("+1 vida", Palette.HeartPickup);
    }
}
