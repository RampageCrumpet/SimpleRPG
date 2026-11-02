using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Delve
{
    /// <summary>
    /// The rules of one delve, owned by the server: whether the party is still in the dungeon, how it ends, and what dying costs.
    /// <list type="bullet">
    /// <item>A player who dies loses everything: the inventory and worn gear move into a loot bag where they fell (<see cref="DeathDrop"/>).</item>
    /// <item>A dead player stays dead until the delve is over; a respawn is refused while it is running.</item>
    /// <item>The party extracts together: every living player has to stand in the extraction zone for the whole channel.</item>
    /// <item>The delve ends when the party extracts or when everyone is dead, and the outcome goes to the <see cref="RewardSink"/>.</item>
    /// </list>
    /// Clients only read the replicated state. The HUD here is a plain on-screen placeholder until the real one exists.
    /// </summary>
    public sealed class DelveController : NetworkBehaviour
    {
        // The server looks at the players this often, not every frame: a death or a step into the zone does not need to be noticed sooner.
        private const float PollSeconds = 0.2f;

        private static readonly Dictionary<NetworkManager, DelveController> Active = new Dictionary<NetworkManager, DelveController>();

        [SerializeField]
        [Tooltip("The loot bag a dead player's belongings go into. Needs a NetworkObject, an Inventory big enough for a full inventory plus all worn gear, and a LootBag.")]
        private NetworkObject lootBagPrefab;

        [SerializeField]
        [Min(0.5f)]
        [Tooltip("How close, in metres, a player has to be to the extraction point to count as at it.")]
        private float extractionRadius = 3f;

        [SerializeField]
        [Min(1f)]
        [Tooltip("How long, in seconds, the whole living party has to stay at the extraction point.")]
        private float extractionSeconds = 10f;

        private readonly NetworkVariable<DelvePhase> phase = new NetworkVariable<DelvePhase>(DelvePhase.Delving);
        private readonly NetworkVariable<bool> extractionKnown = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<Vector3> extractionPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<float> extractionProgress = new NetworkVariable<float>(0f);
        private readonly NetworkVariable<int> survivorCount = new NetworkVariable<int>();
        private readonly NetworkVariable<int> deadCount = new NetworkVariable<int>();
        private readonly NetworkVariable<float> durationSeconds = new NetworkVariable<float>();

        // Server only.
        private readonly Dictionary<Character, Character.NotifyDeath> tracked = new Dictionary<Character, Character.NotifyDeath>();
        private readonly List<Character> seen = new List<Character>();
        private readonly List<Character> departed = new List<Character>();
        private readonly List<Character> pendingDrops = new List<Character>();
        private readonly HashSet<Character> dead = new HashSet<Character>();
        private ExtractionChannel channel;
        private float sincePoll;
        private double startedAt;
        private bool anyPlayerSeen;
        private bool warnedNoBag;

        // Presentation.
        private GameObject marker;

        /// <summary>Where the outcome of a delve goes when it ends. Replace it to store results; the default only logs.</summary>
        public IDelveRewardSink RewardSink { get; set; } = new LogDelveRewardSink();

        /// <summary>Raised on the server when the delve ends.</summary>
        public event System.Action<DelveResult> Ended;

        public DelvePhase Phase => phase.Value;

        /// <summary>The extraction point, if the dungeon has one yet.</summary>
        public bool TryGetExtraction(out Vector3 position)
        {
            position = extractionPosition.Value;
            return extractionKnown.Value;
        }

        public float ExtractionRadius => extractionRadius;

        /// <summary>
        /// Whether a dead character may start its next life now. Always true where no delve is being run (a test scene, an older setup), so
        /// nothing that does not use delves changes.
        /// </summary>
        public static bool AllowsRespawn(Character character)
        {
            NetworkManager manager = character != null ? character.NetworkManager : null;
            return manager == null || !Active.TryGetValue(manager, out DelveController controller) || controller.phase.Value != DelvePhase.Delving;
        }

        /// <summary>The delve controller of this session, or null if there is none.</summary>
        public static DelveController Find(NetworkManager manager)
        {
            return manager != null && Active.TryGetValue(manager, out DelveController controller) ? controller : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        public override void OnNetworkSpawn()
        {
            Active[NetworkManager] = this;
            extractionKnown.OnValueChanged += HandleExtractionChanged;
            extractionPosition.OnValueChanged += HandleExtractionChanged;
            RefreshMarker();

            if (IsServer)
            {
                channel = new ExtractionChannel(extractionSeconds);
                startedAt = ServerClock.Now;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null && Active.TryGetValue(NetworkManager, out DelveController registered) && registered == this)
            {
                Active.Remove(NetworkManager);
            }

            extractionKnown.OnValueChanged -= HandleExtractionChanged;
            extractionPosition.OnValueChanged -= HandleExtractionChanged;
            DestroyMarker();

            foreach (KeyValuePair<Character, Character.NotifyDeath> pair in tracked)
            {
                if (pair.Key != null)
                {
                    pair.Key.OnDeath -= pair.Value;
                }
            }

            tracked.Clear();
        }

        /// <summary>Sets where the party extracts. Server only; called once the dungeon is built.</summary>
        public void ConfigureExtraction(Vector3 position)
        {
            if (!IsServer)
            {
                return;
            }

            extractionPosition.Value = position;
            extractionKnown.Value = true;
            channel?.Reset();
        }

        /// <summary>
        /// Starts the next delve after one has ended: the rules start over and every dead player gets a new life at the spawn point, with nothing
        /// but what they are given, and everyone who got out is returned there too. It does not rebuild the dungeon. Server only.
        /// </summary>
        /// <returns>False if a delve is still running.</returns>
        public bool StartNextDelve()
        {
            if (!IsServer || !IsSpawned || phase.Value == DelvePhase.Delving)
            {
                return false;
            }

            channel.Reset();
            extractionProgress.Value = 0f;
            dead.Clear();
            pendingDrops.Clear();
            startedAt = ServerClock.Now;
            phase.Value = DelvePhase.Delving;

            foreach (Character character in tracked.Keys)
            {
                if (character == null || !character.IsSpawned)
                {
                    continue;
                }

                // The dead come back at the start; so do the ones who got out, so nobody begins the next delve standing in the extraction zone.
                if (character.IsAlive)
                {
                    character.ReturnToSpawnOnServer();
                }
                else
                {
                    character.RespawnOnServer();
                }
            }

            return true;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || phase.Value != DelvePhase.Delving)
            {
                return;
            }

            sincePoll += Time.deltaTime;
            if (sincePoll < PollSeconds)
            {
                return;
            }

            float elapsed = sincePoll;
            sincePoll = 0f;
            Poll(elapsed);
        }

        private void Poll(float elapsed)
        {
            ScanPlayers();
            DropForTheDead();

            int living = 0;
            int livingAtExtraction = 0;
            Vector3 extraction = extractionPosition.Value;
            float limit = extractionRadius * extractionRadius;
            foreach (Character character in seen)
            {
                if (!character.IsAlive)
                {
                    continue;
                }

                living++;
                if (extractionKnown.Value && (character.transform.position - extraction).sqrMagnitude <= limit)
                {
                    livingAtExtraction++;
                }
            }

            channel.Step(elapsed, living, livingAtExtraction);
            if (Mathf.Abs(extractionProgress.Value - channel.Progress) > 0.01f || (channel.Progress == 0f && extractionProgress.Value != 0f))
            {
                extractionProgress.Value = channel.Progress;
            }

            DelvePhase now = DelveRules.Evaluate(anyPlayerSeen, living, extractionKnown.Value && channel.IsComplete);
            if (now != DelvePhase.Delving)
            {
                End(now);
            }
        }

        // Players are the characters a person steers; enemies, which the server owns, never have this component.
        private void ScanPlayers()
        {
            seen.Clear();
            foreach (NetworkObject spawned in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (spawned != null && spawned.TryGetComponent(out FirstPersonCharacterController _) && spawned.TryGetComponent(out Character character))
                {
                    seen.Add(character);
                    if (!tracked.ContainsKey(character))
                    {
                        Track(character);
                    }
                }
            }

            // A player who left (or whose character is gone) is no longer part of the delve.
            departed.Clear();
            foreach (Character character in tracked.Keys)
            {
                if (character == null || !seen.Contains(character))
                {
                    departed.Add(character);
                }
            }

            foreach (Character character in departed)
            {
                if (character != null)
                {
                    character.OnDeath -= tracked[character];
                }

                tracked.Remove(character);
            }
        }

        private void Track(Character character)
        {
            anyPlayerSeen = true;
            Character.NotifyDeath onDeath = () => HandleDeath(character);
            tracked[character] = onDeath;
            character.OnDeath += onDeath;

            // A character that is already dead when first seen still loses what it carries.
            if (!character.IsAlive)
            {
                HandleDeath(character);
            }
        }

        // The drop waits for the next poll rather than running inside the damage event that caused the death, which is still being resolved.
        private void HandleDeath(Character character)
        {
            if (dead.Add(character))
            {
                pendingDrops.Add(character);
            }
        }

        private void DropForTheDead()
        {
            foreach (Character character in pendingDrops)
            {
                if (character != null && character.IsSpawned)
                {
                    DropEverything(character);
                }
            }

            pendingDrops.Clear();
        }

        private void DropEverything(Character character)
        {
            character.TryGetComponent(out Inventory inventory);
            character.TryGetComponent(out Equipment equipment);
            if (!DeathDrop.HasAnything(inventory, equipment))
            {
                return;
            }

            if (lootBagPrefab == null)
            {
                if (!warnedNoBag)
                {
                    warnedNoBag = true;
                    Debug.LogError("DelveController has no loot bag prefab, so a dead player keeps what they carry.", this);
                }

                return;
            }

            NetworkObject bag = Instantiate(lootBagPrefab, character.transform.position + Vector3.up * 0.1f, Quaternion.identity);
            bag.Spawn(true);

            Inventory contents = bag.GetComponent<Inventory>();
            DeathDrop.Counts dropped = DeathDrop.MoveEquipment(equipment, contents) + DeathDrop.MoveInventory(inventory, contents);
            if (dropped.Left > 0)
            {
                Debug.LogError($"{character.name}'s loot bag was too small: {dropped.Left} item(s) stayed with them. Make the bag's inventory bigger.", bag);
            }
        }

        private void End(DelvePhase outcome)
        {
            var survivors = new List<ulong>();
            var died = new List<ulong>();
            foreach (Character character in tracked.Keys)
            {
                (character.IsAlive ? survivors : died).Add(character.OwnerClientId);
            }

            double seconds = ServerClock.Now - startedAt;
            survivorCount.Value = survivors.Count;
            deadCount.Value = died.Count;
            durationSeconds.Value = (float)seconds;
            extractionProgress.Value = channel.Progress;
            phase.Value = outcome;

            var result = new DelveResult(outcome, seconds, survivors, died);
            RewardSink?.Deposit(result);
            Ended?.Invoke(result);
        }

        // ----- Presentation -----

        private void HandleExtractionChanged(bool previous, bool current) => RefreshMarker();

        private void HandleExtractionChanged(Vector3 previous, Vector3 current) => RefreshMarker();

        // A flat disc on the floor where the party gets out. A stand-in for real art, made here so it needs no asset.
        private void RefreshMarker()
        {
            DestroyMarker();
            if (Application.isBatchMode || !extractionKnown.Value)
            {
                return;
            }

            marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "ExtractionMarker (placeholder)";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.position = extractionPosition.Value + Vector3.up * 0.03f;
            marker.transform.localScale = new Vector3(extractionRadius * 2f, 0.03f, extractionRadius * 2f);
            Renderer renderer = marker.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.material.color = new Color(0.2f, 0.9f, 0.5f);
        }

        private void DestroyMarker()
        {
            if (marker != null)
            {
                Destroy(marker);
                marker = null;
            }
        }

        private Character LocalCharacter()
        {
            NetworkObject playerObject = NetworkManager != null && NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;
            return playerObject != null && playerObject.TryGetComponent(out Player player) ? player.character : null;
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsClient)
            {
                return;
            }

            const float width = 360f;
            Rect area = new Rect((Screen.width - width) * 0.5f, 12f, width, 120f);
            GUILayout.BeginArea(area);

            if (phase.Value != DelvePhase.Delving)
            {
                string title = phase.Value == DelvePhase.Extracted ? "EXTRACTED" : "WIPED";
                GUILayout.Box($"{title}\n{survivorCount.Value} survived, {deadCount.Value} died, {durationSeconds.Value:0}s");
                if (IsServer && GUILayout.Button("Start next delve"))
                {
                    StartNextDelve();
                }
            }
            else if (extractionKnown.Value)
            {
                Character local = LocalCharacter();
                string where = local != null ? $"{Vector3.Distance(local.transform.position, extractionPosition.Value):0}m to extraction" : "extraction";
                if (extractionProgress.Value > 0f)
                {
                    GUILayout.Box($"Extracting... {extractionProgress.Value * 100f:0}% (the whole party must stay)");
                }
                else
                {
                    GUILayout.Box(where);
                }
            }

            GUILayout.EndArea();
        }
    }
}
