#!/usr/bin/env python3
"""Checks the planned assembly layering against the code as it is now, and lists every reference that points the wrong way.

PLAN below is the target layering from Docs/AssemblySplitHandoff.md: each assembly, its rank (a lower rank may not use a higher one; leaves,
rank 10 and up, may only use leaves of a LOWER position in LEAF_ORDER) and the files that belong to it. Run it from the repository root:

    python3 Docs/tools/layering_check.py            # every upward and leaf-order violation, with the files behind them
    python3 Docs/tools/layering_check.py --summary  # counts only

When it prints nothing under the violations headings, the code can be split into these assemblies without a cycle. It matches type names as
words (comments stripped), so a word that is a type's name inside a string, or a common name such as `Instance`, shows up as a violation that is
not one: check the file before acting. The compiler is the authority; this finds where to look.
"""
import collections
import fnmatch
import os
import re
import sys

ROOT = 'Assets/Scripts'

# (assembly, rank, patterns relative to Assets/Scripts; the longest matching pattern wins). Edit this when the plan changes.
PLAN = [
    ('Foundation', 1, ['ServerClock.cs', 'Networking/*', 'Controls/*', 'Characters/Damage.cs', 'Characters/BodyLocations.cs']),
    ('Combos', 2, ['Abilities/Combo/*']),
    ('AudioModel', 2, ['Audio/AudioContext.cs', 'Audio/AudioEventBase.cs', 'Audio/AudioEventDefinition.cs', 'Audio/AudioEventTable.cs',
                       'Audio/AudioTag.cs', 'Audio/AcousticSurface.cs', 'Audio/AmbienceProfile.cs', 'Audio/MovementAudioProfile.cs',
                       'Audio/MovementAudioState.cs', 'Audio/SpellAudioProfile.cs']),
    ('Items', 3, ['Inventory/Item.cs', 'Inventory/ItemIds.cs', 'Inventory/ItemAffixes.cs', 'Inventory/AffixDefinition.cs',
                  'Inventory/AffixPool.cs', 'Inventory/AffixRoller.cs', 'Inventory/ArmorData.cs', 'Inventory/InventoryEntry.cs',
                  'WeaponData.cs', 'Audio/ItemAudioProfile.cs']),
    ('Combat', 4, ['Characters/HitAnatomy.cs', 'Characters/HitCandidate.cs', 'Characters/HitRegionProfile.cs', 'Characters/Hitbox.cs',
                   'Characters/Hurtbox.cs', 'Characters/CharacterStats.cs', 'Characters/CombatLoadout.cs', 'Characters/Player/Character.cs',
                   'Weapon.cs', 'SwingHitTracker.cs', 'Projectile.cs', 'Abilities/IAbilitySlot.cs', 'Abilities/ProjectileRules.cs']),
    ('Carrying', 5, ['Inventory/Inventory.cs', 'Inventory/InventoryMoveController.cs', 'Inventory/EquipmentController.cs',
                     'Inventory/ItemDropController.cs', 'Characters/Equipment.cs']),
    ('Melee', 5, ['Abilities/AbilityBehaviors/*']),
    ('AudioPlayback', 10, ['Audio/*']),
    ('Presentation', 10, ['Characters/Animation/*', 'Characters/AnimationControllers/*', 'Characters/Appearance/*',
                          'Characters/Articulation/*', 'Characters/AnimatableCharacter.cs', 'Characters/AnimationAudioEvents.cs']),
    ('Interaction', 10, ['World/Interactables/Interactable.cs', 'World/Interactables/InteractableManager.cs', 'Characters/Interactor.cs']),
    ('Loot', 10, ['LootGeneration/*', 'Encounters/*']),
    ('PlayerControl', 10, ['Characters/Player/*', 'Characters/LocalPlayerControls.cs']),
    ('AI', 10, ['Characters/Enemies/*', 'Characters/NavigationGrounding.cs']),
    ('World', 10, ['World/*', 'Delve/PartyScaling.cs']),
    ('Delve', 10, ['Delve/*']),
    ('Rendering', 10, ['Rendering/*']),
    ('UI', 10, ['UI/*']),
    ('Session', 11, ['Player.cs', 'GameController.cs', 'EnemySpawner.cs', 'NetworkSpawn.cs']),
]

