# Player

**Scripts:** `MainMenu/PlayerSystem.cs`, `MainMenu/DebugAutoJoin.cs`,
`Game/Player/{PlayerSpawner, PlayerMovement, PlayerGrab, PlayerHandIK, JointBreakHandler,
PlayerIndicator, PlayerRingIndicator}.cs`
**Prefabs:** `Assets/Prefab/Character/{Annie, Bun Jovi, Scannor, ZipZub}.prefab` — all feel values
are serialized per prefab (the four currently share Annie's tuning; they were cloned from her).

One to four players share a screen. Everything a player *is* — body, movement feel, grab rules,
animator — lives on that player's character prefab.

## Joining

Joining is polled only in `MainMenu`, because one physical keyboard is shared by two schemes:

| Join input | Scheme | Gameplay half |
|---|---|---|
| Space | `KeyboardLeft` | WASD, Left Shift, E, F |
| Right Shift | `KeyboardRight` | arrows, Right Shift, `/`, L |
| gamepad South (A / Cross) | `Gamepad` | one unpaired gamepad, whole |
| gamepad North (Y / Triangle) on a pad already in use | `GamepadLeft` + `GamepadRight` | that pad, halved — see below |

A keyboard scheme is free when no current player uses its scheme name; a gamepad is free when no
player has paired that device. `LastJoinFrame` stops a join press from also submitting a menu
button in the same frame.

### Splitting one pad between two players

Overcooked-style, added September 2026. Pressing **North** on a pad that one player is already
using whole hands that player the left half and joins a second player on the right half of the
**same device** — the same trick the two keyboard schemes have always used, since the Input System
is happy to pair one device to two users.

| | `GamepadLeft` | `GamepadRight` |
|---|---|---|
| Move | left stick | right stick |
| Grab | left shoulder | right shoulder |
| Dash | left trigger | right trigger |
| UseStation | left stick press | right stick press |

`Pause` and the whole stage-select map work from either half, so those bindings just gained the two
new groups rather than being duplicated.

`PlayerSystem.SplitPad` switches the sitting player's scheme with `SwitchCurrentControlScheme`
**before** joining the partner, and switches it back if that join is refused (roster full), so a
refused split can never leave a player holding half a pad with nobody on the other half. Splitting
a pad is one-way: there is no un-split, because nothing in the lobby can drop a player yet.

> **Verified** in Play mode against a virtual pad: South joined one player on the whole pad, North
> split it into `GamepadLeft` + `GamepadRight` on the same device, and each half then read its own
> stick and its own shoulder. **Not verified on real hardware** — trigger and stick-press feel, and
> what a real pad's North button is called on a PlayStation or Switch layout, still need a
> playtest.

`PlayerSystem` owns a persistent `PlayerInputManager`. Joined player objects, paired devices,
control schemes, and `playerIndex` values survive every menu and scene transition. Always use
`PlayerSystem.Instance.Manager` rather than finding a manager in the scene.

### Which body spawns

Bodies are handed out in **join order** from `PlayerSystem`'s **Character Prefabs** roster on
`Assets/Prefab/Manager/PlayerSystem.prefab`: player 1 gets element 0, player 2 element 1, and
so on, wrapping around when players outnumber entries. The roster is `[Annie, Bun Jovi,
Scannor, ZipZub]` (August 2026 model drop — the new-style Annie replaced the old one in place,
same prefab GUID; ZipZub joined September 2026), one body per player slot. Use that roster
field — not the manager's own *Player Prefab*, which Unity hides while Join Behavior is *Manual*.
The copy that actually boots is `Assets/Resources/Runtime/PlayerSystem.prefab`, a variant of the
Manager prefab with no roster override — edit the roster on the Manager prefab.

The results screen names each player after their body's prefab (`GameHUD.PlayerDisplayName`
strips `(Clone)`), so the prefab name is the character's display name.

`PlayerSystem.JoinPlayer(scheme, device)` is the single join entry point — it swaps
`PlayerInputManager.playerPrefab` to the next roster body right before each
`manager.JoinPlayer` call. Lobby polling and `DebugAutoJoin` both go through it; anything else
that joins a player must too, or every player gets whichever body was assigned last.

