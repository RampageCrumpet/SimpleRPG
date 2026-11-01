# Procedural room geometry

Rooms can build their own structure at runtime inside the footprint and connections the layout already chose. Authored prefab rooms and procedural rooms mix freely in one dungeon. This covers geometry only: no monsters, loot, props or lighting.

## Pipeline

```
Layout (final)  ->  RoomGenerationContext  ->  IRoomGenerator  ->  GeneratedRoom (plan)
                                                                      |
                              RoomPlanValidator  <--------------------+
                                     |  (retry from derived seeds, then fallback open chamber)
                                     v
                       RoomMeshBuilder  ->  one mesh, one MeshCollider under the room root
```

Nothing is generated while the layout search runs. `LevelSpawner.Spawn` calls `ProceduralRoomBuilder` once per accepted room, right after instantiating its prefab.

## Code map (`Assets/Scripts/World/RoomGeneration/`)

| Layer | Types | Notes |
| --- | --- | --- |
| Plan, pure (no scene objects) | `GeneratedRoom`, `GeometryPrimitives` (`RectMm`, `BoxMm`), `RoomGenerationContext`, `IRoomGenerator`, `RoomGenerationSettings`, `ConnectionMap`, `RoomShell`, `StairPlanner`, `RoomPlanValidator`, `RoomPlanChecksum`, `RoomSeed`, `RoomGenerationPipeline` | Testable without Unity objects. |
| Algorithms | `ChamberGenerator` | Corridor and maze generators are not written yet. |
| Unity (`Construction/`) | `RoomMeshBuilder`, `ProceduralRoomSettings`, `ProceduralRoomBuilder`, `ProceduralRoomChecks` | The only code that touches meshes, colliders or prefabs. |

`IRoomGenerator` depends on none of the Unity layer. Its input is the footprint in layout cells, the number of levels, the layout's own `DoorDefinition`s, a seed and settings.

## Conventions

- **Units.** Every length in a plan is a whole number of millimetres (`int`). Decisions, validation and checksums never use floating point. Metres appear only when the mesh is built.
- **Axes.** X and Y are the layout grid's x and y (world x and z), Z is up. The origin is the room's lowest corner, unturned; the instance transform supplies position and rotation, exactly as for authored rooms. The geometry object is offset by `OriginOffset`.
- **Three independent scales** (`RoomGenerationSettings`): the layout cell (`CellSizeMm`, from the roomset, fixes where doors are), the planning resolution (`PlanningResolutionMm`, what generators snap free choices to; never used for doors), and physical architecture (wall, floor and ceiling thickness, ceiling and opening height, minimum passage, stair width, riser and tread). Nothing is derived from the roomset cell size except door positions.
- **Doors never move.** `ConnectionMap` converts each `DoorDefinition` to an opening and a protected region (the doorway through the wall plus an approach in front of it) by `point * cellSize`, exactly. The validator rebuilds the map independently and compares it with the plan.
- **Walls are not cells.** A plan has separate lists: floor surfaces (with elevation and slab thickness), ceiling slabs, walls (a rectangle of any thickness between two heights), free solids (pillars), stair runs, openings and protected regions. `GeneratedRoom.ResolvePieces()` cuts the openings out of the walls and expands the steps; the mesh, the collider and the obstruction checks all use that one list.
- **Levels.** A room `Height` of n has n levels, `FloorHeightMm` apart. A plan has a floor per level used (lowest to highest door level), a well cut in a level's floor wherever stairs arrive from below, and a ceiling over the highest.

## Stairs

`StairPlanner` sizes a staircase from physical dimensions: the fewest steps whose risers are at most `StepHeightMm`, each tread `StepDepthMm` deep, `StairWidthMm` wide. If one straight flight does not fit, it switches back side by side in 2 to `MaxStairFlights` flights joined by `LandingDepthMm` landings. Step heights use integer division, so the top step lands exactly on the next level. A chamber puts a run between every pair of consecutive levels its doors span, tries positions and orientations from its own seeded stream, and keeps the first the validator accepts. Impossible stairs return a failure that says how much space they need.

Supported: doors on different levels of one room, joined by stairs inside it. Not supported: doors of a vertical connection type (`FloorDelta != 0`, ladders and shafts), which are rejected with a diagnostic.

## Validation (`RoomPlanValidator`)