# Lowest to highest. A leaf may use the ones before it in this list and nothing after it.
LEAF_ORDER = ['AudioPlayback', 'Presentation', 'Interaction', 'Loot', 'PlayerControl', 'AI', 'World', 'Delve', 'Rendering', 'UI', 'Session']

EXCLUDE = ('AbilitySystem/', 'Tools/')


def assign(path):
    best = None
    for name, rank, patterns in PLAN:
        for pattern in patterns:
            if fnmatch.fnmatch(path, pattern) or (pattern.endswith('/*') and path.startswith(pattern[:-1])):
                if best is None or len(pattern) > best[0]:
                    best = (len(pattern), name, rank)
    return (best[1], best[2]) if best else (None, None)


def main():
    files = {}
    for directory, _, names in os.walk(ROOT):
        for name in names:
            if name.endswith('.cs'):
                path = os.path.join(directory, name)
                rel = os.path.relpath(path, ROOT).replace('\\', '/')
                if rel.startswith(EXCLUDE):
                    continue
                text = open(path, encoding='utf-8-sig').read()
                text = re.sub(r'/\*.*?\*/', '', text, flags=re.S)
                files[rel] = re.sub(r'//[^\n]*', '', text)

    unassigned = sorted(p for p in files if assign(p)[0] is None)
    if unassigned:
        print('FILES THE PLAN DOES NOT PLACE (add them to PLAN):')
        for p in unassigned:
            print('  ' + p)
        print()

    declared = re.compile(r'\b(?:class|struct|enum|interface|record)\s+([A-Z][A-Za-z0-9_]*)')
    owners = collections.defaultdict(set)
    for path, text in files.items():
        for match in declared.finditer(text):
            owners[match.group(1)].add(assign(path)[0])
    unique = {n: next(iter(a)) for n, a in owners.items() if len(a) == 1 and len(n) > 3}

    rank = {name: r for name, r, _ in PLAN}
    ident = re.compile(r'\b[A-Z][A-Za-z0-9_]*\b')
    upward = collections.defaultdict(lambda: collections.defaultdict(set))
    sideways = collections.defaultdict(lambda: collections.defaultdict(set))
    sizes, lines = collections.Counter(), collections.Counter()
    for path, text in files.items():
        here, rank_here = assign(path)
        if here is None:
            continue
        sizes[here] += 1
        lines[here] += text.count('\n')
        for name in set(ident.findall(text)):
            there = unique.get(name)
            if not there or there == here:
                continue
            if rank[there] > rank_here:
                upward[here][there].add((name, path))
            elif rank[there] == rank_here and rank_here >= 10 and LEAF_ORDER.index(there) > LEAF_ORDER.index(here):
                sideways[here][there].add((name, path))

    summary = '--summary' in sys.argv
    print('Assemblies in the plan (files / lines):')
    for name, r, _ in PLAN:
        print(f'  {name:14s} rank {r:2d}  {sizes[name]:3d} files {lines[name]:5d} lines')
    for title, table in (('UPWARD references: a lower layer uses a higher one', upward),
                         ('LEAF-ORDER violations: a leaf uses a leaf above it in LEAF_ORDER', sideways)):
        print(f'\n{title}')
        total = 0
        for here in sorted(table, key=lambda n: rank[n]):
            for there, hits in sorted(table[here].items(), key=lambda x: rank[x[0]]):
                total += len(hits)
                print(f'  {here} -> {there}: ' + ', '.join(sorted({n for n, _ in hits})))
                if not summary:
                    for n, p in sorted(hits):
                        print(f'      {n:30s} {p}')
        if total == 0:
            print('  none')


if __name__ == '__main__':
    main()
