# Tune player feel

Movement/dash live in one asset, grab/throw in another. Both are ScriptableObjects — edit
the numbers, press Play, repeat. Changes apply to **all** players (shared tuning).

You can edit these **while in Play mode** to feel a change live, but Unity discards
Play-mode edits on Stop — note the value, then re-enter it after stopping.

## Move & dash — `Assets/Config/Tuning/Player_Default.asset`

Fields on `PlayerConfig` (`Assets/Script/Game/Config/PlayerConfig.cs`):

| Field | Default | What it does |
|---|---|---|
| Movement Speed Normal | 10 | Top ground speed. |
| Rotation Speed | 4 | How fast the body turns toward the move direction. |
| Grab Rotation Speed Multiplier | 1 | Scales turn speed while carrying (1 = same). Lower = heavier feel with luggage. |
| Lerp Speed | 0.15 | Velocity smoothing (0–1). Lower = floatier/slower to react, higher = snappier. |
| Dash Speed Multiplier | 2 | Dash burst = normal speed × this. |
| Dash Duration | 0.15 | Seconds the dash burst lasts. |
| Dash Cooldown | 1 | Seconds before dash is available again. |

## Grab / carry / throw — `Assets/Config/Tuning/Grab_Default.asset`

Fields on `GrabConfig` (`Assets/Script/Game/Config/GrabConfig.cs`).

**Designer knobs (safe to tweak):**

| Field | Default | What it does |
|---|---|---|
| Grab Radius | 1.4 | How close you must be to grab luggage (also enables stealing at range). |
| Grab Align Duration | 1.25 | Seconds to pull grabbed luggage into carry pose. |
| Throw Min / Max Hold Time | 0.25 / 1.5 | Hold window that maps to throw strength. |
| Throw Min / Max Force | 200 / 800 | Forward throw force at min/max hold. |
| Throw Min / Max Up Force | 100 / 400 | Upward arc at min/max hold. |

**Advanced (physics joint — change carefully, small steps):** the Bridge Collider, Joint
Limits, Joint Drives, and Projection/Break groups control the ConfigurableJoint that holds
luggage. Wrong values make carrying jittery or snap the joint. Leave at defaults unless
carrying feels broken; tweak one group at a time and playtest.

## Where these get read

Each player prefab references the tuning asset in its inspector (`PlayerMovement` →
Player Config, `PlayerGrab` → Grab Config). One asset, every player. To A/B two feels,
duplicate the asset and swap the reference on the character prefab.
