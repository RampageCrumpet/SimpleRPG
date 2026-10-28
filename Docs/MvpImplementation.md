# SimpleRPG MVP implementation

## Run the prototype

Open `Assets/Scenes/SampleScene.unity` in Unity **6000.6.6f1**. Enter Play Mode and click **Server** (this button starts a playable host; **Dedicated** does not create a local playable client).

- Move with WASD and look with the mouse.
- Press **3** for melee. Press again in the short chain window near the end of each swing to cut the recovery short and blend into the next attack of the three-attack chain. A press before that window opens, or after it closes, does nothing and is not remembered. Each continuation requires a new press; holding the key does not advance it. You can move and turn while swinging. The existing spell slots remain on 1 and 2.
- Approach a skeleton, observe its rise, walk, overhead and diagonal attacks, and fight using the sword. Damage requires blade contact during an animation's active frames.
- Look at treasure within 2.5 units and press **F**. The timed interaction asks the server to add the item to inventory. A full inventory leaves it in the world.
- Press **I** to inspect inventory; press I or Escape to close it.
- Stop and restart Play Mode for a new expedition. There is no extraction, persistence or death/respawn screen in this milestone, and rewards are limited to what dead enemies and chests hold.

The arrival room has no generated enemy. Encounters and treasure populate the other generated rooms after navigation is built. Three gold objects (goblet, bell, ewer) and four gemstones use the supplied meshes. Gold icons are renders of those existing assets. A camera-following exploration light keeps the dungeon readable; connection buttons hide after starting a session.

For a reproducible layout, turn off **Use Random Seed** on GameManager's GameController and set **Seed**. Change the level generator's minimum/maximum room count, floor count, branching and loop settings before starting the host. Normal play keeps random seeds and the existing dungeon size settings.

## Runtime composition

`GameController` generates geometry through the existing `LevelGeneratorController`, builds the server navmesh, then calls `DungeonPopulation`. The population component rolls existing `WeightedRandomTable` assets, including nested tables. Placement samples walkable points, checks body clearance and spacing, and requires a complete navigation path from the player start. The player's network wrapper waits until population has established its start before spawning the character.

The existing room generator, room definitions, arbitrary-object weighted loot implementation, ability graph executor, combat resolver, Character health replication, Weapon swing tracking, animation events, inventory grid, registry and interaction system are reused.

`AIController` owns refreshed perception, nearest valid target acquisition, target tracking, navigation helpers and a single terminal death transition. `EnemyController` composes idle, chase and melee states. Gremlin and skeleton use this same controller; the old Gremlin-specific controller and circular state initialization were removed. Other behavior styles can derive from AIController and compose different states while retaining the shared services.

Melee mechanics share `MeleeDeliveryBase` (tracked swings, weapon hits, telling every machine which attack to play). The player uses `ComboMeleeDelivery`, which runs a `ComboMachine` on the wielded weapon's `ComboDefinition`: the combo data says when each swing's hit window opens and closes and when a press may chain, so hits do not depend on animation events. Conventional enemies use `MeleeDelivery`, whose optional independent attack definitions select an animation trigger, total attack/recovery duration and damage scalar, with hit windows from clip events. Selection and damage remain server authoritative. Entering an attack state does not deal damage. See [ComboChain.md](ComboChain.md).

Animation timing matches the supplied exports at 1x playback: longsword attacks are 70/60, 55/60 and 59/60 seconds; skeleton Walk is 72/60 seconds, Overhead 78/60 and Slash 78/60. Living clips use 60 fps; the supplied physics Die/Rise exports use 30 fps and retain their original 1.6/2.4-second durations. Changing a clip's sample-rate label alone does not change its playback duration.

`AnimatableCharacter` maps movement and character events to available animator parameters. The NavMeshAgent owns movement, with Apply Root Motion disabled. NavigationGrounding adjusts its base offset against the visible World/Walkable floor; it freezes naturally when death disables navigation. This removes the 6–9cm gap introduced by NavMesh voxelization. The skeleton gameplay controller starts in Rise and uses rig-specific transform clips derived from the supplied exports. This preserves the translations needed for the entire bone pile to land, including hands and fingers that humanoid retargeting discards. The original humanoid imports and animation test prefab/controller are preserved. Rebuild the derived clips through SimpleRPG > Rebuild Skeleton Gameplay Clips. The adapter normalizes the export hierarchy and centimetre scale, removes the walk cycle's forward travel, and retains the original events. Transform masks isolate the weapon arms. While walking, the Weapon_Arm layer uses the authored Walk arm motion; while standing, it uses Ready. The agent walks at the refined authored 0.8m / 1.2s stride speed (0.667m/s), with Walk playing at 1x; empty layers use Write Defaults so they release the leg pose.

