# Anatomical hit regions

Where a weapon strikes a body decides what the blow is worth. A skull is worth more than a forearm. Players and monsters use the same regions, the same contact rule and the same damage path; only their hurtboxes and their multiplier tables differ.

Bead: `simplerpg-7du.1` child "Anatomical hit regions". Related: `UpperBodyArticulation.md` (the pose the regions follow), `NetworkAuthority.md`.

## The pieces

| Piece | Where | Does |
|---|---|---|
| `HurtBox` | `Characters/Hurtbox.cs` | One region: a trigger collider on the **HurtBox** layer, on the bone it follows. Names a `BodyLocation`, an owner `Character` and an optional `Priority`. |
| `BodyLocation` | `Characters/BodyLocations.cs` | The vocabulary of regions: `Head`, `Torso`, `Legs`, `Arms`. Append-only (prefabs store the number). |
| `Hitbox` | `Characters/Hitbox.cs` | A weapon's blade. Each physics step it tests the poses the blade passed through and reports **one** region per character through `HitboxCollisionEvent`. |
| `HitCandidate` | `Characters/HitCandidate.cs` | The contact-resolution rule when a blade overlaps several regions of one character. |
| `Weapon` / `SwingHitTracker` | unchanged | One hit per target per swing, never the wielder. |
| `MeleeDeliveryBase` | unchanged | Turns the confirmed hit into a `MeleeHit` carrying the region; the ability graph computes the damage as before. |
| `HitRegionProfile` | `Characters/HitRegionProfile.cs` | Asset: what a blow to each `BodyLocation` is worth against one kind of body. |
| `HitAnatomy` | `Characters/HitAnatomy.cs` | On the character root. Holds the profile, answers the resolver, and turns every region off on death and back on at revive. |
| `IRegionDamageModifier` | `AbilitySystem/Core/Combat` | The one hook the resolver uses. `Character` implements it by asking its `HitAnatomy`. |
| `CombatResolver.AdjustForRegion` | `AbilitySystem/Core/Combat` | Applies the multiplier, once, to a weapon blow. |

## The path of a blow

```
Hitbox pose  ->  HurtBox chosen (HitCandidate rule)  ->  Weapon (once per target per swing)
   -> MeleeDelivery: MeleeHit(victim, bodyLocation)
   -> ability graph computes the damage (weapon damage, combo, Melee Power ...)     [existing]
   -> DamageRequest(amount, instigator, source, bodyLocation, DirectWeaponHit)       [existing]
   -> CombatResolver.Apply
        1. dead / no-damage checks                                                    [existing]
        2. AdjustForRegion: amount x the victim's multiplier for that location        [new]
        3. (future) equipment mitigation for that location
        4. fatal interceptors, health commit, DamageResolvedEvent, death              [existing]
```

Rules of the adjustment:

- Only a request with `DirectWeaponHit` provenance **and** a body location is scaled. Spells, damage-over-time and hazards carry `DamageRequest.NoBodyLocation` (-1) or another provenance and are untouched.
- It happens in exactly one place, so it cannot be applied twice. `DamageResolvedEvent`, rules and death all see the final amount; instigator, source, location and provenance are kept.
- The result is rounded to a whole number, and a blow that lands never rounds below 1. A multiplier of 0 deals nothing.
- A body with no `HitAnatomy`, or no profile, or no entry for the location, takes the blow unchanged (factor 1).

Starting multipliers (playtest values, per asset): Head 1.75, Torso 1.0, Arms 0.75, Legs 0.75. Assets: `Assets/Prefabs/Characters/HitRegions/HumanoidHitRegions.asset` (player, gremlin) and `SkeletonHitRegions.asset`.

## Which region a blade strikes

One swing hits a target once, so the question is *which region of that target it touches first*, and it must not depend on the order physics lists colliders in. `Hitbox.FixedUpdate`:

1. Syncs transforms (hurtboxes ride animated bones) and walks the blade's path from the previous step's pose to this one: at most 12 poses, spaced by the blade's thinnest extent, position lerped and rotation slerped. The first step after the window opens tests only the current pose.
2. At each pose it collects the hurtboxes the blade overlaps, keeps the best one **per character** under the rule below, and reports characters not yet reported. A character is reported once until the hitbox is switched off and on again (the next swing).
3. Poses are tried in travel order, so a character is struck at the **first pose that touches any of its regions**. A chop that goes head, chest, foot is a head blow.

Rule for several regions of one character at the same pose (`HitCandidate.IsPreferredTo`), in order:

1. Smaller distance from the blade's centre to the region's surface (0 when the centre is inside it).
2. Smaller distance from the blade's centre to the region's own centre.
3. Higher `HurtBox.Priority`. Leave at 0 unless two regions are *meant* to overlap and the first two cannot separate them.
4. Ordinal order of the bone path, so equal contacts still give one answer.

Damage never takes part: no region is chosen for being worth more. Distances within 0.1 mm count as equal.

Known simplification: the blade's centre stands for the contact. A long blade whose tip pierces one region while its middle is inside another, at the very first pose of contact, takes the region nearest its middle. In a sweep the tip reaches a region before the middle does, so the earlier pose normally decides.

## Poses, animation and the server

The regions are children of the bones, so animation, `SpineRig` twist/tilt and a lean all move them; there is no second representation. Order in a frame: `Update` (look input) -> animator -> `LateUpdate` (`SpineRig`) -> next `FixedUpdate`: `Hitbox` syncs transforms and probes. Hits therefore see the pose that was drawn.

Only the server decides hits (`Weapon.TargetHit` ignores clients; `CombatResolver` is server only). The server animates every character itself (`AnimatorCullingMode.AlwaysAnimate` is forced for melee characters) and a remote player's upper body is posed on the server from the look input the client sent (`SubmitLookServerRpc`, clamped and validated there). So nothing about regions or bones is replicated, and no message carries a region: a client cannot name the region it hit. `AnatomicalRemotePoseTests` proves a remote client's lean moves its head region on the server.

Death: `HitAnatomy` disables every region's colliders when `Character.OnDeath` fires (every machine) and enables them on `OnRevive`. A corpse answers no physics query and cannot be reported; `CombatResolver` also refuses damage to the dead.

## Adding a region to a new monster

1. Pick the bones that carry the body: give each a trigger collider (box, sphere or capsule, sized to the mesh, fitted in bone-local space) on the **HurtBox** layer, and add a `HurtBox` to the same object. Set its `BodyLocation`. The rig need not be humanoid.
2. Add `HitAnatomy` to the root of the character and assign a `HitRegionProfile` (Create > SimpleRPG > Hit Region Profile) listing the multipliers. A region the profile does not list is worth 1.
3. A body part the four locations do not fit (a spider's abdomen): **append** a value to `BodyLocation` (never reorder or renumber) and list it in the profile. Nothing in combat resolution changes.
4. If two regions must overlap (a neck shared by skull and chest), leave `Priority` at 0 unless the geometry cannot separate them.
5. Do not parent a hurtbox to something that is not animated with the body, and do not put hurtboxes on the weapon's layer.

Checklist the tests enforce: every region is a trigger on the HurtBox layer with an owning `Character`; the body covers the locations its profile talks about.

## Not done here (on purpose)

Armour coverage, mitigation, durability, paper-doll, stagger, dismemberment, localized injury, new animation, new UI/VFX/audio. Step 3 in the path above is where per-region mitigation will go: it takes the final location and the post-multiplier amount, so it needs no change to anything upstream.
