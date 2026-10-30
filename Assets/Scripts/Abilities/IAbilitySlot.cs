using System;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// One entry on a character's ability bar, as the UI sees it: a name, an icon, a cooldown and a way to use it.
    /// It lets the bar show abilities without knowing how each is run.
    /// </summary>
    public interface IAbilitySlot
    {
        string Name { get; }

        Sprite Icon { get; }

        /// <summary>The cooldown in seconds.</summary>
        float CooldownTime { get; }

        /// <summary>Seconds until the ability can be used again, zero or less when it is ready. A prediction on the client; the server decides.</summary>
        float CooldownTimeLeft { get; }

        /// <summary>Raised when the ability is used.</summary>
        event Action<IAbilitySlot> Activated;

        /// <summary>Uses the ability if its cooldown allows.</summary>
        void Activate();
    }

    /// <summary>
    /// The slot for a graph-based <see cref="AbilityDefinition"/>. Using it asks the character's <see cref="IAbilityActivator"/>;
    /// the server decides whether and how the ability runs. The cooldown shown here is only a prediction.
    /// </summary>
    public sealed class GraphAbilitySlot : IAbilitySlot
    {
        private readonly AbilityDefinition definition;

        private readonly Character character;

        private float lastActivationTime = float.NegativeInfinity;

        public GraphAbilitySlot(AbilityDefinition definition, Character character)
        {
            this.definition = definition;
            this.character = character;
        }

        public AbilityDefinition Definition => definition;

        public string Name => definition.AbilityName;

        public Sprite Icon => definition.Icon;

        public float CooldownTime => definition.CooldownSeconds;

        public float CooldownTimeLeft => lastActivationTime + definition.CooldownSeconds - Time.time;

        public event Action<IAbilitySlot> Activated;

        public void Activate()
        {
            // A dead character uses nothing. The server refuses too; this keeps the client from showing a use or a cooldown.
            if (!character.IsAlive)
            {
                return;
            }

            IAbilityActivator activator = character.GetComponent<IAbilityActivator>();
            if (activator == null)
            {
                return;
            }

            // A melee press may continue a combo that is already paying the cooldown, so only the server can say whether it is allowed; the
            // cooldown shown here only tells the melee mechanics whether a fresh chain would be allowed. Every other ability is refused here
            // while its cooldown runs, so a held button does not flood the server.
            bool cooledDown = Time.time > lastActivationTime + definition.CooldownSeconds;
            if (!cooledDown && !definition.HasNode(NodeTypes.MeleeSwing))
            {
                return;
            }

            // Only a press that starts a fresh use pays the cooldown, as on the server: a press that continues a combo, or one the combo
            // ignores, must not push the shown cooldown out, or this display drifts from the server's and wrongly refuses the next fresh chain.
            MeleeDeliveryBase melee = character.GetComponent<MeleeDeliveryBase>();
            bool continuesChain = definition.HasNode(NodeTypes.MeleeSwing) && melee != null && melee.IsChainRunning;

            if (!activator.RequestActivate(definition, cooledDown))
            {
                return;
            }

            if (cooledDown && !continuesChain)
            {
                lastActivationTime = Time.time;
            }

            Activated?.Invoke(this);
        }
    }
}
