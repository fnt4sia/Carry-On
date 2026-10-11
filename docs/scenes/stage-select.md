# Stage select

**Scene:** `Assets/Scenes/Menu/ChooseStage.unity` · **Scripts:** `ChooseStage/{MapController, MapPlane, LevelNode,
MapRoute, LevelTicket, MapStarCount}.cs`

How the map plays: planes, landing rings, boarding, the ticket, the camera, node badges, routes and
the star total. The map's art — terrain, water, palette, island shallows, scene roots — is in
[stage-select map](stage-select-map.md).

An Overcooked-style island map (Carstenz's 3D archipelago, which replaced the old flat map on
30 Sep 2026). `MapController` spawns one `MapPlane` per joined player — up to four — and each
player flies their own. Level nodes sit on the airport islands. There is **no Confirm press**:
boarding together is the confirmation.

## Landing rings

Every node's **landing ring** on the sea shows where that stage stands (Fitra's spec, 5 Oct 2026):

| Stage | Ring | Badge | Planes |
|---|---|---|---|
| completed — finished at least once | yellow | yes | land, can board (replay) |
| current — unlocked, not yet completed | white, scaling up and down (`pulseAmount` 6 %, `pulsePeriod` 1.6 s) | yes | land, can board |
| locked — not reached yet | grey | none | fly straight over: no landing, ticket, sound or slot |

"Completed" is `ProgressionService.IsCompleted`: a `completed` flag (save version 3) set by
`RecordResult` — the same moment the next stage unlocks, stars or not. Unlocking alone creates a
save entry too, so an entry existing doesn't mean completed. Saves written before version 3 read
every level as not completed until it's finished again.

On an unlocked node:

| Planes on it | What the player sees |
|---|---|
| none | badge shows best stars |
| some | badge scrolls down from its stars to one slot per plane, in that player's colour — filled if their plane is here, an outline if not; those planes touch down on the island |
| all, node boardable | the same, while the ring fills over `boardingSeconds` (3 s) — gold on a white ring, white on a yellow one, so the fill shows on both — and the badge's lavender outline fills gold with it. The [boarding pass](#the-boarding-pass) slides in from the left |
| ring full | the group is committed: the pass is stamped APPROVED, every plane's stick and Back are ignored, and `ticketSeconds` (0.8 s) later the cloud transition starts |

If any plane leaves before the ring is full, it drains at the fill rate instead of snapping to
empty. A node is boardable when it is unlocked **and** its scene is in Build Settings; the group
arriving on an unlocked one whose scene can't load plays the wrong-action SFX, logs an error, and
the ring never fills. Back returns to the lobby.

## The boarding pass