Independent of any generator: plan connections match the layout's; nothing outside the footprint or the room's height; nothing in a doorway or its approach; stair risers, treads, widths, landings and continuity; headroom over every tread; and reachability. Reachability rasterises each level at `ValidationResolutionMm` and slides a square of `MinPassageWidthMm` through it (so narrow gaps fail), with stairs as links between the squares before the first step and past the last. Every connection's approach must be reachable from the first one, and a room must have a usable interior beyond its doorways.

### How the raster chooses its resolution and stays conservative

- **Resolution.** `RoomGenerationSettings.ValidationResolutionMm` (default 100 mm), fixed by the settings. It is not derived from the plan, so the same plan is judged the same way everywhere. The raster has `ceil(footprint / step)` cells per axis, one raster per level used.
- **The walker** is a square of `n x n` cells with `n = ceil(MinPassageWidthMm / step)`. Its side is at least the minimum passage, and larger when the step does not divide it (a 500 mm step turns 1200 mm into 1500 mm).
- **Floors.** A cell is walkable only if one floor surface of that level covers the whole cell. Rectangle edges that are not on the step shrink the walkable area.
- **Walls, pillars, steps and anything else solid.** A cell is blocked if any such piece overlaps it at all and reaches into the open range between the floor and `ClearanceHeightMm` above it. A wall 350 mm thick on a 100 mm step blocks 400 mm. Lintels, which start at `OpeningHeightMm` (never below the clearance height), do not block. Because every solid is tested by its resolved box, wall thickness is exact when it is a multiple of the step and rounded up otherwise.
- **Narrow passages.** Movement is in whole cells along the axes, so only a passage that a full square fits through counts, and diagonal squeezes never do. Doorways are checked by walking the square from the inner end of the approach out through the wall.
- **Stairs** link the square just before the first step on the lower level with the square just past the last step on the upper level. The steps themselves are checked from the plan's numbers (riser, tread, width, landing size, heights at each joint, headroom above every tread).
- **What this guarantees.** Every rounding goes the safe way: free space is only ever shrunk and the walker only ever grown, so a room can be refused though it is fine (more often at coarser steps) but should not be accepted when it is not. `Validator_NeverAcceptsAGapNarrowerThanTheMinimumPassage_AtAnyResolutionOrAlignment` checks this by sweeping gap widths just under the minimum across steps of 50, 100, 250 and 500 mm and across offsets from the raster, and a scratch run of about 4000 such cases found none accepted. It is a test of the validator against known gaps, not a proof.
- **What it does not check.** The meshes: the mesh is built from the same pieces, but nothing re-tests colliders. Door frames and door leaves from the shell prefab are not modelled. Falling from a floor edge into a stair well is treated as not walkable rather than as an error.

## Failure handling

`RoomGenerationPipeline.Generate` runs the generator, validates, retries from `RoomSeed.ForRetry(roomSeed, n)`, and finally builds a plain open chamber (same validation). It reports one line per rejected try naming the generator, version, room, footprint, level count, connections, seed and reason, which is enough to reproduce it in a test. If even the fallback fails, the room gets no geometry and an error is logged.

## Seeds and determinism

`RoomSeed.ForRoom(dungeonSeed, layoutAttempt, placedRoomId, definitionIndex, generatorId, generatorVersion)` hashes with explicit FNV-1a over integers and characters (never `string.GetHashCode`). `RoomRandom` is SplitMix64 with integer output. Rooms never share a random stream, so generation order cannot matter. Raise `IRoomGenerator.Version` when a generator's output changes for an existing seed, and `RoomSeed.GeometryVersion` for shared changes (shell, stairs).

## Authoring a procedural room

A shell prefab is a `RoomBlueprint` as usual: two pivots (empty children) at the lowest and furthest corners give the footprint to the packer, each door has a `SealingObject` and a `DoorObject`, and every door has length 0. Add `ProceduralRoomSettings` for the generator, dimensions and materials. Validate Roomset runs the room through the real pipeline with a few seeds and reports any that cannot be built (`procedural-room`).

## Not yet done

- Corridor and maze generators.
- Roomset fingerprint and the dungeon geometry checksum in `LevelGenerationParameters` (the per-room `RoomPlanChecksum` exists).
- Debug visualisation.
- Doors with length above 0 (the passage between rooms lies outside the footprint) and vertical connection types.
