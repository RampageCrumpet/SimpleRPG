# Item affixes

An item can roll bonuses (affixes) when it comes into the world. They are **fixed for the life of the item**: nothing rerolls or changes
them. There is no durability: nothing in the game wears out or breaks through use, by design.

## The rules

- An item asset may point at an **Affix Pool**. An item with no pool has no affixes, and the game rolls nothing for it.
- Each new *instance* rolls once, when it is created from the asset (into a chest, a pickup, any inventory through `AddItemInstance`). The asset is never changed.
- The roll comes from the seeded gameplay stream, in a fixed order: one draw for how many affixes, then for each one a draw to pick it and one for its value. An item with no pool draws nothing, so adding affixes to some items never changes what the rest of a dungeon's loot rolls for a seed.
- An affix never rolls twice on an item, and two affixes with the same **exclusive group** never both roll.
- What is kept is the **result**: the affix's network id and its value in thousandths, up to four per item (`ItemAffixes`). It is not a seed, so editing an affix's range later cannot change an item that already rolled. That is also the shape a stash database can store: item id, instance, and up to four (affix id, value) pairs.
- A rolled affix is an attribute modifier on the wearer, applied through the same grant engine as an item's own grants (`Item.AllGrants` = the asset's grants plus what this instance rolled). Taking the item off releases exactly those modifiers.

## Making them

1. **Items > Affix**: a label ("Strength"), the attribute key, where it applies (flat, percent, or factor), the range it rolls in, a weight, an optional exclusive group, and the slots it may roll on (empty is any).
2. **Items > Affix Pool**: the list of affixes and the fewest and most an item rolls (at most four).
3. On the item asset, set **Affix Pool**.
4. Run **SimpleRPG > Definitions > Rebuild Definition Registry**. An affix has to be in the registry, because clients look it up by id; one that is not is skipped when rolling, and an item that already holds a removed one logs a warning and ignores it.

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
| `strength`, `constitution`, `dexterity`, `agility`, `willpower`, `intelligence` (`armor` and `wisdom` also appear in the project) | `armor_rating`, `max_health`, `physical_power`, `magical_power`, `physical_damage_reduction`, `magical_damage_reduction`, `movement_speed`, `attack_speed`, `casting_speed`, `interaction_speed`, `equip_speed`, `carrying_capacity`, `spell_preparation_capacity` |

Not every derived stat is consumed by gameplay yet (see [Stats](Stats.md)): an affix on one that nothing reads has no visible effect until something does.

## Replication

`InventoryEntry` and `Equipment.EquippedEntry` carry the item's `ItemAffixes`, so a client builds the same item the server has. A client's worn item with affixes is a copy of the asset carrying them; copies are reused between updates and destroyed when no longer worn.

## Not built yet

- **Tooltips.** Nothing in the UI shows an item's affixes yet; `AffixDefinition.Describe` gives the lines ("+3 Strength", "+10% Strength").
- **Tiers or levels of affixes**, and **weapon affixes that change a weapon's own damage**: a weapon's damage is on its asset, so affixes only reach it through attributes the damage rules read.
- **Rolling at pickup, not at the world pickup's creation.** A pickup lying in the dungeon is the asset; it rolls as it enters an inventory.