**The pass is the group's last look before committing** (9 Oct 2026, Fitra picked this over a pass
that only appeared once the ring was full — by then the choice was already made). The moment every
plane is on one boardable stage, `LevelTicket` slides in from the left of the screen with that
stage's flight, route, duration, star thresholds and scores, and the ring starts its 3 s fill; that
fill *is* the reading time. If any plane leaves, the pass slides back out and the ring drains. When
the ring is full, `MapController.Board` locks the planes and the pass gets the APPROVED stamp (the
end card's `Approved Stamp.png` and `Sfx.Stamp`) and the status flips to `BOARDING`; `ticketSeconds`
(0.8 s) later `SceneLoader.Load` starts the [cloud transition](../services/scene-loading.md#the-cloud-transition),
which covers the pass. Measured in Play: ring full 3.0 s after the last plane lands, Level 1 active
about 2.6 s after that.

| | |
|---|---|
| Where | `HUD/Level Ticket` — a screen-space card in the scene's `HUD` overlay canvas, anchored to the left edge, pivot on its own left side, at `(40, 0)` |
| Size | the `Level Ticket` prefab's 1746 × 1277 card at scale 0.55 → **960 × 702 at 1080p: half the screen's width**. Text was set for that size — labels 34–40, values 36–76, the flight code 104 (card px), so ~20 px or more on screen |
| Slide | `slideSeconds` 0.35 s; in eases out with a ~10 % overshoot, out speeds away; it reverses from wherever it is, so hopping in and out is smooth. Hidden means pushed off the left edge |
| Stamp | `Stamp` child, hidden until boarding; its authored pose (over the score box, −12°) is where it lands; drops from 1.8× and transparent in `stampSeconds` 0.2 s |

**The camera makes room for it.** A half-screen card on the left would sit on top of the island —
the follow camera centres the group — so while the pass is up `MapController` sets
`ArenaFollowCamera.ScreenShift` to `ticketScreenShift` (0.5): the shot slides the group halfway to
the right edge, eased by the normal pan, and slides back when the pass leaves. The island, ring and
planes stay clear of the card.

**Every string fits its box.** Airport codes, city names and the status value auto-size: `CGK`
measures 147 px at 64 in a 160 px box (the art leaves −771…−611 left of its printed arrow);
`MAKASSAR` shrinks to 32 in the destination column and `BOARDING` to 34 in the 200 px status
column, leaving a gap after the `STATUS` label. Re-measure with `TMP_Text.GetPreferredValues` before
lengthening a label. Nobody has finished the stage → both scores read `-`, not `0`.

The preview frame stays blank white until a `LevelConfig.previewImage` is set — no level has one yet.

## The ring is the stage's area

**Being "on" a node** means inside its landing ring on the flat — `LevelNode.RingCenter` and
`RingRadius` are read straight off the `Landing Ring` canvas (half its width × its scale), so
**scaling or moving the ring is how you resize a stage's area**; there is no separate radius to
keep in step. Select `Landing Ring` under a `Map Node` and scale it uniformly (scale 0.065 =
26 m across; the canvas is 400 units wide). Edit the `Map Node` prefab to change every island, or
override one node in the scene for an island of another shape; the `MapController` gizmo draws
each ring's area. Node pivots are the islands' own pivots (y 5.2, on the terminal, about 4 m north
of the runway), but the airport island's footprint is centred about 3 m south of that, so the
prefab's ring sits at local `(0, -2.9, -2.96)`: sea level, on the island's centre. At 26 m the
ring's band lies just outside the shore foam — any smaller and the white ring vanishes into it.
A landed plane settles at the pivot's height plus `landingClearance` (3.6 m, half the plane
model), wherever it is inside the ring — which can put it through the terminal and control tower.

`Plane Spawn` (-34, 17.5, -64) must stay outside every ring, or the group boards Level 1 two
seconds after the map opens. It's also far enough south that the plane doesn't *look* parked on
Node 1: at 45° a plane cruising 15.2 m above the sea appears about 15 m up the screen from the
spot below it, which is the spot the rings measure. Its height is the cruise height: 17.5 m keeps
the plane's belly (3.6 m below its pivot) just over the tallest control tower (13.3 m). Planes
cruised at 24 m while the cloud sea existed; it was lowered on 5 Oct 2026 to shrink that offset.

## Camera

**Camera.** The map runs the levels' `ArenaFollowCamera` (the Moving Out rig), handed the planes
through `SetTargets`: rotation `(45, 0, 0)`, FOV 50, pulled back along its own view axis from 75 m
with the group together (about 124 m of map across the frame) to 140 m when they spread out;
margin 14. Fitra asked for 45° and a Moving Out-wide shot on 1 Oct 2026, replacing the tighter
60° / FOV 40 / 50 m first pass. Yaw stays 0: the runways run east–west and the water glints are
world-x dashes, so both stay horizontal only at yaw 0, stick-up is north, and the nodes read left to
right. At 45° the top of the frame shows the mainland terrain, and with the group far north (or
spread out to the 140 m zoom) the terrain's north edge (z 113) and the camera's flat background
colour beyond it — the cloud sea used to cover that until it was removed on 5 Oct 2026.

The rig only writes position, so it can be tuned live: in Play mode, change the Main Camera's
rotation, FOV or the rig's distances and the shot follows on the next frame. Copy the values back
after exiting Play mode. The authored pose is the rig's first frame at `Plane Spawn`. Shadows
reach 150 m on the High Fidelity URP asset; Balanced and Performant stop at 50 m, so at this zoom
they lose the plane and cloud shadows.

## Planes and input

Persisted players are deactivated while the map is open, so their own `PlayerInput` can't drive
anything. Each plane instead gets a runtime clone of the shared input asset, masked to that
player's control scheme and paired to their devices — which is what lets two halves of one
keyboard fly two planes. Back stays on the shared asset so any device can press it. The plane
takes its colour from the player's `PlayerIndicator.CurrentColor` before the player is hidden.
`MapPlane` moves with `PlayerMovement`'s feel — the same `movementLerpSpeed` (0.15) and
`rotationSpeed` (4), converted from per-physics-step to per-frame — and its bubble trail is a copy
of the characters' `WalkSmoke` particle system on a `Trail` child at the tail. Stick-up follows the
camera's flattened **up** vector, not its forward, so a camera pitched past vertical can't invert it.

**Whose plane is whose.** The `Map Plane` prefab's model is Carstenz's `plane1` (`Model/Plane`,
nested `Prefab/StageSelect/plane1`, nose +Z; `Model` at scale 0.18, 3× the FBX — it was 0.12 until
5 Oct 2026, when a blue plane on the blue sea was too hard to find). `MapPlane` paints its
`paintedParts` — the body, wings and tail fin, the renderers on the FBX's `Material.001` — in the
player's colour with a property block, and fills in `Player Pin` (4.4 m up, clear of the plane's
top): the characters' `Prefab/UI/Player Indicator` nested with its `PlayerIndicator` component
removed (that one follows a `PlayerInput`), on the WorldUI layer and turned to the camera every
frame, labelled `1P`–`4P` in the same colour. The 1P colour is almost exactly the deep sea's blue,
so a player-coloured ring on the water would vanish for 1P.

