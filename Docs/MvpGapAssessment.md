# SimpleRPG MVP gap assessment

Reviewed: 2026-10-22  
Scope: the Unity project, shipped scene configuration, current runtime scripts, automated-test inventory, the active Beads board, and the current Unity Console. This is a planning assessment, not a replacement for Beads; use the named beads to track implementation work.

## Verdict

SimpleRPG has the ingredients of a strong **vertical-slice prototype**, not yet a release-ready MVP. The core loop is substantially implemented: start a host, generate a seeded dungeon, navigate it, fight enemies, obtain loot, use inventory/chests, and unlock key gates. Networking and gameplay code have unusually good automated coverage for this stage.

The MVP-critical work is therefore concentrated in three areas:

1. Make the existing playable path robust for a normal player (especially UI/session failure paths).
2. Define and implement a clear finish/return loop, rather than an endless generated combat space.
3. Produce and verify a distributable Windows build with the minimum player-facing experience around it.

## Suggested MVP definition

**A solo-hosted, 10-15 minute dungeon run:** a player launches the Windows build, starts a run, receives clear controls and objectives, fights several enemy groups, collects a key and treasure, unlocks a final gate or exits through it, sees a completion/summary screen, and can start another run. Death must have an intentional outcome (restart or return to menu). A remote client can be a post-MVP validation target unless two-player play is a product promise.

This scope deliberately excludes account systems, persistent character progression, matchmaking, procedural vertical-content expansion, more spells/classes, and a complete settings/accessibility suite. They can follow once the one-run experience is solid.

## What is already in place

| Area | Evidence | Assessment |
|---|---|---|
| Session and dungeon | `SampleScene` is the only enabled build scene. `GameController` generates the dungeon from a seed, builds a server navmesh, and populates the world. | Core implementation present. |
| Exploration and gates | Key pickups, shared keyring state, locked-door interaction, multi-floor layout support, and seeded reconstruction for clients exist. | Playable progression pieces present. |
| Combat | First-person movement, a three-stage longsword combo, hit detection, damage/health, abilities, enemy melee AI, and death animation paths exist. | Core combat present; needs a human feel/balance pass. |
| Loot and inventory | Chests, world pickups, generated loot tables, item registry, equipment, and inventory UI exist. | Core reward loop present. |
| Networking | Server authority and late-join rules are documented; project includes remote-client and latency tests. | Strong technical base, but not yet a tested player-facing multiplayer experience. |
| Automation | Broad EditMode/PlayMode suites cover dungeon, key, animation, ability, and networking behavior. | Good regression base; build and end-to-end smoke validation remain missing. |

## MVP blockers

### 1. Fix the UI attachment failure path

**Why it blocks MVP:** the current Unity Console contains repeated `InvalidOperationException: Sequence contains no matching element` errors from `RegisterWithUI.AttachToUI()` during `DungeonKeyClientTests`. That method uses `Single(...)` to require every tagged UI service (ability buttons, health bar, interaction UI, loot UI, and player UI controller). The existing Beads item explicitly records this failure: `simplerpg-2wv` ("RegisterWithUI.AttachToUI throws when the scene has no UI").

**Plan:** replace mandatory global lookups with an explicit UI root/reference model or safe optional lookup; make headless/dedicated/test sessions work without UI; add a regression test that starts a server/client without UI and one that proves the full SampleScene UI attaches correctly.

**Exit criteria:** normal host startup and automated client tests generate no unhandled UI exception; UI absence is supported for dedicated/headless contexts.

### 2. Finish the run: objective, exit, completion, and death outcomes

**Why it blocks MVP:** key-and-door mechanics exist, but the reviewed gameplay path has no identified objective tracker, final-room/exit interaction, completion screen, restart flow, or game-over/respawn flow. Without those states, players can explore and fight but cannot complete a run.

**Plan:** choose one finish condition—recommended: unlock the final gate after acquiring its key, then interact with a visible exit. Add a lightweight run-state authority component, objective HUD text, completion summary (time, treasure/items collected, enemies defeated), and explicit death behavior (return to title/run restart is sufficient).

**Exit criteria:** a fresh run reaches a clear end state and a death reaches a clear recoverable state; both work in the standalone build and do not leave network objects or UI in a broken session.

### 3. Create a real launch and build experience

**Why it blocks MVP:** project documentation records editor-host validation, but explicitly says no standalone player build or separate human remote-client session is recorded. The only entry UI is a developer-oriented Host/Client/Dedicated panel; there is no visible title/menu, quit flow, connection guidance, or build smoke test.

**Plan:** add a minimal title/session screen with "Play solo (host)", optional "Join by address" only if kept in scope, retry/error handling, restart-to-menu, and quit. Produce a Windows development build first, then a release candidate build. Add a documented build command and a build smoke checklist.

