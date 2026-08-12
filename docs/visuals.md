# Visuals: Palette, Glow & Animation

The game has no art assets — every visual is a `Polygon2D`, `Line2D`, or `Label` built either in a
`.tscn` or procedurally in code. The look comes entirely from colour, glow, and tweens.

## Where colour lives (two places, on purpose)

| Source | Covers | File |
|---|---|---|
| `Palette.cs` | everything drawn procedurally — rings, beams, blasts, floating labels, obstacles, arena edge, health bars | [Scripts/Util/Palette.cs](../Scripts/Util/Palette.cs) |
| `.tscn` `color =` | visuals authored as scene nodes — ship, enemy bodies, projectile bodies, UI dims | the individual scene files |

They're **not** unified, and that's deliberate: `Enemy` reads its own `Visual.Color` back in `_Ready`
as `_visualBaseColor`, the restore target for the [hit flash](enemies.md#hit-reaction). Moving enemy
colours into `Palette` would give the same value two owners and let them silently drift apart. The
two sides are kept in step by hand.

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
| Boss banner | `BossBanner` | Hold then fade. |

**Animations that touch a `Visual` child never touch the parent node's own `Scale`.** On enemies that
property carries the Splitter's per-generation shrink (`SplitVisualScale`); on the player it would
fight the arena clamp. Every scale tween in the project targets the child for that reason.

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
