# Dungeon generation

How a dungeon is made, from room prefabs to objects in the scene, and the rules the tools enforce.

## Pipeline

```
Roomset (room prefabs)
   │  RoomPacker / "Package Rooms"      writes Size, OriginOffset, door locations onto each prefab
   ▼
RoomDefinitionBaker                     prefab data → plain RoomDefinition / DoorDefinition
   ▼
LayoutGenerator  (pure, no Unity objects)   seed + definitions → Layout        (retries, validates, seals what cannot be filled)
   ▼
LayoutValidator  (pure)                 re-checks the finished Layout from its geometry alone
   ▼
LevelSpawner                            instantiates prefabs from the Layout, seals doors, applies RoomNetworkObjects
```

`LevelGeneratorController` runs this on the server and on every client from `LevelGenerationParameters`
(seed, minimum/maximum rooms, floor count, plus the server's roomset fingerprint and layout checksum).
Only the spawner creates objects, so the server and clients reach the same `Layout` from the same inputs.

## Grid conventions

- Everything is in whole cells. Rotation is a quarter turn (`GridMath`), exact integer maths; turns match `Quaternion.Euler(0, 90 * turns, 0)`.
- A room's `Size` and its door locations are measured from its **lowest corner**. `OriginOffset` is where that corner sits relative to the prefab root (never positive).
- A door stands on a **grid point** (a cell corner), centred on the line between cells, so it covers an **even** number of cells across (`WidthCells`, at least 2).
- A door is `LengthCells` long. It joins the door standing at `Far` (point + direction × length) that faces back, has the same length and the same type.
- The **passage** is the row of cells across the door for each cell of its length, plus one more row past its end. Cells in it must be empty, or belong to the room the door joins. When two doors join, any cells between the two rooms are reserved so nothing is placed in the gap.

## Floors

- Cells are `(x, y, floor)`. A room is `Floors` high (`RoomBlueprint.Floors`, default 1) and occupies its footprint on every one of them, so a stairwell room is 2. Floors are world height `Roomset.floorHeight` apart (8 for ModularDungeon: two wall heights, so a vaulted room fits under the floor above); the spawner puts floor `n` at `n * floorHeight` above the prefab's own height.
- A door stands on a floor of its room (`ConnectionBlueprint.Level`, counted up from the room's lowest floor). Joined doors must stand on the same floor, so the stairs themselves live inside a tall room: `Room_Stairwell` is a 16-long, 4-wide hall with its west door on level 0, its east door on level 1 and four flights of stairs between, 12 high under a flat roof.
- A **vertical connection type** (`ConnectionType.FloorDelta` above zero, e.g. a ladder or shaft) makes a door join a door on another floor. Each door says which way it climbs (`ClimbsUp`); the joined pair has opposite deltas, the same type and the same length. The passage of such a door covers every floor between the two doors. A standard door may not climb (`vertical-door-invalid`), and every vertical type needs doors that climb both ways (`roomset-vertical-unpaired`).
- `FloorCount` is a generation parameter (`LevelGenerationParameters.FloorCount`, `LevelGeneratorController.FloorCount`), not a limit baked into the code. The start room stands on the **bottom** floor (0, where players spawn), so a multi-floor dungeon climbs from it; rooms may not leave floors `0 .. FloorCount - 1`. One floor gives the flat dungeon, and rooms taller than the dungeon are never chosen.
- One dungeon, one start: floors are reachable only through stairwells and vertical doors. `LayoutValidator` checks that every room is reachable from the start, that no two rooms share a cell on any floor, and that passages are clear on every floor they cross (`room-outside-floors`, `vertical-door-invalid`, `vertical-mismatch`).
- One `NavMeshSurface` (collecting all geometry) spans every floor and is built on the server after generation, as before.

## Generation rules

- Rooms are added freely until `MinimumNumberOfRooms`. After that only rooms that close more doors than they open are accepted, and the dungeon stops growing at `MaximumNumberOfRooms` (default: twice the minimum, at least four more).
- A door no room fits after enough tries is sealed with its `SealingObject`. Sealing is normal, not a failure.
- If an attempt cannot reach the minimum or fails validation, another attempt runs from a seed derived from `(Seed, attempt)`. All machines run the same sequence.
- Room choice (`IRoomSelector`): weights, per-room `MaxCount`, a `Start` role that is used only to begin, a bias toward dead ends once the minimum is reached, and only rooms with a door that can join the target.
- Doors only join doors of the same `ConnectionType` (no type = standard door).
- Randomness comes from three independent streams (rooms, doors, turns). Layouts are stable for a given `LayoutGenerator.AlgorithmVersion`; raise it whenever a change would give an existing seed a different layout. The version is part of the roomset fingerprint, so a client on different rules refuses to generate instead of building a different dungeon.

## Authoring and tooling

- Select a **Roomset** asset: **Validate Roomset** checks every room and the set as a whole; **Package Rooms** packs all rooms or none.
- Room rules (`RoomValidator`): doors on the grid, axis-aligned, not opening into their own room, no duplicates, a `SealingObject` that belongs to the room and starts inactive, whole-cell door length, positive width, identity root transform, no stray pivots outside the geometry, packed data up to date.
- Roomset rules: not empty, no duplicates, no room without doors, a placeable dead end, something placeable besides start rooms, one length per door type, unique type names.
- Rooms in a roomset are re-packed automatically when their prefab changes and the packed data is stale (**Tools > Rooms > Auto-pack changed rooms** to switch off). Selecting a room in the scene shows its footprint, door points, reach and the cells past each door.
- Builds fail if a roomset the build scenes depend on has problems (`RoomsetBuildGate`). Run the same check from the command line:
  `-batchmode -quit -executeMethod SimpleRPG.EditorTools.RoomsetTools.ValidateRoomsetFromCommandLine -roomset Assets/...asset`

## Tests

`Assets/Tests/EditMode`: `GridMathTests`, `LayoutGeneratorTests` (hand-built and real roomset sweeps, spawner geometry), `LayoutValidatorTests`, `FloorTests` (multi-floor, synthetic and real prefabs), `PassageTests`, `ConnectionTypeTests`, `RoomSelectionTests`, `LayoutFingerprintTests`, `RoomValidatorTests`, `RoomPackerTests`, `RoomsetBuildGateTests`.

## Not done yet

- A vertical `ConnectionType` asset and a ladder/shaft room are supported and tested with synthetic rooms but not yet authored; the only real vertical room is `Room_Stairwell`.
- Nothing makes a multi-floor dungeon *use* its lower floors; they are reached when the dice pick a stairwell.
- Exit and boss room roles.
- Legacy `Dungeon` and `TestDungeon` roomsets still contain unpacked rooms; they are not referenced by a build scene, so the build gate ignores them.