**Exit criteria:** a clean-machine-style Windows launch can begin, finish, and restart a solo run without opening the Unity editor; build has no compilation errors and no new runtime exceptions.

### 4. Lock down the playable-content contract and tune it

**Why it blocks MVP:** the systems can generate a large multi-floor dungeon and populate it, but the MVP needs predictable duration, encounter density, enemy mix, key placement, treasure rewards, and difficulty. Current documentation calls for human inspection of combat balance and several attacking skeletons; it does not record acceptance values for a complete player run.

**Plan:** select a fixed MVP room-count/floor-count range and seed set, author one deliberate objective route, define the first enemy roster and encounter budgets, tune player/enemy damage and healing/survivability, and make loot rewards legible. Preserve procedural variation only inside those constraints.

**Exit criteria:** five complete solo runs meet a target duration and have no unwinnable layout, unreachable key, empty critical path, or unfair spawn; record seed, time, deaths, and defects.

## Important but can follow the MVP blocker pass

| Priority | Component | Why it matters | Existing tracking |
|---|---|---|---|
| P1 | Enemy/encounter polish | Ensure every shipping enemy detects, attacks, takes damage, dies, and gives readable feedback; tune mixed encounters. | `simplerpg-7du.2` |
| P1 | Stats wired to real gameplay | Attributes currently have system support, but the board says they still need to drive gameplay. Do this only if progression/equipment choices are part of the first run. | `simplerpg-7du.1.1` |
| P1 | Remote player experience | A remote owner’s view turn currently lags by a round trip. Do not advertise multiplayer until the separate-client playtest passes and this is resolved or intentionally deferred. | `simplerpg-7du.1.6` |
| P2 | UI hardening/readability | HUD, interaction, inventory, input prompts, empty/error states, and cursor behavior need an end-to-end pass. | `simplerpg-7du.5`, `simplerpg-7du.6.11`, `.12`, `.17` |
| P2 | Audio and feedback | No gameplay audio path was found in the reviewed runtime scripts. Add attack impact, hurt/death, pickup, door, UI, and ambient feedback before calling the slice presentable. | New planning work |
| P2 | Input/accessibility baseline | Provide remapping or clearly surfaced bindings, sensitivity and volume controls, readable text/contrast, and a pause menu. | New planning work |
| P2 | Save/persistence decision | No runtime save/load path was found. For a 10-15 minute MVP, an explicit "no mid-run saving" decision is acceptable; otherwise add a safe between-run save first. | Product decision |

## Explicitly out of MVP unless product direction changes

- Matchmaking, dedicated-server deployment, NAT traversal, and persistent online accounts.
- Multiple classes, a large spell catalogue, Frost Nova, or broad status-effect design. Existing Firebolt/Icebolt safety work should be completed only if those slots ship in the MVP.
- Full progression, crafting, economy, meta-progression, achievements, or analytics.
- More procedural vertical connection types. The generation system supports them, but docs state the real authored vertical set remains limited.
- Large-scale codebase refactors that do not prevent the one-run experience. Track them separately under the current remediation board.

## Proposed delivery order

1. **Stability gate:** resolve UI-without-UI failure; run the relevant PlayMode suite; clear normal-host Console errors.
2. **Complete-run slice:** objective → key → final gate/exit → summary; death/restart; constrain content and tune one reliable route.
3. **Player experience pass:** title/session flow, in-game pause/help, audio feedback, HUD/inventory/readability pass.
4. **Build gate:** produce Windows build, run a clean launch-to-completion smoke test, then run a second human-controlled client session if multiplayer remains in the MVP.
5. **Release candidate:** five-run seed matrix, defect triage, performance/frame-time check on target hardware, and a final regression suite.

## Verification checklist for planning

- [ ] Windows build launches directly into an understandable start screen.
- [ ] Solo player can start, move, fight, loot, open inventory, collect a key, unlock the final gate, and complete a run.
- [ ] Player death is recoverable and never strands input, cursor, UI, or network state.
- [ ] No unhandled exceptions/errors occur during the normal solo loop.
- [ ] Critical-path layouts pass across the selected seed matrix.
- [ ] UI remains usable at target resolutions; controls and primary actions are discoverable.
- [ ] A release smoke test and relevant automated tests are recorded against the candidate build.
- [ ] If multiplayer ships: a human-controlled remote client completes the critical path with acceptable camera/turning behavior.

## Notes for the planning board

The current Beads board already covers player/combat, enemies, items/loot/inventory, UI/multiplayer, dungeon keys, and specific architecture remediation. Create new beads only for the untracked MVP work above—principally run completion/death flow, build/title flow, audio feedback, and the MVP content/balance contract—then add dependencies so the build gate follows stability and complete-run work.
