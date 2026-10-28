using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Animations;
using SimpleRPG.Gameplay;
using SimpleRPG.ObjectInteractions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// The player's controls play the game with no UI anywhere in the scene: ability keys activate abilities, the interact key starts and
    /// stops interactions, a dead player can ask for the next life, and an open menu holds the gameplay keys back.
    /// </summary>
    public class LocalPlayerControlsTests
    {
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";

        private sealed class StubInteractable : Interactable
        {
            private void Awake() => InteractionRange = 5f;

            public override void ActivateInteraction(Interactor interactor) { }

            public override void FinishInteraction(Interactor interactor) { }
        }

        private GameObject managerObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private Character player;
        private LocalPlayerControls controls;
        private Interactor interactor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Unity runs UnitySetUp before any [SetUp], so the static state is cleared here, before the player spawns and announces itself.
            LocalPlayer.Reset();
            GameplayServices.Reset();
            AbilityServices.Reset();
            InteractableManager.Clear();

            managerObject = new GameObject("TestNetworkManager");
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
            Assert.IsTrue(manager.StartHost(), "The test host should start.");
            yield return null;

            // A camera looking along +z from the origin, with the player standing behind it, so what the player "looks at" is controlled by the test.
            GameObject camera = new GameObject("TestMainCamera", typeof(Camera)) { tag = "MainCamera" };
            spawned.Add(camera);

            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), new Vector3(0f, 0f, -1f), Quaternion.identity);
            spawned.Add(instance);

            // The first person controller needs a full camera rig and the animator needs a rig; neither is under test.
            foreach (System.Type type in new[] { typeof(FirstPersonCharacterController), typeof(AnimatableCharacter) })
            {
                Component component = instance.GetComponent(type);
                if (component != null) Object.DestroyImmediate(component);
            }

            player = instance.GetComponent<Character>();
            player.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            controls = instance.GetComponent<LocalPlayerControls>();
            interactor = instance.GetComponent<Interactor>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject instance in spawned)
            {
                if (instance != null) Object.Destroy(instance);
            }

            spawned.Clear();

            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                yield return new WaitUntil(() => !manager.IsListening);
            }

            if (managerObject != null) Object.Destroy(managerObject);
            GameplayServices.Reset();
            AbilityServices.Reset();
            InteractableManager.Clear();
            LocalPlayer.Reset();
            yield return null;
        }

        private static ControlInput Press(int slot) => new ControlInput(1u << slot, 1u << slot, false);

        private static ControlInput Hold(int slot) => new ControlInput(0, 1u << slot, false);

        private static readonly ControlInput InteractKey = new ControlInput(0, 0, true);

        private int SlotWhere(System.Func<GraphAbilitySlot, bool> predicate)
        {
            for (int i = 0; i < player.AbilitySlots.Count; i++)
            {
                if (player.AbilitySlots[i] is GraphAbilitySlot slot && predicate(slot)) return i;
            }

            return -1;
        }

        private int CountActivations(int slot, System.Action act)
        {
            int count = 0;
            System.Action<IAbilitySlot> handler = _ => count++;
            player.AbilitySlots[slot].Activated += handler;
            act();
            player.AbilitySlots[slot].Activated -= handler;
            return count;
        }

        private StubInteractable PlaceTargetInFrontOfTheCamera()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawned.Add(cube);
            cube.transform.position = new Vector3(0f, 0f, 3f);
            StubInteractable target = cube.AddComponent<StubInteractable>();
            Physics.SyncTransforms();
            return target;
        }

        [UnityTest]
        public IEnumerator TheOwningCharacter_IsAnnouncedAsTheLocalPlayer_AndWithdrawnWhenItGoes()
        {
            Assert.AreSame(player, LocalPlayer.Current, "the owner announces itself when it spawns");

            player.GetComponent<NetworkObject>().Despawn(false);
            yield return null;

            Assert.IsNull(LocalPlayer.Current, "a despawned character is no longer the local player");
        }

        [UnityTest]
        public IEnumerator AbilityKeys_ActivateTheirAbilities_WithNoUiAnywhere()
        {
            yield return null;
            Assert.IsEmpty(Object.FindObjectsByType<UI.PlayerUIController>(FindObjectsInactive.Include), "there is no UI in this scene");
            int slot = SlotWhere(s => !s.Definition.HasNode(NodeTypes.MeleeSwing));
            Assume.That(slot, Is.GreaterThanOrEqualTo(0), "the test character has a non-melee ability");

            int activations = CountActivations(slot, () => controls.Apply(Press(slot)));

            Assert.AreEqual(1, activations);
        }

        [UnityTest]
        public IEnumerator AMeleeAbility_NeedsAFreshPress_ButOtherAbilitiesRepeatWhileHeld()
        {
            yield return null;
            int melee = SlotWhere(s => s.Definition.HasNode(NodeTypes.MeleeSwing));
            int other = SlotWhere(s => !s.Definition.HasNode(NodeTypes.MeleeSwing));
            Assume.That(melee, Is.GreaterThanOrEqualTo(0), "the test character has a melee ability");
            Assume.That(other, Is.GreaterThanOrEqualTo(0), "the test character has a non-melee ability");

            Assert.AreEqual(0, CountActivations(melee, () => controls.Apply(Hold(melee))), "holding a melee key without pressing it does nothing");
            Assert.AreEqual(1, CountActivations(other, () => controls.Apply(Hold(other))), "a held key activates an ordinary ability");
        }

        [UnityTest]
        public IEnumerator AMenuHoldsTheGameplayKeysBack_AndTheyResumeWhenItCloses()
        {
            yield return null;
            int slot = SlotWhere(s => !s.Definition.HasNode(NodeTypes.MeleeSwing));
            Assume.That(slot, Is.GreaterThanOrEqualTo(0));

            LocalPlayer.SetMenuMode(true);
            Assert.AreEqual(0, CountActivations(slot, () => controls.Apply(Press(slot))), "no ability while a menu has the player's attention");

            LocalPlayer.SetMenuMode(false);
            Assert.AreEqual(1, CountActivations(slot, () => controls.Apply(Press(slot))), "abilities work again once the menu closes");
        }

        [UnityTest]
        public IEnumerator TheInteractKey_StartsAndStopsAnInteraction_WithNoUiAnywhere()
        {
            StubInteractable target = PlaceTargetInFrontOfTheCamera();
            yield return null;
            Physics.SyncTransforms();
            interactor.RefreshTarget();
            Assert.AreSame(target, interactor.Target, "the player is looking at the target");

            controls.Apply(InteractKey);
            Assert.IsTrue(interactor.IsInteracting);
            Assert.AreSame(target, interactor.Interactable);

            controls.Apply(InteractKey);
            Assert.IsFalse(interactor.IsInteracting, "pressing again stops it");
        }

        [UnityTest]
        public IEnumerator TheInteractKey_DoesNothingWhileAMenuIsOpen()
        {
            PlaceTargetInFrontOfTheCamera();
            yield return null;
            Physics.SyncTransforms();
            interactor.RefreshTarget();

            LocalPlayer.SetMenuMode(true);
            controls.Apply(InteractKey);

            Assert.IsFalse(interactor.IsInteracting);
        }

        [UnityTest]
        public IEnumerator ADeadPlayer_CanAskForTheNextLife_EvenWithTheDeathScreenUp()
        {
            yield return null;
            player.TakeDamage(new DamageInfo { Damage = 100000 }, HitDetection.BodyLocation.Torso);
            yield return null;
            Assert.IsFalse(player.IsAlive);

            // The death screen is a menu, and a menu must not stop the dead from asking.
            LocalPlayer.SetMenuMode(true);
            controls.Apply(InteractKey);
            yield return null;

            Assert.IsTrue(player.IsAlive, "the interact key asked for the next life and the server granted it");
        }
    }
}
