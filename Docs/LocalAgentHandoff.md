# Hand-off: bring-up and verification of the latest changes

**For:** an agent or developer with the Unity editor and compiler. **Why:** the changes below were written without a compiler and are unverified. Your job is to compile them, run the tests, run the armor authoring tool, and report what you find. Fix small mechanical errors; report everything else. Do not redesign anything.

## Ground rules

- **Fix** only: compile errors, a missing `using` or reference, a typo, a test that expects a number the intended design changes (see "Expected test changes"). **Report, do not fix:** anything that needs a design decision, any behaviour that looks wrong, any failing test you cannot explain.
- **Never weaken a test to make it pass** (loosening a tolerance, deleting an assertion, skipping a test). If an expectation must change, change it to the new correct number and say why.
- Do not rename types, fields or serialized members: tests reach private members by name (for example `<ItemSize>k__BackingField`, `<InventorySize>k__BackingField`), and prefabs and assets refer to script GUIDs. Keep `.meta` files with their scripts.
- Obey `CLAUDE.md`: layering is enforced by `AssemblyLayeringTests`; do not add `InternalsVisibleTo`; use `bd` for tracking; commit and push only if the owner has told you to.
- Work from the latest `claude/zen-brahmagupta-i40kaz`.

## What changed since the last verified green run

Oldest first. `git log --stat <hash>~1..HEAD` shows each.

| Commit | Change | Risk |
|---|---|---|
| `75d7380` | End of delve: `DelveHud` (UI) takes the cursor and offers **Exit dungeon**; `MainMenuReturn` shuts the session down and reloads the scene; HUD and extraction marker moved out of `DelveController`. `NetworkManagerUI` returns a refused or dropped client to the menu. | Scene reload with a persistent `NetworkManager`; statics surviving a reload |
| `d2dcb94`, `c5d9bc3` | **Damage mitigation.** `CombatResolver` applies the target's reduction by `DamageType` (Physical or Magical) with an explicit `BypassesMitigation`; rounding once; floor of 1; cap 0.9. The damage node has `Type` and `Bypasses Mitigation`; Firebolt and Icebolt are magical. `Character.TakeDamage` takes both; 27 test call sites pass `bypassesMitigation: true`. | Exact-damage assertions on players |
| same | **Armor Rating** (`simplerpg.stat.armor_rating`): a derived stat; `PlayerStats.asset` has a rule for it and a second contribution to Physical Damage Reduction. Stat sheet UI row. | Asset YAML edits |
| `a310fa9`, latest | **Armor appearance.** `BodyPartDefinition.dressesItem`; `EquipmentAppearance` (server component added to `TestCharacter.prefab`); `ArmorParts`; editor tool **SimpleRPG > Items > Build Armor Items And Parts** (`ArmorSetup`). Standard grid sizes (2x2 head/hands/feet, 2x3 chest). Rigid helmets combined into one head part. | A hand-edited prefab component; the tool has never run |

## Steps

### 1. Pull and compile

Pull, let Unity compile, and fix compile errors. Typical causes here: a missing `using`, a name the compiler resolves differently from what was assumed, a member the tests reach by reflection. List every error and what you changed.

### 2. Layering and definitions

- Run the EditMode `AssemblyLayeringTests` (must pass).
- Run `python3 Docs/tools/layering_check.py --summary` (must report no upward references).
- Run **SimpleRPG > Definitions > Validate Definitions**. Report any error.

### 3. Run all tests

Run EditMode, then PlayMode. Report counts (passed, failed, skipped) for each.

New EditMode tests to look at first: `DamageMitigationTests`, `ArmorRatingTests`, `DamageNodeMitigationTests`, `ArmorSetupTests`, `ArmorAppearanceTests`, `ItemAffixTests`, `AffixRollerTests`, `RequestThrottleTests`, `SessionHandshakeTests`, `ParkedCharacterTests`, `DelveRulesTests`, `DeathDropTests`, `ItemDropTests`.

**Expected test changes.** Players now take real physical and magical reduction (about 8 percent at the baseline attribute of 10: `0.9 * 10 / 110`). A PlayMode test that asserts the *exact damage a weapon or spell deals to a player* may now be a point or two lower. If a failure is exactly that (the number is what the formula gives), update the expectation to the correct new number and say so. If a test sets up health or forces a death with `TakeDamage`, it should already pass `bypassesMitigation: true`; if you find one that does not, add it. Anything else that fails: report it with the assertion text, do not guess.

### 4. Run the armor tool

Run **SimpleRPG > Items > Build Armor Items And Parts**. Copy the dialog text and the Console `[Armor]` line **verbatim** into the report. Then check:

- `Assets/Items/Armor/` has an `ArmorData` per piece with the right slot, name, icon (a sprite, not blank), size (2x2 for head and hands, 2x3 for chest) and exactly one grant: `simplerpg.stat.armor_rating`, Add, the seeded value.
- `Assets/Art/BodyParts/Armor/` has the parts. For each: open it and confirm **Dresses Item** is set, **Slot** is right, and (skinned parts) the bone list is filled and the root bone is named. Run `BodyPartDefinition.Problem()` if you can (the definition check does) and report any message.
- The registry knows the new items and parts (they have short ids); Validate Definitions is clean.
- Run the tool a second time: it must report 0 new items and change nothing it should not.

Pieces most likely to be flagged:

- **Visored Sallet** is rigid. Expect a combined part named `VisoredSallet_Rigid` with a combined mesh asset `VisoredSallet_RigidMesh`. Report whether it was built, the part's attach position and rotation, and (in play) whether the helmet sits correctly on the head, the visor closed.
- **Gloves** need `_L` and `_R` in the mesh names. If the tool says it cannot tell the side, report the mesh names it found so the owner can decide.
- **Padded Coif, Linen Tunic, Gambeson, Mail Hauberk, Plate Armor**: the prefabs looked like instances of the FBX with overrides. Report anything the tool says about them, and the names of the skinned meshes inside each.

### 5. Play check (host, then a second client)

You need the items in an inventory. A temporary script is fine (do not commit it): a context menu on the player that calls `GetComponent<Inventory>().AddItemInstance(item)` for each armor asset.

- Wear each piece (right-click it in the inventory). It should appear on the character. Take it off: the bare part returns. Report which pieces look wrong and how.
- The **Stat Sheet** should show Armor Rating going up by the piece's value and Physical Reduction following it. Report the numbers for one piece and for a full set.
- Fight a skeleton with and without plate armor: the damage taken should drop. Report the damage values.
- With a second client connected, confirm the other player sees the armor, and a client that joins later sees it too.
- Helmets should be hidden from the wearer's own first-person view and visible to the other player.
- Take the delve to its end (extract, then die) and confirm **Exit dungeon** returns to the menu with a visible cursor, and that starting a second session works.

### 6. Gather, do not fix

Report, as a short table, for each armor prefab: the renderers it has (name, skinned or plain), the bone count of each skinned mesh, and the material names. This is the information needed to finish legs and feet.

## What to send back

One markdown report with these sections, in this order:

1. **Compile**: errors found and the fixes made (file and a one-line reason each), or "clean".
2. **Tests**: counts for EditMode and PlayMode; each failure with its assertion text, whether you changed an expectation, and why.
3. **Tool output**: the dialog and Console text, verbatim.
4. **Armor in play**: one line per piece (looks right, or what is wrong).
5. **Numbers**: Armor Rating and reduction for one piece and a full set; damage taken with and without plate.
6. **Prefab table** from step 6.
7. **Anything surprising**, and any question that needs the owner's decision.

Commit your fixes only if you were told to, with a message that says what was wrong.
