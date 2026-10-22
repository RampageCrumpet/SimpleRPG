# Asset dimensions and stair traversal

Unity world units represent metres. Structural prefab transforms and model importer Scale Factor remain one.

## Source audit (2026-10-22)

| Asset | Original/source measurement | Gameplay measurement |
| --- | --- | --- |
| Skeleton_GameReady | 1.7800404 m from feet to skull | 1.7800403 m |
| Small stair module | approximately 4.006 m wide, 2.049 m high including uneven stone | same |
| Dungeon wall/tile module | 4 m nominal | 4 m nominal |
| Dungeon logical grid cell | half a module | 2 m |
| Dungeon floor spacing | two wall modules | 8 m |

The skeleton was checked against `I:/Art/Blender/Skeleton/AnatomicalHumanSkeleton_GameReady_v66_FingerPivots.blend`. That source uses Metric units, one metre per unit, and unit object scales. The imported model and gameplay prefab preserve its dimensions. The nominal 1.8 m skeleton has a measured mesh height of 1.78004 m; no compensating scale was applied.

The dungeon reference is `C:/Users/Alex Van Oss/Downloads/modulardungeoncollection_v10/ModularDungeonCollection-V1.0.unitypackage`. The 22 model exports used by the active ModularDungeon roomset had been enlarged 2.5 times, yielding 10 m modules and roughly 0.4 m stair risers. The player's step limit is 0.3 m, so those stairs blocked the controller.

The active exports were normalized back to the original physical dimensions. Existing baked coordinate orientation, pivots, model identities and Unity GUIDs were retained because the original package FBXs use a different coordinate convention. Replacing those files directly would move or reverse existing room geometry. Prefab positions, collider dimensions, connection distances, cell size and floor spacing were migrated by the same 0.4 factor; logical cell counts and room counts are unchanged. Unused package assets were left untouched.

Magician_RIO is a disposable test asset and is excluded from this audit. Existing decorative candle instance variations are not a world-scale reference.

The shared ModularDungeon_RoomComponents/Connection.prefab was migrated with the rooms. Its existing approach floors and walls must use the same dimensions as their parent rooms; otherwise room packing and physical passages disagree. No replacement or additional floor tiles are required.

## Collision and navigation

Floor seam padding remains 0.6 m per horizontal edge because it supports metre-sized actors across existing placement seams. Upper stair landings use their visible leading edge along the climb direction; extending a landing collider backwards creates a vertical lip before the final stair.

The navigation bake uses the skeleton's actual 0.3 m radius and 1.8 m height. The old 0.5 m bake radius disconnected narrow passages after restoring the dungeon dimensions. The runtime surface uses the default voxel resolution for this agent (radius / 3, approximately 0.1 m); the old explicit 1/6 m override was needed for the enlarged world and is too coarse for some restored doorway seams.

`AssetScaleTests` guards source dimensions and unit structural scales. `StairTraversalTests` exercises the actual player capsule in both directions and an actual NavMeshAgent across all four flights. The generated-dungeon integration tests also cover multiple floor counts and room counts at the restored dimensions.

Final validation: 940 Edit Mode and 167 Play Mode tests passed. This includes the roomset build gate, source dimensions, the actual player capsule climbing/descending, an AI agent traversing all flights, and generated-dungeon navigation/combat/pickup at multiple seeds and floor counts. A normal seed1337 host spawned18 skeletons and54 treasures; Game View proportions were inspected. No human input playtest is claimed.
