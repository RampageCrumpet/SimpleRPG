namespace SimpleRPG.Abilities
{
    /// <summary>
    /// What makes a <see cref="TriggeredEffect"/> run. A trigger is added when the game has the event behind it (a critical hit waits for critical
    /// hits to exist), and each one says who the owner and the other party are in <see cref="TriggerContext"/>.
    /// </summary>
    public enum EffectTrigger : byte
    {
        /// <summary>The owner landed a weapon blow. Owner: the attacker. Other: the one struck. Runs after the damage is resolved, so it knows how much and whether it killed.</summary>
        OnHit = 0,

        /// <summary>The owner died. The ability starts where they died, and it runs even if the host is removed during the death.</summary>
        OnOwnerDeath = 1,

        /// <summary>The owner killed something, by any means. Owner: the killer. Other: the victim.</summary>
        OnKill = 2,

        /// <summary>The owner was damaged. Owner: the one struck. Other: whoever dealt it, if anyone.</summary>
        OnTakeDamage = 3,
    }

    /// <summary>Which party an effect's ability is aimed at (its Current Target).</summary>
    public enum EffectTarget : byte
    {
        /// <summary>The owner: the wearer of the item, the holder of the status.</summary>
        Owner = 0,

        /// <summary>The other party of the trigger: the one the owner struck, or whoever struck the owner.</summary>
        Other = 1,
    }

    /// <summary>Whose actions can trigger an effect.</summary>
    public enum EffectScope : byte
    {
        /// <summary>Anything the owner does, whatever item or spell did it. An armor effect that reacts to every blow the wearer lands.</summary>
        Owner = 0,

        /// <summary>Only what the host itself did: a sword's effect reacts to blows struck with that sword.</summary>
        Host = 1,
    }
}