Death releases outstanding swings, closes hitboxes, stops navigation, disables the standing body collider and plays the configured death trigger. The weapon stays attached to the animated hand, following it into the bone pile. The corpse remains in its final pose, for the whole session.

A dead enemy can be searched like a chest. Enemies carry a `LootableCorpse` and an `Inventory`; the corpse cannot be interacted with while the character lives, and when it dies the server rolls the enemy's own loot table once from the seeded gameplay stream into that inventory. Players open it with the normal interaction and take items through the same validated inventory moves, within the corpse's reach. Nothing drops on the floor. To give an enemy loot, add `LootableCorpse` (which adds the `Inventory`), set the inventory size, interaction name and text, and assign a loot table; an enemy without one is simply empty.

The AI states switch between one another with explicit calls (idle, chase, attack), and death is handled once, centrally, in `AIController`, not in each state. That stays simple at four states; if more behaviours arrive (ranged attack, flee, patrol), replace the calls with a transition table.

## Add another conventional melee enemy

1. Create a prefab with a NetworkObject, server-authoritative NetworkTransform, Character and starting WeaponData. Give Character a melee ability graph; the current reusable graph is `Assets/Abilities/Abilities/MeleeAttackGremlin.asset`.
2. Add EnemyController, NavMeshAgent and a configured PrimarySense such as Blindsense. Configure range, speed, stopping distance and activation duration. Keep the prefab's agent disabled: the controller enables it on the server after spawn.
3. Add Animator, AnimatableCharacter, MeleeDelivery and the graph AbilityUser component. Configure animation triggers and any activation/death presentation. Use in-place clips when the NavMeshAgent supplies movement. For rigs with animated bone translations, use transform clips and matching transform masks rather than humanoid retargeting.
4. Place HurtBox trigger colliders on the HurtBox layer. Fit the standing body collider to the model.
5. Attach a Weapon at the model's hand/socket, with a kinematic Rigidbody and a blade-shaped Hitbox trigger on the Hitbox layer. The weapon's network behaviors use the character's root NetworkObject.
6. Register the prefab in the network prefab lists and add an EncounterEntry to the encounter table.

A different locomotion or attack style can supply its own state composition/delivery instead of duplicating the skeleton implementation. Perception, tracking, health and navigation are independent of skeleton names or humanoid assets.

## Configure an attack

Add an entry to MeleeDelivery's **Independent Attacks** array. Set its animator trigger, full clip/recovery duration and damage scalar. Add the corresponding animator state/transitions and clip. Animation events on the Animator's GameObject must call:

- `ActivateWeapon(int)` at the first active frame.
- `DeactivateWeaponH()` at the last active frame.

Independent clips may share attack number 1 in their events: the server associates contacts with the selected swing. Combo clips retain their distinct numbers. Recovery blocks another independent attack even after its hit window closes. The enemy turns to face its target before committing to the swing.

The overhead uses the approved 1.3-second export with its damage window at 0.55-0.75 seconds (see OverheadPlan.md); slash retains its authored event timings. Weapon tracking deduplicates hits across multiple hurtboxes. Hitbox contact tracking also detects a contact already overlapping when a new active window opens.

## Add treasure through the existing loot system

1. Create an Item asset with a name, icon and positive grid size.
2. Make a world prefab using the supplied visual, a collider, NetworkObject and WorldItemPickup. Set its Item and interaction settings; set the Item's WorldPrefab back to it.
3. Create a LootableItem entry referring to the Item. Add it to the existing gold/gem/nested treasure tables as appropriate.
4. Run **SimpleRPG > Definitions > Rebuild Definition Registry**. Items get a permanent id when created, and the registry gives each a network id that is never reused.
5. Register the pickup prefab in the network prefab lists.

