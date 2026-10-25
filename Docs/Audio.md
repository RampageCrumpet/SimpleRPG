# Audio system

Data-driven, contextual, layered sound for combat, characters, equipment, inventory, spells, interactables and the dungeon. Gameplay asks for an **event**; assets decide what plays. Built on Unity's own audio (AudioSource, AudioMixer, Audio Random Containers). No middleware. For how the sound files themselves are made, see [AudioCreationWorkflow.md](AudioCreationWorkflow.md).

Code is in `Assets/Scripts/Audio/` (namespace `SimpleRPG.Audio`); editor tools in `Assets/Scripts/Tools/Editor/Audio/`.

## Architecture

```
gameplay event ──► adapter (CombatAudio, FootstepEmitter, AudioIdentity, ...)
                      │ builds an AudioContext (surface, footwear, materials, region, intensity)
                      ▼
                 GameAudio.Play(event, context, where)          never throws, never blocks gameplay
                      │
        AudioEventBase.Resolve(context)   table → best row → AudioEventDefinition   (or silence)
                      │
                 AudioService   VoicePlanner (layers, clip choice, pitch/volume) → pooled AudioSource voices
                      │                                          → AudioMixer group by category
                      ▼
                 AudioHandle (only needed to stop a sustained sound)
```

| Piece | What it is |
| --- | --- |
| `AudioEventDefinition` | One authored sound: layers (clips with weights, or an Audio Random Container), volume/pitch variation, chance, delay, spatial settings, mixer category, priority and concurrency limits. OneShot or Sustained (loops until stopped). |
| `AudioEventTable` | Picks an event from context. Rows have conditions (facet = tag), optional body regions and intensity range. The most specific row wins, in any order; a fallback plays if none fit. Tables nest. |
| `AudioTag` / `AcousticSurface` | A named identity (steel, bone, boots, plate, stone). A tag has an optional **parent**: gravel falls back to dirt, dirt to ground. Surfaces are tags kept apart from any visual material. |
| `AudioContext` | Small by-value struct: Surface, Footwear, Source, Target, Armor, Subject tags + Region + Intensity (0 to 1). Add an `AudioFacet` value to add a kind of context. |
| `AudioService` | The only code that touches AudioSources. Voice pool (made once, reused), per-event and global limits, follows transforms, fades, releases. Not created on a headless session. |
| `GameAudio` | Static facade gameplay calls. Null-safe and exception-safe. |
| `AudioRouting` | One asset in `Resources/AudioRouting`: mixer groups per category, voice cap, shared footstep/impact tables, default surface, physics-material to surface map. |

**Audio Random Containers.** Evaluated and used, not duplicated. An ARC has no scripting API (only `AudioSource.resource`), so a layer can point at one and Unity does the random choice, volume and pitch for that layer. Our own weighted choice exists because tables, layers, chance and tests need decisions we can see and assert on; it also skips the variation just heard.

**Randomness.** Sound variation uses its own `System.Random`, never `UnityEngine.Random` and never the gameplay stream, so which clip plays can never change what happens.

### Mixer

`Assets/Audio/Mixers/GameMixer.mixer`: Master with Combat, Characters, Magic, World, Ambience, UI. Each group's volume is exposed as `Volume_<Category>` (`AudioRouting.SetCategoryVolumeDb`). Rebuilt by **SimpleRPG > Audio > Create Mixer And Routing** if missing. Add effects (reverb sends, ducking) to groups in the mixer window; nothing in code needs to change.

### Context and surfaces

- A surface comes from `SurfaceQuery`: an `AcousticSurfaceSource` on the collider or a parent, else the collider's physics material through `AudioRouting`, else the routing default, else none (the table's fallback plays).
- `SurfaceQuery.Below(point)` casts down. The first solid collider is the ground; **trigger** colliders with a source above it are *overlays* (shallow water over stone). A step plays the table for the ground, then again for the overlay.
- `AcousticSurfaceSource.ContactSounds` lets one object add sounds without a surface of its own: an old bridge is `Wood` plus a creak with a 15% chance on footsteps.
- Weapon identity and attack: the **combo step** names its own swing sound; `WeaponData.ImpactSound` is an explicit override; otherwise `WeaponData.AudioMaterial` + the target's `AudioIdentity.BodyMaterial` + region pick a row in the shared impact table. Priority: weapon override, then target override, then shared table.

