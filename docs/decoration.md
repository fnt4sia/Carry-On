# Decoration and ambient props

**Scripts:** `Game/World/{WindSway, CloudDrift, MarqueeText, SignFlicker, AmbientTrafficSpawner}.cs`,
`Game/Core/AmbientAirplaneSpawner.cs`

Animated scene dressing: planes on the apron, swaying palms, scrolling signs, the logo TV, drifting
clouds, window dust and traffic. None of it touches gameplay; most of it dresses the MainMenu
terminal ([main menu](scenes/main-menu.md)). The moving escalator has its own page ([escalator](escalator.md)).

## Ambient airplanes
**Script:** `Game/Core/AmbientAirplaneSpawner.cs`

Pure set dressing — decorative traffic that flies a prefab from one anchor to another and owns
no round rules, so the lobby and gameplay scenes share one component.

Each cycle it waits `initialDelay`, then loops: wait a random `spawnDelayRange` cooldown, pick a
random entry from `airplanePrefabs` (null slots are skipped, so a half-filled array still works),
and fly it from `spawnPoint` to `destinationPoint` at a random `speedRange` speed. Up to
`maxConcurrent` flights overlap; each runs its own coroutine. Instances are pooled in one idle
stack **per prefab**, so a Pesawat1 is never reused as a Pesawat2.

`faceTravelDirection` turns the plane down the path with `LookRotation`; off keeps the spawn
anchor's rotation, which is what the GameManager copy relies on.

`requireActiveRound` is the one thing that differs between the two users:

| Where | Setting | Why |
|---|---|---|
| `GameManager.prefab` | **on** | planes only fly while a round is running |
| `MainMenu` → `AmbientPlanes` | **off** | the lobby has no `GameManager` at all |

Leaving it on in a menu is the failure mode to watch for — `RoundRunning` is false forever, so
nothing ever spawns and there is no error to tell you why.

### MainMenu setup
`AmbientPlanes` holds the component plus its two anchors. The path runs down `Runway2`
(centre `x 248.6`, 122.85 wide, 1212 long, top surface `y −2.94`):

| | |
|---|---|
| `PlaneSpawnPoint` | `(248.6, 1.24, 420)` |
| `PlaneExitPoint` | `(248.6, 1.24, −520)` |

