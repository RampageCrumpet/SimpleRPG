# Combo chain (design)

Status: **built, prediction included.** The player's longsword runs on `ComboDefinition` + `ComboMachine` through `ComboMeleeDelivery`, the owning client predicts its swings, and other clients join late swings part-way through. Tested over a simulated 100 ms-each-way connection. Remaining: a human feel/visual pass on the values below (bead epic simplerpg-7du.1.5).

## Target feel

Dark and Darker style chaining:

- Each attack in the chain is one animation: windup, active hit, recovery.
- Near the end of the animation, in the recovery, there is a short **chain window**. A press inside it **cuts the rest of the recovery short** and crossfades into the next attack.
- A press **before** the window opens is **discarded** (no buffering). A press after it closes is also discarded; the animation plays out and the chain ends.
- Nothing cancels recovery except chaining. Movement and camera rotation are layered on top of the swing, not blocked by it.
- The owning player sees their swing start on the frame they press (client prediction); the server stays authoritative about what is hit.

## What is wrong today

| Problem | Where |
|---|---|
| Chain happens *after* the attack clip ends, through a separate "Combo" bridge clip | `MeleeComboTracker.TryQueueNext`, animator `A1_Combo` / `A2_Combo` |
| Timing is stored three times: tracker arrays on `MeleeDelivery`, animator state names (`"Longsword_A1_Recovery"` string match), clip lengths | `MeleeDelivery.TryQueue`, controller |
| Chain is longsword-shaped (fixed 3, trigger `ComboQueued`, per-attack animator states) | `AnimatableCharacter.GetAttackTrigger`, controller |
| `MeleeDelivery` is two classes in one (`UsesIndependentAttacks` branches in ~8 places) | `MeleeDelivery` |
| Owner waits a network round trip before every swing and chain link | `MeleeAttackClientRpc` is the only thing that starts the animation |
| Sticky animator triggers; chain logic spread over transition graphs | controller |
| Hit windows depend on the server ticking an animator to fire clip events | `ActivateWeapon` / `DeactivateWeaponH` |

## Design

### 1. Data: `ComboDefinition` (immutable ScriptableObject)

A weapon references one combo. A step is plain data, in seconds from the step's start:

| Field | Meaning |
|---|---|
| `State` | Animator state (or clip) the presentation plays for this step |
| `Duration` | Full length of the step; the step ends here if not chained |
| `HitStart`, `HitEnd` | Window in which the weapon hitbox is live (server-driven from this data) |
| `ChainOpen`, `ChainClose` | Window in which a press chains into the next step. Must sit after `HitEnd` and not past `Duration` |
| `Clip` | The clip the state plays; only used to check `Duration` (within one frame at 60 fps) |
| `BlendIn` | Crossfade length when this step is entered, from locomotion (step 1) or from the previous step |
| `StartOffset` | Seconds skipped at the start of this step's clip when entered (hides pose mismatch) |
| `DamageScalar` | Replaces `comboDamageScalars` |

The last step has no chain window. Built: `ComboStep`, `ComboDefinition` (`Assets/Scripts/Abilities/Combo/`), `WeaponData.Combo`. `ComboDefinition.Validate()` checks windows ordered and inside `Duration`, `Duration` matches the clip length, a state name and clip are set, and the last step has no chain window; "Validate Definitions" and the build gate run it on every combo asset. Whether the state exists in the animator controller is not checked yet (the presentation task owns that). This replaces the hand-copied `attackLengths` / `comboLengths` arrays and their test.

### 2. Logic: `ComboMachine` (pure C#, Unity-free)

Input: `Press(time)`. Output: step started / press rejected. Tick-driven events: hit opened, hit closed, step ended. Deterministic from the definition and press times, so **server and owner run the same code** (prediction, tests). Phases per step: windup, active, recovery (chain window inside it), ended.

Rules:

- Press with no chain running: starts step 1 (ability cooldown applies, as today).
- Press while a step runs, inside `[ChainOpen, ChainClose]`: starts the next step now.
- Press anywhere else: rejected, no effect, nothing queued.
- Step ends unchained: chain resets; the next press starts step 1 fresh.
- Last step ends: chain resets. (Assumption: no wrap-around; confirm.)

Built as `ComboMachine` (`Assets/Scripts/Abilities/Combo/`), driven by `ComboStepTiming` so it never touches clips. Callers pass the time in: `Press(now, events)`, `Advance(now, events)` and `Interrupt(now, events)` append `ComboEvent`s (StepStarted, HitOpened, HitClosed, StepEnded) stamped with the time they happened, so a late advance reports the same times as a frame-by-frame one. `Advance` ignores earlier times; a `Press` may carry an earlier time than the last advance (network latency) as long as its step is still running. `ComboDefinition.CreateMachine()` builds one per character. Replaces `MeleeComboTracker`.

