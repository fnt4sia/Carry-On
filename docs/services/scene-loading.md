# Scene loading

**Scripts:** `Game/Services/SceneLoader.cs`, `Game/Services/SceneTransition.cs`, `Game/Core/SceneNames.cs`
**Prefab:** `Resources/Runtime/SceneLoader.prefab` (a variant of `Prefab/Manager/SceneLoader.prefab`,
which holds the view)

`SceneLoader` is the only application-level transition path. It rejects concurrent loads,
validates the target against Build Settings, and runs an async transition:

```text
show the Loading Screen canvas -> clouds cover, plane flies in and stops in the middle
  -> LoadSceneAsync with activation held -> wait for 90%
  -> reset timeScale -> activate -> one frame under the cover
  -> plane flies on, clouds part -> hide canvas
```

Static helpers load the lobby and stage select through names centralised in `SceneNames`. Menu,
map, HUD, and round buttons all delegate here — never add a direct synchronous
`SceneManager.LoadScene`.

## The cloud transition
Built on 9 Oct 2026 from `Assets/UI/Transition/` (`Cloud1`, `Cloud2`, `Airplane`), replacing the
old dark fade and progress bar. Everything is authored under `Loading Screen` (Overlay, sort 32767,
CanvasScaler 1920×1080 in **Shrink** mode — the canvas is never bigger than the reference, so a cover
laid out for 16:9 covers every aspect ratio):

```text
Loading Screen     Canvas + CanvasGroup + SceneTransition
  Sky              full-screen fill #DDEFF6 behind the clouds; plugs any gap once they've settled
  Clouds Top Left  bank of 6 clouds that come in from (−1500, +1000)
  Clouds Right     bank of 6 clouds that come in from (+1900, +120)
  Plane            moved along the flight path
    Shadow         the plane sprite, navy at 28 %, offset (40, −60)
    Body           the plane sprite, 1000 px
```

**Each cloud's authored pose is where it sits once the screen is covered**, so the cover is
rearranged in the prefab, not in code; a bank's `offset` is where its clouds start and they leave
the same way, growing to `exitScale` (1.6×) and travelling 1.6× as far so even the biggest is
fully off screen by the end. Clouds in a bank roll in one after another (`cloudStagger` 0.05 s,
`cloudSeconds` 0.6 s each), growing from `enterScale` 0.5.

One clock drives every piece, so nothing can jump between phases: the plane decelerates into the
middle (cubic ease-out) and accelerates out (cubic ease-in) — zero speed at both ends of the wait
— and its bob (16 px, 1.6 s) and bank (±3°) fade in during the approach. Its shadow drops further
as it rises, which is what reads as height. The clock runs on unscaled time with its step capped at
1/20 s, so a hitch slows the plane for a frame instead of teleporting it.

| | Seconds (min) |
|---|---|
| clouds cover | 0 – 0.85 |
| plane flies in (`planeLead` 0.25 before the cover completes) | 0.6 – 1.5 |
| **load starts** — only once the plane is hovering | 1.5 |
| plane waits at least `minHoverSeconds` | to 2.0 |
| plane flies on (`departSeconds` 0.7); clouds part 0.15 s later | 2.0 – 3.0 |

**Load hitches only ever land in the hover.** In the editor, *starting* `LoadSceneAsync` cost a
259–353 ms frame and activation 631–1068 ms. Before the load waited for the plane, the start hitch
landed mid-approach and froze the plane in flight; now both happen while it bobs in place (it moved
2 px across the 1 s activation frame). Measured in Play, ChooseStage → Level1 and back: no frame
over 34 ms outside the hover.

**Direction** follows Build Settings order: loading a scene at or after the current one (lobby →
map → stage, or a replay) flies top-left → bottom-right; going back flies
bottom-right → top-left. The plane sprite's nose points up-left (`spriteHeading` 133.5°); the code
turns it onto the path (`pathAngle` 35° below horizontal).

`Time.timeScale` is reset to 1 **before** activation, so a scene that freezes time in `Start` keeps
it frozen — a round's countdown does — and `GameManager` holds its "3" until
`SceneLoader.IsLoading` clears, so the countdown is never spent under the clouds
([round flow](../mechanics/delivery-and-scoring.md)).

The view starts and ends pixel-identical to the bare scene (checked by rendering both ends against
the map, both directions), so showing and hiding the canvas never pops. Preview frames and a GIF of
the forward run were left in `Assets/Screenshots/Consult/transition_forward*`.

If the prefab or its `transition` reference is unavailable during direct testing, the service
builds a minimal overlay fallback that just fades, so mechanics stay testable without
presentation assets. Fades and the transition use unscaled time, so they work from pause and from
the time-frozen result screen.
