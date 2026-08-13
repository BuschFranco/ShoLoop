namespace ShooterLoop;

// World drop from an enemy kill (see Enemy.TryDropPickup) — a bonus on top of the coins an enemy
// already grants on death, worth whatever CoinsReward that specific enemy carried.
public partial class CoinPickup : PickupBase
{
    public int CoinAmount = 1;

    protected override void OnCollected(Player player)
    {
        GameManager.Instance?.AddCoins(CoinAmount);
        SpawnPickupLabel($"+{CoinAmount} monedas", Palette.CoinPickup);
    }
}