### Footsteps and equipment

`FootstepEmitter` takes foot contacts from the animation (`OnFootDown(int)` animation event: 0 left, 1 right) or, with *Detect From Bones*, from the foot bones' height above the root. Never a repeating timer. A step resolves `Surface + Footwear + Intensity` (intensity from the character's measured speed), plays the overlay, the object's contact sounds, then **equipment layers**.

`MovementAudioState` holds what the character wears. `MovementAudioProfile` (on an `Item`) can set a footwear tag, an armor tag and a step layer with chance, intensity floor, cooldown and an **exclusive group** (two metal items never clink on the same step). `MovementAudioState.MaxLayersPerStep` caps the noise. `EquipmentMovementAudio` refills the state when `Equipment.Changed` fires; it is the only link, so a future paper doll needs nothing more than items carrying a profile.

### Combat

- **Swing**: `ComboStep.SwingSound` + `SwingSoundTime` (seconds into the step). `CombatAudio` plays it on every machine that shows the attack, joins late if the news is late, and cancels it if the owner's predicted swing is refused.
- **Impact**: `MeleeDeliveryBase` sends `HitConfirmedClientRpc` (victim id, region, attack number, blade position) when the server confirms a hit on a living target. `CombatAudio` plays from that. Entering an attack never plays an impact.
- **Hurt and death**: `AudioIdentity` follows the replicated `OnTakeDamage` / `OnDeath` events.
- **Spells**: `SpellAudioProfile` (cast prepare, release, flight, impact, periodic). `ProjectileAudio` on a projectile prefab plays release at spawn, a flight loop *attached to the projectile* (it cannot outlive it), and impact at the last position unless the projectile simply expired.

### Inventory, interactables, ambience

- `ItemAudioProfile` per item (`Item.AudioProfile`), with a `Parent` family (gems, coins, heavy weapons). Actions: Pickup, Place, Move, Stack, Drop. Played from transactions: the local player completing a move (`Inventory.RequestMoveItem`) and a world pickup being collected (`WorldItemPickup` despawn). Not while dragging across cells.
- `InteractableAudio` on a door, chest or lever: Open, Close, Activate, Deactivate, Locked, Unlock. Chest and locked door call it today; anything else calls `Play(cue)` or an animation event `OnInteractionCue(int)`.
- `AudioLoopEmitter` on any object: a sustained, 3D event that starts with the object and ends when it is disabled or destroyed.
- `AmbienceProfile`: quiet beds (non-spatial loops), intermittent sounds placed at a random spot around the listener, and a chance of a long quiet spell after each. `AmbienceController` (one per scene) runs it; an `AmbienceZone` (trigger collider on a room prefab) switches profile by where the listener is. There is no music.

## Multiplayer

Audio is client-side and sends nothing. Triggers:

| Sound | Trigger | Authority |
| --- | --- | --- |
| Swing | `MeleeAttackStarted` (the owner's prediction, or the server's word) | One event per attack; a refused prediction cancels it |
| Impact | `HitConfirmedClientRpc` from the server on a confirmed hit | Server |
| Hurt, death | Replicated health and life state | Server |
| Footsteps | Each machine's own animation | None, never replicated, so never duplicated |
| Inventory place | The local player's own move request | Courtesy; a refused move snaps back |
| Pickup | Server despawning the pickup | Server |
| Projectile | Network spawn / despawn | Server |

`AudioEventLedger` is the duplicate guard: a predicted sound and its confirmation share a key and only the first plays. A session with no graphics device (dedicated server) never creates the service; every request there is a no-op. Sound choice cannot affect gameplay: it reads state, never writes it.

## Authoring

1. **SimpleRPG > Audio > Create Example Catalog** makes `Assets/Audio/Catalog/` (existing assets are kept). Open any event or table: the inspector lists problems and has **Play / Stop** (2D, no scene needed). A table inspector has a context form to hear what a surface/footwear/material combination resolves to.
2. **SimpleRPG > Audio > Validate Audio Definitions** checks the whole project. Errors are unusable definitions; warnings are silent ones (no clip yet).
3. Replace a clip: drag it onto the layer's variation slot. Several slots = variations (weights). Use a Random Container in *Resource* instead if you prefer Unity's own tooling.

