# Audio

**Scripts:** `Game/Core/{AudioManager, Sfx, LoopingSound}.cs`, `Game/Player/PlayerFootsteps.cs`,
`MainMenu/MenuAnnouncements.cs`, the pass sound in `Game/World/AmbientTrafficSpawner.cs`, and the
transition fade in `Game/Services/SceneLoader.cs` — **Prefab:** `Prefab/Manager/AudioManager.prefab`
holds the sound list; `Resources/Runtime/AudioManager.prefab` is its no-override variant that boots it.

## Rows: one place to tune every sound

Every sound is a **row** on the AudioManager prefab: a name, a clip, a **volume** (0–1) and a
**pitch range** (each play picks a pitch between min and max; 1–1 plays it as recorded; music
ignores pitch). Code and components ask for a sound by row name and never carry their own volume,
so the whole mix is one Inspector list, grouped Music → UI → Round → Player → Luggage → World →
Ambience.

The volume that plays is **row volume × the caller's share**. The caller's share is only for
behaviour: a luggage hit is louder the harder it lands, a plane pass is louder the closer it comes,
a fade-in or fade-out. Nothing else scales a sound.

**Tuning by ear.** Enter Play, select `AudioManager(Clone)` under DontDestroyOnLoad, drag the
sliders. Volumes are rebuilt from the rows every frame, so music and loops change while they play;
a one-shot picks up its new values on its next play. Play-mode edits are lost on Stop, so before
stopping: the component's ⋮ menu → *Copy Component*; after stopping: select
`Prefab/Manager/AudioManager.prefab` → *Paste Component Values*.

**Volume can only turn a sound down** (Unity caps an AudioSource at 1). A clip that is still too
quiet at 1 needs a louder export, not a bigger number. The October 2026 starting values were
level-matched from measured loudness: the four footsteps against the quietest (Step 3), the djapf
menu track down to the old menu track's level (0.27), the announcement down to its chimes (0.12).

## Methods

| Method | Use |
|---|---|
| `PlaySFX(id, volume)` | one-shot on the shared pool (8 sources; when all are busy, source 0 is reused) |
| `PlayLoopingSFX(id, volume, fadeIn)` | a loop on its own source, returned |
| `PlayDedicatedSFX(id, volume)` | a one-shot on its own source: long sounds the pool could cut, or ones shaped while they play; cleans itself up |
| `SetSFXVolume(source, volume)` | changes a looping/dedicated sound's own share |
| `StopSFX(source, fadeOut)` | stops a looping/dedicated sound, optionally fading |
| `PlayMusic(id, seamFade)` / `StopMusic(duration)` | cross-fade to a track / unscaled fade out |
| `PlayMenuMusic` / `PlayGameplayMusic` | the two scene tracks |

A missing ID warns once instead of failing silently. Code uses `Sfx` constants, never raw strings;
`Sfx.LuggageCollision` builds the tiered collision ID in one place. Components that need a sound
picked per prefab or per scene (a loop, a character's step, a traffic group's pass) take the row
name as a serialized string instead.

**Every row must be asked for** by an `Sfx` constant, one of the two music fields, or a sound field
on a component (`LoopingSound.sound`, `PlayerFootsteps.stepSound`, a traffic group's `passSound` —
the AmbientTraffic one is a prefab override in MainMenu). A row nothing asks for is dead; delete
it. When adding audio: file into the folder below, add a row, then a constant or a component field.

## Where each sound plays

| Row | Played by | Heard in |
|---|---|---|
| Menu Music | `AudioManager` when the lobby loads, once the transition's clouds have parted | lobby only |
| Gameplay Music | `GameManager` after the countdown | levels |
| Button Select | `MainMenuManager`, `GameHUD` button handlers | menu, pause, end card |
| Player Join | `PlayerSystem.JoinPlayer`, the single join entry point | lobby |
| Stamp, Star, wrong | `LevelTicket`, `MapController`, `GameHUD`, `MainMenuManager`, `DispatchGate` | all three |
| Start, Time's Up, Score | `GameManager`, `DispatchGate` | levels |
| Player Dash, Throw buildup/throwonly | `PlayerMovement`, `PlayerGrab` | levels |
| Footstep Annie / Bun Jovi / Scannor / ZipZub | `PlayerFootsteps` on each character prefab | levels |
| ground/window Luggage Collision 1–3 | `Luggage` | levels, menu belt |
| Conveyor Hum | `LoopingSound` on MainMenu's `FullsetConveyor` (trial: menu only) | menu |
| Announcement Bell → Announcement → Announcement Bell End | `MenuAnnouncements` (`Menu Announcements` object) | menu |
| Plane Flyby | AmbientTraffic's Planes group | menu |
| Map Plane Idle | `LoopingSound` on `Map Plane.prefab`, one per player's plane | stage select |
| Sea Wind | `LoopingSound` on ChooseStage's `Environment/Water` | stage select |

## Music

