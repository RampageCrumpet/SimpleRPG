# Cloth simulation: what we learned

Read this before touching the hauberk's Cloth, adding another cloth garment, or chasing shimmer, jitter or poke-through. How the hauberk is built and wired is in [Equipment.md](Equipment.md) ("Mail hauberk skirt: native Unity Cloth"); this page is the reasoning, the traps and the diagnostics that cost days to find.

## The one rule: use Unity's Cloth the way it is documented

Unity's `Cloth` is a well-documented component with a long history of examples. None of them split a garment into pieces to make it behave. When it misbehaves, the mesh or the settings are wrong; do not invent structure around it. We did once (a separate skirt mesh with its own normals, a hidden tuck, subdivision) and it was wrong: it made a visible break between hauberk and skirt, it was never needed, and it was removed. The cure for the shimmer was a clean mesh, not a different architecture.

Before proposing any workaround, say which documented workflow it departs from and why, and get the user's go-ahead.

## Shimmer on the hem: coincident back-to-back faces

**Symptom.** Constant sparkle or flicker on a metallic, normal-mapped garment (mail), worst at the hem, present even when the wearer stands still. Looks like noise, not motion.

**Cause.** Cloth keeps one particle, and one normal, per vertex *position*. Vertices that share a position are welded into one particle (first occurrence order). `Cloth.normals` itself warns that its indices may not match mesh indices when duplicate vertices exist. If two faces sit on the same positions facing opposite ways (a double-sided garment modelled as an outer and an inner shell), their shared particles get a normal that is neither face's, and it flips by 20 to 80 degrees from frame to frame. On a surface lit by a normal map that is visible shimmer.

**What it is not.** All of these were tested and made no measurable difference to the shimmer:

- solver frequency, fixed-step timing, the physics time step
- stretching, bending, damping and friction values
- mesh density (we subdivided the skirt: "no difference")
- a softer material on the skirt only (also rejected: it makes a seam)

**Fix.** The game mesh must have:

1. no coincident back-to-back faces (keep the face that points away from the body, drop the other),
2. no custom normals,
3. every face smooth.

The back side of the mail is drawn by the material instead of by geometry: URP Lit, Render Face: **Both** (`_Cull` = 0), as in `MailHauberk.mat`. The authoring mesh may keep the pairs and the custom normals (`MailHauberk_Work` does); the game mesh is derived from it and must be rebuilt that way after every change to the work mesh. Do not export the work mesh.

**Measured result** (one clean mesh, the whole hauberk, with the disposable `ClothDiagnostics`):

| | Before | After |
|---|---|---|
| Cloth normal change per frame, wearer idle | 9.8 degrees (360 of 768 skirt particles were the bad ones, the rest 0.03) | 0.15 degrees |
| Frozen pose | 36 degrees | 0.05 degrees |
| Character pixels changing per frame, frozen pose (of about 137,000) | about 14,500 | about 113 (none with Cloth off) |

The 24 duplicate-position vertices that remain (the slit twins) are harmless by measurement: 0.12 degrees on those particles.

## The particle order is Unity's, so bake it and check it

Cloth coefficients (Max Distance and the rest) are indexed by Cloth's own particle order, not by vertex index. `ClothProfileBaker` computes the order by welding positions (`RoundToInt(pos * 10000)`, first occurrence) and then compares the count with the one Unity reports. If the two disagree the profile is refused and the garment shows as plain skinned mesh, with a logged reason.

A real case: after subdividing, Unity reported 815 particles and the weld predicted 768. Forty-seven slit-twin pairs differed by float error well below the weld resolution, so Unity treated them differently from our rounding. Fix: snap coincident groups to bit-identical coordinates in Blender before export. If the counts disagree, look for near-coincident vertices; do not loosen the check.

After any re-export or reimport that changes the mesh, rebake (SimpleRPG > Items > Bake Hauberk Cloth Profile). The profile stores a fingerprint of the mesh, so a stale one is caught at bind time. The FBX has to be Read/Write for this.

## Settings that matter, and why they are what they are

All set by `ClothProfileBaker`; tuned by eye in a sway demo.

