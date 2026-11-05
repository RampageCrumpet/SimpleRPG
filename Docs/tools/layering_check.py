#!/usr/bin/env python3
"""Checks the planned assembly layering against the code as it is now, and lists every reference that points the wrong way.

PLAN below is the layering described in Docs/CodeLayout.md: each assembly, its rank (a lower rank may not use a higher one; leaves,
rank 10 and up, may only use leaves of a LOWER position in LEAF_ORDER) and the files that belong to it. Run it from the repository root:

    python3 Docs/tools/layering_check.py            # every upward and leaf-order violation, with the files behind them
    python3 Docs/tools/layering_check.py --summary  # counts only
    python3 Docs/tools/layering_check.py --uses     # for each assembly, which others it uses: the starting point for its .asmdef references

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
    ('Foundation', 1, ['Foundation/*']),
    ('Combos', 3, ['Combos/*']),
    ('AudioModel', 2, ['AudioModel/*']),
    ('Items', 4, ['Items/*']),
    ('Combat', 5, ['Combat/*']),
    ('Carrying', 6, ['Carrying/*']),
    ('Melee', 6, ['Melee/*']),
    ('AudioPlayback', 10, ['AudioPlayback/*']),
    ('Presentation', 10, ['Presentation/*']),
    ('Interaction', 10, ['Interaction/*']),
    ('Loot', 10, ['Loot/*']),
    ('PlayerControl', 10, ['PlayerControl/*']),
    ('AI', 10, ['AI/*']),
    ('World', 10, ['World/*']),
    ('Delve', 10, ['Delve/*']),
    ('Rendering', 10, ['Rendering/*']),
    ('UI', 10, ['UI/*']),
    ('Session', 11, ['Session/*']),
    ('Integration', 12, ['AbilitySystem/Integration/*']),
]

# Lowest to highest. A leaf may use the ones before it in this list and nothing after it.
LEAF_ORDER = ['AudioPlayback', 'Presentation', 'Interaction', 'Loot', 'PlayerControl', 'AI', 'World', 'Delve', 'Rendering', 'UI', 'Session']

EXCLUDE = ('AbilitySystem/Core/', 'AbilitySystem/Runtime/', 'AbilitySystem/Editor/', 'Tools/')

# (file, word) pairs read by hand and confirmed not to be a use of the type that shares the word: a member, a property, a tooltip, a tag string.
KNOWN_COLLISIONS = {
    ('Foundation/Networking/SessionHandshake.cs', 'Pending'),                     # ConnectionApprovalResponse.Pending
    ('Foundation/Controls/GameInput.cs', 'Inventory'),                             # the Inventory input action property
    ('Items/Item.cs', 'Inventory'),                                            # a tooltip
    ('Items/Item.cs', 'Instance'),                                             # DefinitionRegistry.Instance
    ('Items/ItemIds.cs', 'Instance'),                                          # DefinitionRegistry.Instance
    ('Items/ItemTooltip.cs', 'Instance'),                                      # DefinitionRegistry.Instance
    ('Items/ArmorData.cs', 'Chest'),                                           # the armor slot's name, in a tooltip and a message
    ('Presentation/Articulation/ArticulationSettings.cs', 'Chest'),                  # HumanBodyBones.Chest
    ('AI/PrimarySenses/Blindsense.cs', 'Player'),               # the "Player" tag
}


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
    uses = collections.defaultdict(set)
    for path, text in files.items():
        here, rank_here = assign(path)
        if here is None:
            continue
        sizes[here] += 1
        lines[here] += text.count('\n')
        for name in set(ident.findall(text)):
            there = unique.get(name)
            if not there or there == here or (path, name) in KNOWN_COLLISIONS:
                continue
            uses[here].add(there)
            # Two core assemblies of the same rank may not use each other either; only leaves (rank 10) are ordered by LEAF_ORDER instead.
            if rank[there] > rank_here or (rank[there] == rank_here and rank_here < 10):
                upward[here][there].add((name, path))
            elif rank[there] == rank_here and rank_here >= 10 and LEAF_ORDER.index(there) > LEAF_ORDER.index(here):
                sideways[here][there].add((name, path))

    if '--uses' in sys.argv:
        for name, _, _ in PLAN:
            print(f'{name:14s} -> ' + ', '.join(sorted(uses[name], key=lambda n: rank[n])))
        return

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