### How to

**A new weapon swing sound.** Create an *Audio > Event* (Combat, OneShot), add the clip. Open the weapon's `ComboDefinition`, find the step, set *Swing Sound* and *Swing Sound Time* (the second the sound should start). Add `CombatAudio` to the character if it does not have one.

**Footsteps for a new surface.** Create an *Audio > Acoustic Surface* (set a Parent for a fallback). Add the event(s) and a row to the footstep table (condition Surface = it, optionally Footwear). Put an `AcousticSurfaceSource` with that surface on the floor object (or map the physics material in `AudioRouting`). Add a `FootstepEmitter` to a character and feed it foot-down animation events.

**Footwear or armor movement sounds.** Create a *Movement Profile*: set *Footwear* for boots (and add Footwear rows to the footstep table), *Armor* for body armor, and/or a *Step Sound* with chance, cooldown and a group. Assign it to the `Item` (Audio > Movement Audio). The character needs `MovementAudioState` and `EquipmentMovementAudio`.

**Inventory sounds on an item.** Create an *Item Profile* (or reuse a family such as Gems), set only the actions that differ and a Parent for the rest, assign it to `Item > Audio > Audio Profile`.

**A looping environmental emitter.** A Sustained event (Ambience, World3D, loop on its layer), then `AudioLoopEmitter` on the prop. Tune Min/Max Distance on the event.

**A spell or projectile.** Create a *Spell Profile*, fill the events it has, put `ProjectileAudio` on the projectile prefab and assign it. For cast preparation call `GameAudio.PlayAttached(profile.CastPrepare, ...)` and keep the handle to stop it if the cast is interrupted.

## Known limitations

- Nothing here is verified by ear. Tests check which sound is chosen, when it starts, how many play and that everything is released; they do not and cannot check how it sounds, how loud attenuation is, or that the mixer is balanced.
- The example catalog has real clips for the longsword swings, the steel-on-bone impacts and the skeleton's bare-foot footstep only. The boots footstep, plate rattle, gem sounds, torch loop and drip are valid but silent placeholders (they show as validation warnings).
- **Wired:** `SimpleRPG > Audio > Wire Gameplay Prefabs And Scene` (idempotent) put `CombatAudio`, `AudioIdentity`, `FootstepEmitter` and `MovementAudioState` on the player (`TestCharacter`) and skeleton prefabs, plus `EquipmentMovementAudio` on the player, an `AmbienceController` in `SampleScene`, the gem profile on the four cut gems, and a steel material on the skeleton sword. The skeleton's two swing events now name audio events (`PlayEvent`) instead of bare clips, and its walk clip has foot-down events at 0.06 s and 0.68 s. The player's footsteps come from the foot bones (humanoid rig, root 0.43 m above the soles, measured from the run clip). The skeleton's death plays `Death_Skeleton`.
- Not wired: skeleton rise and hurt sounds, `AmbienceZone`s on room prefabs (the scene's default profile covers every room), door and lever interactables other than the chest and locked door, and the player's hurt and death sounds (no recordings).
- The boots footstep (the player's default footwear), plate rattle, gem sounds, torch loop and drip are silent placeholders, so **the player's own footsteps make no sound until a boots recording is added**. Skeleton footsteps do sound.
- The service plays nothing when there is no `AudioListener` in the scene, and does not warn about events with no clip: use the validator for that.
- Cast preparation and periodic effects have a profile slot and an API, but no ability node calls them yet. Drop has an action but no drop mechanic exists. Stack has an action but there is no stacking.
- `ProjectileAudio` decides "expired" from the projectile's age; a projectile destroyed exactly at its lifespan by a hit would be heard as expired.
- A pickup sound is played from the pickup's despawn on clients; a despawn for another reason (room teardown outside shutdown) would also play it.
- No occlusion, room reverb or AI hearing, by design.
- Hit sounds travel with the blade position at the confirmed moment; there is no per-surface impact for world geometry hits (arrows on walls) beyond the projectile's own impact event.
