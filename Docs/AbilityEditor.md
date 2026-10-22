# Ability editor

Author abilities visually, with no C# and no hand-editing of assets. The ability asset (`AbilityDefinition`) is the only copy of the graph; the editor is a view over it.

## Authoring an ability

1. **Create it:** Project window, Create > SimpleRPG > Ability. A new ability already has an Activate node.
2. **Open it:** double-click the asset (or right-click the component header > Open Graph Editor).
3. **Details:** click an empty part of the canvas. The right-hand panel shows the name, tooltip, icon, sound and cooldown.
4. **Add nodes:** press Space or right-click the canvas and search the palette. It lists every registered node type.
5. **Connect:** drag from an output port to an input port. Only compatible ports light up. Flow ports (white) pass control; data ports are coloured by type (number, entity, entity set). A data input takes one value; a flow output leads to one step, so a new connection replaces the old one.
6. **Configure:** click a node. Its settings appear in the right-hand panel (projectile prefab, amounts, areas, attribute keys, damage kind...).
7. **Check:** the bottom-left list shows everything the compiler objects to; nodes and ports at fault are outlined and badged. Click a problem to jump to its node. An ability with errors will not run.
8. **Try it:** press Run in Test World. The ability runs on the real executor in a small scripted world (a caster and one dummy target, projectiles that hit the target, swings that connect). The canvas shows which nodes ran, what they computed and what they did, and the toolbar notice says who lost health.
9. **Save:** Save button or Ctrl+S. Undo and Redo work for every edit.

Copy, paste and duplicate (Ctrl+C, Ctrl+V, Ctrl+D) work between abilities. Pasted nodes get fresh IDs and keep the connections between them.

## Watching the real game

Turn on Observe live in the toolbar while a host or server is running in play mode. The window then shows the newest run of the open ability, from the real game, on the canvas. Pick earlier runs from the Trace list.

## What the automated tests cover

- `AbilityGraphEditorTests`, `AbilityNodeInspectorTests`, `AbilityGraphValidationTests`: the editing operations, the settings panel in a live window, validation display, copy and paste.
- `AbilityTraceTests`, `AbilityTraceViewerTests`, `LiveTraceNetworkTests`: tracing on the real executor, the canvas viewer, and a real networked cast.
- `AbilityAuthoringAcceptanceTests`: new abilities authored through the palette, dragged connections and the settings panel, saved, reopened from disk, compiled and run in the test world.

## Manual checks (gestures automation cannot perform)

Run these once after changing the editor:

1. Space and right-click each open the palette at the mouse; choosing an entry drops the node under the mouse.
2. Dragging from a port highlights only compatible ports; releasing on an incompatible one connects nothing.
3. Moving a node with the mouse, then Undo, puts it back.
4. Ctrl+C, Ctrl+V and Ctrl+D copy, paste and duplicate the selected nodes, and the pasted nodes end up selected.
5. Deleting selected nodes with the Delete key removes them and their connections.
6. Observe live: enter play mode as a host, cast an ability that is open, and see its nodes light up.
