# Notes from the 2026-11-04/05 work: what to check, and what to tidy

Written at the end of a long run of changes, some of them not yet compiled or tested together. This is the starting list for the next audit.

## Untested together
The last commits (interaction authority, soft caps, weapon and armor affix catalogs, the tooltip, world forms, rare names, bodies filling on search) were written without a compiler. The first thing to do with credits back is a full EditMode and PlayMode run and a read of the console for warnings. Most likely trouble spots:
- `Interactable`/`Interactor`: RPCs in an abstract `NetworkBehaviour`, `RpcTarget.Single(..., Temp)`, `RequireOwnership` on `[Rpc]`; the host-less (offline) path.
- `ItemAffixes`: a private nested struct serialized through `INetworkSerializable`, and the two rare-name fields on the wire.
- Editor tools that write assets through `SerializedObject` with `managedReferenceValue` (the affix catalogs, bleeding).
- `ArmorWorldFormSetup`: `NetworkPrefabsList.Add`, baking skinned meshes in an instantiated rig.

## Design points worth another look
- **Physical Power lives in an ability asset** (`MeleeAttack.asset`: 1 + 0.05 per point), while the rest of the weapon damage calculation is code (`WeaponDamageCalculator`). It works and keeps today's numbers, but the split should be a decision, not an accident.
- **`AttributeConsumers` is a hand-kept list.** It keeps non-functional affixes out of loot only while someone updates it. A test checks the entries exist, not that something really reads them.
- **`Item.DisplayName` is cached on first use** and reads `DefinitionRegistry.Instance`. Fine in play; stale if the registry is swapped under a live item (tests do this).
- **`ItemTooltipView` is a static singleton** panel. Acceptable for one HUD; revisit with split-screen or several canvases.
- **Chests and bodies roll on first search**, from the shared gameplay stream, so the stream's order now depends on when players open things. Fine for single-seed reproducibility of the layout, not for replaying a whole session from a seed.
- **`WeaponHitConfirmedEvent`** is still raised and kept on purpose as a valid trigger, but nothing in the game listens to it now.
- **Interaction cancel** is now server-side for ability use, death, distance and a vanished target. It still cannot see an item being dropped or a menu action; add those if they should end an interaction.

## Docs to tidy
- `Docs/LocalAgentHandoff.md` and the two reports are history; keep or archive.

## Content waiting on the owner
- The rare item chance and counts (15%, three to six affixes) are first guesses.
- A hand-made world form for any armor whose generated one folds badly.
- The art and rig list in `Docs/ArtLeftoversHandoff.md`.
