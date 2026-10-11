# Workflow

How work arrives in the project and how to land it cleanly. The folder layout itself — and where a
new file goes — is in [CLAUDE.md](../CLAUDE.md#how-the-project-is-laid-out).

## Merging Carstenz's pushes

Carstenz also pushes to `redesign` (stage-select map art, level layouts), often from a tree older
than the current layout. After merging their work:

1. **Check new files against the layout.** Anything at the `Assets/` root or in a type folder that
   no longer exists goes where the layout says, moved inside Unity (or with
   `AssetDatabase.MoveAsset`) so references survive. The 8 Oct 2026 terrain landed as
   `Assets/New Terrain.asset`; it now lives at `Assets/Terrain/ChooseStage TerrainData.asset`.
2. **Check every GUID they reference exists.** An asset-pack prefab can arrive without its
   material — `Prefab/StageSelect/vfx_Tornado_01` still renders magenta because its particle
   material was never pushed.
3. **Untick *Import Cameras* and *Import Lights* on any new FBX.** Blender exports carry the Blender
   scene's camera and lights, and an imported camera renders over the game — `plane1.fbx`'s hid the
   whole stage-select map until 1 Oct 2026.
4. **Check shared assets they touched.** A commit about one scene can change something every scene
   uses: the 8 Oct terrain commit also turned the shared post-processing filter pink
   ([look](look.md#post-processing-and-palette)).
5. **Check empty folders.** Git can carry a folder's `.meta` without the folder's files — the 8 Oct
   commit added an empty `Model/Environment/Select Stage/`.

## Layout history

- **There is no `New…` prefab folder anymore.** The old `Environment` / `Decoration` sets were
  retired in July 2026 and the `NewEnvironment` / `NewDecoration` folders took their names.
- **30 Sep 2026** moved `Shader/Water.mat` → `Material/World/` and `Prefab/Vfx/vfx_Tornado_01` →
  `Prefab/StageSelect/`; VFX prefabs go by area since then, never into a type folder.
- **22 Sep 2026** (`0f39a36`) moved `Level2` into `Scenes/Archive/` while it stayed live, and deleted
  the `Test` decoration-staging scene ([levels](scenes/levels.md#archived-scenes)).

## Git hygiene

- Never commit secrets or build artifacts. `.gitignore` covers `Library/`, `Temp/`, `*.csproj`,
  `*.sln`, `*.slnx`, builds and logs. `*.slnx` (the IDE package's newer solution format) was only
  added on 30 Sep 2026, after `Carry-On.slnx` had been committed in `9f2313a` — so the file is still
  tracked and shows as modified whenever Unity regenerates it. `git rm --cached Carry-On.slnx`
  stops that.
- `Assets/Screenshots/` is git-ignored: MCP and debug captures. Comparison images for a consult go
  in `Assets/Screenshots/Consult/`.