`Ramp Agent Body.prefab` is still in `Assets/Prefab/Character/` but **nothing references
it** — dead weight, not a fourth shipping body.

Each character prefab is the same component stack cloned from Annie; the model lives on a
child named `Model` (a nested instance of that character's FBX), and the **Animator sits on
`Model`, not the prefab root** — the FBX clips bind bone paths relative to it.
`PlayerGrab`/`PlayerMovement` reach it through their serialized `animator` fields.

### Adding a body

Every body so far shares one Rigify `DEF-` skeleton, so a new one is a copy job, not a rig job:

1. FBX in `Assets/Model/Characters/<Name>/Model/`, textures in `<Name>/Textures/`. Importer:
   Scale Factor **5**, Generic, No Avatar, the same clip list and loop flags as the others (the
   artist's take names can drift — ZipZub's jog take is `AN_2-jog_loop`, renamed to `AN_2-jog`
   on the importer). Materials copied from an existing body's (URP Lit + `_BaseMap`) into
   `<Name>/Materials/` and remapped on the importer.
2. Copy an existing controller to `Assets/Animation/<Name>/<Name>.controller` and swap the
   seven motions to the new FBX's clips — the graph stays identical.
3. Copy an existing character prefab, replace the `Model` child with the new FBX (identity
   transform, add an `Animator` with the new controller, no avatar, no root motion), then
   re-point `PlayerGrab.animator`, `PlayerMovement.animator` and `PlayerHandIK`'s eight arm and
   four spine bones at the new Model.
4. Add it to the roster. A value diff against Annie should differ only in the controller.

If the new skeleton's proportions differ from the table under [Hand IK](#hand-ik), the capsule,
`GrabAnchor` and IK values need a real look instead.

### Shared input asset

`Assets/InputAction/GameInput.inputactions` is the single action source:

- `Player`: Move, Dash, Grab, UseStation
- `System`: Pause
- `Map`: Move, Confirm, Back

Joining stays raw-device polling because of the two keyboard schemes. Gameplay, pause, and map
navigation never construct their own actions in code.

## Spawning

Each gameplay scene has a `PlayerSpawner` with up to four ordered spawn transforms. On scene
start it sorts active `PlayerInput` objects by `playerIndex`, reactivates them, and moves each to
the matching point. `MovePlayerToSpawn` also serves hazard respawn, and zeroes velocity before
teleporting. If an index has no point, the spawner falls back to point 0 and warns — author four
valid points even if you usually test with two players.

## Indicators

Two pieces of art say which body is yours. Both are nested prefab instances on every
character prefab, and both take their colour from one palette so they can never disagree
about who is 1P.

| | Pin | Ring |
|---|---|---|
| Prefab | `Assets/Prefab/UI/Player Indicator.prefab` | `Assets/Prefab/Character/Player Ring.prefab` |
| Script | `PlayerIndicator` | `PlayerRingIndicator` |
| What | world-space canvas "1P" tag over the head | eight flat segments on the floor |
| Lifetime | shrinks away `gameplayShowSeconds` (3s) into a round | up for the whole game |

**Pin.** Forced onto the `WorldUI` layer, so the overlay camera draws it over level geometry.
Its height comes from the `SkinnedMeshRenderer` bounds, **not the capsule** — every body shares
one capsule that stops at the skull, so Annie's hat and Bun Jovi's ears would poke through a
capsule-derived height. It copies the camera's rotation rather than aiming at the camera:
aiming tilts pins near the screen edge and the perspective skew reads as a stretched sprite.
The hide timer restarts on every `sceneLoaded` (the player object persists, so `Awake` fires
once for the whole run) and only counts down when a `GameManager` exists — in the lobby the
pin never hides.

**Ring.** One shared material, `Assets/Material/World/PlayerRing.mat` (URP Unlit, transparent),
tinted per player through a `MaterialPropertyBlock` — nothing is cloned and no material leaks.
`alpha` is deliberately low (0.35): the ring is on screen all round and must not compete with
the luggage. It spins at `spinSpeed` (45°/s) written as a **world** rotation, so it turns
steadily instead of swinging every time the body does, and it sits at the collider's
`bounds.min.y` plus `groundOffset` so it stays on the floor without z-fighting.

Each segment is 0.55 long against a 0.887 arc. The overlap is on purpose in reverse: the bars
used to be 1.0 and butted into a solid octagon, which made the spin invisible. **If you close
those gaps, the ring stops reading as moving.**

`PlayerIndicator.CurrentColor` is the single source of truth. The palette is serialized only on
the pin prefab; `PlayerRingIndicator` resolves the pin at `Awake` through the shared
`PlayerInput` and reads that property each frame. Nothing on the ring is wired by hand, so the
same prefab drops onto any body.

> Before September 2026 the ring was a loose `Indicator (1)` object hand-built out of cubes on
> Annie and Bun Jovi only — Scannor had none, and the two that existed had each other's
> materials baked in (Annie, player 1, wore `Player2Indicator.mat`). `Player1Indicator.mat` and
> `Player2Indicator.mat` are now unreferenced.

## Movement and dash

Movement is camera-relative and position-driven. `Update` reads the `Move` and `Dash` actions;
`FixedUpdate` smooths velocity, clamps it against walls, moves the transform, and turns toward
the travel direction.

`PlayerMovement` owns normal speed, rotation speed, carry rotation multiplier, smoothing, dash
multiplier, dash duration, and cooldown as serialized fields on the character prefab.

**Wall-slide sweep.** Transform motion has no continuous collision sweep, so both the player
rigidbody and any carried luggage body run `SweepTest` along the proposed delta. Static,
kinematic, or heavier collisions remove only the into-wall component from displacement and
velocity, so the player slides instead of sticking. Triggers and lighter dynamic props don't
block movement.

**Only near-vertical surfaces count as walls.** The sweep returns early when `hit.normal.y > 0.5`,
because a surface facing mostly upward is a floor, a ramp, or the lip of a low platform. The
check used to flatten the normal and compare it against ~0 instead, which was wrong: flattening
a near-flat hit like `(-0.09, 0.99, -0.09)` leaves a small horizontal component that normalises
to a *full-strength* wall normal and cancels the entire move. That is what stopped players dead
when stepping between rotating platforms, and only intermittently — a non-kinematic body with
`defaultContactOffset` 0.01 rests somewhere in a 0.005–0.05 penetration band that varies step to
step, and only part of that band produced the bad hit.

The `Ground` tag is an earlier, weaker workaround for the same problem and is now largely
redundant. It is checked on **`hit.collider`**, so it must be on the object that owns the
collider — tagging a parent does nothing. Level 2 has nine `Ground` overrides applied by hand,
three of them on rotating-platform *roots* where they never took effect.

**Carry anchor delta.** While carrying, the controller records `grabAnchor.position` before
movement and rotation, then moves the luggage by the anchor's full world delta — linear motion
plus the rotational arc. The joint corrects whatever pitch and roll is left.

**Dash** multiplies normal speed for its duration and stays steerable. Duration and cooldown use
scaled gameplay time, so pause freezes them.

**Bubble VFX** uses a per-player object pool. Each bubble moves, shrinks, and fades through a
`MaterialPropertyBlock`, then returns to the pool — no material is cloned, nothing is destroyed
per emission. `bubbleSpawnOffset` is per-prefab because bodies put their pivot in different
places (Annie's is at the feet).

Persisted players are inert outside a gameplay scene, detected through the active `GameManager`,
and reacquire the scene camera after each load.

## Grab, carry, and throw

**Grab is also the machine button.** On a redesign level a press near a machine loads the bag you
are carrying, and *keeping the button down* then works the machine — one continuous hold covers
carry, load and crank. Empty-handed at a loaded machine, a press starts cranking instead of
grabbing. `ProcessStationInput` runs before the normal grab logic and consumes the press when it
acts, so loading a bag never starts a throw charge and cranking never grabs the bag back out.
Releasing pauses the machine; walking out of range ends the session on its own. See
[stations](stations.md#manual-crank-the-redesigns-machine-mechanic).

Grab is bound to **E** (and `/` for the second keyboard player). UseStation, on **F**, is the older
secondary button: it still loads a station and still pulls levers, but it does not crank.

One Grab action covers pickup, hold-to-charge throw, and quick drop. UseStation puts a held bag
into a compatible machine.

**Detection.** `Physics.OverlapSphereNonAlloc` searches the configured luggage layer around
`grabPoint` with one shared 32-collider buffer. A ray to the rigidbody centre rejects targets
hidden behind geometry, then the nearest valid `Luggage` wins; in-station items are skipped.

**Ownership.** Pickup force-drops any existing grabber, so exactly one player holds an item at a
time — that's the stealing mechanic. `lastGrabber` persists after release so a thrown delivery
still credits the thrower. `GetPlayerIndex()` reads the live `PlayerInput.playerIndex`.

**Carry setup.** Child mesh bounds pick a connection point on the luggage's back face. A
`ConfigurableJoint` is created immediately, then yaw aligns over `grabAlignDuration`; the arc is
chosen so the bag never swings through the player's body. A runtime bridge collider spans player
and luggage so the carrier can't walk through the held item, while collisions with the held item
itself are ignored until release. Joint limits, springs, dampers, maximum forces, projection, and
break thresholds are the `joint…` fields on `PlayerGrab`. `JointBreakHandler` performs a clean
forced drop if Unity breaks the joint.

**Throw and drop.**

- Release before `throwMinHoldTime` → drop.
- Hold longer → forward and upward impulse interpolate through the configured ranges.
- Charge feedback — the arrow child, the looping buildup SFX, and the `isThrowing` throw
  pose — starts only once the press outlives `throwMinHoldTime`, so a quick tap reads as a
  plain drop with no wind-up (changed 2026-07-27; it used to start on press).

**Station use** reuses the same non-allocating buffer, finds a `MachineStation` that is free and
accepts the held bag, and calls `TryPlace`. Placement force-releases the player first. See
[stations](stations.md).

The same press also works a [lever](hazards-and-props.md#lever): `TryUseStation` now returns
`bool`, and `TryUseLever` runs only when it returns `false` — empty-handed, or no station took
the bag. Station first means a player putting a bag into a machine can never also flip a lever
standing beside it. Both sweeps use `grabRadius`, so that one field is the reach for grabbing,
station use, and levers alike.

`OnDrawGizmosSelected` draws the grab radius and the clear/blocked rays — useful when tuning
layers and geometry.

## Hand IK

`PlayerHandIK` pins the hands onto the carried luggage so they track it instead of holding a
fixed pose. Unity's `OnAnimatorIK` needs a Humanoid avatar and this rig is Generic with no
avatar, so the two-bone solve is written by hand.

It runs in `LateUpdate`, after the Animator has written the clip pose, and writes only the
clavicle, upper arm, forearm, and hand. The Rigify twist bones (`DEF-upper_arm.R.001`,
`DEF-forearm.R.001`) are children, so they inherit the correction untouched. `spineStabilize`
and `carryLean` can square and lean the torso in to buy the arms slack, but both are **zeroed on
Annie.prefab** (2026-07-27, at Fitra's request that IK touch nothing but the arms) — the spine
plays the raw animation.

### The hands reach toward the case, not onto it

Measured on the August 2026 bodies (all four share skeleton proportions at import scale 5):

| | |
|---|---|
| Arm, shoulder → wrist | **0.84** |
| Shoulder → grip, case on `GrabAnchor` | **~1.45** |

Better than the old model (0.763 against ~1.76) but the grip still sits beyond the arm, so the
`maxReach` clamp still does the work — the hands get close enough to read as holding the case.

### All four bodies are one rig

Annie, Bun Jovi, Scannor and ZipZub share the **same skeleton at the same proportions** —
shoulder `2.358`, head bone `2.681`, feet `0.182`, identical on all four. Only the silhouette
differs: Annie's hat and hair reach `4.27`, Bun Jovi's ears `4.89`, Scannor's bare head `3.84`,
ZipZub's UFO head `3.44`. ZipZub's rig also carries 45 extra `A_*` bones under `DEF-head` — the
alien pilot in the UFO. Its clips animate them; nothing in code touches them.

Because of that, **every gameplay value on the four prefabs is identical, deliberately** — the
capsule included (`height 3.85`, `center y 1.925`, `radius 0.726`). A capsule sized to each
mesh would size it to hair and ears, giving Bun Jovi a collider `1.2` taller than Scannor's for
the same body, so one character would be stopped by gaps another walks through. `3.85` wraps
every body (Scannor's mesh is the tallest bare skull at `3.839`) and sits below the `4.11` top
the game originally shipped with, so it cannot collide with anything the old capsule cleared.
Hair, hat and ears intentionally poke above it.

> If you change a feel value — speed, grab radius, joint spring, capsule — **change it on all
> four prefabs.** (Ring and pin are the exception that proves the rule: they are nested
> prefabs now, so editing one asset covers every body.) They are meant to be interchangeable
> bodies, not balance variants. A diff of every serialized property across the four should
> differ only in the Animator's controller.

`maxReach` is what keeps the out-of-reach case safe (2026-07-27; it replaced the older
`reachFade`, which faded the solve out entirely and left the arms playing the plain clip). When
the grip sits beyond `maxReach × armLength`, the target is clamped onto that sphere: the hands
reach toward the case with the elbows still bent, selling "trying to hold it" without ever
landing on it. **Never let the raw solve chase an untouchable grip at full extension — it clamps
the elbow straight and swings the whole arm every frame, which reads on screen as a throw pose
that never ends.** That was a real shipped bug, fixed 2026-07-26.

Per-arm `grip` (luggage-local) and `elbowHint` on the prefab steer where the hands aim and which
way the elbows fold. On a body whose arms genuinely reach, the clamp never engages and the hands
land on the grip exactly.

## Animator

**Controllers:** `Assets/Animation/{Annie/Annie, Bun Jovi/Bun Jovi, Scannor/Scannor,
ZipZub/ZipZub}.controller` — four copies of the same graph, each pointing at its own FBX's clips
(clips named `AN_1-idle-Loop`, `AN_2-jog`, `AN_4-throw_Enter`, `AN_5-throw-Loop`,
`AN_6-throw-End`, `AN_7-throw-loop-jog`, `AN_8-dash`, with loop flags set on the importer).
Annie's spare `AN_3-jog-Stop` and the others' `AN_1-idle-RARE` are imported but unused (the rare
idle is reserved for later). Parameters `isMoving`, `isDashing`, `isGrabbing`, `isThrowing`, all
bool, all hashed once in `AnimId`. A graph change must be made four times — or made once and
re-copied with motions swapped.

**Scannor's clips run slower.** Every FBX keys the same frame counts, but Scannor was exported at
**24 fps** while Annie, Bun Jovi and ZipZub are **30 fps**, so Scannor's idle is 4.25 s against
3.40 s and every state of his plays at 0.8× the others' speed. The poses are identical at the
same normalized time — only the timing differs.

```text
Idle ⇄ Move                     isMoving
Idle/Move → Throw Start          isThrowing
Throw Start → Loop Idle/Walk     after one cycle, split on isMoving
Loop Idle ⇄ Loop Walk            isMoving
Loop */Throw Start → Throw End   !isThrowing
Throw End → Idle/Move            exit time 0.8, split on isMoving
Throw End → Throw Start          isThrowing (instant re-charge)
AnyState → Dash                  isDashing
Dash → Idle/Move                 !isDashing, split on isMoving
```

**Carrying does not change the locomotion state.** `isGrabbing` is still set by `PlayerGrab` and
still exists as a parameter, but nothing in the graph reads it — a carrying player plays plain
Idle/Walk, and the held bag is sold by the joint, not by a separate pose. The graph previously
had `Annie Idle Luggage` and `Annie Move Luggage` states that pointed at the *same clips* as
plain Idle/Move, gated on `isGrabbing`; they were duplicates and were removed on 2026-07-26.

`isThrowing` is true only while the Grab button has been held **past `throwMinHoldTime`** with a
bag in hand — that is the throw charge. A shorter tap never sets it. `PlayerGrab` is the only
writer.

`throwTorsoWeight` on `PlayerHandIK` lets the wind-up show through the spine while charging,
since the wind-up lives almost entirely in the torso. It only matters if the torso assist is
enabled; on Annie `spineStabilize` and `carryLean` are zeroed, so it has no visible effect.
