namespace ShooterLoop;

// World drop from an enemy kill (see Enemy.TryDropPickup) — a bonus on top of the XP an enemy
// already grants on death, worth whatever XpReward that specific enemy carried.
public partial class XpPickup : PickupBase
{
    public int XpAmount = 1;

    protected override void OnCollected(Player player)
    {
        // Score stays synced 1:1 with XP everywhere else (see GameManager.RegisterKill), so a
        // picked-up gem follows the same rule instead of only inflating the level bar.
        GameManager.Instance?.AddXp(XpAmount);
        GameManager.Instance?.AddScore(XpAmount);
        SpawnPickupLabel($"+{XpAmount} XP", Palette.XpPickup);
    }
}