`y 1.24` puts the wheels on the runway — `Airplane.prefab`'s pivot sits 4.18 above its lowest
point. Both anchors are off-screen (the view's edges are about `z +248` and `z −358`), so planes
enter and leave cleanly. 940 units at 70–130 u/s is a 7–13 s crossing.

Only `Airplane.prefab` is in the fleet. `Pesawat1` and `Pesawat2` are 4–5× larger at scale 1
(262 and 169 units long against Airplane's 53, and Pesawat1's 206-unit wingspan is wider than the
runway) — they need scaled prefab variants before they can be mixed in.

## Wind sway
**Script:** `Game/World/WindSway.cs` · **Prefab:** `Assets/Prefab/Decoration/HangingPlants2.prefab`

Ambient wind for hanging decoration. Composes a swing *onto* the transform's authored
`localRotation` — captured once at `Awake`, never replaced — so an FBX's imported orientation
survives. The swing axes are taken in **parent space**, which is why the plant reads as a
pendulum even though its own mesh is tilted 75°.

| Field | On the prefab | Does |
|---|---|---|
| `swayAngle` | 3 | swing along the wind, in degrees. True peak is `swayAngle * (1 + gustStrength)` |
| `crossSwayAngle` | 1.2 | swing across the wind; keep it under `swayAngle` |
| `swaySpeed` | 0.35 | swings per second |
| `windDirection` | 90 | degrees around Y; `0` = wind along world `+Z` |
| `randomness` | 1 | `0` = every instance in lockstep; `1` = own phase and own rate (±35%) |
| `gustStrength` | 0.6 | how much slow noise swells and stills the swing; `0` = a metronome |
| `gustSpeed` | 0.12 | gusts per second |

Desync is seeded from `GetInstanceID()`, so a row of identical prefabs never swings as one
block; the global `Random.state` is borrowed and restored rather than left reseeded. The cross
swing runs at `0.63×` the main rate on purpose — the two never resync, so the pot traces a
wandering ellipse instead of a flat back-and-forth.

### The pivot is the whole trick
`WindSway` swings about its **own transform origin**, so where that origin sits decides what the
motion looks like. `HangingPlants2.prefab` is therefore rigged in two parts:

```text
HangingPlants2      root at the top of the rope, identity rotation, scale 1   <- WindSway
  Model             localPosition (0, -5.00, -1.34), rotation (75, 180, 0), scale 2.74
```

The FBX's own pivot sits at mesh-local `z 0`, which is exactly where the rope meets the pot —
put `WindSway` there and the rope's *mounting point* sweeps a wider arc than the pot does, so the
plant looks like it is detaching from the wall. The rope runs from mesh-local `z −1.888` to
`−0.39` (cross-section radius ~0.09) and the pot occupies `−0.39 … 0.686` (radius ~0.66), so the
top of the rope is `(0, +5.00, +1.34)` above the FBX pivot. The root is placed there and `Model`
carries the offset back, which leaves the rendered rest pose bit-for-bit identical while pinning
the mount: at the peak of a 4.8° swing the rope top moves **0.000** units and the pot moves
**0.570**.

`Model` keeps the authored `(75, 180, 0)` / `2.74` from the FBX untouched — the root is what
rotates. Because the root is identity, `WindSway`'s parent-space axes are world axes, which is
what makes `windDirection` mean what it says.

The longer lever arm is why the prefab runs at `swayAngle 3` rather than the script's default 5:
the pot now hangs ~6.8 units under the anchor instead of ~1.8, so the same angle throws it about
3.5× further.

`MainMenu` has four instances along the interior wall, anchors at `y 11.25`, `z 116.28`, with
`windDirection 90` so the swing is sideways along the wall, the most visible axis from the fixed
menu camera.

## Signage text
**Scripts:** `Game/World/{MarqueeText, SignFlicker}.cs` ·
**Prefabs:** `Assets/Prefab/Decoration/{WallSignage, WallSignage2, StandingSignage2}.prefab`

Three signage meshes carry a world-space canvas as a **child of the prefab**, so the text is
authored once and every instance gets it. Nothing is built in code at runtime — the scripts only
move and fade what is already there.

All the canvases share one setup, matching `MenuBanner/MenuCanvas`:

| | |
|---|---|
| localRotation | `(0, 90, 0)` — puts canvas `+Z` into the sign, so `+X` reads screen-right |
| localScale | `0.001`, so `sizeDelta` is in thousandths of a sign-local unit |
| Canvas | World Space, `worldCamera` empty, **no `GraphicRaycaster`** — decoration must not swallow clicks |
| CanvasScaler | ConstantPixelSize, `dynamicPixelsPerUnit 4` |
| Layer | `UI` (5) |
| Font | `LiberationSans SDF`, same as the board rows |

> The font atlas is **Static / ASCII-only**. `·`, `—`, arrows and other non-ASCII glyphs render
> as missing-character boxes. Keep signage copy to plain ASCII.

Every sign's visible face is local **−X**. These rects were measured off the meshes'
front-facing vertices, not guessed:

| Prefab | Canvas | localPosition | sizeDelta | Content |
|---|---|---|---|---|
| `WallSignage` | `SignText` | `(-0.020, -0.064, -0.4745)` | `1862 × 386` | ticker, font 170 |
| `StandingSignage2` | `SignText` | `(-0.020, -0.0455, -0.435)` | `1862 × 385` | ticker, font 210 |
| `WallSignage2` | `SignTextUpper` | `(-0.100, 0, 0.279)` | `940 × 237` | `BAGGAGE CLAIM`, font 105 |
| `WallSignage2` | `SignTextLower` | `(-0.100, -0.4995, 0.279)` | `940 × 237` | `GATES 1 - 4`, font 105 |

`WallSignage` and `StandingSignage2` share the same board mesh, so their rects are nearly
identical. `x −0.020` sits in front of the flat panel but **behind** the header block that
protrudes to `x −0.054` — that is what makes the text look recessed into the sign instead of
pasted onto it. `WallSignage2`'s two rects sit inside the painted blue field of each plaque, left
of the baked arrow icon; that field is texture, not geometry, so those numbers came off a head-on
render rather than the mesh.

`MarqueeText` slides its label right-to-left across the viewport and wraps once the tail clears.
The viewport needs a `RectMask2D` or the text spills past the sign's edge, and the label needs
word-wrap off with a left-middle pivot so `preferredWidth` is its true length. `startDelay`
staggers signs so a row of them is not in step.

`SignFlicker` drives the canvas's `CanvasGroup` — mostly steady, with short random bursts of
dipping to `dimAlpha`, covering every graphic under the canvas at once. Keep `dimAlpha` above 0
so a sign never fully blanks.

Colours are deliberately low-contrast against the dark boards: amber `(0.72, 0.58, 0.36)` for
the tickers, pale steel `(0.58, 0.67, 0.76)` for the plaques. Same reasoning as the board rows'
bronze — the signs should sit in the environment, not glare out of it.

### Logo TV
**Prefab:** `Assets/Prefab/Decoration/LogoTV.prefab` · **Materials:** `Material/World/{TV Casing, TV Screen Plain}.mat`

> **6 Oct 2026 test (Fitra's ask):** the CRT screen shader below moved to the menu banner
> (`Board Screen.mat` on `MenuBanner/Screen`, see [ui](scenes/main-menu.md#the-banner-canvas)) and the TV's
> `Screen` now wears `TV Screen Plain` — URP Unlit, flat `(0.09, 0.35, 0.46)`, no scanlines — so the
> logo reads cleaner. `TV Screen.mat` is untouched and unused: putting the effect back on the TV is
> one material swap on the prefab's `Screen`.

In `MainMenu` this replaced the `WallSignage` on the pillar above the menu banner (September 2026 —
the `WallSignage` prefab is kept). It is built from primitives, no FBX: all sizes are world units at
root scale 1, screen facing local **−Z**.

```text
LogoTV                     root at (−64.95, 11.59, 92.85), identity rotation
├── Body                   Cube 8.6 × 5.0 × 0.35, TV Casing
├── Bezel/Top,Bottom,Left,Right   0.3-wide bars, 0.08 proud of the front — the outline pass
│                          catches that step, which is what makes the screen read as recessed
├── Screen                 Quad 8.0 × 4.4, TV Screen Plain (was TV Screen, the unlit CRT, until 6 Oct 2026)
├── ScreenCanvas           World Space, 8000 × 4400 at scale 0.001, 0.015 in front of Screen,
│   │                      CanvasGroup + SignFlicker (same values as WallSignage), layer UI, no raycaster
│   └── Logo               CarryOnLogo sprite, preserveAspect, anchors 0.1–0.9 × 0.12–0.88
└── Mount/ArmUpper,ArmLower  2.8-long rods out of the left side at y ±1.6, into the pillar
```

`TV Casing` is a copy of the `AIRPORT` material with no texture and base colour **`#460046`** —
the palette texel the signage boards and their arms sample, so the TV matches the other signs.
The screen is a real mesh rather than a black `Image`: when `SignFlicker` dips the canvas alpha the
logo fades against the screen, and the outline pass has flat geometry behind the logo instead of
edge-detecting whatever is behind the canvas. The logo is tinted `(0.88, 0.93, 1)` — a cool
white-balance so it reads as emitted light rather than a sticker; change `Logo`'s Image colour to
retune. The arms are sized to this one pillar (the old sign's arms ended at world `x −71.85`), and
the `WallSignage2` plaque's posts end at `y 10.43` behind the TV — keep the TV's bottom edge below
that if it moves.

`CarryOnLogo.png` is imported as a **Single** sprite with mipmaps on: it is 3104 px wide (capped to
2048) and drawn a few hundred pixels wide in the lobby, so without mips it shimmers.

#### The screen shader
`Assets/Shader/TVScreen.shadergraph` (URP **Unlit**, opaque) makes a quad read as a powered CRT
instead of a black rectangle. It now runs on the menu banner (`Board Screen.mat`: screen colour
`(0.045, 0.16, 0.22)`, dark colour `(0.008, 0.01, 0.018)`, 64 scanlines for the taller board); the
values below are the TV's original `TV Screen.mat`. It uses the quad's own UV0 — a Unity primitive, so unlike the airport
meshes its UVs are a clean `0…1` and no world-space trick is needed. Three terms, all on `uv.y`:

```text
base  = lerp(Dark Colour, Screen Colour, saturate(scan * vignette + sweep))
scan  = lerp(1 − Scanline Strength, 1, 0.5 + 0.5·sin((uv.y + t·Scanline Speed)·Scanline Count))
sweep = frac(uv.y − t·Sweep Speed) ^ Sweep Sharpness · Sweep Strength
vign  = 1 − length(uv − 0.5) · Vignette
```

| Property | Value | Does |
|---|---|---|
| `Screen Colour` | HDR `(0.09, 0.35, 0.46)` | the lit tone; cool so the cream logo still reads warm against it |
| `Dark Colour` | `(0.012, 0.012, 0.02)` | what the screen falls to between scanlines — the old flat colour |
| `Scanline Count` / `Speed` / `Strength` | `48` / `−0.06` / `0.5` | band count over the quad, drift, how dark the gaps go |
| `Sweep Speed` / `Sharpness` / `Strength` | `0.15` / `6` / `0.35` | the slow refresh bar crawling up the screen |
| `Vignette` | `0.9` | corner falloff; `length(uv−0.5)` maxes at `0.707`, so `0.9` darkens corners ~36% |

**Scanline count is a viewing-distance number, not a taste one.** 48 bands over a 4.4-unit-tall quad
is about the limit before the lines alias at lobby distance — there is no mip or derivative fade in
the graph. Raise it only if the camera gets closer, and re-check on a screenshot rather than in the
graph preview.

The screen is Unlit on purpose: the casing is lit, the screen emits. Bloom picks the bright
scanlines up through the Global Volume, which is why `Screen Colour` is an HDR field.

### Don't confuse these with the older prefabs
`StandingSignage.prefab` is a **different sign** — mesh `Signage03`, scale 2.9, facing `+90` — and
is not what `MainMenu` uses. The menu's standing sign was a loose FBX drop of `Signage03.001`, so
it became `StandingSignage2.prefab` rather than overwriting the existing asset. Same story for
`HangingPlants.prefab` (mesh `HangingPlants`, scale 1.75) versus the menu's
`HangingPlants2.prefab` (mesh `HangingPlants.001`, scale 2.74).

## Cloud drift
**Script:** `Game/World/CloudDrift.cs` · **Scene object:** `MainMenu` → `Clouds`

**Prefabs:** `Assets/Prefab/Decoration/Cloud1…Cloud6.prefab`

Moving sky for a fixed camera. Each cloud is a `Cloud1…6` prefab whose children are its puffs —
nested `Bubble.prefab` instances (the `Cloud.001` mesh from `Model/Effects/cloudsculpt2_anim.fbx`)
with per-puff scale, yaw and shadows-off overrides. `Bubble.prefab` is shared with the character
bubble, so never restyle it for the sky; restyle a `Cloud` prefab instead.

- **Opening sky.** The six `Cloud1…6` instances under `Clouds` are the hand-composed sky the scene
  opens on. They drift like the rest and are removed once they leave.
- **Spawning.** Every `spawnInterval` seconds a random `cloudPrefabs` entry is spawned upwind at a
  random depth, height and size, unless `maxClouds` are already up. Short intervals with a low cap
  make clouds arrive in waves (every exit is instantly refilled); 15–35 s with a cap of 12 keeps
  them spread across the view.
- **Per-cloud speed.** Each cloud moves at `windSpeed × (1 ± speedVariation)`, so clouds overtake
  each other, and near clouds still cross the screen faster than far ones — that parallax is what
  makes the layer read as deep. Negative wind (the default) blows right-to-left: new clouds enter
  over the open apron on the right, where they are seen arriving, and leave behind the terminal.
- **Nothing pops.** Spawns are pushed upwind until neither view can see them; a cloud is destroyed
  only when it is downwind *and* outside both the home view (camera pose at `Awake`, current
  aspect) and the live view (wherever `CameraFocus` has the camera now). The old mirror-wrap
  was removed: at a fixed angle it could jump inside the lean-in view.
- **Billow.** Each puff scales on its own slow sine (golden-angle phases, spread rates), so a
  cloud's outline keeps changing even while the cloud barely moves.

The deck sits at ~110–140 m altitude, 320–700 m out. **Far + low clouds hide behind the terminal's
upper floor** on the left half of the frame, which is why the spawn depth stops at 700 — an 830 m
cap left most spawned clouds invisible. Hand-placed far clouds work only over the open apron on the
right. When authoring a new cloud prefab: keep its puff bottoms level (cumulus have flat bases) and
squash puff local Z for flatter far clouds — local Z is world-up at the `(270, y, 0)` import
rotation.

## Window dust
**Scene object:** `MainMenu` → `MenuEnvironment/Interior/WindowDust` (a plain `ParticleSystem`, no script)

Sunlit specks drifting just inside the glass facade, kept deliberately faint. Mesh particles on the
`Cloud.001` bubble mesh with `Bubble Colour`, so they are lit by the sun like everything else and
need no texture. A 17 × 16 × 70 box centred at `(−29.5, 6, 105)`, 7/s, 10–16 s lives, size
1.2–2.4 (≈0.15–0.3 u), slow noise drift, prewarmed so the menu opens with dust already up. Against
the bright window sky they read as snow if they get bigger or denser than this.

## Ambient traffic
**Script:** `Game/World/AmbientTrafficSpawner.cs` · **Prefab:** `Prefab/Environment/AmbientTraffic` ·
**Scene objects:** `MainMenu` → `AmbientTraffic`; Level1 → `level1/Ambient Traffic (Cars)` and
`(Planes)`, two instances at different scales, each with the other group's fleet emptied
([levels](scenes/levels.md#the-new-level1-environment))

The general version of [ambient airplanes](#ambient-airplanes): decorative vehicles that cross the
view, get pooled, and own no round rules. One component holds a list of **groups**, and each group
is one kind of traffic with its own fleet, endpoints and timing — so planes and cars are two
entries, not two scripts.

| Field | Does |
|---|---|
| `name` | inspector label only |
| `fleet` | prefab + `yawOffset` pairs, one picked at random per trip. **Empty means the group never spawns** |
| `sideOne` / `sideTwo` | the endpoints, split into two sides |
| `faceTravelDirection` | `LookRotation` down the path; off keeps the start anchor's rotation |
| `initialDelay` | quiet period before this group's first trip |
| `spawnDelayRange` | random cooldown between trips |
| `speedRange` | random world units per second per trip |
| `maxConcurrent` | how many of this group travel at once |

**A trip always crosses sides.** The start side is picked at random, then the destination is taken
from the *other* list — never within one side. Which point on each side is used is also random, so
three points per side give `3 × 3 × 2 = 18` distinct routes from one group.

Empty inspector slots are skipped everywhere (`HasAny` / `PickRandom`), so a half-filled array
still works and an all-null array simply yields nothing rather than throwing. A group with prefabs
but a missing side logs a warning once — that is a wiring mistake, unlike an empty fleet, which is
a legitimate off switch. Vehicles are pooled in one idle stack **per prefab**, so a bus is never
reused as a plane.

### Which way is the nose?
`LookRotation` puts an object's **+Z** down the path. Nothing in Unity knows where a model's nose
actually is, so a prefab authored facing any other axis travels backwards or sideways. That is what
`Vehicle.yawOffset` is for — it is folded in after the look rotation:

```csharp
Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f)
```

| Nose authored along | `yawOffset` |
|---|---|
| `+Z` | 0 |
| `+X` | 270 |
| `-X` | 90 |
| `-Z` | 180 |

**It is per prefab, not per group** — the two menu planes disagree with each other, so a
group-level setting could not express it.

There is no reliable way to detect this at runtime; it is measured once by eye. Drop the prefab in
at **identity rotation** and look at it from above and from the side — mesh bounds alone will not
tell you, because a nose and a tail-fin end look identical to a bounding box. Measured values for
the models currently in `MainMenu`:

| Prefab | Nose | `yawOffset` |
|---|---|---|
| `Pesawat1` | `+Z` | 0 |
| `Pesawat2` | `+X` | 270 |
| `BaggageTruck1` | `-X` | 90 |
| `Firetruck` | `-X` | 90 |
| `PassengerBus` | `-X` | 90 |

All three road vehicles checked so far are authored nose-along `-X`, so `90` is the likely starting
guess for others from that pack — but confirm it, since `Pesawat1` and `Pesawat2` prove one pack
can be inconsistent. A useful corroborating hint: if the artist gave the **prefab root** a non-zero
Y rotation, that value is usually the offset already (`Pesawat2.prefab` is authored at `(0, 270, 0)`).

With `faceTravelDirection` off, `yawOffset` is ignored entirely and the start anchor's rotation is
used instead.

### MainMenu setup
The anchors are placed and named to match the `1A 1B 1C` / `2A 2B 2C` convention:

| Group | Side one | Side two | Trip | Speed | Cooldown |
|---|---|---|---|---|---|
| `Planes` | `1A/1B/1C` low on the left (`y 60–100`, 800–850 out), **hidden behind the terminal** | `2A/2B/2C` off-screen right (`y 120–200`, 75–85° right of the lobby camera) | 1310–1480 u | 130–350 u/s | 8–12 s, first after 1 s |
| `Cars` | `1A/1B/1C` on the apron at `y −3.8`, viewport `x 0.14` | `2A/2B/2C`, viewport `x 1.06` | ~764 u | 45–75 u/s (10–17 s) | 5–10 s |

**Plane anchors (September 2026).** The old side-one anchors sat in open sky at viewport
`(0.32, 0.78)`, ~820 deep, so every plane popped into existence mid-screen. The planes are huge —
`Pesawat1` is 206 × 262 u, `Pesawat2` 169 × 162 — so "off-frustum" needs real margin. The anchors
were placed by render-diff, not by frustum maths: render the camera with and without a plane at the
anchor and count changed pixels. Every anchor reads 0 (±noise) from the lobby pose **and**
`MenuFocusPose` (and the proposed square focus pose), at 16:9 **and** 21:9. A plane enters from behind the
upper-floor structure or the right frame edge, climbs across the windows, and is on screen for
~40–55% of the trip — so roughly 1–4 s of each trip is hidden lead-in, which the 8–12 s cooldown
accounts for. **Move the lobby camera or `MenuFocusPose` and these need re-checking.**

Two placement constraints, both learned the hard way:

- **The camera's far clip is 1000.** Anchors past that spawn a vehicle that is invisible until it
  flies into range. Every anchor above is inside 740.
- **Ground anchors must clear the terminal.** The glass wall sits at `x −24.7 … 24.9` spanning
  `z 30.9 … 330.9`, so a ground point with `x < 30` is *inside the building* unless `z > 345`. The
  car `1x` anchors sit at `x ≈ −80` and get away with it only because they are at `z 461 … 665`,
  out past the end of the wall.

Unlike the planes, the cars' deep end cannot be pushed fully off-frustum: on the ground the view's
left edge is asymptotic, so marching that way leaves the far clip before it leaves the screen. The
`1x` car anchors sit at viewport `x 0.14` instead — far enough to be small and behind the window
mullion. If a car ever reads as popping into existence, that anchor is the one to nudge.

### Contrail puffs
The `Planes` fleet flies `Pesawat1 Flying` / `Pesawat2 Flying` — prefab **variants** of the plain
planes that add one nested `ContrailPuffs.prefab` at the tail (root-local `(0, 24, −125)` on
`Pesawat1`, nose `+Z`; `(−78, 22, 0)` on `Pesawat2`, nose `+X`). Same `yawOffset`s as the bases.
Variants, not an edit to the base prefabs, because `Level1` has a **parked** `Pesawat2` that must
not puff.

`ContrailPuffs` is a world-space mesh-particle system on the `Cloud.001` bubble mesh with
`Bubble Colour` — the exact mesh and material the `Cloud` prefabs are built from — so the trail
reads as a line of small cartoon clouds rather than a ribbon. 18/s by **time**, not distance:
a pooled plane teleports back to its start anchor, and rate-over-distance risks spraying a line of
puffs along that jump. Puffs live 1.0–1.6 s (≈1–2 plane lengths at trip speed) and shrink to
nothing. Known limit: when a plane reaches its far, occluded anchor and is pooled, the tail still
in open sky vanishes with it — kept short so that pop is small and far away.

> The older `AmbientAirplaneSpawner` is no longer in `MainMenu`; it lives only on
> `GameManager.prefab` (gameplay levels), flying `Airplane.prefab` along `+Z` with
> `faceTravelDirection` **off** — the plane keeps the spawn anchor's rotation `(0, 0, 0)`. That is
> correct only because `Airplane`'s nose is authored along `+Z`. It has no `yawOffset`, so a model
> with any other nose axis will fly sideways there.
>
> **Facing verified September 2026:** every fleet entry above was posed exactly as the spawner poses
> it and rendered side-on, and at runtime the nose and motion direction measured 0.0° apart.
