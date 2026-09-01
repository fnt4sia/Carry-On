# Player

**Scripts:** `MainMenu/PlayerSystem.cs`, `MainMenu/DebugAutoJoin.cs`,
`Game/Player/{PlayerSpawner, PlayerMovement, PlayerGrab, PlayerHandIK, JointBreakHandler}.cs`
**Prefabs:** `Assets/Prefab/Character/{Annie, Bun Jovi, Scannor}.prefab` — all feel values are
serialized per prefab (the three currently share Annie's tuning; they were cloned from her).

One to four players share a screen. Everything a player *is* — body, movement feel, grab rules,
animator — lives on that player's character prefab.

## Joining

Joining is polled only in `MainMenu`, because one physical keyboard is shared by two schemes:

| Join input | Scheme | Gameplay half |
|---|---|---|
| Space | `KeyboardLeft` | WASD, Left Shift, E, F |
| Right Shift | `KeyboardRight` | arrows, Right Shift, `/`, L |
| gamepad South | `Gamepad` | one unpaired gamepad |

A keyboard scheme is free when no current player uses its scheme name; a gamepad is free when no
player has paired that device. `LastJoinFrame` stops a join press from also submitting a menu
button in the same frame.

`PlayerSystem` owns a persistent `PlayerInputManager`. Joined player objects, paired devices,
control schemes, and `playerIndex` values survive every menu and scene transition. Always use
`PlayerSystem.Instance.Manager` rather than finding a manager in the scene.

### Which body spawns

Bodies are handed out in **join order** from `PlayerSystem`'s **Character Prefabs** roster on
`Assets/Prefab/Manager/PlayerSystem.prefab`: player 1 gets element 0, player 2 element 1, and
so on, wrapping around when players outnumber entries. The roster is `[Annie, Bun Jovi,
Scannor]` (August 2026 model drop — the new-style Annie replaced the old one in place, same
prefab GUID). Use that roster field — not the manager's own *Player Prefab*, which Unity hides
while Join Behavior is *Manual*.

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
- Sticky luggage can't be voluntarily dropped or thrown. Station placement, stealing, hazards,
  and joint breaks can still force it loose.

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

Measured on the August 2026 bodies (all three share skeleton proportions at import scale 5):

| | |
|---|---|
| Arm, shoulder → wrist | **0.84** |
| Shoulder → grip, case on `GrabAnchor` | **~1.45** |

Better than the old model (0.763 against ~1.76) but the grip still sits beyond the arm, so the
`maxReach` clamp still does the work — the hands get close enough to read as holding the case.

### All three bodies are one rig

Annie, Bun Jovi and Scannor share the **same skeleton at the same proportions** — shoulder
`2.358`, head bone `2.681`, feet `0.182`, identical on all three. Only the silhouette differs:
Annie's hat and hair reach `4.27`, Bun Jovi's ears `4.89`, Scannor's bare head `3.84`.

Because of that, **every gameplay value on the three prefabs is identical, deliberately** — the
capsule included (`height 3.85`, `center y 1.925`, `radius 0.726`). A capsule sized to each
mesh would size it to hair and ears, giving Bun Jovi a collider `1.2` taller than Scannor's for
the same body, so one character would be stopped by gaps another walks through. `3.85` wraps
every body (Scannor's mesh is the tallest bare skull at `3.839`) and sits below the `4.11` top
the game originally shipped with, so it cannot collide with anything the old capsule cleared.
Hair, hat and ears intentionally poke above it.

> If you change a feel value — speed, grab radius, joint spring, capsule — **change it on all
> three prefabs.** They are meant to be interchangeable bodies, not balance variants. A diff of
> all 1020 serialized properties across the three should come back empty.

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

**Controllers:** `Assets/Animation/{Annie/Annie, Bun Jovi/Bun Jovi, Scannor/Scannor}.controller`
— three copies of the same graph, each pointing at its own FBX's clips (takes named
`AN_1-idle-Loop`, `AN_2-jog`, `AN_4-throw_Enter`, `AN_5-throw-Loop`, `AN_6-throw-End`,
`AN_7-throw-loop-jog`, `AN_8-dash`, with loop flags set on the importer). Annie's spare
`AN_3-jog-Stop` and the others' `AN_1-idle-RARE` are imported but unused. Parameters
`isMoving`, `isDashing`, `isGrabbing`, `isThrowing`, all bool, all hashed once in `AnimId`.
A graph change must be made three times — or made once and re-copied with motions swapped.

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