`Model/Plane` carries a white **QuickOutline** (`Outline`, OutlineAll, width 4 — screen-space, so it
reads at any zoom). It sits on `Plane`, not `Model`, so the bubble trail's particle renderer stays
out of it. QuickOutline bakes smoothed normals into the meshes at `Awake`, which is why
`plane1.fbx` imports with **Read/Write enabled** — without it every plane logs "Not allowed to
access vertices" and Error Pause stops play. It costs about 64 ms per plane at map load, two extra
draws per renderer (72 materials on the plane instead of 24), and the mesh data kept in memory.

The **propeller** (`Circle.004`, three blades and the spinner) spins at `propellerSpeed` (900°/s;
much faster and three blades strobe at 60 fps) about `Model/Propeller Hub` — a marker at the
propeller's centre, forward along the nose. The propeller's own pivot isn't on its axis, so it
can't simply rotate in place. `plane1` is heavy — about 313k triangles a plane, so four planes are
over a million triangles; the game's own `Pesawat1` / `Pesawat2` are 19k / 14k.

Opened directly in the editor with nobody joined, the map spawns one plane on the unmasked shared
asset, in `soloPlaneColor` (1P blue). To test several planes, start in `MainMenu`, join, then go to
stage select — `DebugAutoJoin` players joined inside a level aren't persisted and die on the scene
change. Virtual gamepads (`InputSystem.AddDevice<Gamepad>()`) joined as `Gamepad` cover 3P and 4P.

## Nodes and the Map Node prefab

Each `LevelNode` references one `LevelConfig` — display strings, scene name, default unlock, save
key, and next-stage relationship all come from that asset. The node asks `ProgressionService` for
unlocked state and best stars, refreshing on `ProgressChanged`, and draws what `MapController`
drives. The `Map Node` prefab owns all of it:

| Child | Role |
|---|---|
| `Badge` | world-space canvas 9 m above and 16 m behind the node, on the WorldUI layer, turned to the camera every frame. Hidden on a locked node. Layout below |
| `Landing Ring` | a flat world-space canvas on the sea, always shown; its size and position are the stage's area. `Pulse` holds `Ring` (the `LandingRing` sprite, tinted by stage state) and `Ring Fill` (the same sprite, radial-filled with boarding); `LevelNode` scales `Pulse` while the stage is current, so the fill pulses with the ring and the area never changes |
| `Route` | the dashed route from this stage to the next (below) |

## Level islands