### 3. Server authority and prediction

Existing rule stands: clients ask, server decides ([NetworkAuthority.md](NetworkAuthority.md)).

- Owner presses: owner's machine runs its own `ComboMachine`, starts the animation immediately, and sends a press carrying the **network tick** of the press (same tick basis as `CooldownTracker`).
- Server replays the press at that tick (rewind bounded by a small tolerance, like the cooldown's 3 ticks) against its own machine, which decides the step. Hit windows and damage derive from the step's server start tick, so the server does not rely on its animator.
- Server accepts: it broadcasts `(step, startTick)`. Owner already shows it; other clients start the step and seek by elapsed time.
- Server rejects: it tells the owner the authoritative state; owner crossfades back to it.
- Each step still runs the ability graph (`ExecuteQueued`, no second cooldown charge) so `OnHit` effects keep working per swing.

### 4. Presentation

One generic path: `PlayStep(state, blendIn, startOffset)` using `CrossFadeInFixedTime` on an attack layer. No `ComboQueued`, no `Combo` states, no per-attack transitions; the step end fades back out through the same path. `AnimatableCharacter` no longer knows "attack 1 / 2 / 3".

### 5. Layering with movement and camera

Attack runs on its own animator layer so locomotion continues under it (upper-body mask, weight tuned so authored in-place foot steps do not fight real movement). Body yaw follows the camera during swings; the server must see the same yaw because the hitbox follows the animated weapon bone. Both are **spikes** (see below).

### 6. Split `MeleeDelivery`

`IMeleeSwingHost` keeps `Begin`; the combo-specific `TryQueue` / `CanStartFreshChain` become a combo host method. Player gets a combo host built on `ComboMachine`; enemies get an independent-attack host (today's `UsesIndependentAttacks` path). Per project convention, **delete** the old tracker, trigger, bridge states and string checks when replaced; no shims.

## Assets

The existing Cascadeur set (`I:\Art\Cascadeur\Longsword_Combo_ClipRanges.md`) already has one strip per attack: attack 0-70/0-55/0-59 then recovery to Ready. The "Combo" bridge clips go away.

- Likely **no Cascadeur work is needed to start**: re-import each attack as one clip (Attack + Recovery). That is an importer range change.
- **Pose mismatch is the main art risk.** Attack 2 and 3 were authored to start from the previous attack's *combo-end* pose, not from mid-recovery. If `ChainOpen` is placed shortly after the hit ends the poses are close; a `BlendIn` / `StartOffset` hides the rest. If it looks bad, re-author each attack's start (guard-ish pose) in Cascadeur.

## As built

- **Press path.** Every melee press, first or chained, is `IAbilityActivator.RequestActivate`. On a client it sends `ActivateServerRpc(shortId, pressTick)`. `AbilityUser.Run` asks the character's `IMeleeSwingHost.Press(pressTick, freshAllowed)`: `Rejected` does nothing; `Fresh` charges the ability cooldown; `Continued` does not. The host reserves the step, then the ability's MeleeSwing node calls `Begin`, which starts the swing; `CancelPress()` releases an unused reservation. `GraphAbilitySlot` no longer gates melee presses on its local cooldown, since only the server knows if a press is a continuation.
- **Hosts.** `MeleeDeliveryBase` (shared swing tracking, weapon hits, `MeleeAttackClientRpc`) with `MeleeDelivery` (enemies, clip-event hit windows, independent attacks) and `ComboMeleeDelivery` (players, `ComboMachine`, data hit windows). `ComboMeleeDelivery` lives in the Integration assembly because it reads the network clock; tests move its `Clock` by hand.
- **Latency credit.** The server credits a stamped press with its time, but at most `ComboMeleeDelivery.MaxRewindSeconds` (0.15 s) before now and never in the future. A shipped combo test requires each chain window to close at least that long before its step ends.
- **Presentation.** The host describes an attack as an `AttackAnimation` (trigger for enemies, state + blend + offset for combos). `AnimatableCharacter` plays states with `CrossFadeInFixedTime`. The player controller has an `Attack` layer masked to body, head, arms and fingers (`Longsword_UpperBody.mask`) with states `Longsword_A1..A3` that exit to an empty state; locomotion keeps running underneath. The old Attack/Combo/Recovery states, `ComboQueued`, `Attack_OneHanded` and `MeleeComboTracker` are gone.
- **Owner prediction.** On a pure client the owner's press calls `IMeleeSwingHost.BeginClientPress(freshAllowed)`, which asks a `ComboPredictor` (a client-side `ComboMachine` on the local clock plus the list of unanswered guesses). An accepted guess shows the attack at once (`MeleeAttackStarted(attack, 0)`) and the press goes to the server with an id. The server echoes the id in `MeleeAttackClientRpc(attackNumber, startTime, pressId)`: the same attack is `Confirmed` and not replayed; a different or unpredicted attack is `Replay`ed and the predictor `Resync`s to it; a press the server refuses sends `PressRejectedClientRpc(pressId)` to the owner (via `AbilityUser`), which raises `MeleeAttackCancelled` and fades the attack layer to `Empty`. A guess nobody answers is given up after 1 s. A fresh chain is not predicted while the slot's cooldown shows. The press is always sent, even when the client predicted nothing, since the server credits up to 0.15 s of latency and may accept a press the client judged just outside a window.
- **Remote seek.** The attack RPC carries the server time the step started; every client computes the seconds already elapsed on its own server-time estimate (clamped to the step) and `AnimatableCharacter` joins the clip that far in, on top of the step's own `StartOffset`.
- **Latency tests.** `RemoteClientTestBase` is the shared host-plus-client harness; `RemoteClientLatencyNetworkTests` runs it through the Multiplayer Tools Network Simulator (100 ms each way, 20 ms jitter; the transport's own debug simulator no longer works). It checks that the owner's swing shows on the press and is not replayed by the late confirmation, that a chain press made in the window is accepted despite the delay, that an early press is discarded by both sides, that a refusal arrives only after the round trip, and that another player's swing reaches the client once.
- **Hit and clip sync.** The blade hitbox rides the animated weapon bone, so the server animator must play each step in step with the machine; data hit windows are authored from the Cascadeur hit frames. Server animators are forced to always animate.
- **Shipped longsword combo** (`Assets/Items/Weapon Data/Longsword_Combo.asset`, assigned to the player's `TestWeaponData`): hit windows from the Cascadeur frames (A1 56-70, A2 38-55, A3 45-59); chain windows A1 1.45-2.0 s and A2 1.2-1.7 s are **first guesses** to tune by feel; blend-in 0.15 s; damage scalars 1 / 0.79 / 0.84 as before.

## Open questions

1. Chain after step 3: reset to step 1 (assumed), or allow a wrap?
2. Hit windows from data (proposed, server no longer needs animation events) vs keeping clip events for the hit. Recommendation: data, to match "keep it as data".
3. Rejected owner prediction: rollback visually immediately, or fade? (Proposed: crossfade to server state over `BlendIn`.)

## Spike results (simplerpg-7du.1.5.1, answered)

- **Body yaw reaches the server.** The player prefab (`TestCharacter`) uses `FirstPersonCharacterController`. Each frame the owner sends the mouse yaw delta (`RotateCharacterServerRPC`), the server rotates the transform, and a server-authority `NetworkTransform` replicates it. The server's weapon hitbox therefore already follows the real facing. Press RPCs and yaw RPCs travel the same reliable channel from the same client, so the server sees a press with the yaw the owner had (give or take the rewind tolerance).
- **Side effect to know about:** the owner's camera/body only turns when the server's rotation comes back, so on a remote client the view lags by a round trip. Predicting the *swing* does not fix that; a predicted view turn is a separate task (see bead).
- **No attack layer exists.** The player controller has a single `Base Layer` (no mask), and attacks are reached through one Any State transition on `Attack_OneHanded`. A masked attack layer must be added.
- **Precedent exists:** the skeleton has `Weapon_Attack` / `Weapon_Arm` / `OffHand_Attack` avatar masks and "empty layers use Write Defaults so they release the leg pose" (see MvpImplementation.md). Reuse that approach for the player with an upper-body mask.
- **Swings do not block movement anywhere.** No movement lock or attack check exists in either controller or `Character`. Free movement during a swing is the current behaviour.
- **Clip caveat:** the Cascadeur clips are full-body with in-place foot steps. Under an upper-body mask the legs come from locomotion; the mask boundary (spine vs hips) needs a visual check in the tuning task.
- **Still open (not spiked):** how `WeaponData` references a `ComboDefinition`. Probably no registry id is needed: the wire carries step index only.

## Test plan

- EditMode: `ComboMachine` boundaries (press exactly at open/close, before, after, spam, last step, reset), data validation, tick rewind clamp.
- PlayMode: hit window from data hits a target on a real host; chain through all steps; early press discarded.
- PlayMode with simulated latency: owner predicts, server accepts/rejects, remote client seeks.
- Replaces `LongswordComboStateMachineTests` (animator transition timings) with tests of the new path.