- **Pinned vs free.** Max Distance 0 at and above z 0.78 (torso and sleeves), smoothstep to 5 cm at z 0.65, 5 cm below. A short leash keeps the hem from swinging out through the legs and keeps the cost down.
- **Stretching 0.84, bending 0.10, damping 0, friction 0.5.** Bending low enough that mail drapes; no damping, because damping on a worn garment reads as the mail dragging behind the wearer.
- **World velocity and acceleration scale 0.3.** Full values throw the skirt about whenever the wearer starts, stops or turns. This is the main control for "jerking".
- **Solver frequency 120.** In Unity 6 `Cloth.solverFrequency` (bool) is obsolete; set `clothSolverFrequency` (float).
- **`updateWhenOffscreen` on.** Cloth keeps stale vertices while its renderer is culled.
- **Clear motion** on equip, on enable and when the wearer moves more than 0.75 m in a frame (teleport, respawn). Otherwise a jump is read as a huge acceleration.

## Colliders and poke-through

- Thigh colliders are one tapered sphere pair per thigh, on the wearer's own `UpperLeg` bone, fitted to the animated breeches (smallest pair holding every breeches vertex between z 0.60 and 0.96 over idle, walk, run, stop and lunge, with 3 mm to spare). They live on a child object on the cloth-collision layer, which collides with nothing in gameplay physics.
- **Pinned particles are still pushed by cloth colliders.** Max Distance 0 does not make a particle immune: the hip sphere moves some of them up to about 44 mm (zero with colliders off). That is what keeps the mail off the breeches, and it is by design. Tests that assume pinned means "exactly on the skin" are wrong with colliders on; the pinned-torso bound is 5 cm.
- With colliders fitted this tightly, "how deep did the cloth go into the collider" is the wrong test: it shows nothing. Compare vertices with working colliders against the same run with the colliders turned to triggers, and assert that they move.

## Diagnostics worth reusing

These found the real cause after the settings sweeps had found nothing. Rebuild them if needed (the originals were disposable files in `Assets/ClothFitTest`).

1. **Normal jitter by particle.** Per fixed step, record `Cloth.normals`, and report the mean angle change between frames. Split it into "seam" particles (a position shared by more than one vertex) and the rest. A big gap between the two is this exact bug.
2. **Frozen pose.** Hold the wearer still and compare to an animated run. Residual jitter in a frozen pose is the simulation's own noise, not the animation's.
3. **Pixel difference.** Render the character at a small size (384 by 384 is enough; 768 was too slow) from a fixed camera, and count changed pixels frame to frame, with Cloth on and off. It measures what the eye sees, and it is the only check that ties a number to the shimmer.
4. **Variant sweeps.** Run the same checks while varying one setting at a time. Useful mostly to *rule things out*, which is what they did here.
5. **Position jitter in millimetres** alongside the angle: the hauberk's is 0.5 mm animated, 0.08 mm frozen.

## Testing notes

- `HauberkClothEquipmentTests` (20 tests, play mode, a host and a client) derive their expectations from the `ClothProfile` (leash, damping, acceleration scale, collider count) instead of hard-coding values. When the profile is tuned the tests follow; do not paste numbers back in.
- The hem is bounded by its leash plus one run-step of lag (`leash + 3.2 * fixedDeltaTime + 0.02`), not by the leash alone.
- Running them from the editor: SimpleRPG > Debug > Run Hauberk Cloth Tests existed only as a disposable helper. The filter that works is `assemblyNames = { "SimpleRPG.Abilities.Tests.PlayMode" }` with a regex `groupNames`; a plain test-name filter ran zero tests.

## Authoring and tooling traps

- **Armor FBX only through `I:/Art/Blender/SimpleRPGHuman/Authoring/export_armor.py`.** It exports `GAME_MailHauberk` as the single mesh with `Mail_Repeat` UVs and one material.
- Blender 5.3: `bpy.ops.mesh.customdata_custom_splitnormals_clear()` removes custom normals; `mesh.has_custom_normals` tells you if any remain. To choose which of a back-to-back pair to keep, use a `BVHTree` over the body mesh and keep the face whose normal points away from the nearest body point.
- Never leave a custom-normal or back-to-back mesh as the game mesh "for now". It looks fine in a screenshot and fails in motion.
- Unity MCP: recompiling after `AssetDatabase.Refresh()` can take minutes; `execute_code` needs fully qualified names, and types in a disposable asmdef must be found by reflection; the `unity_*` tools need `port: 7890` and a fresh `unity_select_instance` after every domain reload; objects and scenes made in Play mode are discarded when it stops.

## Not yet verified

- The hem and torso judged by eye in walk, run and seat views (the numbers are good; the user has not signed off).
- The inside of the mail seen from behind or below, with Render Face: Both.
- Plate armor against the flared skirt.