**Level islands.** Every stage on the map is one prefab instance that carries everything — the
island art, the badge, the landing ring and the route out — so a stage is placed, moved and
re-ordered as one object (5 Oct 2026, Fitra's ask). The five templates in
`Prefab/StageSelect/Level Islands/` are **Prefab Variants of `Map Node`**, each adding the islands
of one cluster that was already on the map. The `LevelNode` sits on the variant's root, so picking
the stage is one field on the object you dragged in; badge, ring and route changes go on
`Map Node` and reach all five.

| Template | Islands (offsets from the airport island's pivot) |
|---|---|
| `Level Island - Solo` | `Island_airport` alone |
| `Level Island - Rock` | + one `Island_Rock` to the south-east |
| `Level Island - Lighthouse` | + `Island_LightHouse` to the south-west |
| `Level Island - Lighthouse Bay` | + `Island_LightHouse` to the west, two `Island_Rock` to the south |
| `Level Island - Twin Rocks` | + two `Island_Rock` to the east, `Island_LightHouse` to the north |

`Island_airport` sits at local `(0, -0.14, 0)`, rotated 180°; the root is the node pivot (y 5.2).
`Island_Rock` (`Prefab/StageSelect/`) is the `island.fbx` islet as a prefab — the scene used to
hold nine loose copies of the FBX. To add a stage, follow [the checklist in levels](levels.md#adding-a-stage) — on the map it comes
down to dragging a template under `Level Islands` and setting its `Level`; the previous stage's
`nextLevel` draws the route. To re-order, swap the `Level` on two islands; the routes follow.

## Routes

**Routes.** Each `Map Node`'s `Route` child draws one dashed route: a `LineRenderer` (Tile texture
mode, world-space, flat on the sea, 2.6 m wide) with `Material/World/Route Dash.mat` on
`Shader/RouteDash.shader`, laid out by `MapRoute`. **The route finds its own end**: it runs from
its node to whichever node holds that level's `nextLevel`, so the routes always match the real
unlock chain, and a stage with no next level (or one that isn't on the map) draws nothing — which
is why there is no 2 → 3 route while Stage 2 doesn't unlock the archived Stage 3. It runs a cubic
curve from 7.5 m off one node to 7.5 m off the next, measured from the node pivots, bent by its
own `bendStart` / `bendEnd`: the same sign curves it one way, opposite signs make an S. The bends
are per-instance overrides on each island's `Route` (Level 1: 12 / 4, Level 2: −8 / 8, Level 3:
−10 / −4, picked to miss the lighthouse islets) — re-tune them after moving an island. The shader
draws each dash from the UVs as a rounded capsule in metres with a navy outline, fades the ends,
and marches the dashes slowly toward the next stage. `MapRoute` runs in the editor too, so a route
follows an island as it's dragged (the editor only ticks it while the Scene view repaints).

## The node badge
Rebuilt on 9 Oct 2026 from Fitra's `Assets/UI/StageSelect/` art. Sizes are in canvas pixels:

```text
Badge             995 × 500 at scale 0.0147 → 14.6 × 7.4 m
  Outline         Node_Outline, always shown — the badge's border at rest
  Outline Fill    Node_Outline_Fill in gold #FFC726, Filled Radial360 from the top, clockwise — fillAmount = boarding
  Panel           Node_Inner, 902 × 407
    Viewport      inset 20 → 862 × 367, RectMask2D
      Strip       the part that scrolls
        Stars Row     3 × Star (224 × 211), Star 1 / StarEmpty, spacing 36
        Players Row   one viewport-height lower: 4 × Slot nP (140 × 225), Player_filled / Player_Outline, spacing 46
```

**The strip scrolls between the rows.** Stars sit on top, players underneath; `LevelNode`
SmoothDamps `Strip` up by the gap between the two rows (367) when a plane lands and back to 0 when
the island empties (`scrollTime` 0.12 → about 0.4 s, no overshoot). The distance is read off the
rows' positions, so moving a row in the prefab moves the scroll with it. Measured in Play: 0 → 367
over ~35 frames on landing, 367 → 0 on leaving.

**Slots** are tinted in the player's colour either way — `Player_filled` when their plane is on
this island, `Player_Outline` when it isn't — so the missing player reads at a glance. Slots keep
updating while the row scrolls away, so a leaving plane's slot empties before the stars return.

The badge was 10.5 m wide until the first Play check: at map zoom the player icons came out ~40 px
tall at 1440p and the glow outline a few pixels thick, which undersold both the scroll and the
boarding, so it went up 1.4×.

**The outline is always there; boarding fills it** (9 Oct 2026, Fitra). `Outline` is the
lavender border as drawn; `Outline Fill` sweeps over it in the ring fill's gold while every plane
waits. Gold can't come from tinting the lavender art (a tint only multiplies, and lavender × gold is
mud), so `Node_Outline_Fill.png` is a white copy of `Node_Outline.png` with the same alpha, made to
be tinted — regenerate it if the outline art changes. Picked from three renders over a dark navy
"track" with a lavender fill and a faint-lavender version; gold on the full outline was the only one
that read at map zoom.

> **Empty stars barely show.** `StarEmpty` is `#2B245E` on `Node_Inner`'s `#00113A`: 1.33 : 1
> contrast, against 8.97 : 1 for the filled `Star 1`. It's the art as specified; an empty star
> drawn lighter (or with a light outline) would fix it.

## Star total
Scene root `HUD` (Screen Space - Overlay, sort 10, CanvasScaler 1920×1080 match 0.5, no
`GraphicRaycaster`) holds `Star Count`: `StarCount.png` at 150 × 149, anchored top-right at
`(−36, −28)`. Its `Count` text is Knewave 50, white, on `Font/Knewave-Regular SDF - Navy Outline.mat`
(outline 0.25 in navy `#282456`, face dilate 0.15) — picked over navy text and an orange edge from
a mock; it matches the HUD's white Knewave score. It sits 7 px below the box's middle, because a
star's visual middle is lower than its bounding box's. `MapStarCount` writes the sum of every
node's `BestStars` and refreshes on `ProgressChanged`. The loading canvas (sort 32767) covers it
during transitions.
