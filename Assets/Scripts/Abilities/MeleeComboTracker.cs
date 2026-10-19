namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Server-side state for a melee combo chain. The server decides which attack number is playing and when a queued
    /// press is accepted, so every client plays the same branch of the chain. Times are server seconds.
    /// </summary>
    public class MeleeComboTracker
    {
        /// <summary>
        /// Seconds of slack around each attack window so network latency doesn't reject a press that was made in time on the client.
        /// </summary>
        public const float Tolerance = 0.1f;

        private readonly float[] attackLengths;
        private readonly float[] comboLengths;

        // The attack the chain is currently on (1-based), or 0 if no chain has started. Advances as soon as a press is queued.
        private int currentAttack;

        // When the current attack's clip starts, including the combo gap before it.
        private float currentAttackStart;

        // When the last queued attack finishes. A fresh chain may not start before this.
        private float busyUntil = float.NegativeInfinity;

        /// <summary>
        /// Creates a combo tracker for a chain of attacks.
        /// </summary>
        /// <param name="attackLengths">Length in seconds of each attack clip, in chain order. Its length is the number of attacks in the chain.</param>
        /// <param name="comboLengths">Length in seconds of the combo continuation after each attack. Entries past the last attack are unused.</param>
        public MeleeComboTracker(float[] attackLengths, float[] comboLengths)
        {
            this.attackLengths = attackLengths;
            this.comboLengths = comboLengths;
        }

        /// <summary>
        /// The attack number the chain is currently on (1-based), or 0 if no chain has started.
        /// </summary>
        public int CurrentAttack => currentAttack;

        /// <summary>
        /// Checks whether a fresh chain may start. A fresh chain is blocked only while a queued chain is still running,
        /// so unqueued attacks are limited by the ability's cooldown alone.
        /// </summary>
        /// <param name="now">The current server time in seconds.</param>
        public bool CanStartAttack(float now)
        {
            return now >= busyUntil - Tolerance;
        }

        /// <summary>
        /// Starts a fresh chain at attack 1. Call only after <see cref="CanStartAttack"/> returned true and the cooldown was accepted.
        /// </summary>
        /// <param name="now">The current server time in seconds.</param>
        public void StartAttack(float now)
        {
            currentAttack = 1;
            currentAttackStart = now;
            busyUntil = float.NegativeInfinity;
        }

        /// <summary>
        /// Queues the next attack in the chain if a press arrives during the current attack's clip.
        /// Presses in the combo gap, after the last attack, or before any chain has started are rejected.
        /// Once a press is accepted the window moves to the next attack, so repeated presses in the same window are rejected.
        /// </summary>
        /// <param name="now">The current server time in seconds.</param>
        /// <param name="nextAttack">The attack number that will play next, or 0 if the press was rejected.</param>
        /// <returns>True if the press was accepted.</returns>
        public bool TryQueueNext(float now, out int nextAttack)
        {
            nextAttack = 0;

            // No chain has started, or the current attack is the last one in the chain.
            if (currentAttack < 1 || currentAttack >= attackLengths.Length)
            {
                return false;
            }

            float windowStart = currentAttackStart - Tolerance;
            float windowEnd = currentAttackStart + attackLengths[currentAttack - 1] + Tolerance;
            if (now < windowStart || now >= windowEnd)
            {
                return false;
            }

            float nextStart = currentAttackStart + attackLengths[currentAttack - 1] + comboLengths[currentAttack - 1];
            currentAttack++;
            currentAttackStart = nextStart;
            busyUntil = nextStart + attackLengths[currentAttack - 1];
            nextAttack = currentAttack;
            return true;
        }
    }
}
