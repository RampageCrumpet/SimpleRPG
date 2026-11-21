# ClothValidator

Diagnostic-only validator for Unity's native `Cloth` + `SkinnedMeshRenderer`. It reads; it never changes a garment, collider, Cloth setting or animation. Disposable test tooling (not part of the game, no asmdef).

Files: `ClothValidator.cs` (runtime component, one file), `Editor/ClothValidatorMenu.cs` (attach menu), `Assets/ClothFitTest/FitHarness.cs` (existing timeline harness; only `CurrentFrame` was added so frame numbers match harness runs).

## Use

1. Select the character (the object with the Animator, or the garment) and run `Tools > Cloth Validator > Attach To Selected Cloth Character`. Or `AddComponent<ClothValidator>()` yourself.
2. Set `startFrame` / `endFrame` (the sampled interval), `detailFrames` (frames that also get a per-id csv), `outDir` (default `ClothValidation/<scene>_<garment>`).
3. Enter Play Mode. With a `FitHarness` on the same object the frame index is `FitHarness.CurrentFrame` (frame 784 = the usual raised-knee frame); without one it counts rendered frames from Play. Sampling runs at the end of each rendered frame; the report is written at `endFrame` (or when Play Mode ends). Set `pauseAtFrame` to pause the editor at a frame and look at the gizmos in the Scene view.
4. Equipment compared with the cloth defaults to every other enabled `SkinnedMeshRenderer` on the character; assign `equipment` to override.

Cloth with no colliders, a different mesh or another rig works unchanged: particles are the unique welded positions of the garment mesh in first-occurrence order (Unity's order); the tool checks that this matches the Cloth's coefficient count and says so in the report.

## Output (in `outDir`)

| file | content |
|---|---|
| `report.md` | one section per validation, with a result line, measurements, offending ids and a "cannot be evaluated reliably" list |
| `frames.csv` | one row per sampled frame, every metric |
| `particles.csv` | per particle: frames inside a collider, leash violations, jitter, equipment clipping, conflicts (only flagged particles) |
| `triangles.csv` | triangle id, particle ids, mesh vertex ids, kind (`collider_triangle_only`, `collider_vertex_assisted`, `equipment_clip_clear_of_colliders`), frames, max depth |
| `detail_f<frame>.csv` | every flagged particle / triangle at that frame |

Gizmos (Scene view, Play Mode): grey = collider capsules; red dots = free particles inside a collider; magenta triangles = triangle interior inside a collider with all vertices outside; orange lines = skinned position to simulated position beyond Max Distance; cyan dots = cloth clear of every collider but inside visible equipment.

## What each check measures

1. **Particle penetration**: signed distance of every simulated particle to each assigned collider (sphere pairs as cones, capsule colliders as capsules, using the colliders' real world positions, `lossyScale` radii). Inside = deeper than `penTol` (1 mm).
2. **Triangle penetration**: barycentric samples (at most 10 mm apart, 14 subdivisions) inside every triangle that has a free vertex; reported as *triangle-only* when all three vertices are outside, *vertex-assisted* otherwise.
3. **Collider coverage**: particles and triangle samples that are clear of all colliders but more than `equipTol` (3 mm) behind the surface of the baked equipment meshes (nearest-vertex normal); plus the signed distance of equipment vertices near the garment to the collider union.
4. **Synchronization**: `FixedUpdate` calls per rendered frame; the "state lag", the fractional number of rendered frames by which the cloth read-back trails the skinned pose, estimated from the pinned particles (which Cloth drives from the skin), in the cloth's local space so root motion does not bias it; penetration and leash evaluated both at the current pose and at the pose the solver saw.
5. **Self-collision / jitter / constraints**: whether self-collision is enabled (distance > 0, stiffness > 0, selected particles), minimum rest spacing of particles against that distance, second difference and direction reversals of the cloth-minus-skin offset (raw and lag-matched), Max Distance excess, edge stretch, collapsed triangles.
6. **Constraint conflicts**: for particles whose skinned pose is inside a collider: exit distance versus Max Distance, exit point versus tether reach (surface path to the nearest pinned particle), closeness to the collider axis (escape direction ill-defined), neighbours with opposite escape directions.

Thresholds are public fields on the component (`penTol`, `triTol`, `equipTol`, `leashTol`, `jitterTol`).
