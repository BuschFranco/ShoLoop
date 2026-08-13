namespace ShooterLoop;

// World drop from an enemy kill (see Enemy.TryDropPickup). Only ever spawned for a player who
// already owns a Barrier (MaxShieldCharges > 0) — adds 1 shield charge on contact, capped at
// MaxShieldCharges, same as the passive Regeneración timer.
public partial class ShieldPickup : PickupBase
{
    protected override void OnCollected(Player player)
    {
        player.AddShieldCharge();
        SpawnPickupLabel("+1 escudo", Palette.ShieldPickupColor);
    }
}
