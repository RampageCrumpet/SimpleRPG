using SimpleRPG.Abilities;
using SimpleRPG.Controls;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SimpleRPG
{
    /// <summary>
    /// What the player asked for this frame.
    /// </summary>
    public readonly struct ControlInput
    {
        /// <summary>Bit i is set when ability slot i was pressed this frame.</summary>
        public readonly uint AbilityPressed;

        /// <summary>Bit i is set while ability slot i is held.</summary>
        public readonly uint AbilityHeld;

        /// <summary>The interact key was pressed this frame.</summary>
        public readonly bool InteractPressed;

        public ControlInput(uint abilityPressed, uint abilityHeld, bool interactPressed)
        {
            AbilityPressed = abilityPressed;
            AbilityHeld = abilityHeld;
            InteractPressed = interactPressed;
        }
    }

    /// <summary>
    /// Turns the owning player's input into gameplay: activating abilities, starting and stopping interactions, and asking for the next life
    /// after death. It also announces the character as the local player (see <see cref="LocalPlayer"/>) so a UI can observe it. Nothing here
    /// needs a UI, so the game stays playable with the presentation removed or replaced.
    /// </summary>
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(Interactor))]
    public class LocalPlayerControls : NetworkBehaviour
    {
        private Character character;

        private Interactor interactor;

        private InputAction[] abilityActions;

        private bool[] requiresNewPress;

        public override void OnNetworkSpawn()
        {
            character = GetComponent<Character>();
            interactor = GetComponent<Interactor>();

            // Only the owner's machine reads this player's input.
            if (IsOwner)
            {
                LocalPlayer.Announce(character);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && character != null)
            {
                LocalPlayer.Withdraw(character);
            }
        }

        private void Update()
        {
            if (!IsOwner || character == null || interactor == null)
            {
                return;
            }

            interactor.RefreshTarget();
            Apply(Read());
        }

        /// <summary>
        /// Reads this frame's input from the bound controls.
        /// </summary>
        private ControlInput Read()
        {
            EnsureAbilityActions();

            uint pressed = 0;
            uint held = 0;
            for (int i = 0; i < abilityActions.Length && i < 32; i++)
            {
                InputAction action = abilityActions[i];
                if (action == null)
                {
                    continue;
                }

                if (action.WasPressedThisFrame()) pressed |= 1u << i;
                if (action.IsPressed()) held |= 1u << i;
            }

            return new ControlInput(pressed, held, GameInput.Interact.WasPressedThisFrame());
        }

        /// <summary>
        /// Acts on one frame of input. Public so a test, or another input source, can drive the character without a keyboard.
        /// </summary>
        public void Apply(ControlInput input)
        {
            if (character == null || interactor == null)
            {
                character = GetComponent<Character>();
                interactor = GetComponent<Interactor>();
            }

            // A dead player's only control is asking for the next life. The death screen is a menu, so a menu does not hold this back.
            if (!character.IsAlive)
            {
                if (input.InteractPressed)
                {
                    character.RequestRespawn();
                }

                return;
            }

            // While a menu has the player's attention the gameplay keys are not read.
            if (LocalPlayer.InMenu)
            {
                return;
            }

            EnsureAbilityActions();
            IReadOnlyList<IAbilitySlot> slots = character.AbilitySlots;
            for (int i = 0; i < slots.Count && i < 32; i++)
            {
                uint bit = 1u << i;
                bool wanted = requiresNewPress[i] ? (input.AbilityPressed & bit) != 0 : (input.AbilityHeld & bit) != 0;
                if (wanted)
                {
                    slots[i].Activate();
                }
            }

            if (input.InteractPressed && interactor.Target != null && interactor.CanInteract)
            {
                if (interactor.IsInteracting)
                {
                    interactor.StopInteraction();
                }
                else
                {
                    interactor.StartInteraction(interactor.Target);
                }
            }
        }

        /// <summary>
        /// Works out, once, the control for each ability slot and whether holding it repeats the ability. A melee swing needs a fresh press each time.
        /// </summary>
        private void EnsureAbilityActions()
        {
            IReadOnlyList<IAbilitySlot> slots = character.AbilitySlots;
            if (abilityActions != null && abilityActions.Length == slots.Count)
            {
                return;
            }

            abilityActions = new InputAction[slots.Count];
            requiresNewPress = new bool[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                abilityActions[i] = GameInput.Ability(i + 1);
                requiresNewPress[i] = slots[i] is GraphAbilitySlot graph && graph.Definition.HasNode(NodeTypes.MeleeSwing);
            }
        }
    }
}
