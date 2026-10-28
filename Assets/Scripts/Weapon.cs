using SimpleRPG.HitDetection;
using SimpleRPG;
using System;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// This script handles collision detection for weapons. It decides which touches count as hits (a target once per swing, never the wielder);
    /// what a hit does is up to whoever is listening, which is the wielder's melee delivery.
    /// </summary>
    public class Weapon : NetworkBehaviour
    {
        private Hitbox hitbox;

        /// <summary>
        /// Tracks who the current swing has already hit.
        /// </summary>
        private readonly SwingHitTracker swing = new SwingHitTracker();

        /// <summary>
        /// The character swinging this weapon, who must never be hit by it. Null for a weapon not carried by a character.
        /// </summary>
        private Character owner;

        /// <summary>
        /// Raised on the server when the open hit window touches a valid target for the first time this swing.
        /// </summary>
        public event Action<Character, BodyLocation> HitConfirmed;

        public void Awake()
        {
            hitbox = GetComponentInChildren<Hitbox>();
            if (hitbox != null)
            {
                hitbox.HitboxCollisionEvent.AddListener(TargetHit);
                hitbox.enabled = false;
            }
        }

        public override void OnDestroy()
        {
            if (hitbox != null)
            {
                hitbox.HitboxCollisionEvent.RemoveListener(TargetHit);
            }

            base.OnDestroy();
        }

        public void TargetHit(Character character, BodyLocation location)
        {
            if (this.IsServer)
            {
                // Resolved late so a weapon equipped after Start still knows its wielder.
                if (owner == null) owner = GetComponentInParent<Character>();
                if (owner != null && !owner.IsAlive) return;

                // One swing hits a target once (however many of its hurtboxes the blade touches) and never its own wielder.
                if (swing.TryRegisterHit(character, owner))
                {
                    HitConfirmed?.Invoke(character, location);
                }
            }
        }

        /// <summary>
        /// Opens the hit window for a swing. Opening it again during the same swing (a doubled animation event) keeps the record of who was hit.
        /// </summary>
        public void ActivateHitbox()
        {
            swing.Begin();
            // Only the server acts on hits (see TargetHit), so a client that is only animating the swing does not run the sweeps.
            if (hitbox != null) hitbox.enabled = ResolvesHits;
        }

        /// <summary>
        /// True where hits are resolved: on the server, or when no network session is running (offline play and tests).
        /// </summary>
        private static bool ResolvesHits
        {
            get
            {
                NetworkManager manager = NetworkManager.Singleton;
                return manager == null || !manager.IsListening || manager.IsServer;
            }
        }

        public void DeactivateHitbox()
        {
            swing.End();
            if (hitbox != null) hitbox.enabled = false;
        }
    }
}
