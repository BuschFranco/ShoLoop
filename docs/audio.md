# Audio: Generated SFX, Throttling & Volume

Every sound in the game is synthesised by [`tools/gen_sfx.py`](../tools/gen_sfx.py) and played through
the `AudioManager` autoload. Nothing is downloaded. See [assets.md](assets.md) for the pipeline the
generators share.

**There is currently no music.** Generated chiptune loops were tried and cut for sounding bad — see
[Music](#music) below for what survives and what it would take to bring music back.

## The sounds are build output, not assets

The `.wav` files in `Assets/Audio/` are generated, the same way the character portraits are generated
by `tools/prep_character_sprite.py`. **To change how something sounds, change its recipe in the script
and re-run it** — don't hand-edit a binary, because the next regeneration would overwrite it.

```bash
python tools/gen_sfx.py                  # all of them
python tools/gen_sfx.py --only shoot hit # just these two
python tools/gen_sfx.py --out /tmp/x     # audition somewhere else first
```

Stdlib only — `wave` is enough to write PCM, so there's nothing to install. The synthesis is square
waves, white noise and fast pitch sweeps at 22 kHz. That low rate is deliberate and is *part of the
sound*: it rolls the high end off the square waves the way the hardware being imitated did.

The noise seed is fixed (`--seed`, default 7), so regenerating without changing a recipe produces
byte-identical files and git stays quiet.

### The manual step you can't skip

Godot assigns every asset a `uid://` in a `.import` sidecar, and **only the editor can write that** —
a Python script can't. So the full workflow is:

1. `python tools/gen_sfx.py`
2. **Open Godot once** (or `godot --headless --import`) so it writes the `.wav.import` files
3. Commit the `.wav` and `.wav.import` files together

Between steps 1 and 2 the paths don't resolve. `AudioManager.LoadStreams` guards every load with
`ResourceLoader.Exists`, so during that window the game runs **silently rather than crashing** — and
that guard is load-bearing, not defensive padding.

## The mix is the sound design

`VOLUME` at the top of `gen_sfx.py` is the most important table in the audio system. Each sound is
normalised to its own peak and then scaled to its slot, so a recipe change can't silently make one
effect twice as loud as everything else.

**The rule is frequency-of-play, inverted.** `hit` fires several times a second in a late round with
Perforación and Rebote and sits at 0.24; `player_die` fires once per run and sits at 1.00. A sound
that plays constantly has to be quiet enough to live under the ones that don't.

## Throttling: the actual hard problem

Generating arcade blips is easy. Keeping four thousand of them per run from turning into white noise
is not. `AudioManager` has three mechanisms, and all three matter:

**1. One sound per event, not per projectile.** A single volley is up to `1 + 2 + ExtraFiringLines`
calls to `FireInDirection`. The shot sound is hooked to `Player.SpawnMuzzleFlash`, which the codebase
had already established runs once per volley for exactly this reason. Five copies of one 85 ms blip
on a single frame don't sound five times louder — they sound like a broken speaker.

**2. A minimum interval per sound type** (the `Defs` table). `Hit` can be triggered a dozen times in
one frame by a piercing, ricocheting shot crossing a crowd, and every trigger past the first few is
inaudible anyway — dropping them costs nothing. Sounds that fire at most once per event (level up,
round start, any death) have an interval of 0 and are never dropped.

**3. Pitch jitter.** The same blip 300 times a run reads as a stuck machine; detuned a few percent
each time it reads as a gun. This does more for repetition fatigue than anything else in the system.

If a late round still sounds like mush, the knobs in order are: raise `Hit`'s interval → lower its
`VOLUME` → widen its jitter.

## Playback

One `AudioStreamPlayer` holding an `AudioStreamPolyphonic`, with `PlayStream()` per effect — the
idiomatic Godot 4 way to get polyphony with a voice cap, and it avoids hand-rolling a pool of N
players to round-robin through. When `Polyphony` (24) is exceeded, `PlayStream` returns an invalid id
and the sound is dropped, which is the right failure for a sound nobody would have picked out.

**`AudioManager` runs with `ProcessMode.Always`, and this is not optional.** The five modals
(`PauseMenu`, `Shop`, `UpgradePicker`, `GameOverScreen`, `ConfirmDialog`) all set `Always` because the
tree is paused the entire time they're open, and an autoload is `Pausable` by default. Without it the
shop and pause-menu buttons would be silent exactly when they're being used, and the death sting
would never play at all — `NotifyPlayerDied` calls `Pause()` on the next line.

## Where each sound is hooked

Most of these are one line at a place the codebase had already identified as "the single point this
event happens".

| Sound | Hook | Why there |
|---|---|---|
| `Shoot` | `Player.SpawnMuzzleFlash` | Once per volley, not per bullet |
| `EnemyShoot` | `ShooterEnemy.OnFireTimeout` | Lower/duller than yours, so incoming fire is identifiable without looking |
| `Hit` | `Enemy.PlayHitFeedback` | Deliberately skipped on the killing blow. Burn ticks pass `showFlash: false` and so never reach it — a DoT shouldn't machine-gun the impact sound |
| `EnemyDie` | `Enemy.TakeDamage` death branch | Skipped for the boss, which has its own |
| `BossDie` | `GameManager.RegisterKill` | |
| `Explosion` | `Missile`/`PlayerMine`/`Mine`/`MissileZone` blast spawners | *Not* in `Juice.Blast` — that helper also draws the level-up nova, Onda, Vendaval and the boss shockwave, none of which are explosions |
| `PlayerHurt` | `Player.LoseLife`, after the death check | So the last life plays the death sting instead of stacking both |
| `Shield` | `Player.TakeHit` shield branch | **A different sound, not a quieter one** — see below |
| `Dodge` | `Player.TakeHit` dodge branch | |
| `PlayerDie` | `GameManager.NotifyPlayerDied` | Needs `ProcessMode.Always` |
| `Pickup` | `PickupBase.OnBodyEntered` | One sound for all four types; the interval collapses a magnet-pulled cluster into one ding |
| `LevelUp` | `GameManager.AddXp`, on `LevelsGained` | **Not** `OnLevelGained`, which runs once per threshold — a triple level-up would stack three arpeggios on one frame |
| `Countdown` | `HUD._Process` countdown block | Reuses the existing once-per-second change detector that already drives the pop |
| `RoundStart` | `GameManager.BeginRoundAfterCountdown` | The "GO" resolving the 3-2-1 |
| `RoundComplete` | `GameManager.BeginRoundEnd` | Single entry point for both timer expiry and boss death |
| `UiClick` | `Juice.WireButtonFeedback`'s `ButtonDown` | **One line covers all 22 buttons across ten screens.** Inside the `Disabled` guard, so a refused press doesn't sound accepted |
| `UiBuy` / `UiDenied` | `Shop.OnBuyPressed` | `UiDenied` only on the can't-afford branch — the other two early returns are cards that already say "bought"/"maxed" on their face |
| `Modal` | `Juice.ModalIn` / `ModalOut` | See the `sound:` flag below |

### Two hooks that needed care

**Shield vs. hurt are deliberately different sounds, not volume variants.** "The shield ate that" and
"that cost you a heart" are different facts about your run, and the player is usually looking at the
enemy rather than at the HUD. `shield` has no noise layer at all, which is what separates them.

**Dodge had no feedback of any kind before this** — no flash, no number, nothing. The only way to know
Esquiva had done something was to notice a hit that didn't cost you, which is indistinguishable from
not having been hit. The sound is the entire feedback for that reward.

### The `sound:` flag on ModalIn/ModalOut

`HUD` uses those two helpers to fade the **boss health bar** in and out — it isn't a modal, and a menu
whoosh every time a boss appears would read as a UI screen that never opened. Both helpers take
`sound: true` by default and HUD passes `false`.

It's an explicit flag rather than inferring "not a modal" from `fromScale == 1f`, because reduced
motion *also* sets `fromScale` to 1 — inferring from it would have silenced every modal for anyone
with reduced motion enabled.

Note `UpgradePicker` opens with `ModalIn` but closes with a bare `Visible = false`, so it has no close
whoosh. That's pre-existing, not deliberate.

## Music

**The game currently ships no music.** Two generated chiptune loops (a slow menu theme and a faster
arena one) were built and then removed for sounding bad. Procedurally generating music that survives a
long session turns out to be a much harder problem than generating a 90 ms laser blip, and the honest
outcome was that the loops grated. `tools/gen_music.py` is gone with them.

**The playback plumbing was kept**, because it's small and it's the part that *did* work:

- A second `AudioStreamPlayer` on the Music bus, child of `AudioManager` — so it inherits
  `ProcessMode.Always` and would keep playing while the tree is paused, which is what you want: the
  shop and pause menu shouldn't drop into silence.
- `PlayMusic(track)` crossfades and no-ops when the requested track is already playing, so backing
  out of a submenu doesn't restart the theme. `MainMenu._Ready` asks for Menu and `HUD._Ready` asks
  for Arena — the HUD because `Arena.tscn`'s root has no script and the HUD is the one node
  guaranteed present in every arena, which also covers the Game Over retry path for free. Both calls
  are harmless no-ops today.
- `AudioManager.LoadMusic` sets `AudioStreamWav.LoopMode = Forward` **in code** rather than relying on
  each `.wav.import`'s `edit/loop_mode`. Those sidecars are written by the editor and easy to lose,
  and a track that silently stops looping because a file nobody reads got rewritten is a miserable
  bug to chase.

**To bring music back**, drop `music_menu.wav` and `music_arena.wav` into `Assets/Audio/`, open Godot
once so it writes the `.import` sidecars, and everything lights up — including the music slider, which
hides itself while `AudioManager.HasMusic` is false rather than sitting there controlling nothing.

If music is never coming back, the honest cleanup is to delete the music player, `PlayMusic`, the
`Music` bus, `MusicVolume` and its options row. It's about forty lines plus a bus.

## Volume: three sliders, three buses

General, Efectos and Música, all 0–100 in steps of 5. **No mute toggles** — a slider that reaches 0 is
the mute, exactly as it already works for the two opacity settings on the same screen.

**Música is hidden while the game has no music track** (see above), so in practice two sliders show
today. It reappears by itself the moment a track exists.

The buses come from `default_bus_layout.tres`: **SFX and Music both send to Master.** That routing is
what makes the General slider work with *no arithmetic anywhere* — attenuating Master attenuates both
children by definition of a send. The alternative (one bus and three numbers multiplied in C#) would
oblige every future sound source to remember to apply that multiplication.

- `GameManager.MasterVolume` / `SfxVolume` / `MusicVolume`, with `SetMasterVolume` / `SetSfxVolume` /
  `SetMusicVolume`. The three setters differ only in which key they write and which bus they drive, so
  the shape (clamp → `ConfigFile` → `Load` → `SetValue` → `Save` → apply) lives once in a private
  `SetVolume`. Keys `master_volume` / `sfx_volume` / `music_volume` in `user://settings.cfg`.
- Music defaults to **0.7**, not 1.0 — music the player notices is music competing with the gunfire
  it's supposed to sit under.
- The defaults live on the field declarations, not in `LoadSettings` — that method returns early when
  there's no settings file yet and never reaches its own defaults on a first-ever launch.
- At 0 the bus is **muted** rather than merely set to `LinearToDb(0)`: a slider at zero should be
  provably silent, not very quiet.
- `AudioManager` pulls the saved values in its own `_Ready` rather than `GameManager` pushing them —
  autoloads initialise in declaration order and `AudioManager` is listed second, so they're already
  loaded by then.

In the Options layout, General carries the explanatory hint and a full-height separator while Efectos
and Música sit under it with tighter 14px gaps and no hints of their own. Three consecutive hint
paragraphs would be noise, and the spacing is what says "these two are inside that one".

Only in the main menu's Options screen. Deliberately *not* duplicated into the pause menu:
`PauseMenu.cs` documents a past bug where one setting ended up with three different names across two
screens.