**Only the lobby has music.** Stage select is ambience only (sea wind, the planes' idle), and a
level's music waits for its countdown. `GameManager` starts gameplay music after the countdown and
fades it back out over 1.25 s in `EndRound`, so the result card plays over the Time's Up sting
instead of the gameplay loop.

**A scene change fades the music away, it never cuts or carries over.** The moment a transition
starts, `SceneLoader` fades the music out over `musicFadeOut` (1.5 s: the clouds cover in 0.85 s,
the plane is in by 1.5 s). The lobby's music then waits for `SceneLoader.IsLoading` to clear —
the clouds fully parted — before fading in over 1 s; with no transition (the first scene, or Play
pressed in a scene) it starts at once. Fades use unscaled time, so pause and the result screen
don't freeze them — which is the whole reason the round-end fade works at all, since `EndRound`
sets `timeScale` to 0 on the same frame.

**The djapf menu track isn't cut to loop**: it plays at full level to its last sample, and that
sample jumps 0.30 back to the first (normal sample-to-sample movement is 0.02), so a plain loop
pops every 5:53. `menuMusicSeamFade` (2 s) dips the volume before the loop point and brings it
back in 0.5 s. `unmastered looping.wav` (gameplay) is a real seamless loop and plays plainly; the
mastered theme wasn't used for gameplay because it isn't. Set the seam fade to 0 if a loop-ready
menu track arrives.

**Music streams from disk** (load type Streaming on every `Music/` clip). The menu track is 5:53 of
24-bit audio; decoded in memory it would be ~68 MB, unpacked on the main thread at boot.

## Components

- **`LoopingSound`** plays one row as a loop while its object is enabled, fading in and out
  (`fade`, 0.5 s; the sea uses 1 s) so a scene change never cuts it dead. It goes on the object that
  makes the sound, so each fullset or plane has exactly one.
- **`PlayerFootsteps`** plays the character's step each time the jog loop passes a frame where a foot
  lands. Measured on `AN_2-jog` (identical poses on all four characters): left foot down at 0.00 of
  the loop, right at 0.47. So steps follow the legs — 3.5 a second, Scannor's 24 fps jog 2.8 — and
  stop on idle, dash and falls faster than `airborneFallSpeed`. It reads the base-layer state
  `Annie Move` (`AnimId.MoveState`), the jog's name in all four controllers.
- **`MenuAnnouncements`** waits 15–20 s, plays the chime, starts the voice 2.8 s in (when the chime's
  tone is done), and the closing chime when the voice ends — pitch included, since pitch changes its
  length. Leaving the menu mid-announcement fades it out.
- **Traffic pass sound** (`passSound`, `passSoundPeak`, `passSoundRange` on a traffic group): the
  clip's loudest moment (5.9 s into the flyby) is lined up with the vehicle's closest pass to the
  camera, so a trip starts the clip partway in with a 0.3 s ease-in. After the peak it fades to
  nothing just before the clip's own end, which is cut off. Loudness is `1 − closest / range`, and a
  pass that stays beyond the range is silent. In the menu, passes come 288–482 units close, so
  range 600 gives 0.2–0.52. The pan follows the plane across the screen.

## Files

`Assets/Audio/` splits `Music/` from `SFX/`, and sorts SFX by what makes the sound, not by scene:
most clips play in more than one scene (`wrong` plays in all three).

| Folder | Clips |
|---|---|
| `Music/` | djapf main-menu track, unmastered looping (gameplay); old: Main Menu, Carry On Main Theme Mastered |
| `SFX/UI/` | Click Button, Player Join, Stamp, Star, wrong; Player Join2*, Player Leave*, Pop Up Level*, UI collecting stars*; old: Button Select, Hover Button (button hover was tried and dropped, 21 Oct 2026) |
| `SFX/Round/` | Start, Time's Up, Score |
| `SFX/Player/` | Player Dash, Throw buildup, Throw throwonly, Gameplay Step 1–4; Gameplay Step 5* |
| `SFX/Luggage/` | ground/window Luggage Collision 1–3 |
| `SFX/World/` | Conveyor, Plane idle — loops that belong to an object |
| `SFX/Ambience/` | Bell Announcement Ring (+ end), Announcement low noise, Plane passing by long reverb delay, Sea Wind super LONG; Announcement*, Plane passing by long/medium/short*, Sea Wind* |

\* Delivered October 2026 and not wired yet: no row, so nothing plays them. They are waiting to be
implemented, not dead — don't delete them in a cleanup. "old" files were replaced by the October
delivery and have no row either. Keep delivered file names, so a re-export from the sound designer
drops in over the old file and keeps its GUID. Code finds a clip by its row, never by file name or
folder, so moving or renaming a file is safe.

## Editor gotcha: audio playing fast

If the Mac's audio output changes after Unity starts (headphones, Bluetooth), the editor can keep
mixing at the old rate. On 20 Oct 2026 it mixed at 44.1 kHz into the 48 kHz MacBook speakers:
every sound played 8.8% fast and ~1.5 semitones sharp, measured as `AudioSettings.dspTime` running
1.088× wall-clock time. Restart Unity before tuning by ear (`AudioSettings.Reset` in Play also
fixes the session).
