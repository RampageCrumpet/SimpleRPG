# Item affixes

An item can roll bonuses (affixes) when it comes into the world. They are **fixed for the life of the item**: nothing rerolls or changes
them. There is no durability: nothing in the game wears out or breaks through use, by design.

## The rules

- An item asset may point at an **Affix Pool**. An item with no pool has no affixes, and the game rolls nothing for it.
- Each new *instance* rolls once, when it is created from the asset (into a chest, a pickup, any inventory through `AddItemInstance(item, itemLevel)`). The asset is never changed.
- An affix is a **prefix or suffix** of the item's name and does **one or more things** (its *effects*). Each effect rolls its own value, independently, from its own range. There is no limit to the number of effects in an affix.
- Affixes of one **family** are its tiers: an item never gets two of a family, nor the same affix twice. A pool limits how many prefixes and suffixes an instance gets, and how many in all.
- **Item level and tier.** An affix has a minimum item level, so a higher tier is only offered from a stronger loot source. The level is passed in by whoever makes the item; it is not stored on the item (the result is).
- The roll comes from the seeded gameplay stream, in a fixed order: one draw for how many affixes, then for each one a draw to pick it and one per value in order. An item with no pool draws nothing, so adding affixes to some items never changes what the rest of a dungeon's loot rolls for a seed.
- What is kept is the **result**: for each affix its network id and the value of each effect in thousandths, as one flat run of whole numbers (`ItemAffixes`: `[id, count, values...]`, affix after affix). It is not a seed, so editing an affix's range later cannot change an item that already rolled; a stash database stores the same run. The run is a fixed 32 words because it is replicated inside an inventory entry. That bounds a whole item, not an affix, and `AffixPool.Problems()` reports a pool whose largest roll could exceed it.
- A rolled affix is applied through the same grant engine as an item's own grants (`Item.AllGrants` = the asset's grants plus what this instance rolled). Taking the item off releases exactly those modifiers.

## Effects

An effect is a class (`AffixEffect`), serialized by reference, so a new kind is a new class and needs no change to the roller, the storage or the affix. The first kind is `StatModifierEffect`: one attribute, one layer (flat `Add`, `PercentAdd`, or `Multiply`), one range. `TriggeredAffixEffect` is the second: it gives the item a **triggered effect** (on hit, on a killing blow to the head, and so on; see [Triggered effects](TriggeredEffects.md)), which any item, spell or affix can use. An affix can have any number of these beside its stat effects, and each reacts independently. Granted abilities are not built.

- **Percentages of the item itself.** A stat effect with scope *This Item* is a percentage of what that item's own asset grants of the attribute, and is applied as a flat amount. "+10% Armor Rating (this item)" on a 40-armor cuirass is +4, and does not scale armor worn elsewhere. A flat Armor Rating and a wearer-scoped percentage are the other two forms.
- **Live effects only.** `AttributeConsumers` lists the attributes gameplay reads. An affix on an attribute nothing reads (Dexterity, Intelligence, Magical Power, attack, casting and interaction speed, and so on) is not live, and `AffixPool.Problems()` and `AffixContentTests` fail for a pool that holds one. Add an attribute to `AttributeConsumers` in the same change that makes something read it.

## Making them

1. **Items > Affix**: the name it adds ("Riveted"), prefix or suffix, family and tier, the minimum item level, a weight, the slots it may roll on (empty is any), and its effects (each with its attribute key, layer, scope and range).
2. **Items > Affix Pool**: the list of affixes, the fewest and most an item rolls, and the most prefixes and suffixes.
3. On the item asset, set **Affix Pool**.
4. Run **SimpleRPG > Definitions > Rebuild Definition Registry**. An affix has to be in the registry, because clients look it up by id; one that is not is skipped when rolling, and an item that already holds a removed one logs a warning and ignores it. An item holding an affix whose number of values has since changed ignores that affix with a warning.

## Where item level comes from

**Not decided, and nothing in the codebase constrains it yet.** Item level is an argument of `AddItemInstance` and `Item.RollAffixes` that defaults to the lowest level, so everything rolls first-tier affixes only until a loot source passes a level. The loot containers and world pickups have no level to pass, and the dungeon has no difficulty value to derive one from. (A search of the code found no existing area or drop-power limit; if one is meant to exist, it is not in this repository.) The pieces to decide are the source's own strength, the dungeon's difficulty, and the ceiling an area allows; the level is then a pure function of those, computed where loot is placed.

## Dropped items

An item keeps its affixes wherever it goes, because it moves as the same instance and is never rolled again:

- **A death drop, and the belongings of a player who did not return in time,** go into a loot bag as the same items with the same instance ids and affixes.
- **A character parked while its player is away** restores its items as they were.
- **A player dropping an item on the ground** (drag its icon out of the inventory onto the world) puts the very instance on the ground. The world pickup carries the instance and its instance id, shows what it rolled (`WorldItemPickup.Affixes`), and hands that same item back to whoever picks it up, in the first place it fits. An item still lying where the dungeon placed it is only the asset and rolls when it is first picked up.

## Dropping items

`ItemDropController` owns the rules, in the same validate-then-commit shape as `InventoryMoveController` and `EquipmentController`:

- Only the owner of a character's own inventory can drop from it, and only while the character is alive. A chest, a loot bag or another player's inventory is somewhere to take from, not drop from.
- The client only names the item. The server decides where it lands: a metre in front of the character, on the floor, and at their feet instead if a wall is in the way.
- An item whose asset has no `WorldPrefab` (with a `WorldItemPickup` on it) cannot be dropped, and a refused drop changes nothing.
- Dropping is rate limited (2 a second, a burst of 5), since each one makes a world object.
- A dropped item nobody picks up ends with the pickup, at the end of the session.

