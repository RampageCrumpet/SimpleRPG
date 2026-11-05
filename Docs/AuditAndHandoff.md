# Audit and hand-off, 2026-11-05

The state of the project at the end of the long session that built the A-pose migration, the effect system, affixes and the equipment loop. It says what exists, what has and has not been run, what is risky, what the owner still has to decide, and what to do first. Branch: `claude/zen-brahmagupta-i40kaz`.

## 1. Verification status

The owner ran EditMode and PlayMode after each round and fixed what failed. The last full green run covered everything up to **`d866d6c`** (weapon and armor affixes, bleeding, soft caps, server-authoritative interaction, the tooltip, the weapon-source change, item level, weapon damage).

**Not run by anyone since it was written** (no compiler was available):

| Change | Where |
|---|---|
| Bodies fill on first search | `LootContainer.RollsWhenOpened`, `LootableCorpse`, `LootableCorpseNetworkTests` |
| Armor world forms | `ArmorWorldFormSetup`, `GarmentFold`, `GarmentFoldTests` |
| Rare items: chance on a pool, one name from a list | `AffixPool`, `AffixRoller`, `ItemAffixes`, `RareNames`, `RareNameSeed`, `RareNameTests`, ten patched pool assets, `Assets/Items/RareNames.asset` (written by hand) |
| The roll no longer draws for a pool with nothing to roll | `AffixRoller` |

Mechanical checks done on the current tree: every file has a `.meta`, no duplicate GUIDs, braces and parentheses balance in every C# file, the layering check is clean, and no code refers to the removed rule engine, pickup requests or rare-name classes.

## 2. What exists (by system)

- **Rig and body.** The game body, avatar, parts and armor are on the canonical 52-bone A-pose rig (`RigMigrationReport.md`, `BodyMigrationReport.md`). A part can be several pieces (`BodyPartPiece`), so plate and its mail undercoat both render. `Socket_Headgear` is the head attachment; `RigConformance` and `RigConformanceTests` guard against the rest poses drifting apart again.
- **Triggered effects** (`TriggeredEffects.md`). One engine, a trigger plus conditions plus an ability; hosts are worn items (with the effects their affixes bring) and applied statuses. Triggers today: `OnHit`, `OnKill`, `OnTakeDamage`, `OnOwnerDeath`. A weapon swing runs with the weapon as source and the wielder as instigator.
- **Affixes** (`Affixes.md`). Prefixes and suffixes, any number of effects each, independently rolled; families, tiers by item level; item-scoped percentages; triggered effects as an affix effect; names ("{prefix} {item} {suffix}"), tooltips, rare names; item level from the area, the source and the nested tables. Catalogs for armor and weapons and a bleed are built by menu tools and are provisional.
- **Stats** (`Stats.md`). Weapon damage (base plus bonuses, times a multiplier; then the ability's combo scalar and Physical Power; then armor), Interaction Speed, soft caps on both damage reductions, movement speed, interaction speed and the weapon damage multiplier.
- **Authority** (`NetworkAuthority.md`). Interaction is held by the server: the target, the time (over Interaction Speed), reach, cancel on walking off, dying, a vanished target or any ability use. Chests and bodies fill on first search.
- **Equipment loop.** World forms for armor (generated or authored), the tooltip view, drop and pickup keeping the same item instance.

## 3. Findings, most important first

1. **Item level is 1 everywhere until `DungeonPopulation.floorLoot` is set.** The field is not in `SampleScene` yet (checked), so every area is the lowest level and only first-tier affixes can roll. Set a base and ceiling per floor on the `DungeonPopulation` component. This is the single most likely "why do I never see tier 2" report.
2. **The untested set above.** Most likely to need a fix: the editor tool's mesh baking (`ArmorWorldFormSetup`), `RareNameTests` against the final pool fields, and the hand-written `RareNames.asset` and pool YAML (if Unity rejects a field, re-run **Build Rare Names** after deleting the asset and **Enable Rare Items On All Pools**).
3. **Melee reach fell** from about 0.8 m to 0.5 m for the longsword to hit a skeleton (`MvpGameplayTests` was changed to match). Almost certainly the A-pose migration and the re-fitted sword mount; confirm in play. This changes how combat feels.
4. **Hands on the sword are not right** (`simplerpg-5e5`); armor gloves were not re-fitted. See `ArtLeftoversHandoff.md`.
5. **Physical Power lives in an ability asset** (`MeleeAttack.asset`, 1 + 0.05 per point), the rest of weapon damage is code. Deliberate (it keeps today's numbers), but a split worth deciding about.
6. **`AttributeConsumers` is a hand-kept list.** It is what keeps affixes that do nothing out of loot. A test checks the entries exist, not that something reads them; update it in the same change that makes a system read a stat.
7. **Loot rolls depend on when players open things.** Chests and bodies draw from the shared gameplay stream when first searched, so the stream's order is no longer fixed by the seed alone. The dungeon layout and population still are.
8. **`WeaponHitConfirmedEvent` is raised and nothing uses it.** Kept on purpose by the owner as a valid trigger.
9. **Small design debts.** `Item.DisplayName` is cached on first use and reads `DefinitionRegistry.Instance`; `ItemTooltipView` is a static singleton panel; a stat sheet's soft caps apply to derived profiles only (an enemy's direct profile states its values).
10. **Authority gaps that remain.** An interaction is not ended by dropping an item or other menu actions; only the listed events end it. Enemies have no armor stat, so an armor-shredding effect has nothing to act on.

## 4. Decisions the owner has made (do not reopen)

One part per slot made of several pieces; world forms generated by default with authored overrides; one canonical A-pose rig and no per-item fixes; per-item affix pools (Diablo II style); armor percentages scoped to the item; no uniform balancing rule, provisional numbers are fine; item level from the area, the source and the nested tables, never the floor number; Enhanced Damage is local, other bonuses global; bleeding first; everything server-authoritative; chests and bodies fill when first searched; rare names from the owner's list of 100; soft cap on damage reduction first; first-guess numbers are acceptable until more systems exist.

## 5. First hour with a compiler

1. Pull the branch, open Unity, read the console for errors and warnings before running anything.
2. Run EditMode, then PlayMode. Expect to fix small things in the section 1 list.
3. Set `floorLoot` on `DungeonPopulation` (finding 1).
4. Run **SimpleRPG > Items > Build Armor World Forms**; look at each generated form; make a hand-made model for any that fold badly and put it in `Assets/Art/WorldForms/Overrides`.
5. Play: kill skeletons, search the bodies and a chest, hover items, wear a rare, hit a dummy with the longsword and watch the bleed. Check the reach (finding 3).
6. File the art items from `ArtLeftoversHandoff.md` as beads.

## 6. Where to add things

`CodeLayout.md` for assemblies. A new behaviour is assets first: an ability graph (start it at a **Triggered Effect** node) and a status, a `TriggeredEffect` asset, then a host (item list, status list or an affix's `TriggeredAffixEffect`). A new trigger needs the event behind it (`EffectTrigger`, `TriggeredEffectEngine`), a new condition is an `EffectCondition` subclass, a new affix effect kind is an `AffixEffect` subclass. A new stat that affixes may use goes in `DerivedStats`, a profile rule and, once something reads it, `AttributeConsumers`. Rare names: add to the end of `RareNames.asset` with the next id.

## 7. Waiting on other systems (not oversights)

`OnCrit`, `OnAttack`, `OnBlock`, a "while equipped" condition, rolled parameters for triggered affixes, granted-ability affixes, enemy armor, legs and feet art, unique items with fixed affixes, a UI for item level on the tooltip.
