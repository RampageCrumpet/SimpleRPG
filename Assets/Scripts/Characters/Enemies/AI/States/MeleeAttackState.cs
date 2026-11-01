using SimpleRPG.Abilities;

namespace SimpleRPG.AI
{
    /// <summary>Selects a ready melee ability; actual hits remain animation-driven weapon contacts.</summary>
    public sealed class MeleeAttackState : IState
    {
        private readonly AIController controller;
        private readonly System.Action outsideRange;
        public MeleeAttackState(AIController controller, System.Action outsideRange)
        {
            this.controller = controller;
            this.outsideRange = outsideRange;
        }
        // Not every enemy has melee delivery, so this may stay null; looked up once, not every frame.
        private MeleeDelivery delivery;

        public void OnStateEnter()
        {
            delivery = controller.GetComponent<MeleeDelivery>();
            controller.StopMoving();
        }
        public void OnStateExit() { }
        public void OnStateUpdate()
        {
            // Commit to direction and stay planted through windup, strike and recovery.
            if (delivery != null && !delivery.CanStartFreshChain) return;
            if (!controller.TrackTarget() || !controller.InAttackRange) { outsideRange(); return; }
            controller.FaceTarget();
            if (!controller.IsFacingTarget) return;
            GraphAbilitySlot slot = ReadyMeleeSlot();
            slot?.Activate();
        }

        // The first ready melee slot, found without allocating: this runs every frame for every attacking enemy.
        private GraphAbilitySlot ReadyMeleeSlot()
        {
            var slots = controller.Character.Loadout.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] is GraphAbilitySlot s && s.Definition != null && s.Definition.HasNode(NodeTypes.MeleeSwing) && s.CooldownTimeLeft <= 0) return s;
            }

            return null;
        }
    }
}
