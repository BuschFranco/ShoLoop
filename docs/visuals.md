# Visuals: Palette, Glow & Animation

Almost nothing here is an art asset. Pickups, mines, novas, blasts, projectiles, obstacles, health
bars, the arena edge, the joystick and every HUD element are a `Polygon2D`, `Line2D`, `ColorRect` or
`Label` built either in a `.tscn` or procedurally in code. The look comes from colour, glow, and
tweens.

The one exception is the **entity bodies** — the player and the 11 enemies — which are `Sprite2D`
nodes fed by the flat white SVG silhouettes in `Assets/Sprites/`. They were `Polygon2D` until commit
`e0ee940`; the shapes are the same, only the mechanism changed. Three rules keep that mechanism
honest, and breaking any one of them is what made `e0ee940` a visual regression:

1. **The SVG is pure white.** The scene's `modulate` supplies the entity's colour by multiplying over
   it, exactly as `Polygon2D.color` used to. A pre-tinted sprite would come out doubly tinted.
2. **Size lives in the scene's `scale`, not in the SVG.** Each SVG is authored at 4× its world size
   and every entity scene sets `scale = Vector2(0.25, 0.25)` — one number across all twelve, so a
   wrong size is obvious on sight. (The 4× supersample is what keeps edges crisp through the 1.25–1.3×
   punch and telegraph pulses.)
3. **No animation may assume `Vector2.One` or `Colors.White` is the resting state.** Because `scale`
   and `modulate` are now load-bearing, every tween has to return to `Enemy.VisualBaseScale` /
   `Enemy.VisualBaseColor` (snapshotted in `_Ready` before the spawn animation runs) or to
   `Player._visualBaseScale`. Ignoring this is what inflated every entity to its texture's full
   resolution and left the boss permanently white after its first dash.

Replacing a placeholder with real art is therefore overwriting one `.svg` — as long as it stays white
and the scene keeps `scale` as its size knob.

## Where colour lives (two places, on purpose)

| Source | Covers | File |
|---|---|---|
| `Palette.cs` | everything drawn procedurally — rings, beams, blasts, floating labels, obstacles, arena edge, health bars | [Scripts/Util/Palette.cs](../Scripts/Util/Palette.cs) |
| `.tscn` `color =` / `modulate =` | visuals authored as scene nodes — projectile bodies, UI dims, and the ship/enemy bodies (`modulate`, since those are white sprites) | the individual scene files |

They're **not** unified, and that's deliberate: `Enemy` reads its own `Visual.Modulate` back in
`_Ready` as `_visualBaseColor`, the restore target for the [hit flash](enemies.md#hit-reaction) and
for the boss/stalker dash telegraphs. Moving enemy colours into `Palette` would give the same value
two owners and let them silently drift apart. The two sides are kept in step by hand.

The player is the one entity whose colour is *not* authored in its scene: `Player._Ready` overwrites
`Visual.Modulate` with the selected character's `CharacterInfo.Color` (see
[CharacterCatalog.cs](../Scripts/Player/CharacterCatalog.cs)), so the value in `Player.tscn` is only
the editor-time preview.

### The palette

Pink/magenta primary, cyan for the player, violet/purple for variety, deep purple-black backdrops.

