namespace SimpleRPG.AI
{
    public sealed class ChaseState : IState
    {
        private readonly EnemyController controller;
        public ChaseState(EnemyController controller) => this.controller = controller;
        public void OnStateEnter() { }
        public void OnStateExit() => controller.StopMoving();
        public void OnStateUpdate()
        {
            if (!controller.TrackTarget()) { controller.Enter(EnemyState.Idle); return; }
            if (controller.InAttackRange) { controller.Enter(EnemyState.Attack); return; }
            controller.SetDestination(controller.Target.transform.position);
        }
    }
}
