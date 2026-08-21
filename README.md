# ShooterLoop

A mobile auto-shooter arena game built in **Godot 4.7** with **C# / .NET 8**. Vampire-Survivors-style
round loop, Geometry-Wars-style visuals — every sprite in the game is a procedurally-built
`Polygon2D` or `Line2D`, so there are no art assets to manage.

## The loop

Move with a virtual joystick (or WASD); your gun fires automatically at the nearest enemy inside
your range ring. Survive a 60-second round, spend Coins in the shop, repeat. Every 5th round is a
Boss Round. Killing your first boss unlocks an Ultimate — a Score-charged special you fire with a
HUD button or **R**.

## Running it

Open the project folder in Godot 4.7 (Mono/.NET build) and press Play. The main scene is
`Scenes/UI/MainMenu.tscn`. The .NET SDK 8 toolchain must be installed for the C# assembly to build.

## Layout

| Path | What's in it |
|---|---|
| `Scenes/` | All `.tscn` files — arena, player, enemies, UI |
| `Scripts/Autoload/` | `GameManager` — the single autoload singleton; rounds, XP, economy, run state |
| `Scripts/Player/` | Player movement/combat, orbit blades, companion drone |
| `Scripts/Enemy/` | Base `Enemy`, `ShooterEnemy`, `Boss`, and the spawner |
| `Scripts/Upgrades/` | Reward catalog and tier rolling |
| `Scripts/UI/` | HUD, shop, upgrade picker, pause/game-over screens |
| `Scripts/Util/` | `RoundCurve`, `DifficultyBalancer`, `CameraRig` |
| `docs/` | Design documentation — see below |

## Documentation

The `docs/` folder explains *why* each system works the way it does, not just what it does. Worth
reading before changing balance numbers:

- [rounds.md](docs/rounds.md) — the round lifecycle, boss rounds, the modal queue
- [enemies.md](docs/enemies.md) — enemy categories, shooters, splitting, collision layers
- [player.md](docs/player.md) — movement/inertia, lives, shield, abilities, the arena & camera
- [characters.md](docs/characters.md) — the playable cast, their perks, and how to change them
- [rewards.md](docs/rewards.md) — the reward catalog and the stacking rules
- [economy.md](docs/economy.md) — Coins vs. Score, shop pricing
- [difficulty-scaling.md](docs/difficulty-scaling.md) — `RoundCurve` and adaptive difficulty
- [visuals.md](docs/visuals.md) — palette, glow, and animation

Nearly all balance tuning is done by editing named constants in one place per system — the
`RoundCurve` fields at the top of `EnemySpawner.cs`, the cap constants in `Player.cs`, and the
catalog in `UpgradeData.cs`.
