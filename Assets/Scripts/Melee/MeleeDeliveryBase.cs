using SimpleRPG.HitDetection;
using SimpleRPG;
using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG
{
    /// <summary>
    /// A weapon hit the server confirmed, as every machine is told it, so presentation (sound, effects) can react to a real contact.
    /// </summary>
    public readonly struct MeleeHitCue
    {
        public MeleeHitCue(Character victim, BodyLocation location, int attackNumber, Vector3 point)
        {
            Victim = victim;
            Location = location;
            AttackNumber = attackNumber;
            Point = point;
        }

        /// <summary>The character struck, or null if it is not known on this machine.</summary>
        public Character Victim { get; }

        public BodyLocation Location { get; }

        public int AttackNumber { get; }

        /// <summary>Where the blade was when it struck.</summary>
        public Vector3 Point { get; }
    }

    /// <summary>
    /// A blade meeting a wall, as every machine is told it, so presentation (a sound, sparks, a recoil) can react.
    /// </summary>
    public readonly struct MeleeBounceCue
    {
        public MeleeBounceCue(int attackNumber, Vector3 point, Vector3 normal)
        {
            AttackNumber = attackNumber;
            Point = point;
            Normal = normal;
        }

        public int AttackNumber { get; }

        /// <summary>Where the blade struck the wall.</summary>
        public Vector3 Point { get; }

        /// <summary>The way the wall's surface faces, so a recoil or spark can point away from it.</summary>
        public Vector3 Normal { get; }
    }

    /// <summary>
    /// What every character's melee mechanics share: the swings the server is tracking, the weapon's hits and how they are reported to the
    /// ability that started the swing, and telling every machine which attack to play. A melee ability asks for a swing and is told what the
    /// swing hits; subclasses decide which attack plays and when its hit window is open, so abilities never handle animation, hitboxes or combo
    /// timing themselves. The server decides which attack plays and what it hits; clients only play the animation.
    /// </summary>
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(Animator))]
    public abstract class MeleeDeliveryBase : NetworkBehaviour, IMeleeSwingHost
    {
        /// <summary>
        /// A swing the server has started and whose hits it is waiting to report.
        /// </summary>
        protected sealed class Swing
        {
            public ContinuationToken Token;
            public IMeleeEvents Events;
            public ExecutionId Execution;
            public int AttackNumber;
            public double StartedAt;
        }

        /// <summary>
        /// Swings the server is tracking, by attack number. A chained swing can exist before the one playing now has finished.
        /// </summary>
        protected readonly Dictionary<int, Swing> Swings = new Dictionary<int, Swing>();

        private Weapon weapon;

        /// <summary>
        /// The character who owns this melee mechanics component.
        /// </summary>
        protected Character Character { get; private set; }

        /// <summary>
        /// The swing whose hit window is open, or null. Hits outside any window are ignored.
        /// </summary>
        protected Swing ActiveSwing { get; set; }

        protected Weapon WeaponComponent => weapon != null ? weapon : (weapon = GetComponentInChildren<Weapon>());

        /// <summary>
        /// Raised on every client when an attack should start showing. The int is the attack number (1 for the first attack of a chain) and the
        /// float how many seconds into the attack it already is, because the news took time to arrive.
        /// </summary>
        public delegate void NotifyMeleeAttackStarted(int attackNumber, float secondsIntoAttack);

        public event NotifyMeleeAttackStarted MeleeAttackStarted;

        /// <summary>
        /// Raised on every machine when the server confirms a weapon hit on a living target. Only confirmed contacts raise it, never an attack merely starting.
        /// </summary>
        public event System.Action<MeleeHitCue> MeleeHitConfirmed;

        /// <summary>
        /// Raised on every machine when the server finds the blade has met a wall. The swing deals no damage from then on.
        /// </summary>
        public event System.Action<MeleeBounceCue> MeleeBounced;

        /// <summary>
        /// Raised on a client when an attack it started showing before the server had spoken turned out not to have happened.
        /// </summary>
        public event System.Action MeleeAttackCancelled;

        /// <summary>
        /// How an attack is shown on every machine.
        /// </summary>
        public abstract AttackAnimation AnimationFor(int attackNumber);

        /// <summary>
        /// The damage multiplier of an attack.
        /// </summary>
        protected abstract float DamageScalar(int attackNumber);

        /// <inheritdoc/>
        public abstract MeleePressOutcome Press(long pressTick, bool freshAllowed, int pressId = 0);

        /// <inheritdoc/>
        public abstract void CancelPress();

        /// <inheritdoc/>
        public abstract bool Begin(in MeleeSwingLaunch launch, IMeleeEvents events);

        /// <inheritdoc/>
        public virtual bool IsChainRunning => false;

        /// <inheritdoc/>
        public virtual int BeginClientPress(bool freshAllowed) => 0;

        /// <inheritdoc/>
        public virtual void RejectClientPress(int pressId)
        {
        }

        protected virtual void Awake()
        {
            Character = GetComponent<Character>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            // A server with no screen has nothing to draw, and an animator that is culled never moves the weapon its hitbox rides on.
            foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            Weapon equipped = WeaponComponent;
            if (equipped != null)
            {
                equipped.HitConfirmed += HandleWeaponHit;
                equipped.Bounced += HandleWeaponBounced;
            }

            Character.OnDeath += ReleaseAllSwings;
        }

        public override void OnNetworkDespawn()
        {
            if (weapon != null)
            {
                weapon.HitConfirmed -= HandleWeaponHit;
                weapon.Bounced -= HandleWeaponBounced;
            }

            if (Character != null)
            {
                Character.OnDeath -= ReleaseAllSwings;
            }

            if (IsServer)
            {
                ReleaseAllSwings();
            }
        }

        /// <summary>
        /// Starts tracking a swing and tells every machine which attack to play. Server only.
        /// </summary>
        /// <param name="startTime">When the attack started, on the server clock, so a machine that hears late can join it part-way through.</param>
        /// <param name="pressId">The id of the client press that started it, or zero.</param>
        protected void StartSwing(int attackNumber, in MeleeSwingLaunch launch, IMeleeEvents events, double startTime, int pressId)
        {
            if (Swings.TryGetValue(attackNumber, out Swing stale))
            {
                Finish(stale);
            }

            Swings[attackNumber] = new Swing
            {
                Token = launch.Token,
                Events = events,
                Execution = launch.Execution,
                AttackNumber = attackNumber,
                StartedAt = ServerClock.Now,
            };

            MeleeAttackClientRpc(attackNumber, startTime, pressId);
        }

        /// <summary>
        /// Triggers a melee attack on all clients.
        /// </summary>
        /// <param name="attackNumber">The attack the server has started. Every client plays the same one.</param>
        /// <param name="startTime">When the server started it, on the server clock.</param>
        /// <param name="pressId">The press that started it, which only the pressing client uses.</param>
        [Rpc(SendTo.Everyone)]
        public void MeleeAttackClientRpc(int attackNumber, double startTime, int pressId)
        {
            float secondsIn = (float)System.Math.Max(0d, NetworkManager.ServerTime.Time - startTime);
            OnAttackStarted(attackNumber, secondsIn, pressId);
        }

        /// <summary>
        /// Tells every machine a weapon hit was confirmed, so each can show it. Carries what happened, never a sound.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void HitConfirmedClientRpc(ulong victimObjectId, int location, int attackNumber, Vector3 point)
        {
            Character victim = null;
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(victimObjectId, out NetworkObject spawned))
            {
                victim = spawned.GetComponent<Character>();
            }

            RaiseHitConfirmed(new MeleeHitCue(victim, (BodyLocation)location, attackNumber, point));
        }

        /// <summary>
        /// Tells every machine the blade met a wall. Carries what happened, never a sound.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void MeleeBouncedClientRpc(int attackNumber, Vector3 point, Vector3 normal)
        {
            var cue = new MeleeBounceCue(attackNumber, point, normal);
            OnBounceShown(cue);
            MeleeBounced?.Invoke(cue);
        }

        /// <summary>
        /// The word that the blade met a wall reached this machine. By default the attack being shown is cut short: its layer fades back to the
        /// ready stance. Subclasses add what they keep for themselves, such as the owning client's guess about the combo.
        /// </summary>
        protected virtual void OnBounceShown(in MeleeBounceCue cue)
        {
            RaiseAttackCancelled();
        }

        /// <summary>
        /// Server only. The swing has just been cut short by a wall. Subclasses add the price: how long the wielder is thrown off balance.
        /// </summary>
        protected virtual void OnBladeBounced()
        {
        }

        /// <summary>
        /// Server only. The open swing's blade met a wall; no hit lands past it, the swing ends, and everyone is told where it struck.
        /// </summary>
        private void HandleWeaponBounced(Vector3 point, Vector3 normal)
        {
            Swing swing = ActiveSwing;
            if (swing == null || !Character.IsAlive)
            {
                return;
            }

            MeleeBouncedClientRpc(swing.AttackNumber, point, normal);

            // The swing ends here, as if the wielder had died mid-swing: the ability is told, the hit window closes and any chain stops.
            ReleaseAllSwings();
            OnBladeBounced();
        }

        /// <summary>
        /// Tells this machine's listeners a weapon hit was confirmed.
        /// </summary>
        protected void RaiseHitConfirmed(MeleeHitCue cue)
        {
            MeleeHitConfirmed?.Invoke(cue);
        }

        /// <summary>
        /// The server word that an attack has started reached this machine. Shows it unless it is already showing.
        /// </summary>
        protected virtual void OnAttackStarted(int attackNumber, float secondsIntoAttack, int pressId)
        {
            RaiseAttackStarted(attackNumber, secondsIntoAttack);
        }

        protected void RaiseAttackStarted(int attackNumber, float secondsIntoAttack)
        {
            MeleeAttackStarted?.Invoke(attackNumber, secondsIntoAttack);
        }

        protected void RaiseAttackCancelled()
        {
            MeleeAttackCancelled?.Invoke();
        }

        /// <summary>
        /// Ends a swing: the ability that started it is told, and it is no longer tracked.
        /// </summary>
        protected void Finish(Swing swing)
        {
            if (Swings.TryGetValue(swing.AttackNumber, out Swing tracked) && tracked == swing)
            {
                Swings.Remove(swing.AttackNumber);
            }

            if (ActiveSwing == swing)
            {
                ActiveSwing = null;
            }

            swing.Events.Completed(swing.Token);
        }

        /// <summary>
        /// Closes the weapon's hit window and ends every swing in progress, telling their abilities. Server only.
        /// </summary>
        protected virtual void ReleaseAllSwings()
        {
            WeaponComponent?.DeactivateHitbox();
            foreach (Swing swing in Swings.Values.ToList())
            {
                Finish(swing);
            }

            ActiveSwing = null;
        }

        /// <summary>
        /// Server only. The weapon touched a target for the first time this swing. Reports the hit to the ability that started the swing.
        /// </summary>
        private void HandleWeaponHit(Character victim, BodyLocation location)
        {
            Swing swing = ActiveSwing;
            if (swing == null || victim == null || !Character.IsAlive)
            {
                return;
            }

            WeaponData weaponData = Character.Loadout.Weapon;
            float baseDamage = weaponData != null && weaponData.damage != null ? weaponData.damage.Damage : 0f;
            bool victimWasAlive = victim.IsAlive;

            MeleeHit hit = new MeleeHit(victim.Id, (int)location, swing.AttackNumber, DamageScalar(swing.AttackNumber), baseDamage);
            swing.Events.Hit(swing.Token, hit);

            // A blow that lands on a living target is a confirmed weapon hit; the damage above has already been applied, so rules reacting to it see the result.
            if (victimWasAlive)
            {
                HitConfirmedClientRpc(victim.NetworkObjectId, (int)location, swing.AttackNumber, WeaponComponent != null ? WeaponComponent.transform.position : transform.position);

                GameplayEventDispatcher events = GameplayServices.Current.Events;
                events.Enqueue(new WeaponHitConfirmedEvent(Character.Id, victim.Id, (int)location, swing.AttackNumber, swing.Execution));
                if (!events.IsDraining)
                {
                    events.Drain();
                }
            }
        }
    }
}
