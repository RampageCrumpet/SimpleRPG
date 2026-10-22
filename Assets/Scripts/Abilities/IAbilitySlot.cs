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
            IAbilityActivator activator = character.GetComponent<IAbilityActivator>();
            if (activator == null)
            {
                return;
            }

            // A press that continues a use in progress (the next swing of a combo) is taken even while the cooldown runs, and doesn't restart it.
            if (activator.TryQueue(definition))
            {
                return;
            }

            if (Time.time <= lastActivationTime + definition.CooldownSeconds || !activator.RequestActivate(definition))
            {
                return;
            }

            lastActivationTime = Time.time;
            Activated?.Invoke(this);
        }
    }
}