| Role | Colour | Notes |
|---|---|---|
| Player ship | `#7dfdfe` electric cyan | The one cold colour. The player must stay findable in a pink crowd — a pink ship wouldn't be. |
| Player bullets | `#ff4fd8` hot pink | |
| Crit bullets | `#ffe066` gold | Deliberately *outside* the pink family so a crit reads as different. |
| Orbit blades | `#f9c2ff` pink lilac | |
| Laser | `#ff2fb9` magenta | |
| Missile / blast | `#ff8ae2` / `#ff6ec7` | |
| Grunt | `#ff2e88` hot pink | |
| Rare | `#b14aed` violet | |
| Tank | `#ff5c8a` coral pink | |
| Speedy | `#ff71ce` bright pink | |
| Splitter | `#c77dff` lavender | |
| Shooter | `#e040fb` purple magenta | |
| Hidden | `#fff0f7` near-white | Reads as the rare/valuable one. |
| Boss | `#a60057` deep crimson magenta | |
| Enemy bullets | `#ff3860` red pink | |
| Obstacles | `#1a0f2b` fill, `#ff4fd8` outline | |
| Burn tint | `#ff8a5c` | Applied via `Modulate`, see [enemies.md](enemies.md#burn-incendiario). |

Enemy hues stay **varied within the neon family** rather than all going pink. Telling a Tank from a
Speedy at a glance is gameplay information, and shape alone isn't enough to carry it.

**Reward tier colours are exempt** ([RewardTierRoller.cs](../Scripts/Upgrades/RewardTierRoller.cs)).
They were brightened into the neon range but keep four clearly distinct hues (mint / sky / violet /
gold), because tier is information the player reads while deciding what to buy — four shades of pink
would cost more in legibility than it gains in cohesion.

## The dark backdrop

Neon needs something dark to sit on. With `background_mode = Canvas` the `Environment` draws no
background of its own, so the arena originally fell through to Godot's **default clear colour —
a mid-grey**, which washed the whole palette out. Fixed in two places, deliberately redundant:

- `project.godot` → `rendering/environment/defaults/default_clear_color` = `#0b0614`, so every scene
  (including the menus) shares the backdrop.
- `Arena.tscn` → a `Backdrop` `ColorRect` on a `CanvasLayer` at **layer −10**. Being on a CanvasLayer
  it's screen-anchored rather than world-anchored, so it covers the viewport no matter where the
  camera has panned. This guarantees the dark field even if the clear colour is ever overridden.

Contrast values tuned against that backdrop:

| Element | Value | Why |
|---|---|---|
| Obstacle fill | `#2a1745` | Several steps lighter than `#0b0614` — at the old `#1a0f2b` blocks read as holes in the floor rather than solid cover. |
| Arena boundary | `#ff4fd8` @ 0.7α | Raised from 0.45 — it's the edge of the playfield and needs to be unmistakable. |
| Fire-range ring | `#ff4fd8` @ 0.3α | Raised from 0.16, which was nearly invisible. |
| Grid | `#6b3fa0` @ 0.16α | Faint enough never to compete with enemies. |

### The grid

`ArenaBounds` also draws a 200px grid across the playfield. The arena is several screens wide and
the camera follows the player, so on an otherwise empty dark field there's nothing for the eye to
measure motion against — you can be moving fast and not feel it. It's ~26 `Line2D`s created once.

## Glow (the part that makes it "neon")

`Arena.tscn` holds a `WorldEnvironment` whose `Environment` enables glow. Without it the palette is
just pink; with it, bright pixels bleed light and the whole thing reads as neon signage.

Two settings matter and are easy to get wrong:

- **`background_mode = 3` (Canvas).** Glow in a 2D game requires the canvas background mode. The
  default (`Clear Color`) produces no bloom at all.
- **`glow_hdr_threshold = 0.85`.** Only pixels brighter than this bloom, which is why `Palette.cs`
  keeps most channels at or near `1.0`. Dim colours simply won't glow.

Glow is supported by Godot's **Mobile** renderer, which this project uses
(`renderer/rendering_method = "mobile"`) — but it is not free. If framerate suffers on device, the
cheapest wins are lowering `glow_strength`/`glow_bloom`, or disabling `adjustment_enabled`.

## Animation

All of it is `Tween`-driven; there are no `AnimationPlayer`s.

| Effect | Where | Notes |
|---|---|---|
| Enemy spawn pop-in | `Enemy.PlaySpawnAnimation` | Scale 0→1 with a `Back` ease plus a small unwind rotation, so enemies arrive rather than blink in. |
| Enemy hit flash | `Enemy.PlayHitFeedback` | White flash + scale punch. See [enemies.md](enemies.md#hit-reaction). |
| Player idle breathe | `Player.StartIdleAnimations` | Looping ±6% scale, so the ship is never perfectly static. |
| Range-ring drift | `Player.StartIdleAnimations` | 24s full rotation — a static circle reads as UI, a drifting one as a field. |
| Level-up nova / ultimates | `Player` | Expanding, fading rings. |
| Missile blast | `Missile.SpawnBlastVisual` | Parented to the Bullets container, since the missile frees itself the same frame. |
| Score / damage popups | `Enemy` | Drift up and fade. |
| Boss banner | `BossBanner` | Hold then fade; hazard chevrons slide in alongside it. |
| Danger alarm pulse | `DangerOverlay` | Looping two-leg `Modulate` ping-pong. See below. |
| Arena tone shift | `DangerDirector` | 1.5s colour lerps on round change. See below. |
| Boss arrival shake | `CameraRig.Shake` | Decaying random `Offset`, not a Tween — see below. |

**Animations that touch a `Visual` child never touch the parent node's own `Scale`.** On enemies that
property carries the Splitter's per-generation shrink (`SplitVisualScale`); on the player it would
fight the arena clamp. Every scale tween in the project targets the child for that reason.

## Reward card flare

Offers in the shop and the level-up picker flash in **their own tier's colour** at three moments, all driven from [`RewardCard.cs`](../Scripts/UI/RewardCard.cs) via a `ColorRect` overlay child whose `Modulate` alpha is animated (the rect's own `Color` carries the tier hue). See [rewards.md](rewards.md#the-reward-card-rewardcard) for the card's information design.

| Moment | Shape | Why |
|---|---|---|
| **Appear** | alpha 0 → 0.5 → 0, plus a `Back/Out` scale pop from 0.94, staggered `i × 0.08s` | The first thing you see on open is how good each option is, before reading a word. The stagger makes the three cascade instead of strobing on one frame. |
| **Touch / hover** | alpha 0 → 0.26 → 0 over 0.3s | Acknowledges the input. Fires on `MouseEntered` *and* on press, because there is no hover on touch. |
| **Confirm** | alpha 0 → 0.75 → 0 plus a 1.04× scale, `ConfirmFlareDuration` = 0.3s | The payoff for choosing. |

Two implementation notes that are easy to get wrong:

- **The pick/purchase applies immediately; only the modal's *exit* waits for the flare.** The shop deducts coins and applies the upgrade before flaring, then defers `CheckAutoAdvance`'s close by `ConfirmFlareDuration`. The picker is the reverse order for a specific reason: `ApplyUpgradeAndResume` **unpauses the tree**, so applying it first would resume gameplay underneath a modal still animating. Either way no gameplay state waits on an animation.
- **Scale tweens need a centred `PivotOffset`**, or a card grows out of its top-left corner. `Size` isn't known until the parent container lays the card out, so the card tracks it via the `Resized` signal rather than setting it once in `_Ready`.

Both modals set `ProcessMode.Always`, which is what lets these tweens run at all — the tree is paused the whole time a modal is up.

## Danger escalation

Everything the game does to communicate "this is getting dangerous" derives from one number:
`DangerLevel.ForRound(round)`, a `RoundCurve(-0.1875, 0.0625, 0, 1)` that reads **0 through round 4**
(the onboarding rounds stay visually clean), takes its first bite at round 5, and pins to **1.0 from
round 20**. [`DangerLevel`](../Scripts/Util/DangerLevel.cs) is a stateless static class in the same mould
as `DifficultyBalancer` — it owns every colour, threshold and duration below so no two consumers can
drift apart, and it knows nothing about nodes.

[`DangerDirector`](../Scripts/DangerDirector.cs) is the **only** subscriber to `GameManager.RoundChanged`
for this feature, and resolves its targets by group (`backdrop`, `arena_bounds`, `obstacles`,
`danger_overlay`, `camera_rig`) the way every other cross-tree lookup in the project works. One
subscriber means one place that has to get the `_ExitTree` unsubscribe right — see the note in
`HUD._ExitTree` for why that matters with a persistent autoload and scene-scoped nodes.

| Round | Danger | What's on screen |
|---|---|---|
| 1–4 | 0.00 | Baseline. Nothing added. |
| 5–8 | 0.06–0.25 | Backdrop, arena wall, grid and obstacles begin warming toward red. |
| 9–14 | 0.31–0.63 | Alarm bars appear at the screen edges, pulsing ~0.67–0.83 Hz. |
| 15–19 | 0.69–0.94 | Tint near maximum; alarm pulses faster and brighter. |
| 20+ | 1.00 | Max tint, alarm at ~1.19 Hz, and the backdrop breathes on a 2.5s-per-leg loop. |

### The 0.85 glow threshold cuts both ways here

This is the first feature in the project deliberately tuned **against** `glow_hdr_threshold` in *both*
directions, and it's the constraint that shaped the whole design:

- **The backdrop must never bloom.** A glowing floor washes out everything drawn on it, so both ends of
  its lerp (`#0b0614` → `#1c0713`) sit an order of magnitude below the threshold.
- **The alarm bars must bloom** — that glow *is* their soft falloff, which is why they're a single flat
  `ColorRect` per edge instead of a stack of translucent rects faking a gradient (which would band
  visibly against a flat dark field, and never bloom anyway).

That second point forced a non-obvious choice: **the pulse animates `Modulate` as a `Color`, not as
alpha.** The threshold tests the *composited* pixel, so a bar at 0.30α over the dark backdrop composites
to roughly 0.31 luminance and would never cross it at any point in its cycle. The bars are therefore
drawn at full alpha, and the pulse scales their brightness via a neutral grey `Modulate` between a dim
trough and a bright peak. The peak blooms, the trough doesn't, and that contrast is the effect.

### Readability and safety bounds

- **The tint's risk isn't the cyan player** (`#7dfdfe` survives any dark red) — it's `EnemyBullet
  #ff3860` and `Warning #ff2e88` against a crimson floor. The danger backdrop keeps a violet undertone
  rather than going to pure red specifically so there's blue left in the floor to separate them.
- **`BoundsModulate` caps at `(1, 0.72, 0.80)`.** Push the green/blue channels lower and the arena
  wall's own `#ff4fd8` lands on `EnemyBullet`'s hue, at which point the boundary starts reading as a
  threat instead of as scenery. Its `A` is always left at 1.0 — `Modulate` multiplies, and the wall and
  grid already carry their own 0.7 and 0.16 alphas that must not be scaled. Tinting `ArenaBounds` covers
  the grid for free, since the grid lines are its children.
- **Pulse rate is bounded at 0.56–1.19 Hz with a hard 0.40s floor per leg**, well inside WCAG 2.3.1's
  three-flashes-per-second limit even allowing for bloom enlarging the perceived flashing area. The bars
  cover ~1.7% of the screen.
- **`DangerLevel.Reduced`** holds the alarm at its trough and halves camera shake. Nothing sets it yet —
  there's no settings menu to hang a toggle off — but every consumer already honours it, so wiring one
  later is a single line rather than a refactor.

### Layering and the pause interaction

`DangerOverlay` lives on its own `CanvasLayer` at **`layer = 2`**: above every world `Node2D` (layer 0),
below the HUD (layer 5). Remember `z_index` does nothing across `CanvasLayer`s — only `layer` orders them.

Its tweens **freeze when the tree pauses, and that's correct.** Every modal sits at layer 8 or above, so
a bar frozen mid-pulse is completely hidden behind whatever is open. Deliberately *not*
`ProcessMode.Always`: an alarm that kept strobing behind a shop screen the player is reading would look
worse and be a photosensitivity liability.

Two Godot sharp edges the implementation has to respect, both of which bit during development:

- **Tweens are not auto-retired.** Godot leaves a prior tween on the same property running, so every
  rebuild `Kill()`s the stored reference first. Without it, each round stacks another pulse on the same
  `Modulate` and the alarm visibly jitters by the mid teens.
- **`SetLoops()` replays the *whole* tween.** The max-danger heartbeat is therefore its own two-leg loop
  rather than something chained onto the entry lerp — chaining it would replay that lerp every cycle and
  stall each beat with a dead 1.5s hold.

`DangerDirector` also applies the current level **synchronously in `_Ready`**, not just on the next
`RoundChanged`: after a mid-run `ReloadCurrentScene()` the round number is already high, and without it
the arena would come back wearing round-1 colours until the following round ticked over.

### Boss arrival

Three things land together when a Boss spawns, all on top of the existing banner:

1. **A pre-arrival alarm burst** during the 3s countdown (`DangerOverlay.SetBossAlert`), forcing the bars
   to full intensity regardless of round. This is also the only reason the round-5 Boss gets an alarm at
   all — normal escalation hasn't reached `AlarmThreshold` that early.
2. **Hazard chevrons** on the banner's **top edge only**: in portrait, a bottom row would land squarely on
   the virtual joystick's touch area. They're children of `BossBanner`, so its existing 2s-hold + 0.6s
   fade covers them for free.
3. **Camera shake** — `CameraRig.Shake(14, 0.45s)`, which perturbs **`Offset`**, not `GlobalPosition`
   (the follow overwrites that outright every physics frame), and which reads crisp precisely because
   `Offset` is applied *after* Godot's position smoothing. It zeroes `Offset` on completion rather than
   leaving it wherever the last frame put it: a leftover offset would survive the player's death and
   leave the camera permanently mis-framed for the rest of the session.

One non-obvious gotcha for anything new added here: `ColorRect` defaults to `MouseFilter = Stop`. These
are full-width and full-height rects sitting exactly where the virtual joystick's touch area is, so every
one of them sets `Ignore` explicitly — the default would silently eat movement input on mobile.

## Screen orientation (Horizontal / Vertical)

Chosen from the main menu (`MainMenu.tscn`'s `OrientationRow`, two mutually-exclusive toggle
`Button`s sharing a `ButtonGroup`), persisted to `user://orientation.save`, and applied by
`GameManager.SetOrientation`/`ApplyOrientation` — takes effect immediately, no restart needed.
**Portrait is the default** (`GameManager.CurrentOrientation`'s field initializer and
`LoadOrientationPreference`'s fallback both default to Portrait — only an explicit saved
"Landscape" switches it), matching `project.godot`'s pre-boot `window/handheld/orientation`.

The two buttons use the same styled-`StyleBoxFlat` pattern as the HUD's own buttons rather than
the bare default theme, sized up to 140×48 with `font_size = 17` so they read clearly as buttons
rather than plain text — and critically, the *selected* one uses a distinct bright magenta-filled
style (`StyleBoxFlat_orientation_selected`, applied via `theme_override_styles/pressed`) instead
of the same subdued outline as the unselected one, so which orientation is currently active is
obvious at a glance.

- `project.godot` sets an explicit base viewport of **1152×648** (was previously Godot's implicit
  default of the same size, now spelled out since code needs an exact reference to swap against)
  with `window/stretch/mode="canvas_items"` + `aspect="expand"`. That combination scales UI, and
  reveals extra world through the camera, relative to whichever size is currently set as the root
  `Window`'s `ContentScaleSize` — landscape uses the base 1152×648, portrait swaps to 648×1152.
  Swapping this reference size to match the chosen orientation, rather than leaving it fixed at
  the landscape values, is what keeps both UI scale and how much of the arena is visible
  consistent between the two orientations instead of "expand" distorting whichever one doesn't
  match the base aspect.
- `DisplayServer.ScreenSetOrientation` is also called (Landscape/Portrait, not a Sensor variant —
  the player picks one explicitly, the device doesn't auto-rotate under them mid-run). Has no
  visible effect on desktop; it's what actually rotates the physical screen on Android.
- No bespoke portrait layout exists for any menu. Every screen (`HUD`, `Shop`, `PauseMenu`,
  `UpgradePicker`, `RoundSummary`, `GameOverScreen`, `MainMenu`) is anchored by corner/edge/center
  rather than absolute position, so swapping the base size reflows all of them into a narrower,
  taller canvas without needing a second authored version of each scene. The widest fixed-pixel
  panel (Shop, 480px) still leaves comfortable margin against the 648px portrait width.
- `project.godot`'s `window/handheld/orientation="portrait"` is only the pre-boot/manifest
  default for the very first launch before `GameManager._Ready` applies whatever was actually
  saved — a returning player who picked Horizontal last time won't see a portrait flash beyond
  that first frame.
- `VirtualJoystick` is the one control that *does* get a bespoke per-orientation position, since
  "anchored by corner" isn't enough on its own to answer "which corner": its `.tscn` anchors are
  the Landscape layout (bottom-left, shifted right off the screen edge rather than flush against
  it — the corner itself is an awkward thumb reach), and `ApplyOrientationLayout()` overrides
  those anchors/offsets to bottom-center at `_Ready` when Portrait is active, where a one-handed
  grip naturally sits in the middle. Read once at startup, not live-updated, since this scene only
  exists during a run and orientation can't change without returning to the main menu first. Base
  pad and knob were also bumped ~1.5× (140px box → 210px, `MaxRadius` 60 → 90) for an easier touch
  target.
- `HUD`'s Ultimate button/bar are bottom-**right**-anchored (`anchors_preset = 3`), not
  bottom-center — mirroring the joystick's bottom-left placement so the two touch controls sit on
  opposite thumbs instead of competing for the same hand. Positioned with a taller bottom margin
  (`offset_bottom = -90`) than the joystick's (`-40`) so it sits visibly *above* the joystick's
  vertical band rather than level with it, and sized up to 150×70 for a comfortable tap target.

## Modal interstitials: telling Shop / Reward / Ultimate apart

Three different screens (`Shop`, `UpgradePicker` in its two modes) used to share the exact same
undecorated panel look, which made it easy to tap "Comprar" thinking it was a free level-up pick,
or vice versa. Each now gets its own border colour, applied via a `StyleBoxFlat` on the panel's
`theme_override_styles/panel` (and matching the title label's font colour):

| Screen | Border colour | Why |
|---|---|---|
| `UpgradePicker` (level-up) | `Palette.RewardPanelBorder` (cyan, = `Accent`) | Free pick — ties to the HUD/player's own colour, reads as "regular game screen". |
| `UpgradePicker` (post-boss Ultimate choice) | `Palette.UltimatePanelBorder` (gold) | A rare, one-off pick — deliberately outside the pink/cyan families so it stands out as a special moment. |
| `Shop` | `Palette.ShopPanelBorder` (magenta) | Spending coins — the game's core accent colour, distinct from both pickers above. |

`UpgradePicker.Open(choices, title, isUltimate)` picks between the reward/Ultimate style; `Shop`
only ever needs its own. Both panels also switched from a fixed-offset rect to a `CenterContainer`
wrapping a `PanelContainer` with only a fixed *width* (`custom_minimum_size.X`) — the previous
fixed-height rect didn't grow with content, so a longer reward description or a third stacked item
would silently overflow past the card's visible edge instead of the card growing to fit it.

**Whole-card tap targets**: each Option/Item card stays a `PanelContainer` — an earlier attempt
made the outer card node itself a `Button`, which broke completely, since `Button` isn't a
`Container` and doesn't lay out or size arbitrary children the way `PanelContainer` does; every
card's Label+inner-button just overlapped at (0,0) instead of stacking in the `VBoxContainer`.
Instead, the card's `Label`/`HBoxContainer`/`VBoxContainer` children are `mouse_filter = Ignore`,
and the `PanelContainer` itself listens on `GuiInput` — a tap anywhere on the card (description
text, empty margin) reaches the panel's `GuiInput` and fires the same handler as the inner
"Elegir"/"Comprar" button, while a tap landing precisely on that inner button is still claimed by
its own default `mouse_filter = Stop` first and never double-fires. `Shop.OnBuyPressed` already
re-validates resolved/useless/afford on every call, so it needs no extra guard; `UpgradePicker`'s
`OnChoicePressed` doesn't re-validate, so `OnOptionGuiInput` checks a parallel `_useless[]` array
before forwarding the tap.

**`RoundSummary` no longer competes for the same screen real-estate as those modals.** Two
separate bugs compounded here:

1. It used to sit vertically centered and offset left — positioned to just clear a landscape-only
   Shop, which broke the moment Portrait (a much narrower canvas) became the default and the two
   boxes started overlapping outright, text interleaving with text. It's now pinned below the
   HUD's own top-left stats panel instead — repositioned every frame off `TopBarPanel`'s live
   size, same as `RoundTimerLabel` — so it starts out of the vertically-centered band any
   interstitial modal appears in.
2. Even after that, tall content (5+ stat lines, or a Shop with long wrapped reward descriptions)
   could still make the two boxes' *heights* meet in the middle. Setting a `z_index` on
   `RoundSummary`'s Control did **nothing** for this, because `RoundSummary`/`Shop`/`UpgradePicker`
   each live on their own `CanvasLayer` (`RoundSummaryLayer`/`ShopLayer`/`UpgradeLayer` in
   `Arena.tscn`) — `z_index` only orders siblings *within* the same layer; cross-layer stacking is
   decided entirely by each `CanvasLayer`'s own `layer` number. `RoundSummaryLayer` was `layer =
   17`, **above** `ShopLayer` (15) and `UpgradeLayer` (10) — the opposite of what was intended, so
   it drew fully opaque on top of whichever modal was open. It's now `layer = 7` (below every
   modal layer, above the base HUD/BossBanner layers), so a geometric overlap now means it renders
   *behind* the modal's own `Dim` and gets genuinely dimmed instead of interleaving with it.

## Round timer

Moved out of the cramped top-left stat list (`font_size = 15`, easy to miss) into its own big
`Label` (`font_size = 32`) pinned just **below** the stats panel, left-aligned with it —
repositioned every frame off `TopBarPanel`'s live position/size in `HUD._Process` (same trick
`CooldownIcons` already uses off the panel's right edge), since the panel's own height breathes
too (e.g. the Shield row only appears once you own a Barrier). Arguably the most time-pressured
number on screen deserved more than the smallest text in the corner. On the last
`TimerPulseThreshold` (5) seconds, it turns `Palette.Warning` red and does a scale-punch
(`PulseRoundTimer`, 1.5× → 1.0 with a `Back` ease) once per second — tracked via
`_lastPulsedSecond` so it fires exactly once per second boundary rather than every frame the
countdown happens to be under 5.
