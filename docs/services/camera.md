# Camera

**Scripts:** `Game/Core/ArenaCamera.cs`, `ArenaFollowCamera.cs`, `MultiplayerCamera.cs`,
`WorldUIOverlayCamera.cs`
**Prefab:** `Assets/Prefab/Manager/Main Camera.prefab`

There are three camera behaviours, and which one a scene uses is a **scene override on the Main
Camera prefab instance** — the prefab itself still carries `MultiplayerCamera`.

None of the three ever writes rotation. The authored angle is the shot, and two other systems read
it as a fixed basis: `PlayerMovement` builds its movement axes from the camera's forward/right, and
the world-space UI (gate manifest board, station progress bars) is authored to face that same
angle. Re-aiming a level's camera means re-aiming its world UI and turning the controls with it.

## ArenaCamera — the gameplay camera (redesign)
`ArenaCamera` is the Overcooked-style shot: the whole playfield is in frame at all times, and the
camera never follows anyone and never zooms. It reads the pose authored in the scene on `Awake`
and only adds a slow drift on top — two sine axes on deliberately unequal periods, plus a tiny
roll — so a held frame doesn't read as a frozen image.

Drift is meant to be *below* conscious notice: at the default 0.35 units and ~34 units of viewing
distance it moves the picture about 1.5% of screen width. If you can see it as camera movement it
is too strong. Zero either amplitude to switch that half off.

Because the shot is fixed, every spawn point, the belt and the gate must all sit inside the
frustum — moving any of them means re-checking the framing.

**In `Level1` and `Level2` it is present but disabled** — it is the other half of the camera A/B
described below. Its authored pose is untouched, so re-enabling it restores the shot exactly.

## ArenaFollowCamera — the Moving Out-style shot (Level1, Level2)
`ArenaFollowCamera` tracks the players' midpoint and dollies back along its own view axis as they
spread. Enabled in `Level1` and `Level2`; `ArenaCamera` sits beside it disabled. **Never enable
both** — they each write `transform.position` in `LateUpdate` and will fight.

It is a separate component rather than a retune of `MultiplayerCamera` on purpose: that script is
still live in `ChooseStage` (and in the archived scenes), and its `(offset, 0, offset)` pull-back
is only a view-axis dolly at the 45° yaw those scenes use.
At the redesign's 0° yaw it is a pure sideways slide that shoves the players toward the frame edge.

Three things it does that the older rig does not:

- **Fits the real frustum.** `framingMargin` is a world-unit gap around the outermost player,
  solved against fov and aspect, so the shot survives a change of screen. There is no
  `zoomLimiter` magic number, and `bounds.size.magnitude` — which mixed X, Z *and* Y, so standing
  on the raised gate platform zoomed the camera out — is gone. The pivot is flattened to one
  height for the same reason.
- **Damps zoom asymmetrically.** Pulling back is quick (`zoomOutSmoothTime`), coming back in is
  slow (`zoomInSmoothTime`). The level is a shuttle between the belt and the gate, so an honest
  zoom breathes in and out every few seconds — right in the band that makes people queasy. With
  the shipped values the zoom moves about 1.1× in normal two-player play.
- **Frames on the first tick.** The round opens on a three-second countdown at `timeScale` 0, and
  it damps on `unscaledDeltaTime`, so the shot is correct *during* the countdown instead of
  swooping into place on "GO".

`minDistance` is doing real work: the arena is sparse and the billboard props are tall, so a
closer shot fills the frame with empty floor and lets props cut across it. `maxDistance` is sized
to hold four players across the whole belt-to-gate run; past that a straggler goes off-screen
rather than shrinking the level to a postage stamp. **The floor is not fenced** (a player can walk
~87 × 58 units), so a wanderer will eventually leave the frame — the fix for that is level
colliders, not camera code.

**`ScreenShift` (stage select only).** A runtime property, 0 by default, that slides the shot
sideways so the group sits right of centre, as a share of half the frame width at the group's
depth; the pan's smoothing eases it and the zoom ignores it. Stage select sets 0.5 while the
boarding pass covers the left half of the screen ([stage select](../scenes/stage-select.md#the-boarding-pass)).
The levels never set it.

**`extraTargets` and the Clone marker.** Anything in `extraTargets` is framed exactly as if it
were a joined player. `Level1` and `Level2` each carry a `Clone` object at spawn point 2 — an
empty transform with a collider-less capsule child sized like a character — wired into that array,
so the two-player framing can be judged with one controller. Deactivating `Clone` returns the solo
shot, which makes it a one-click A/B. **It is a development marker and renders in game; deactivate
or delete it before a build.**

## MultiplayerCamera — stage select, and the not-yet-redesigned levels
`MultiplayerCamera` discovers `PlayerInput` objects, sorts them by index, drops destroyed
references, and frames the active group:

- one player → centre on them;
- several → centre on their world bounds;
- height interpolates between configured Y limits using group spread;
- the isometric offset interpolates between min and max diagonal offsets as height changes;
- motion is `SmoothDamp` toward the target pose.

Players persist between scenes, so the camera resolves its set per stage rather than storing
prefab references.

`MapController` in stage select drives it through `SetTargets`, framing the map planes instead of
players. **That is why the script still exists** — no gameplay level uses it any more, but stage
select does.

`WorldUIOverlayCamera` self-installs on `Camera.main`. It removes the `WorldUI` layer from the
base camera, creates a child URP overlay camera that renders only `WorldUI`, and keeps projection
synchronised. `PlayerIndicator` uses this so its world-space pin stays readable over level
geometry. If layer 10 is removed or renamed, the system warns and world UI can be occluded.

Per-level framing tunables may legitimately differ when a level's footprint does. Never drag
player transforms into the camera prefab.
