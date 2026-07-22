# Scene loading

**Scripts:** `Game/Services/SceneLoader.cs`, `Game/Core/SceneNames.cs` — **Runtime prefab:** `Assets/Resources/Runtime/SceneLoader.prefab`

`SceneLoader` is the only application-level transition path. It bootstraps before scene load, persists, rejects concurrent loads, validates the target against Build Settings, and runs an asynchronous transition:

```text
activate loading canvas -> unscaled fade in
  -> LoadSceneAsync with activation held
  -> update progress -> activate at 90%
  -> reset timeScale -> unscaled fade out -> hide canvas
```

Static helpers load the lobby and stage select through names centralized in `SceneNames`. Menu, map, HUD, and round buttons delegate to this service; do not add direct synchronous `SceneManager.LoadScene` calls.

The runtime prefab provides a swappable loading-screen visual. If the prefab or references are unavailable during direct testing, the service builds a minimal overlay fallback, so mechanics remain testable without presentation assets.

Because fades use `Time.unscaledDeltaTime`, transitions work from pause and the time-frozen result screen. `Time.timeScale` is restored after scene activation.
