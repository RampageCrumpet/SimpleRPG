namespace AI
{
    public sealed class IdleState : IState
    {
        private readonly EnemyController controller;
        public IdleState(EnemyController controller) => this.controller = controller;
        public void OnStateEnter() => controller.StopMoving();
        public void OnStateExit() { }
        public void OnStateUpdate()
        {
            if (controller.TrackTarget()) controller.Enter(EnemyState.Chase);
        }
    }
}
