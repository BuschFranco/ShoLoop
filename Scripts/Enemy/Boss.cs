namespace ShooterLoop;

// The boss-round encounter: a big, slow, very tanky melee threat that also plinks away with a
// low-cadence ranged attack. All of that behavior now comes from ShooterEnemy (chase + HP +
// timed EnemyBullets); this type exists so EnemyBoss.tscn has a boss-specific script to hang
// future boss-only mechanics on, and so `is Boss` checks stay possible.
public partial class Boss : ShooterEnemy
{
}
