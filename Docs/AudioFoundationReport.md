# Core audio infrastructure: completion report

**Finding first.** The foundation this assignment describes already existed in the project (`Docs/Audio.md`, `AudioModel`, `AudioPlayback`, `Tools/Editor/Audio`, five test files) and meets the checklist. Building another would have created the competing system the assignment forbids, so this pass was an inspection against each requirement, the gaps filled, and this report. Nothing was written without a compiler's help; what was and was not run is stated in section 5.

## 1. Architecture (what exists)

`gameplay event -> adapter -> AudioContext -> GameAudio.Play -> AudioEventBase.Resolve -> AudioService -> pooled AudioSource -> AudioMixer group`. Full description and the table of pieces: `Docs/Audio.md`. Requirement by requirement:

| Requirement | Where it is met |
|---|---|
| Multiple clips, randomised variation, volume and pitch variation, layers | `AudioEventDefinition` layers with weighted clips; `VoicePlanner` (skips the clip just heard) |
| One-shot and sustained looping | `AudioPlaybackMode.OneShot` / `Sustained`; `AudioHandle.Stop` with fade |
| Spatial and nonspatial, attenuation | `AudioSpatialMode`; min/max distance, rolloff mode, custom curve, doppler per event |
| Mixer routing | `AudioCategory` -> `AudioRouting` mixer groups; exposed `Volume_<Category>` parameters; `GameMixer.mixer` |
| Priority and concurrency | `importance`, `maxConcurrent`, `AudioLimitBehavior` (stop oldest or drop new), `minInterval`, global voice cap |
| Missing-asset fallbacks | null event, missing clip, no table row, no mixer, no listener: silence, never an exception; a table's fallback row; the validator reports silent events |
| Unity 6 Audio Random Containers | a layer may point at one (`AudioSource.resource`); our weighted choice exists for tables, chance and assertions (reasoning in `Audio.md`) |
| Play at a position, follow a transform, nonspatial UI, start/stop sustained | `GameAudio.Play`, `PlayAttached`, `Play2D`, `Stop`; a loop attached to a destroyed owner ends |
| Pooling and cleanup | voices made once and reused; `FrequentPlayDoesNotCreateGameObjectsAfterWarmUp` |
| Dedicated/headless | `AudioService.IsHeadless` (no graphics device): the service is never created, every call a no-op |
| Contextual selection | `AudioContext` (Surface, Footwear, Source, Target, Armor, Subject, Region, Intensity); `AudioEventTable` rows, most specific wins, tag parents, nested tables |
| Surfaces independent of visuals | `AcousticSurface` assets (stone, wood, dirt, gravel, metal, water, ground); `SurfaceQuery` (collider source, physics-material map, default; trigger overlays); `AcousticSurfaceSource.ContactSounds` (the creaking bridge) |

## 2. Gameplay events integrated

- **Swing**: the combo step's `SwingSound` and time, from the attack-started event (prediction or server word), one event per attack, cancelled if refused.
- **Confirmed impact**: `HitConfirmedClientRpc` from `MeleeDeliveryBase`. **`WeaponHitConfirmedEvent` was inspected and is not used for audio**: it is a server-only dispatcher event raised after damage resolves and never reaches clients. Impact audio hangs off the RPC raised at the same line, so there is one hit pathway and two consumers (effects on the server, sound on every machine).
- **Hurt, death**: `AudioIdentity` on the replicated health and life state.
- **Collapse (added in this pass)**: `AudioIdentity.OnCollapse()`, an animation event at the landing frame; resolved by ground surface, creature and body material, plus the ground object's impact contact sounds.
- **Footsteps and equipment**: `FootstepEmitter` from foot-down animation events or foot bones; `MovementAudioState` / `EquipmentMovementAudio` layers (footwear, armor, exclusive groups, chance, cooldown).
- **Spells and projectiles**: `SpellAudioProfile` and `ProjectileAudio` (release, attached flight loop, impact).
- **Inventory**: `ItemAudioProfile` with family parents; place and move from the local move transaction; pickup from the server despawning the pickup; **drop (added)** from `WorldItemPickup.DroppedRpc`, sent once when the server makes the pickup for a dropped item.
- **Interactables**: `InteractableAudio` cues. Since interaction became server-held, the open, close, unlock and locked sounds reach only the interacting player's machine through `Interactable.NotifyInteractor`, as they did before.
- **Ambience**: `AmbienceController`, `AmbienceZone`, `AmbienceProfile` (quiet beds, sparse intermittent sounds, no music).

