using SimpleRPG.Abilities;
using System.Linq;

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
            var slot = controller.Character.AbilitySlots.OfType<GraphAbilitySlot>()
                .FirstOrDefault(s => s.Definition != null && s.Definition.HasNode(NodeTypes.MeleeSwing) && s.CooldownTimeLeft <= 0);
            slot?.Activate();
        }
    }
}
