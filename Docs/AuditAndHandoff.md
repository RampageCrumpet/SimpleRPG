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

---

# Appendix: equations, data and reference

Everything here was read from the code and assets as they stand. Numbers marked *(first guess)* are provisional by the owner's decision.

## A. From a swing to health lost

For a direct weapon blow (`MeleeAttack.asset` with the weapon damage calculation):

```
bonus      = WeaponDamage stat            (flat bonuses from every worn item, plus Enhanced Damage as a flat amount)
multiplier = WeaponDamageMultiplier stat  (1 + sum of the percentage bonuses of worn items, soft-capped)
weaponHit  = max(0, (weaponBase + bonus) x multiplier)               WeaponDamageCalculator, when the weapon touches the target
raw        = weaponHit x comboScalar(step) x (1 + 0.05 x PhysicalPower)   the attack ability's nodes; 0.05 is a constant in the asset
landed     = max(1, round( raw x regionFactor x (1 - reduction) ))        DamageMitigation.Final, rounded once
reduction  = clamp( the target's reduction stat, 0, 0.9 )                 0 if the request bypasses mitigation
```

- `regionFactor` is the struck body region's multiplier (the target's hit anatomy profile); 1 where no region was struck. A factor of 0 or less takes no damage.
- Which reduction is used depends on the damage type (`Physical` or `Magical`), nothing else. A bleed ignores armor because its tick sets `BypassesMitigation`.
- A character whose profile has no weapon damage stats (the skeleton) uses `weaponBase` as it is.
- Ability and magical damage skip the weapon damage calculation entirely.