**Duplicates**: `AudioEventLedger` makes a predicted swing and its confirmation one sound. Footsteps, hurt, death and collapse are heard from each machine's own state or animation and are never sent. Network-triggered sounds (impact, drop) come from one server RPC each.

## 3. Authoring and editor setup

`SimpleRPG > Audio > Create Mixer And Routing`, `Create Example Catalog` (both idempotent, never overwrite), `Validate Audio Definitions`, `Wire Gameplay Prefabs And Scene` (idempotent). Inspectors for events and tables have Play/Stop and a context form. No production recordings are generated. Details: `Docs/Audio.md`, "Authoring".

## 4. Example configurations

`Assets/Audio/Catalog/`: three longsword swings, two skeleton swings, steel-on-bone impacts (with a head row), a stone footstep for boots and bare feet with the tag fallback chain (gravel -> dirt -> ground), plate rattle, gem pickup and place, torch loop, drip and the crypt ambience profile, the skeleton death, and the seven required cases checked by `ExampleCatalogCoversTheSevenRequiredCases`.

## 5. Tests: written and run

**Run by the owner and green** (the full suites at `d866d6c`, which include these before this pass): `AudioResolutionTests` (table specificity and fallback, planner weights and chance, validation, footwear, equipment layers, item families, ledger, intermittent sounds, regions, headless, null safety), `AudioCatalogTests` (assets, routing, shipped prefab wiring), `AudioServicePlayModeTests` (voice reuse, per-event and global limits, minimum interval, layers, loop start/stop and fade, attached follow and owner destruction, loop emitter lifecycle, spatial settings, mixer routing, missing clips, no listener, surface query and overlays, footsteps, ambience and zones, no allocation after warm-up), `AudioCombatPlayModeTests` (swing timing, refused swings, late join, impact by material and region, weapon override, duplicate hit once, delivery event reaches the listener, safe on missing identity), `AudioPrefabWiringPlayModeTests`.

**Written in this pass, not run** (no compiler): `AudioCombatPlayModeTests` gains three tests, `ACollapseSoundsLikeTheGroundTheBodyLandsOn`, `ADyingBodyDoesNotCollapseUntilTheAnimationSaysSo_AndAnOldBridgeCreaksUnderIt`, `ACollapseWithNoSoundOrNoSurfaceIsSilentAndSafe`.

**Not tested at all**: `WorldItemPickup.DroppedRpc` (it needs a networked drop; `ItemTransferIdentityTests` exercises the drop but not its sound); anything about how it sounds.

**Validation still needed**: run the three new tests; drop an item in a host session and a client session and listen once on each; add `OnCollapse` to the skeleton's death clip and play it.

## 6. Remaining integrations (not architecture)

Spell preparation and periodic effects (the profile slots and API exist; no ability node calls them, so an ability needs a small adapter that holds the handle and stops it on interrupt); the `OnCollapse` animation event on each death clip; doors and levers other than the chest and locked door; the player's hurt and death; skeleton rise and hurt; `AmbienceZone`s per room; world-geometry impacts for projectiles; stacking (no mechanic).

## 7. Sounds needed for the first combat pass

Existing real clips: the three longsword swings, steel on bone, the skeleton's bare-foot step, the skeleton death. Needed, in order of how often they are heard:

1. Steel on flesh, and steel on plate (impacts for the player being struck; a head-row variant each).
2. Player boots on stone, and on the other five surfaces (wood, dirt, gravel, metal, water): a short set each.
3. Player hurt (two or three) and death.
4. Skeleton hurt, rise, and the collapse (bones landing on stone).
5. Plate armor rattle for movement.
6. A shield or parry impact when blocking exists.
7. Spell prepare, release, flight loop and impact for the first spell.
