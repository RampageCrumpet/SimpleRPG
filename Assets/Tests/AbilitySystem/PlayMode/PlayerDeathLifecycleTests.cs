using SimpleRPG.AI;
using SimpleRPG.HitDetection;
using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Articulation;
using SimpleRPG.Animations;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using SimpleRPG.ObjectInteractions;
using SimpleRPG.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Follows a real player character (movement, interaction, abilities, melee, inventory) through death and respawn on a host: once the
    /// server commits the death the character does nothing, and a respawn returns it to a fully working life.
    /// </summary>
    public class PlayerDeathLifecycleTests
    {
        private const string MeleeAttack = "Assets/Abilities/Abilities/MeleeAttack.asset";
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";
        private const string MonsterPrefab = "Assets/Prefabs/Characters/Enemies/SkeletonFootman.prefab";
        private const string BloodMaterial = "Assets/Art/Material/UIBloodBar.mat";
        private const int StartingHealth = 100;
        private const int WeaponDamage = 10;
        // Melee Power multiplies a weapon's damage by 1.5 at the default attributes, so a weapon of 10 deals 15.
        private const int Blow = 15;

        private GameObject managerObject;
        private GameObject cameraObject;
        private GameObject floorObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> created = new List<Object>();
        private GameplayServices services;
        private List<DeathContext> deaths;
        private WeaponData weaponData;
        private AbilityDefinition melee;

        // The local player and menu state are static, so nothing may carry over from one test to the next.
        [TearDown]
        public void ClearLocalPlayerState() => LocalPlayer.Reset();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LocalPlayer.Reset();
            services = GameplayServices.Reset();
            AbilityServices.Reset();
            deaths = new List<DeathContext>();
            services.Events.Subscribe<EntityDiedEvent>(e => deaths.Add(e.Context));

            weaponData = ScriptableObject.CreateInstance<WeaponData>();
            weaponData.damage = new DamageInfo { Damage = WeaponDamage };
            ComboDefinition combo = ComboTestSupport.CreateCombo();
            ComboTestSupport.GiveCombo(weaponData, combo);
            created.Add(weaponData);
            created.Add(combo);

            melee = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(MeleeAttack);
            Assert.IsNotNull(melee, $"Could not load {MeleeAttack}.");

            // Something to stand on, so characters under gravity stay put. The character's origin is 0.63 above the soles of its feet.
            floorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObject.transform.position = new Vector3(0f, -1.13f, 0f);
            floorObject.transform.localScale = new Vector3(200f, 1f, 200f);

            // The first person controller attaches the main camera to its character.
            cameraObject = new GameObject("TestMainCamera", typeof(Camera)) { tag = "MainCamera" };

            managerObject = new GameObject("TestNetworkManager");
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };

            Assert.IsTrue(manager.StartHost(), "The test host should start.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                yield return new WaitUntil(() => !manager.IsListening);
            }

            foreach (GameObject instance in spawned)
            {
                if (instance != null) Object.Destroy(instance);
            }

            spawned.Clear();
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (floorObject != null) Object.Destroy(floorObject);
            if (managerObject != null) Object.Destroy(managerObject);
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return null;
        }

        /// <summary>
        /// Spawns a character on the host. The HUD registration and the animator adapter are removed; everything that makes the player a
        /// player (movement, interaction, abilities, inventory, melee) stays.
        /// </summary>
        private Character Spawn(string path, Vector3 position, bool player)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Could not load test prefab {path}.");

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            spawned.Add(instance);

            foreach (System.Type type in new[] { typeof(LocalPlayerControls), typeof(AnimatableCharacter) })
            {
                Component component = instance.GetComponent(type);
                if (component != null) Object.DestroyImmediate(component);
            }

            if (!player)
            {
                Component controller = instance.GetComponent<FirstPersonCharacterController>();
                if (controller != null) Object.DestroyImmediate(controller);
            }

            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        private Character SpawnPlayer(Vector3 position = default)
        {
            Character player = Spawn(PlayerPrefab, position, player: true);
            player.gameObject.tag = "Player";
            player.WeaponData = weaponData;
            ComboTestSupport.UseManualClock(player);
            return player;
        }

        private Character SpawnVictim() => Spawn(TargetPrefab, new Vector3(0f, 0f, 2f), player: false);

        private static void Kill(Character target) => Hit(target, StartingHealth);

        private static void Hit(Character target, int damage) => target.TakeDamage(new DamageInfo { Damage = damage }, BodyLocation.Torso);

        private static Weapon WeaponOf(Character character) => character.GetComponentInChildren<Weapon>();

        private bool Press(Character attacker) => attacker.GetComponent<AbilityUser>().TryActivateOnServer(melee);

        /// <summary>
        /// Sends the movement request the owning client sends every physics step. A host's request to itself is delivered on a later update,
        /// so the caller lets a few frames pass before asking <see cref="Travelled"/>.
        /// </summary>
        private static Vector3 SendMoveForward(Character player)
        {
            FirstPersonCharacterController controller = player.GetComponent<FirstPersonCharacterController>();
            MethodInfo move = typeof(FirstPersonCharacterController).GetMethod("MoveCharacterServerRPC", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector3 before = player.transform.position;
            move.Invoke(controller, new object[] { player.transform.forward });
            return before;
        }

        private static float Travelled(Character player, Vector3 before)
        {
            Vector3 delta = player.transform.position - before;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static float SendTurn(Character player, float yaw)
        {
            // The view change the owner sends; the server turns the body toward it over the next frames.
            UpperBodyArticulation articulation = player.GetComponent<UpperBodyArticulation>();
            MethodInfo look = typeof(UpperBodyArticulation).GetMethod("SubmitLookServerRpc", BindingFlags.Instance | BindingFlags.NonPublic);
            float before = player.transform.eulerAngles.y;
            look.Invoke(articulation, new object[] { yaw, 0f });
            return before;
        }

        private static float Turned(Character player, float before) => Mathf.Abs(Mathf.DeltaAngle(before, player.transform.eulerAngles.y));

        /// <summary>Asks to move forward and turn, lets the request arrive, and reports how far the character went and turned.</summary>
        private static IEnumerator TryToMoveAndTurn(Character player, System.Action<float, float> result)
        {
            Vector3 position = SendMoveForward(player);
            float angle = SendTurn(player, 30f);
            for (int i = 0; i < 3; i++) yield return null;
            result(Travelled(player, position), Turned(player, angle));
        }

        // ----- Dying -----

        [UnityTest]
        public IEnumerator LethalDamage_MakesThePlayerDead_AndRaisesDeathOnce()
        {
            Character player = SpawnPlayer();
            yield return null;
            int onDeath = 0;
            player.OnDeath += () => onDeath++;

            Kill(player);
            yield return null;

            Assert.IsFalse(player.IsAlive);
            Assert.AreEqual(0, player.Health);
            Assert.AreEqual(1, onDeath);
            Assert.AreEqual(1, deaths.Count);
        }

        [UnityTest]
        public IEnumerator RepeatedLethalDamage_DoesNotRepeatTheDeath()
        {
            Character player = SpawnPlayer();
            yield return null;
            int onDeath = 0;
            player.OnDeath += () => onDeath++;

            Kill(player);
            for (int i = 0; i < 4; i++)
            {
                Kill(player);
                yield return null;
            }

            Assert.AreEqual(1, onDeath, "The presentation event fires once.");
            Assert.AreEqual(1, deaths.Count, "The gameplay event fires once.");
            Assert.AreEqual(1, player.LifeId, "A corpse does not start another life by being hit.");
        }

        // ----- A dead player does nothing -----

        [UnityTest]
        public IEnumerator MovementAndTurning_WorkWhileAlive_AndDoNothingOnceDead()
        {
            Character player = SpawnPlayer();

            // Let the character settle on the floor and finish starting up before steering it.
            yield return new WaitForSeconds(0.5f);

            float moved = 0f;
            float turned = 0f;
            yield return TryToMoveAndTurn(player, (m, t) => { moved = m; turned = t; });
            Assert.Greater(moved, 0.01f, "Control: a living player moves.");
            Assert.Greater(turned, 1f, "Control: a living player turns.");

            Kill(player);
            yield return null;

            yield return TryToMoveAndTurn(player, (m, t) => { moved = m; turned = t; });
            Assert.Less(moved, 0.0001f, "The server ignores movement from a dead player.");
            Assert.Less(turned, 0.01f, "The server ignores turning from a dead player.");
        }

        [UnityTest]
        public IEnumerator DeadPlayer_CannotStartAnAttack_OrActivateAnyAbility()
        {
            Character player = SpawnPlayer();
            yield return null;
            IAbilitySlot meleeSlot = player.AbilitySlots.OfType<GraphAbilitySlot>().First(s => s.Definition == melee);
            int activated = 0;
            meleeSlot.Activated += _ => activated++;

            Kill(player);
            yield return null;

            Assert.IsFalse(Press(player), "The server refuses a melee press.");
            foreach (AbilityDefinition ability in player.GraphAbilities)
            {
                Assert.IsFalse(player.GetComponent<AbilityUser>().TryActivateOnServer(ability), ability.name + " must not run for a dead player.");
            }

            meleeSlot.Activate();
            Assert.AreEqual(0, activated, "The ability bar does not even show a use.");
            Assert.AreEqual(0, AbilityServices.Current.Executor.PendingContinuations);
        }

        [UnityTest]
        public IEnumerator AttackInProgress_CannotDealDamageAfterTheAttackerDies()
        {
            Character attacker = SpawnPlayer();
            Character victim = SpawnVictim();
            yield return null;

            Assert.IsTrue(Press(attacker));
            ComboTestSupport.Advance(attacker, ComboTestSupport.InsideHitWindow);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            Assert.AreEqual(StartingHealth - Blow, victim.Health, "Control: the open hit window deals damage.");

            Kill(attacker);
            yield return null;
            ComboTestSupport.Advance(attacker, 0.05);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Head);
            yield return null;

            Assert.AreEqual(StartingHealth - Blow, victim.Health, "The swing no longer hurts anyone.");
            Assert.AreEqual(0, AbilityServices.Current.Executor.PendingContinuations, "The ability the swing belonged to was released.");
        }

        [UnityTest]
        public IEnumerator DeadPlayer_CannotStartOrCompleteAnInteraction()
        {
            Character player = SpawnPlayer();
            Interactor interactor = player.GetComponent<Interactor>();
            RecordingInteractable quick = CreateInteractable(0.05f);
            RecordingInteractable slow = CreateInteractable(0.5f);

            // Let the character settle on the floor and finish starting up first.
            yield return new WaitForSeconds(0.5f);

            interactor.StartInteraction(quick);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1, quick.Activations, "Control: a living player completes an interaction.");
            interactor.StopInteraction();

            interactor.StartInteraction(slow);
            Assert.IsTrue(interactor.IsInteracting);
            Kill(player);
            yield return null;
            Assert.IsFalse(interactor.IsInteracting, "Dying ends the interaction in progress.");
            Assert.AreEqual(1, slow.Finishes);

            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(0, slow.Activations, "The interrupted interaction never completes.");

            interactor.StartInteraction(quick);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(interactor.IsInteracting);
            Assert.AreEqual(1, quick.Activations, "A dead player cannot start a new one.");
            Assert.IsNull(interactor.FindInteractionTarget());
        }

        [UnityTest]
        public IEnumerator DeadPlayer_CannotHandleItems()
        {
            Character player = SpawnPlayer();
            yield return null;
            Inventory inventory = player.GetComponent<Inventory>();
            MethodInfo canAccess = typeof(Inventory).GetMethod("CanBeAccessedBy", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsTrue((bool)canAccess.Invoke(inventory, new object[] { player.OwnerClientId }), "Control: the owner of a living character may use its inventory.");

            Kill(player);
            yield return null;

            Assert.IsFalse((bool)canAccess.Invoke(inventory, new object[] { player.OwnerClientId }), "The server refuses item moves from a dead player.");
        }

        [UnityTest]
        public IEnumerator DeadPlayer_IsNotATargetForEnemies()
        {
            Character player = SpawnPlayer(new Vector3(0f, 0f, 1f));
            yield return null;
            Character monster = SpawnMonster(Vector3.zero);
            AIController ai = monster.GetComponent<AIController>();
            yield return null;

            Assert.IsTrue(ai.SenseTargets().Contains(player), "Control: a living player in range is sensed.");
            Assert.IsTrue(ai.TrackTarget());
            Assert.AreSame(player, ai.Target);

            Kill(player);
            yield return null;

            Assert.IsFalse(ai.SenseTargets().Contains(player));
            Assert.IsFalse(ai.TrackTarget(), "The enemy has nothing left to track.");
            Assert.IsNull(ai.Target);
        }

        // ----- Coming back -----

        [UnityTest]
        public IEnumerator Respawn_RestoresALivingPlayer_AtTheSpawnPointWithFullHealth()
        {
            Character player = SpawnPlayer(new Vector3(3f, 0f, 4f));
            yield return null;
            Vector3 spawnPoint = player.transform.position;
            int onRevive = 0;
            player.OnRevive += () => onRevive++;

            Kill(player);
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position = spawnPoint + new Vector3(20f, 0f, 0f);
            player.GetComponent<CharacterController>().enabled = true;
            yield return null;
            Assert.IsTrue(player.RespawnOnServer());
            yield return null;

            Assert.IsTrue(player.IsAlive);
            Assert.AreEqual(StartingHealth, player.Health);
            Assert.AreEqual(2, player.LifeId);
            Assert.AreEqual(1, onRevive);
            Assert.Less(Vector3.Distance(player.transform.position, spawnPoint), 0.5f, "The player is back where it started.");

            // The respawn point sits a few centimetres above the floor, and a controller that is still falling takes no horizontal step,
            // so let the character land before steering it, as the alive-movement test does.
            yield return new WaitForSeconds(0.5f);

            float moved = 0f;
            float turned = 0f;
            yield return TryToMoveAndTurn(player, (m, t) => { moved = m; turned = t; });
            Assert.Greater(moved, 0.01f, "Movement works again.");
            Assert.Greater(turned, 1f, "Turning works again.");
        }

        [UnityTest]
        public IEnumerator Respawn_DoesNothingForALivingPlayer()
        {
            Character player = SpawnPlayer();
            yield return null;
            Hit(player, 40);

            Assert.IsFalse(player.RespawnOnServer());
            player.RequestRespawn();
            yield return null;

            Assert.AreEqual(StartingHealth - 40, player.Health, "A respawn is not a heal.");
            Assert.AreEqual(1, player.LifeId);
        }

        [UnityTest]
        public IEnumerator AfterRespawn_AbilitiesAttacksAndInteractionsWorkAgain()
        {
            Character player = SpawnPlayer();
            Character victim = SpawnVictim();
            RecordingInteractable pickup = CreateInteractable(0.05f);
            yield return null;

            // Die in the middle of a swing with an interaction under way.
            Assert.IsTrue(Press(player));
            ComboTestSupport.Advance(player, ComboTestSupport.InsideHitWindow);
            player.GetComponent<Interactor>().StartInteraction(pickup);
            Kill(player);
            yield return null;
            Assert.IsTrue(player.RespawnOnServer());
            yield return new WaitForSeconds(1.2f);

            Assert.IsTrue(Press(player), "A new swing starts.");
            ComboTestSupport.Advance(player, ComboTestSupport.InsideHitWindow);
            WeaponOf(player).TargetHit(victim, BodyLocation.Torso);
            Assert.AreEqual(StartingHealth - Blow, victim.Health, "It hurts, so no stale state survived the death.");

            player.GetComponent<Interactor>().StartInteraction(pickup);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, pickup.Activations, "Interaction works again.");
        }

        [UnityTest]
        public IEnumerator RepeatedDeathAndRespawn_StaysConsistent()
        {
            Character player = SpawnPlayer();
            yield return null;
            int onDeath = 0;
            int onRevive = 0;
            player.OnDeath += () => onDeath++;
            player.OnRevive += () => onRevive++;

            for (int life = 1; life <= 4; life++)
            {
                Assert.IsTrue(player.IsAlive, "Life " + life + " begins alive.");
                Assert.AreEqual(life, player.LifeId);
                Assert.AreEqual(StartingHealth, player.Health);

                Kill(player);
                Kill(player);
                yield return null;
                Assert.IsFalse(player.IsAlive);
                Assert.AreEqual(life, onDeath);

                Assert.IsTrue(player.RespawnOnServer());
                yield return null;
                Assert.AreEqual(life, onRevive);
            }

            Assert.AreEqual(4, deaths.Count, "Each life ended exactly once.");
            Assert.AreEqual(4, deaths.Select(d => d.Transition).Distinct().Count());
            Assert.AreEqual(1, services.Combatants.Count, "The player is registered once, not once per life.");
        }

        // ----- The health bar -----

        [UnityTest]
        public IEnumerator HealthBar_EmptiesOnDeath_AndRefillsOnRespawn()
        {
            Character player = SpawnPlayer();
            yield return null;
            HealthBarUI bar = CreateHealthBar(out Material blood);
            bar.SetCharacter(player);
            Assert.AreEqual(1f, blood.GetFloat("_Fill"), 0.01f);

            Kill(player);
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(0f, blood.GetFloat("_Fill"), 0.01f, "The bar shows no health once dead.");

            Assert.IsTrue(player.RespawnOnServer());
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(1f, blood.GetFloat("_Fill"), 0.01f, "The bar shows the restored health.");
        }

        // ----- The death screen -----

        [UnityTest]
        public IEnumerator DeathScreen_ShowsOnDeath_AsksForRespawn_AndHidesOnceRevived()
        {
            Character player = SpawnPlayer();
            yield return null;
            DeathScreenUI screen = CreateDeathScreen(out PlayerUIController playerUI);
            screen.SetCharacter(player, playerUI);
            Assert.IsFalse(screen.IsShowing);

            Kill(player);
            yield return null;
            Assert.IsTrue(screen.IsShowing, "The player is told they died.");
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "The cursor is free for the button.");
            Assert.IsFalse(player.GetComponent<FirstPersonCharacterController>().enabled, "The camera is held still.");

            screen.Respawn();
            yield return null;
            Assert.IsTrue(player.IsAlive, "The host asked and the server revived the character.");
            Assert.IsFalse(screen.IsShowing);
            Assert.IsTrue(player.GetComponent<FirstPersonCharacterController>().enabled, "Control returns.");
            Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);

            // Another life repeats the same without leaving anything behind.
            Kill(player);
            yield return null;
            Assert.IsTrue(screen.IsShowing);
            screen.SetCharacter(null, playerUI);
            Assert.IsFalse(screen.IsShowing, "Detaching hides the screen and releases the controls.");
            Assert.IsTrue(player.GetComponent<FirstPersonCharacterController>().enabled);
        }

        [UnityTest]
        public IEnumerator DeathScreen_BoundToAnAlreadyDeadCharacter_ShowsAtOnce()
        {
            Character player = SpawnPlayer();
            yield return null;
            Kill(player);
            yield return null;

            // A late joiner binds the screen to a character that is already dead.
            DeathScreenUI screen = CreateDeathScreen(out PlayerUIController playerUI);
            screen.SetCharacter(player, playerUI);

            Assert.IsTrue(screen.IsShowing);
            screen.Respawn();
            yield return null;
            Assert.IsFalse(screen.IsShowing);
        }

        // ----- Helpers -----

        private sealed class RecordingInteractable : Interactable
        {
            public int Activations;
            public int Finishes;

            public void Configure(float seconds)
            {
                InteractionTime = seconds;
                InteractionRange = 100f;
            }

            public override void ActivateInteraction(Interactor interactor) => Activations++;

            public override void FinishInteraction(Interactor interactor) => Finishes++;
        }

        private RecordingInteractable CreateInteractable(float seconds)
        {
            GameObject instance = new GameObject("TestInteractable", typeof(NetworkObject), typeof(BoxCollider));
            spawned.Add(instance);
            RecordingInteractable interactable = instance.AddComponent<RecordingInteractable>();
            interactable.Configure(seconds);
            return interactable;
        }

        private Character SpawnMonster(Vector3 position)
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefab), position, Quaternion.identity);
            spawned.Add(instance);

            // Chasing needs a navigation mesh; this controller only senses and tracks.
            Object.DestroyImmediate(instance.GetComponent<AIController>());
            NavMeshAgentRemover.Remove(instance);
            AttackOnlyAIController controller = instance.AddComponent<AttackOnlyAIController>();
            controller.AttackRange = 0.1f;

            foreach (Blindsense sense in instance.GetComponents<Blindsense>())
            {
                sense.Range = 10f;
                sense.sensableLayers = ~0;
            }

            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        private static class NavMeshAgentRemover
        {
            public static void Remove(GameObject instance)
            {
                // The skeleton's grounding component requires the agent, so it goes first.
                NavigationGrounding grounding = instance.GetComponent<NavigationGrounding>();
                if (grounding != null) Object.DestroyImmediate(grounding);
                UnityEngine.AI.NavMeshAgent agent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) Object.DestroyImmediate(agent);
            }
        }

        private HealthBarUI CreateHealthBar(out Material blood)
        {
            // The bar copies its image's material in Awake, so wire the image up before the component wakes.
            GameObject root = new GameObject("TestHealthBar");
            root.SetActive(false);
            spawned.Add(root);
            Image image = root.AddComponent<Image>();
            image.material = AssetDatabase.LoadAssetAtPath<Material>(BloodMaterial);
            HealthBarUI bar = root.AddComponent<HealthBarUI>();
            typeof(HealthBarUI).GetField("bloodImage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bar, image);
            root.SetActive(true);
            blood = image.material;
            return bar;
        }

        private DeathScreenUI CreateDeathScreen(out PlayerUIController playerUI)
        {
            GameObject root = new GameObject("TestDeathScreen");
            spawned.Add(root);
            playerUI = root.AddComponent<PlayerUIController>();

            GameObject screenObject = new GameObject("Screen");
            screenObject.transform.SetParent(root.transform);
            screenObject.SetActive(false);
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(screenObject.transform);
            DeathScreenUI screen = screenObject.AddComponent<DeathScreenUI>();
            typeof(DeathScreenUI).GetField("panel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(screen, panel);
            screenObject.SetActive(true);

            // While a menu holds the player's attention the first person controller switches itself off (it listens to LocalPlayer).
            Character player = Object.FindObjectsByType<Character>().First(c => c.GetComponent<FirstPersonCharacterController>() != null);
            playerUI.SetCharacter(player);
            return screen;
        }
    }
}
