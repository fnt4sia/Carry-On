# Audio

**Scripts:** `Game/Core/AudioManager.cs`, `Sfx.cs` — **Prefab:** `Resources/Runtime/AudioManager.prefab`

`AudioManager` builds a case-insensitive clip dictionary, one music source, and a small one-shot
SFX source pool. Retained looping effects get dedicated sources, so a busy one-shot can't steal
them.

| Method | Use |
|---|---|
| `PlaySFX(id, volume)` | one-shot on an available source |
| `PlayLoopingSFX(id, volume)` | returns a source for charged or looped effects |
| `StopSFX(source)` | releases a loop |
| `PlayMusic(id)` | cross-fades to a registered clip |
| `StopMusic(duration)` | unscaled fade out |
| `PlayMenuMusic` / `PlayGameplayMusic` | named scene modes |

If every one-shot source is busy, source 0 is reused. A missing clip ID warns once instead of
failing silently.

Gameplay code uses constants from `Sfx`, never raw strings: Start, Time's Up, Star, Stamp, Score,
Wrong, Button Select, dash, throw buildup and release, and the three ground/window luggage
collision tiers. `Sfx.LuggageCollision` builds the tiered collision ID in one place. Menu and
game music names are serialized service fields, because they select registered clips rather than
one-shot IDs.

When adding audio, register the clip on the runtime prefab and add or reuse a constant in
`Sfx.cs`. Don't scatter a new literal through callers.

On a level load, menu music fades out; `GameManager` starts gameplay music after the countdown and
fades it back out over 1.25 s in `EndRound`, so the result card plays over the Time's Up sting
instead of the gameplay loop. Menu and stage-select scenes play menu music. Fades use unscaled
time, so pause and the result screen don't freeze them — which is the whole reason the round-end
fade works at all, since `EndRound` sets `timeScale` to 0 on the same frame.