In the UI, letting go of an icon over the world drops it; letting go over another inventory moves it, and over the paper doll does nothing.

## Which attributes an affix can change

An affix names an attribute by its stable key, and any attribute in a character's set works, the core ones and the derived ones alike: modifiers apply on top of a derived stat's resolved value through the same layers (add, percent, factor). So an affix on maximum health adds to the maximum; one on Strength changes the stats derived from it.

| Core attributes (`simplerpg.attr.`) | Derived stats (`simplerpg.stat.`) |
|---|---|
| `strength`, `constitution`, `dexterity`, `agility`, `willpower`, `intelligence` (`wisdom` is the old key for Willpower and still works) | `armor_rating`, `max_health`, `physical_power`, `magical_power`, `physical_damage_reduction`, `magical_damage_reduction`, `movement_speed`, `attack_speed`, `casting_speed`, `interaction_speed`, `equip_speed`, `carrying_capacity`, `spell_preparation_capacity` |

Not every derived stat is consumed by gameplay yet (see [Stats](Stats.md)): an affix on one that nothing reads has no visible effect until something does.

## Replication

`InventoryEntry` and `Equipment.EquippedEntry` carry the item's `ItemAffixes`, so a client builds the same item the server has. A client's worn item with affixes is a copy of the asset carrying them; copies are reused between updates and destroyed when no longer worn.

## Not built yet

- **Tooltips.** Nothing in the UI shows an item's affixes yet; `AffixDefinition.Describe` gives the lines ("+3 Strength", "+10% Strength").
- **Weapon affixes that change a weapon's own damage**: a weapon's damage is on its asset, so affixes only reach it through attributes the damage rules read.
- **Rolling at pickup, not at the world pickup's creation.** A pickup lying in the dungeon is the asset; it rolls as it enters an inventory.

## Item level

An item's level decides which affix tiers it may roll (an affix has a minimum item level). It promises no rarity or quality. It is decided where an item enters the world and passed to the roll (`AddItemInstance(item, itemLevel)`); it is not stored on the item.

It is built from three things, all configured by hand and none of them the floor's number:

- **The area** (`LootArea`): the level its ordinary loot starts at and a **ceiling** nothing there can pass. `DungeonPopulation` has a list of these, one per floor (`floorLoot`); a floor past the end of the list uses the last, and with none, all loot is the lowest level.
- **The source's potential** (`ItemLevelOffset`): how many levels better or worse than the area's ordinary loot a source is. It is a field on every loot entry, so it adds up through the nested weighted tables an item came through (`LootDrop`), and on a `LootContainer` (a corpse or chest). A boss table at +4 makes everything it drops +4.
- **The result** is `clamp(area base + offsets, lowest, area ceiling)` (`LootArea.LevelFor`).

Placed by the population: world pickups carry their level (`WorldItemPickup.ItemLevel`) and roll with it when picked up; enemies' corpses get their floor's area and their encounter's offset. **Not yet:** chests roll their contents the moment they spawn with the room, before the population knows their floor, so they are the lowest level until they roll later. The loot box that enemies' bags hold, and items nothing places, are the lowest level.

**Chests** fill themselves the first time a player opens them (`LootContainer.RollsWhenOpened`), on the server, so nothing about their contents is decided until someone looks and anything that changes a roll in the meantime still counts. The population gives every container in a room its floor's loot area, so a chest's level is the area's. Corpses still fill at death and loot bags are filled by their owner's death drop.

## The catalogs

Provisional first catalogs, made by menu tools that create an asset only if it is missing and never change one that exists (so the numbers are yours from the first run):

- **SimpleRPG > Items > Build Armor Affix Catalog**: 13 families of three tiers (tier 1 from item level 1, tier 2 from 8, tier 3 from 16) and one pool per armor item that has none: none to two affixes, at most one prefix and one suffix. Every effect is on a stat the game reads.
- **SimpleRPG > Effects > Build Bleeding Effect**: the stacking bleed and an "of Bleeding" suffix.
- **SimpleRPG > Items > Build Weapon Affix Catalog** (after the other two): Enhanced Damage (a percentage of that weapon's own base) and flat damage prefixes, and a pool for the player's weapons (listed in `WeaponAffixCatalog.Weapons`) that adds the armor catalog's Strength and Weapon Damage suffixes and the bleed.

Tests build the same catalogs in memory and check they are sound, live, climb with item level, and fit in a pool.

## Rare items and their names

A pool can let its items be **rare**: with its `rareChance` an instance rolls the pool's rare counts (three to six affixes, up to three prefixes and three suffixes, to start with) instead of the ordinary ones, and is called by one of a list of names, such as "Grave Bite", with its base item named beneath it in the tooltip. Any other item is named from its affixes, "{prefix} {item} {suffix}".

- The names are an asset (`Assets/Items/RareNames.asset`, class `RareNames`): a list of names, each with a permanent id. Add new names at the end with the next id; never reuse or renumber one. The 100 names the owner supplied are the seed (`RareNameSeed`).
- Whether an item is rare is the first draw of its roll, and only for a pool that can make rares; the name is one more draw after the affixes. A pool that cannot be rare draws exactly what it did before. The name is stored as its id, so editing, adding, reordering or removing names never renames an item already rolled; if its name is removed, the item reads by its affix names instead.
- Every armor and weapon pool made by the catalog tools has rare items on (15%, a first guess). **SimpleRPG > Items > Enable Rare Items On All Pools** turns it on in any pool that does not have it, and **Build Rare Names** makes the asset if it is missing. `AffixPool.Problems()` reports a pool that can make rares without names or with a rare minimum above its maximum.