The pickup RPC resolves the requesting client's controlled character. The server checks ownership context, distance, living state and inventory capacity before creating the inventory instance and despawning the pickup. Inventory clones retain SourceAsset identity and receive the existing inventory instance ID.

## Geometry and navigation corrections

The active dungeon geometry uses the original package dimensions: 4m modules, 2m grid cells and 8m floor spacing. Room counts and logical footprints are preserved. The shared connector prefab uses the same dimensions as its room instances so the existing doorway approaches align. Shared floor collision extends 0.6m per horizontal edge across placement seams; upper stair landings keep their actual leading edge. The navmesh includes World and Walkable colliders and uses the actual enemy dimensions (.3m radius, 1.8m height) and default voxel resolution. Meshes used by runtime collider-based navigation have Read/Write enabled, including stairs. See AssetScale.md for source measurements and migration details.

The player's existing sword was aligned to the hand axis used by its existing attack clips. The inventory panel now uses the supplied repeatable InventoryGrid sprite, avoiding Unity's tiled-image limit. Grid sizing uses canvas units once, backdrop anchors stretch within the panel, item icons preserve their aspect within their grid footprint, and screen/grid coordinate conversion accounts for the panel pivot. The integration scenario checks visible icon positions and coordinate round trips. The scene uses separate player and container inventory views. Direct Game View inspection confirmed the cell grid and collected gold/gem icons are visible without covering the ability bar.

## Verification and visual review

Unity compilation succeeded. The full suites passed **940 EditMode tests** and **167 PlayMode tests**, with zero failures. The shipped roomset build gate and checked prefab references also passed. Automated results are recorded in `Library/MvpEditModeResults.xml` and `Library/MvpPlayModeResults.xml`. The editor helper `SimpleRPG.TestAutomation.MvpTestRunner.Run(false)` runs EditMode; `Run(true)` runs PlayMode. The suites are also available in Unity's Test Runner.

`MvpGameplayTests` opens the shipped gameplay scene on a real host. It exercises seeds 42 / 1337 / 9001 with 8 / 25 / 40 requested rooms and 1 / 3 / 3 floors, checks reachable placement throughout the generated layouts, verifies multiple enemies, timed interaction/RPC pickup, registry identity, inventory display, duplicate/range/full-inventory rejection, real skeleton blade damage, real player sword kills and terminal animated death.

Human playtesting should inspect camera framing, attack readability, foot planting, weapon grip and blade alignment, the rise-to-idle blend, the falling-apart animation and attached sword, treasure visibility, inventory icon sizing, and combat balance with several attacking skeletons. Automated physical-contact checks do not establish visual polish.

Direct Game View inspection also confirmed a lit generated dungeon with an animated skeleton and successful normal Server-button startup with the connection menu hidden. The final normal host check reported no runtime errors. A standalone player build and a separate human-controlled remote-client session are not part of the recorded editor validation.


The player now holds the supplied Longsword_lowpoly_singlemesh export, imported as Longsword.fbx, with its origin at the right-hand grip and its blade along local Y. A separate trigger covers the blade. The existing three-stage longsword attack/continuation/recovery chain remains bound to the player controller and melee ability (3). The metal material is a placeholder; no longsword texture export was available in the source art folders.

Arrival placement also rejects isolated NavMesh islands: the first room's candidate must have a complete path to another room before it becomes the population anchor. This matters for stairwell starts. Integration coverage includes seed 42 with the normal 25-room, three-floor configuration and an unbounded maximum, alongside the other generation cases.

Dungeon dimensions were restored against the original package on 2026-10-22: active modules are 4m instead of10m, logical cells2m, floor spacing8m. The skeleton retains its exact1.78004m source height and unit transforms. Navigation bake radius/height now match the skeleton (.3m/1.8m). See AssetScale.md for source locations, export normalization and stair traversal details.

The Low and Very Low URP presets use per-pixel additional lights. The exploration point light requires this for normal maps and metallic specular highlights; per-vertex lighting made walls flat and fully metallic treasure black. Other presets already used per-pixel lighting. Existing packed maps are valid (R metallic, A smoothness); no texture repacking is needed for this fix. Reflection intensity remains unchanged. Visual captures: Library/GoldPerPixel.png and Library/WallPerPixel.png.

Lighting correction validation: both MVP integration tests passed after updating the two quality presets, and the gold ewer plus stone wall relief were inspected in the live generated dungeon.
