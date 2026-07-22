# Audio

**Scripts:** `Game/Core/AudioManager.cs`, `Sfx.cs` — **Runtime prefab:** `Assets/Resources/Runtime/AudioManager.prefab`

`AudioManager` bootstraps before scene load, persists, and uses the shared singleton lifecycle. It builds a case-insensitive clip dictionary, one music source, and a small one-shot SFX source pool. Retained looping effects receive dedicated sources so a busy one-shot cannot steal them.

## API

| Method | Use |
|---|---|
| `PlaySFX(id, volume)` | one-shot on an available source |
| `PlayLoopingSFX(id, volume)` | returns a source for charged/looped effects |
| `StopSFX(source)` | releases a loop |
| `PlayMusic(id)` | cross-fades to a registered clip |
| `StopMusic(duration)` | unscaled fade out |
| `PlayMenuMusic` / `PlayGameplayMusic` | named scene modes |

If every one-shot SFX source is busy, source 0 is reused. A missing clip ID produces one warning instead of failing silently.

## IDs

Gameplay code uses constants from `Sfx` rather than repeating strings. These include Start, Time's Up, Star, Stamp, Score, Wrong, Button Select, dash, throw buildup/release, and the three ground/window luggage collision tiers. `Sfx.LuggageCollision` builds the tiered collision ID in one place. Menu/game music names are serialized service fields because they select registered clips rather than one-shot gameplay IDs.

When adding audio, register the clip on the runtime prefab and add/reuse a constant in `Sfx.cs`; do not scatter a new literal through callers.

## Scene behavior

On a level scene load, menu music fades out. `GameManager` starts gameplay music after countdown. Menu and stage-select scenes play menu music. Fades use unscaled time so pause and results do not freeze them.

No level should contain its own `AudioManager` instance. To replace or reconfigure the service, edit the manager authoring prefab and its `Resources/Runtime` variant deliberately.
