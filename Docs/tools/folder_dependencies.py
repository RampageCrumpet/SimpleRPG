#!/usr/bin/env python3
"""Which area of Assets/Scripts uses which other area, by the type names it mentions.

A rough map, not a compiler. It strips comments, collects every type declared in each area, and for each file lists the types of *other* areas
whose names appear in it. It over-reports common names (a type called `Instance`, `Pending`, `Item`) and cannot see generics or reflection, so
use it to find where to look and let the compiler decide. Run from the repository root:

    python3 Docs/tools/folder_dependencies.py                 # the matrix, the cycles, and what uses the main assembly
    python3 Docs/tools/folder_dependencies.py Characters Audio  # the type names behind one edge (Characters -> Audio)
"""
import collections
import os
import re
import sys

ROOT = 'Assets/Scripts'
MAIN = ['Root', 'Abilities', 'Audio', 'Characters', 'Controls', 'Delve', 'Encounters', 'Inventory', 'LootGeneration', 'Networking', 'Rendering', 'UI', 'World']


def area(path):
    parts = path.replace('\\', '/').split('/')
    if len(parts) == 1:
        return 'Root'
    if parts[0] == 'AbilitySystem':
        return 'AbilitySystem/' + parts[1]
    if parts[0] == 'Tools':
        return 'Tools/' + (parts[1] if len(parts) > 2 else '')
    return parts[0]


def load():
    files = {}
    for directory, _, names in os.walk(ROOT):
        for name in names:
            if name.endswith('.cs'):
                path = os.path.join(directory, name)
                text = open(path, encoding='utf-8-sig').read()
                text = re.sub(r'/\*.*?\*/', '', text, flags=re.S)
                files[os.path.relpath(path, ROOT)] = re.sub(r'//[^\n]*', '', text)
    return files


def main():
    files = load()
    declared = re.compile(r'\b(?:class|struct|enum|interface|record)\s+([A-Z][A-Za-z0-9_]*)')
    owners = collections.defaultdict(set)
    for path, text in files.items():
        for match in declared.finditer(text):
            owners[match.group(1)].add(area(path))
    unique = {name: next(iter(a)) for name, a in owners.items() if len(a) == 1 and len(name) > 3}

    ident = re.compile(r'\b[A-Z][A-Za-z0-9_]*\b')
    edges = collections.defaultdict(collections.Counter)
    detail = collections.defaultdict(collections.Counter)
    for path, text in files.items():
        here = area(path)
        for name in set(ident.findall(text)):
            there = unique.get(name)
            if there and there != here:
                edges[here][there] += 1
                detail[(here, there)][name] += 1

    if len(sys.argv) == 3:
        a, b = sys.argv[1], sys.argv[2]
        print(f'{a} -> {b}: ' + ', '.join(f'{n}' for n, _ in detail[(a, b)].most_common()))
        return

    print('Each main-assembly area, and the other main areas it uses (distinct type names):')
    for a in MAIN:
        row = {o: c for o, c in edges[a].items() if o in MAIN}
        print(f'  {a:15s} -> ' + ', '.join(f'{o}({c})' for o, c in sorted(row.items(), key=lambda x: -x[1])))
    print('\nTwo-way pairs (each uses the other):')
    for a in MAIN:
        for o in MAIN:
            if a < o and edges[a][o] and edges[o][a]:
                print(f'  {a} <-> {o}: {a} uses {edges[a][o]}, {o} uses {edges[o][a]}')
    print('\nMain assembly -> other assemblies:')
    for a in MAIN:
        row = {o: c for o, c in edges[a].items() if o not in MAIN}
        if row:
            print(f'  {a:15s} -> ' + ', '.join(f'{o}({c})' for o, c in sorted(row.items(), key=lambda x: -x[1])))
    print('\nOther assemblies -> main assembly:')
    for a in sorted(edges):
        if a in MAIN:
            continue
        row = {o: c for o, c in edges[a].items() if o in MAIN}
        if row:
            print(f'  {a:28s} -> ' + ', '.join(f'{o}({c})' for o, c in sorted(row.items(), key=lambda x: -x[1])))


if __name__ == '__main__':
    main()
