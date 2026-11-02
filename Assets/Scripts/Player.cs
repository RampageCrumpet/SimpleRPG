using SimpleRPG;
using SimpleRPG.Delve;
using SimpleRPG.Networking;
using SimpleRPG.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG
{
    public class Player : NetworkBehaviour
    {
        /// <summary>
        /// The prefab we want to use to spawn our player character.
        /// </summary>
        [SerializeField]
        [Tooltip("The prefab we want to use to spawn our player character.")]
        private GameObject playerPrefab;

        /// <summary>
        /// The character our player is controlling.
        /// </summary>
        [HideInInspector]
        [Tooltip("Our player's character")]
        public Character character;

        /// <summary>
        /// The spawned character, replicated so clients that join later receive it too. Written once by the server.
        /// </summary>
        private readonly NetworkVariable<NetworkObjectReference> characterReference = new NetworkVariable<NetworkObjectReference>();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer) StartCoroutine(SpawnWhenDungeonReady());
            else StartCoroutine(BindCharacterWhenAvailable());
        }

        /// <summary>
        /// Clients wait for the replicated reference and for the character object it points at, which may arrive after the player object.
        /// </summary>
        private IEnumerator BindCharacterWhenAvailable()
        {
            while (IsSpawned && character == null)
            {
                if (characterReference.Value.TryGet(out NetworkObject characterObject, NetworkManager))
                {
                    character = characterObject.GetComponent<Character>();
                    character.Initialize();
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator SpawnWhenDungeonReady()
        {
            var population = UnityEngine.Object.FindAnyObjectByType<SimpleRPG.LevelGeneration.DungeonPopulation>();
            while (population != null && !population.IsReady && !population.HasFailed) yield return null;
            if (population != null && population.HasFailed)
                Debug.LogError($"Spawning player {OwnerClientId} at the fallback position because dungeon population failed: {population.FailureReason}", this);
            if (IsSpawned) SpawnCharacter();
        }

        /// <summary>
        /// Creates the character object across the network.
        /// </summary>
        private void SpawnCharacter()
        {
            // Ensure that only the server tries to spawn a character.
            if (IsServer)
            {
                var population = UnityEngine.Object.FindAnyObjectByType<SimpleRPG.LevelGeneration.DungeonPopulation>();
                Vector3 startPosition = population != null && population.IsReady ? population.PlayerSpawnPosition + Vector3.up * 0.15f : new Vector3(0, 5, 0);

                // A player who dropped out and is back finds their character as it was; the first time, or without delves, it starts at the start.
                DelveController delve = DelveController.Find(NetworkManager);
                string playerId = null;
                bool keepable = delve != null && PlayerSessions.For(NetworkManager).TryGetPlayerId(OwnerClientId, out playerId);
                ParkedCharacter returning = keepable ? delve.TakeParked(playerId) : null;

                Vector3 spawnPosition = returning != null ? returning.Position : startPosition;
                Quaternion spawnRotation = returning != null ? returning.Rotation : playerPrefab.transform.rotation;
                GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, spawnRotation);

                // A character that can be kept for its player must survive the player's connection ending; delve.Park takes care of it from there.
                NetworkObject characterObject = playerInstance.GetComponent<NetworkObject>();
                characterObject.DontDestroyWithOwner = keepable;
                characterObject.SpawnWithOwnership(this.OwnerClientId, true);

                // Ensure that the server has the connection betweeen the character and the client.
                character = playerInstance.GetComponent<Character>();
                character.Initialize();

                if (keepable)
                {
                    character.SetRespawnPointOnServer(startPosition, playerPrefab.transform.rotation);
                    delve.RegisterCharacter(playerId, OwnerClientId, character);
                }

                if (returning != null)
                {
                    int lost = returning.RestoreInto(character);
                    if (lost > 0) Debug.LogError($"{lost} item(s) of returning player {playerId} could not be put back.", this);

                    // A character that was dead when its player left is still dead; its belongings are already in a loot bag.
                    if (returning.WasDead)
                    {
                        character.TakeDamage(new DamageInfo { Damage = character.MaxHealth }, SimpleRPG.HitDetection.BodyLocation.Torso);
                    }
                }

                characterReference.Value = new NetworkObjectReference(playerInstance);
            }
        }
    }
}
