# Ability and effect system

How abilities, effects, statuses, grants and rules work, where each part lives, and where the implementation deliberately differs from `SimpleRPG_Ability_Effect_System_Architecture.md` and its Decisions Addendum. The addendum wins where the two disagree. The visual editor is described in `AbilityEditor.md`; the network rules are in `NetworkAuthority.md`.

## Shape

An ability is a ScriptableObject (`AbilityDefinition`) holding a flat list of nodes and edges. It is compiled into runtime operations and run by one server-only executor. Nothing about the user is assumed: a player, a monster, an item, a trap or the world can all run the same asset, supplying a source, an optional instigator and a position. Definitions never change while the game runs; everything that changes lives in runtime services.

| Assembly | Holds | May reference |
|---|---|---|
| `SimpleRPG.Gameplay.Core` | Ids, `SourceHandle`, clock, attributes, combat and death pipeline, event dispatcher, equipment and item-state records | nothing in the game |
| `SimpleRPG.Abilities.Runtime` | Definitions and registry, graph schema, compiler, executor, rules, statuses, grants, item casting | Core |
| `SimpleRPG.Abilities.Integration` | Adapters to the game: network clock, real projectile and melee delivery, spawner, `AbilityUser`, `AbilityServices` | Runtime, Core, the game assembly, Netcode |
| `SimpleRPG.Abilities.Editor` | Graph editor, test world, trace viewer, registry tools and build gate | everything above |
| `SimpleRPG.Abilities.Tests.*` | EditMode tests (real services, in-memory world) and PlayMode tests (real host and a real remote client) | |

## Decisions, and where this differs from the spec

- **Delivery is C#, composition is graph.** Projectile and melee mechanics stay as reusable code (`ProjectileDelivery`, and for melee `ComboMeleeDelivery` for combo weapons or `MeleeDelivery` for conventional enemies, both on `MeleeDeliveryBase`). The graph chooses them and says what happens on `OnImpact`, `OnHit` and `Completed`. A paused ability resumes through a typed continuation token. If a projectile is removed without hitting or expiring, it reports expiry so nothing waits forever.
- **Identity.** Every definition has a permanent id independent of name and path. Runtime executions, item instances, status stacks and death transitions have session ids. A registry in `Resources` maps ids to short network ids, so builds and dedicated servers need no `AssetDatabase`. The old `Type.GetType` and name-key lookups are gone, and no content hashes are used (addendum Decision C).
- **Build gate.** A build fails when a definition has a missing or duplicate id, when the registry is out of date, or when any ability graph has a compile error. Errors name the asset, node and port.
- **Activation request.** The spec sketches a request with targets, parameter bindings, trigger payload and authority context. This implementation passes source, instigator, origin, direction and an optional status invocation. Aim is the character's facing; a client never supplies targets, damage or timing. Parameter bindings are node settings (`AttributeBinding`), not part of the request.
- **Evaluation policies.** Attribute reads are `OnActivation` (snapshot) or `OnExecution` (live). `OnApplication` and `Continuous` are not implemented because no scenario needed them.
- **Primitive library.** Only primitives that real content needed exist: Activate, Status Tick, Constant, Attribute, Add, Multiply, event values, Entities In Radius, Current Target, Chance, For Each Target, Melee Swing, Launch Projectile, Damage, Apply Status, Spawn Entity and the status value nodes. Branch, Sequence, Delay, Heal, ExecuteAbility, RemoveStatus, grants as nodes and presentation requests are not built (spec §14, addendum exclusions). Nothing is subgraph-reusable yet.
- **Statuses.** Grouping, reapplication, duration, refresh, magnitude, overflow and periodic policies are all implemented and tested. `MagnitudePolicy` has `Additive` and `StrongestOnly`; the spec's `IndependentContributions` behaved identically to `Additive` for combined ticks and per-stack ticks are already `PeriodicMode.PerStack`, so it was removed. Statuses cannot yet change attributes, so effects such as Slow are not possible (tracked in the stats bead).
- **Deferred by the addendum:** perception and invisibility, brazier and area-membership events, recharge, content hashes, save persistence, JSON mods.

## Lifetimes and policies

- **Death.** Lethal damage commits `Alive -> Dead` once per life. Then, in order: `DamageResolved`, `EntityDied` (carrying the death position and the items that qualified at that moment), `DeathFinalize`. Reactions to the death use the captured snapshot, so taking the amulet off during the death does not cancel its reaction. The same transition never reacts twice; a revive starts a new life.
- **What death cleans up.** Statuses end with reason `TargetDied` at `DeathFinalize`. Equipment grants and item rules stay on a dead wearer until the item is unequipped or the entity despawns. This is deliberate: a revived character keeps its equipment, and a second death can fire the death rules again. `DeathFinalize` is delivered even when the event queue is over budget.
- **Event queue.** Ordered, one queue, depth limit 8 and 256 events per drain. Over the limit, events are dropped and reported, except death cleanup.
- **Potency and grants.** Timed effects snapshot potency when applied; persistent grants read live values.
- **Charges.** Kept per item instance in `ItemStateStore`, spent only on the server, replicated read-only to the owner. There is no recharge.
- **Compiled form.** The compiled graph is cached beside the asset, never in it.
- **Tracing.** One optional `IExecutionTrace` on the real executor. Without a trace attached, nothing is recorded and no text is built.

## Authority

Clients ask; the server decides. An ability request carries only an ability short id (or a slot and an ability position for items). The server checks ownership, that the character has the ability, life, cooldown, eligibility and charges. A client cannot change health, equipment or charges, and cannot read another player's charges. These are tested with a real second client on the loopback network (`RemoteClientNetworkTests`).

## Running the tests

With the editor open, use the Test Runner window. Without it (the project must not be open in another editor):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Unity.exe" -batchmode -projectPath "<project>" -runTests -testPlatform EditMode -testResults results-edit.xml -logFile unity-edit.log
"C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Unity.exe" -batchmode -projectPath "<project>" -runTests -testPlatform PlayMode -testResults results-play.xml -logFile unity-play.log
```
