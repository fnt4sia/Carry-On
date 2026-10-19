# Game HUD

**Prefab:** `Assets/Prefab/UI/GameHUD.prefab`, nested inside `GameManager.prefab` · **Script:** `Game/UI/GameHUD.cs`

`GameHUD` is presentation only. Every panel, text, image, and button reference is wired once
inside the prefab; it subscribes to the scene `GameManager` and stores no level rules.

| `GameManager` event | HUD response |
|---|---|
| `ScoreChanged` | update score text |
| `TimeChanged` | update `m:ss` timer |
| `CountdownChanged` | dim and show 3–2–1 |
| `RoundStarted` | reveal and animate timer and score |
| `PauseChanged` | show pause panel, select Resume |
| `RoundEnded(GameResult)` | play the result sequence |

The result sequence runs on unscaled time, because the round ends with `Time.timeScale = 0`.
See [the stage end card](#the-stage-end-card) below.

## Timer and score plaques
Both in-round readouts are the same widget built twice, from `Assets/UI/Gameplay/`:

```text
Timer  (bottom-left, anchoredPosition 100,40)     Score  (bottom-right, −40,40)
├── Image  MoneyBar, 228 × 110                    ├── Image  MoneyBar, 228 × 110
├── Icon   TimeIcon, 118.7 × 124                  ├── Icon   MoneyIcon, 117.6 × 124
│   └── Fill   red radial, 56 × 56                └── Text   score, 150 × 60 at x +30
└── Text Timer  m:ss, 150 × 60 at x +30
```

The icon is anchored to the bar's **left edge with pivot `0.5`**, so half of it hangs outside the
plaque — that overlap is the look, and it is why the icon is 124 tall against a 110 bar. The text
sits at `x +30` to clear the icon's inner half; at the plaque's 228 width a 150-wide text box is
the widest that still fits inside the bar.

> **The timer's x is 100, not 40.** Its icon hangs 59 px left of the container, so at the score
> plaque's mirrored inset of 40 the stopwatch would run off the bottom-left corner of the screen.
> 100 puts the icon's left edge at ~41, which is what actually mirrors the score side.

`GameHUD.gameTimerFill` drives `Timer/Icon/Fill` from `OnTimeChanged`, as
`timeRemaining / LevelConfig.gameTime`. It is a plain drain in one authored colour with **no
warning/danger thresholds**, using the built-in `Knob` sprite, `Filled` / `Radial360`, origin Top,
counter-clockwise.

Buttons call the `SceneLoader` API. `Next` (labelled CONTINUE) always returns to stage select
(18 Oct 2026, Fitra's call), where the map shows what the round unlocked. `nextLevel` still
drives that unlock and the map's routes, but no button loads it.

## The stage end card
**Art:** `Assets/UI/StageEnd/`. Authored under `GameHUD/Stage End`.

The card replaced the old `Visa` panel in September 2026. `Visa` and its delivery-count rows are
gone, and `Assets/UI/OldAsset/Visa.png` with them; the card reads **score**, not deliveries.

```text
Stage End                       1000 × 880 at (0, 20)      the object that slides in
├── Level Completed             TMP, top-left of the root
├── Window                      PopupWindow, 1000 × 708 at (0, 42)
│   ├── Departure Icon / Departure Title / Flight Label / Flight Code
│   ├── Level Image             LevelImageFrame, 434 × 328 at (−214, 44)
│   │   └── Preview             LevelConfig.previewImage; blank white when unset
│   ├── Stage Tag               LevelInfoBar, rotated −8°, holds displayName
│   ├── Score Label + Stars     GridLayoutGroup, 3 × (110 × 150)
│   │   └── Star N/Empty/Filled gold star, switched on per earned tier
│   ├── Score Row / Time Row    TimeScoreBar, 434 × 54, each with a CanvasGroup
│   └── Players                 GridLayoutGroup, 2 cols, cell 434 × 70, spacing 24/17
│       └── Player Row 1–4      PlayerScoreBar + CanvasGroup
├── Approved                    stamp, rotated −12°, over the card's bottom-right corner
└── Next                        button under the card, "CONTINUE", calls ContinueToStageSelect
```

`Next` reuses `TimeScoreBar` so it reads as part of the card rather than as the old blue
`Button.png`. Because the sprite is 844 × 110 and the button is 300 × 72, the Image is **Sliced**
— `TimeScoreBar.png` carries a 30 px border for exactly this, which is a shade over its 26 px
corner radius. The score and time rows stay `Simple`; they scale close enough to uniform that
their corners never distorted. The bar is dark navy, so a ColorTint highlight can only ever
darken it: idle sits at 0.72 grey and highlighted/selected return to full white. The label is the
card's gold `(0.949, 0.761, 0.290)`.

Level-derived text (`displayName`, `flightCode`, `previewImage`, round length, star thresholds)
is filled in `SyncCurrentState` at `Start`. Only the score and the player rows are filled from
`GameResult` when the round ends.

The sequence: Time's Up scale-in → card slides down from `+Screen.height` → Score Row → Time Row
→ one player row at a time → earned stars one at a time with `Sfx.Star` → `Sfx.Stamp` and the
Approved stamp if at least one star → Next, selected for gamepad.

**Rows fade, they don't pop.** `FillPlayerRows` switches on one row per joined player *before*
the reveal starts and leaves each row's `CanvasGroup` at alpha 0. If reveal used `SetActive`
instead, the `GridLayoutGroup` would re-centre the block on every row and the whole list would
jump. Player count comes from `PlayerInput.all` (clamped to 1–4), and the name is the character
prefab's own name with `(Clone)` stripped — Annie, Bun Jovi, Scannor.

Star tiers are **three**, matching `LevelConfig.CalculateStars`. Each `Star N` is an
`EmptyStar(DarkVer)` with an inactive `StarCompleted` child; earning the tier switches the child
on, so the swap stays a scene decision rather than two sprite fields on the script.

The card is a fixed size, so a one-player round leaves the lower half of the window empty. That
is deliberate — the window does not resize to the player count.

To redesign the HUD, edit or variant `GameHUD.prefab`. Don't push its child references back onto
`GameManager` or wire each level scene separately.
