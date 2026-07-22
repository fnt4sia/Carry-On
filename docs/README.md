# Carry On technical documentation

This is the entry point for the Unity project. Open the smallest topic that matches the work; do not load the entire folder by default. Code and serialized Unity assets are the source of truth. These docs were reconciled with the refactored project on 2026-07-14.

## Core

- [Project overview](core/project-overview.md) — pitch, stack, repository layout, scenes, and runtime flow.
- [Architecture and services](core/architecture-and-services.md) — assembly boundaries, singletons, level context, events, and dependencies.
- [Asset and prefab guidelines](core/asset-and-prefab-guidelines.md) — what belongs in prefabs, tuning assets, level configs, and scene overrides.

## Gameplay

- [Luggage](gameplay/luggage.md)
- [Conveyors](gameplay/conveyors.md)
- [Delivery gates](gameplay/delivery-gates.md)
- [Machine stations](gameplay/machine-stations.md)
- [World interactables](gameplay/world-interactables.md)

## Player and UI

- [Join and spawn](player/join-and-spawn.md)
- [Movement and dash](player/movement-and-dash.md)
- [Grab, carry, and throw](player/grab-carry-throw.md)
- [Main menu](ui/main-menu.md)
- [Game HUD](ui/game-hud.md)
- [Stage-select UI](ui/stage-select-ui.md)

## Levels and systems

- [Level configuration](levels/level-configuration.md)
- [Stage select](levels/stage-select.md)
- [Level authoring](levels/level-authoring.md)
- [Round and scoring](systems/round-and-scoring.md)
- [Audio](systems/audio.md)
- [Camera](systems/camera.md)
- [Scene loading](systems/scene-loading.md)
- [Save and progression](systems/save-and-progression.md)

## Development

- [Design-scene testing](development/design-scene-testing.md)
- [Level validator](development/level-validator.md) — `Carry On ▸ Validate…`, catches unwired levels and conveyor seam errors.

When a system changes, update its focused doc in the same pass. Keep high-level routing here and detailed behavior in one canonical topic; link instead of copying sections between files.
