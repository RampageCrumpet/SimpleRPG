namespace AI
{
    /// <summary>Terminal state. Shutdown and presentation are committed once by Character's death event.</summary>
    public sealed class DeadState : IState
    {
        public void OnStateEnter() { }
        public void OnStateExit() { }
        public void OnStateUpdate() { }
    }
}
