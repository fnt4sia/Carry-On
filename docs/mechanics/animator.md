# Animator

**Controllers:** `Assets/Animation/{Annie/Annie, Bun Jovi/Bun Jovi, Scannor/Scannor,
ZipZub/ZipZub}.controller` · **Mask:** `Assets/Animation/UpperBody.mask` · **Scripts:**
`Game/Core/AnimId.cs`, `Game/Player/{PlayerMovement, PlayerGrab}.cs`, `MainMenu/MainMenuManager.cs`

The character animator graph. Split out of [player](player.md) in October 2026, which still covers
what drives it: movement, the dash and the grab.

## Controllers and clips
Four copies of the same graph, each pointing at its own FBX's clips (clips named
`AN_1-idle-Loop`, `AN_2-jog`, `AN_4-throw_Enter`, `AN_5-throw-Loop`, `AN_6-throw-End`,
`AN_7-throw-loop-jog`, `AN_8-dash`, with loop flags set on the importer). The others'
`AN_1-idle-RARE` takes are imported but unused. Parameters `isMoving`, `isDashing`, `isGrabbing`,
`isThrowing` (bool) and `dashProgress` (float), all hashed once in `AnimId`. A graph change must be
made four times — or made once and re-copied with motions swapped. The jog state is named
`Annie Move` in all four, and [footsteps](../services/audio.md#components) find the jog by that
name (`AnimId.MoveState`) — rename it in one controller and that character goes silent.

**Annie runs on `Annie@animation_v2.fbx` since 20 Oct 2026.** Same mesh size and `DEF-` skeleton,
plus 22 eye and eyelid bones, but the export renamed the armature node from `root` to `Armature`
(bone paths are `Armature/root/DEF-*`, not `root/root/DEF-*`) and renumbered the takes:
`AN_3-throw_Enter`, `AN_4-throw-Loop`, `AN_5-throw-End`, `AN_6-throw-loop-jog`, `AN_7-dash`, plus
the new `AN_2-idle-RARE`. Unity names a file's first take after its `@` suffix, so the dash arrives
as `animation_v2` and is renamed on the importer. Compared bone by bone, the jog, throws and dash
match the old file to 0.0° (the dash keeps the frames 2–10 trim); the idle loop is the same poses
retimed to **2.57 s** (was 3.40 s), so her idle now runs ~30% faster than the other three.
Because the paths changed, the old clips can't drive the new mesh or the reverse: the model, all
seven motions, the mask and the hand-IK bones moved together. `Annie_Anim.fbx` is kept, unused.

**Scannor's clips run slower.** Every FBX keys the same frame counts, but Scannor was exported at
**24 fps** while Annie, Bun Jovi and ZipZub are **30 fps**, so Scannor's idle is 4.25 s against
3.40 s and every state of his plays at 0.8× the others' speed. The poses are identical at the
same normalized time — only the timing differs.

## Layers
Two layers since 19 Oct 2026. The legs always come from the **Base Layer**; the throw lives in
**Upper Body**, an override layer masked by `UpperBody.mask` (one mask for all four). The mask
covers `DEF-spine.001` and everything under it; the pelvis (`DEF-spine`) and the legs stay with the
base. Before the split, the throw states were on the base layer, and their wind-up and release
clips stood the legs still for about a second of every throw, even mid-run.

**A mask drops every transform it doesn't list.** So it lists each path twice — under `root/` (the
August exports) and under `Armature/` (Annie's v2) — 164 entries. With only the `root/` set,
Annie's throw silently did nothing: 0° of arm movement, measured. A body exported with yet another
armature name needs its paths added the same way.

```text
Base Layer                       Upper Body (UpperBody.mask)
Idle ⇄ Move     isMoving         Empty (default, no motion, write defaults off)
AnyState → Dash isDashing        Empty → Throw Start              isThrowing
Dash → Idle/Move !isDashing,     Throw Start → Loop Idle/Walk     after one cycle, split on isMoving
                 split on        Loop Idle ⇄ Loop Walk            isMoving
                 isMoving        Loop */Throw Start → Throw End   !isThrowing
Idle Rare → Idle at clip end     Throw End → Empty                exit time 0.8
Idle Rare → Move isMoving        Throw End → Throw Start          isThrowing (instant re-charge)
(Annie only, nothing leads in)
```

Every Upper Body state has write defaults off (with it on, `Empty` snaps the arms to the bind pose).
But such a layer **keeps writing the last pose it played**: after the first throw the arms stayed
frozen in the follow-through (63° off idle), even through later carries and drops. So
`PlayerGrab.UpdateUpperBodyWeight` fades the layer to 0 over 0.15 s whenever it sits in `Empty`, and
back to 1 when it leaves; `Empty → Throw Start` is instant so the stale pose never blends back in.

## The dash clip
**The dash clip is played by `PlayerMovement`, not by its own clock.** The `Dash` state's *Motion
Time* is `dashProgress`, which `DashCoroutine` runs from 0 to 1 over `dashClipDuration` (0.25 s).
The speed boost lasts only `dashDuration` (0.15 s); the clip runs on through the slowdown after it
(at 0.15 s it played at 1.8× and looked twitchy). `isDashing` spans the whole clip. The dash take
is trimmed to **frames 2–10** on every character's animation FBX: the dip, the lunge (peak at
frame 7) and the landing. Frames 13–25 were a dead standing pose that stood the character upright
between the lunge and the jog. Frame 10 matches jog frame 0 (left foot leading by 1.58 vs 1.56,
pelvis 1.65 vs 1.63), so `Dash → Move` blends in 0.08 s at offset 0; `Dash → Idle` takes 0.15 s.

## Carrying and throwing
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

## Rare idle (lobby only)
`Idle Rare` (Annie only: `AN_2-idle-RARE`, 6.5 s — knuckle crack, watch check, neck stretch, with
eye blinks) is **entered from code only**: no transition leads into it. In the lobby,
`MainMenuManager.IdleRareLoop` crossfades into it (0.25 s) `idleRareFirstDelay` (2 s) after a
character joins, then again every `idleRareInterval` (20–30 s, random per character), skipping a
beat if the base layer is mid-transition. It hands back to `Annie Idle` at its end in 0.1 s — the
clip starts and ends exactly on idle frame 0, so neither hand-off pops — and leaves for
`Annie Move` if `isMoving` comes on.

Gameplay never plays it: the coroutine lives on the lobby's manager and dies with that scene, and
stage select deactivates the players, which resets their animators. A body whose controller has no
`Idle Rare` state is skipped, so giving another character one is a graph change on that controller
alone — ZipZub's FBX already carries an `AN_1-idle-RARE` take.