**Enhanced Damage** is an affix stat effect with scope *This Item* on `weapon_damage`, layer `PercentAdd`. It is applied as the flat amount `value x weaponBase` (the weapon's `damage.Damage`), so it never scales another item's bonus. Percentages of Armor Rating scoped to the item work the same way on the item's own Armor Rating grant.

## B. Stats

`AttributeSet.Get(attribute)`:

```
value = ( start + sum(Add) ) x ( 1 + sum(PercentAdd) ) x product(Multiply)
final = softCap( value )                  if the stat's rule has a soft cap
start = formula for a derived stat, the base value for a primary one
```

A derived stat's formula is `baseValue + sum(contribution(source))` with `Linear: scale x x` or `Diminishing: scale x x / (x + halfPoint)` (zero for x at or below 0). Modifiers (item grants, statuses, affixes) are the layers above.

`SoftCap(threshold, slope)`: `f(v) = v` for `v <= threshold`, else `threshold + (v - threshold) x slope`; a threshold of 0 is no cap.

**Player profile (`Assets/Stats/PlayerStats.asset`)**, with every core attribute at 10:

| Stat | Formula | At 10 | Soft cap *(first guess)* |
|---|---|---|---|
| Max Health | 10 x Constitution | 100 | none |
| Movement Speed | 0.4 x Agility | 4.0 | counts half past 6 |
| Physical Power | 1 x Strength | 10 | none |
| Magical Power | 1 x Intelligence | 10 | none (nothing reads it) |
| Weapon Damage | 0 base | 0 | none |
| Weapon Damage Multiplier | 1 base | 1 | counts half past 1.5 |
| Interaction Speed | 1 base | 1 | counts half past 2 |
| Armor Rating | 0 base; items add | 0 | none |
| Physical Damage Reduction | 0.9 x Con/(Con+100) + 0.9 x Armor/(Armor+100) | 0.082 | counts a quarter past 0.6 |
| Magical Damage Reduction | 0.9 x Will/(Will+100) | 0.082 | counts a quarter past 0.6 |

Stats gameplay reads today: `AttributeConsumers.Keys`. Not read: Dexterity, Intelligence, Magical Power, Attack, Casting and Equip Speed, Carrying Capacity, Spell Preparation Capacity.

**Worked examples** (Strength, Constitution and the rest at 10):

- Bare longsword of 10, first combo step with scalar 1: `10 x 1 x (1 + 0.05 x 10) = 15` before mitigation. Against a target with 0.082 reduction and no region factor: `round(15 x 0.918) = 14`.
- A longsword of 10 with Enhanced Damage +50%, a +3 flat bonus from a ring and +20% weapon damage from a belt: `(10 + 5 + 3) x 1.2 = 21.6`, then `x 1.5 = 32.4` before mitigation.
- Armor Rating at Constitution 10: 28 gives reduction 0.279; 75 gives 0.468; 100 gives 0.532; 200 gives raw 0.682 and **0.62 after the soft cap**; 1000 gives raw 0.9 and **0.675**.
- Movement: Agility 10 with +50% is 6.0; with +200% the raw 12 reads **9.0**. Interaction Speed: raw 5 reads **3.5**.

## C. Affix roll (`AffixRoller`, `Item.RollAffixes`, `Inventory.AddItemInstance(item, itemLevel)`)

An item rolls once, when it first enters an inventory from an asset (a dropped instance keeps what it has). An item with no pool draws nothing.

1. **Eligible affixes**: in the pool, registered (has a short id), weight above 0, allowed in the item's slot, `minimumItemLevel <= itemLevel`, and `Problem() == null`.
2. **Rare?** Only if the pool can be rare (`rareChance > 0` and names set) *and* something is eligible: **one draw**, rare if `draw < rareChance`.
3. **Limits**: ordinary `minimumCount..maximumCount` with `maximumPrefixes`, `maximumSuffixes`; rare uses the `rare*` fields (default 3..6, up to 3 and 3).
4. **Count**: one draw, `count = min + floor(draw x (max - min + 1))`, capped at the eligible number.
5. **For each affix**: one draw to pick (weighted among those whose kind still has room and that still fit the storage), then **one draw per value** in effect order, each `min + draw x (max - min)` stored in thousandths. The chosen affix and every other of its family leave the candidates.
6. **If rare and anything rolled**: one more draw picks the rare name (`floor(draw x names)`), stored as its id.

**Storage** (`ItemAffixes`): a run of 32 words `[affixId, valueCount, values...]` repeated, ended by an id of 0, plus one `rareName` id. An affix takes `2 + valueCount` words. `AffixPool.Problems()` checks the largest possible roll fits. It is replicated inside inventory and equipment entries and the world pickup, and is the form a stash would store.

**Item level**: `level = clamp(area.base + sum of ItemLevelOffset along the path, 1, area.ceiling)` where the offsets are those of every table and entry the drop came through (`LootDrop.LevelOffset`) plus the container's own. The area is `DungeonPopulation.floorLoot[floor]` (the last entry for floors past the list; the lowest level with no list). Tiers: tier 1 from level 1, tier 2 from 8, tier 3 from 16 in both catalogs.

**Names**: magic: `{prefixes} {item name} {suffixes}`; rare: the name from `RareNames` with the base item beneath it in the tooltip; a removed rare name falls back to the magic form.

## D. Interaction (`Interactor`)

Owner asks `BeginRpc(anchor, id)` (throttled to 5 a second, burst 10). The server accepts if the character is alive, the target is active and `CanBeInteractedWith`, and `distance <= InteractionRange + 2`. It sets `duration = InteractionTime / max(0.1, InteractionSpeed)`, runs the clock with `Time.deltaTime`, ends the interaction if the character moves more than 0.5 m from where it started, dies, the target is destroyed, or the server accepts an ability or item cast, and calls the interactable's `ActivateInteraction` once the time is up. `FinishInteraction` runs when it ends for any reason. What the player's machine needs is sent by `Interactable.NotifyInteractor` to that machine only.

## E. Triggered effects (`TriggeredEffectEngine`)

- **Events**: `OnHit` and `OnTakeDamage` come from `DamageResolvedEvent` (a hit only for direct weapon provenance), `OnKill` and `OnOwnerDeath` from `EntityDiedEvent`.
- **Order**: trigger kind, then scope (`Host` needs the damage's source to be the effect's host), then every condition, then the chance (one draw, only when the chance is between 0 and 1), then the ability runs with `host` as source, the owner as instigator, the other party as `Trigger Other` and the chosen party as `Trigger Target`.
- **Hosts**: a worn item (its asset's effects plus its affixes'), an applied status (each status group is its own host). A host's effects start when it starts and end when it ends.
- **Guards**: depth 8 and 256 events per drain stop an effect from looping.

## F. Defaults and first guesses

| What | Value | Where |
|---|---|---|
| Rare chance, counts | 15%; 3 to 6 affixes, up to 3 prefixes and 3 suffixes | each pool; `RareNameSeed.DefaultChance` |
| Ordinary counts | 0 to 2 affixes, 1 prefix, 1 suffix | each pool |
| Tier item levels / weights | 1, 8, 16 / 10, 5, 2 | `AffixCatalogBuilder` |
| Bleed | 20% on hit; 2 damage per stack per second; 6 s; 5 stacks; ignores armor | `BleedingSetup` |
| Soft caps | listed in B | `PlayerStats.asset` |
| Garment fold | body half-width 0.22 m, sleeves kept to 0.25, floor size 0.55 m | `GarmentFold.Settings.Default` |
| Interaction | reach slack 2 m, move limit 0.5 m, minimum speed 0.1 | `Interactor` |
| World pickup | range 2.5 m, time 0.1 s | `ArmorWorldFormSetup` |

The catalogs hold 13 armor families (6 prefix: armor flat, armor enhanced, movement, ward, burden, protection; 7 suffix: vigor, constitution, agility, strength, weapon flat, weapon percent, hands) and 2 weapon prefix families (Enhanced Damage, flat damage), each three tiers, plus the single "of Bleeding".

## G. Menu tools, and the order to run them

| Menu | Makes | Never |
|---|---|---|
| Items > Build Armor Items And Parts | armor items and their parts from the art | changes an existing item |
| Appearance > Build Human Male Parts And Rig | body parts, rig prefab | |
| Effects > Build Bleeding Effect | bleed abilities, status, effect, affix | changes an existing asset |
| Items > Build Armor Affix Catalog | armor affixes, a pool per armor item | changes an existing asset or pool |
| Items > Build Weapon Affix Catalog (after the two above) | weapon affixes, the longsword's pool | |
| Items > Build Rare Names | `Assets/Items/RareNames.asset` | changes an existing one |
| Items > Enable Rare Items On All Pools | rare fields on pools that lack them | |
| Items > Build Armor World Forms | pickup prefabs, network-prefab entries | replaces an item's existing form |
| Definitions > Rebuild Definition Registry | short network ids | run after creating any definition |

## H. Where things live

| Thing | Files |
|---|---|
| Triggered effects | `AbilitySystem/Runtime/Rules/*`, `Integration/EquipmentEffectProvider.cs`, `Integration/BodyLocationCondition.cs`, `Items/TriggeredAffixEffect.cs` |
| Affixes | `Items/Affix*.cs`, `StatModifierEffect.cs`, `ItemAffixes.cs`, `ItemTooltip.cs`, `RareNames.cs`, `LootArea.cs` |
| Loot | `Loot/LootGeneration/*`, `World/Interactables/LootContainer.cs`, `World/DungeonPopulation.cs` |
| Stats | `AbilitySystem/Core/Attributes/*` (`SoftCap`, `DerivedStats`, `AttributeConsumers`), `Combat/CharacterStats.cs`, `Combat/WeaponDamageCalculator.cs`, `Stats/PlayerStats.asset` |
| Interaction | `Interaction/Interactor.cs`, `Interactable.cs`, `World/Keys/*` |
| Appearance | `Presentation/Appearance/*`, `Tools/Editor/Appearance/*` |
| Catalog tools | `Tools/Editor/Effects/*` |
| Tests | `Tests/EditMode/*`, `Tests/AbilitySystem/*`, `Tests/PlayMode/*` |

Companion documents: `TriggeredEffects.md`, `Affixes.md`, `Stats.md`, `Equipment.md`, `NetworkAuthority.md`, `CharacterAppearance.md`, `RigMigrationReport.md`, `ArtLeftoversHandoff.md`, `CodeLayout.md`.
