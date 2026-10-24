using AI;
using HitDetection;
using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.AI;
using SimpleRPG.Animations;
using SimpleRPG.Articulation;
using SimpleRPG.Gameplay;
using SimpleRPG.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// A real weapon strikes real player and skeleton bodies on a real host: the player's longsword, its combo and melee delivery, the real hurtboxes
    /// on the real bones, and the real combat pipeline. The wielder's animator is frozen and its blade is placed by the test, so which region is
    /// struck is decided by geometry alone; the damage that follows is the product of the whole chain. Each blade is placed inside a region, which
    /// is where the rule for overlapping regions (see <see cref="HitCandidate"/>) looks.
    /// </summary>
    public class AnatomicalHitNetworkTests
    {
        private const string MeleeAttack = "Assets/Abilitities/Abillities/MeleeAttack.asset";
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string SkeletonPrefab = "Assets/Prefabs/Characters/Enemies/SkeletonFootman.prefab";
        private const int WeaponDamage = 20;

        // The longsword's 20 is multiplied by the wielder's Melee Power (x1.5 at the default attributes), so a torso blow is 30; head x1.75 = 53, limbs x0.75 = 23 (rounded).

        private GameObject managerObject;
        private GameObject cameraObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> created = new List<Object>();
        private GameplayServices services;
        private WeaponData weaponData;
        private AbilityDefinition melee;
        private readonly List<WeaponHitConfirmedEvent> hits = new List<WeaponHitConfirmedEvent>();
        private readonly List<DamageResolvedEvent> damage = new List<DamageResolvedEvent>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            services = GameplayServices.Reset();
            AbilityServices.Reset();
            hits.Clear();
            damage.Clear();
            services.Events.Subscribe<WeaponHitConfirmedEvent>(hits.Add);
            services.Events.Subscribe<DamageResolvedEvent>(damage.Add);

            weaponData = ScriptableObject.CreateInstance<WeaponData>();
            weaponData.damage = new DamageInfo { Damage = WeaponDamage };
            created.Add(weaponData);
            ComboDefinition combo = ComboTestSupport.CreateCombo();
            created.Add(combo);
            ComboTestSupport.GiveCombo(weaponData, combo);

            melee = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(MeleeAttack);
            Assert.IsNotNull(melee);

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

            if (cameraObject != null) Object.Destroy(cameraObject);
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

        // ----- Bodies -----

        private Character Spawn(string path, Vector3 position, float yaw = 0f)
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.Euler(0f, yaw, 0f));
            spawned.Add(instance);

            foreach (System.Type type in new[]
            {
                typeof(RegisterWithUI), typeof(FirstPersonCharacterController), typeof(ThirdPersonCharacterController), typeof(AnimatableCharacter),
                typeof(NavigationGrounding), typeof(AIController), typeof(NavMeshAgent), typeof(Blindsense),
            })
            {
                foreach (Component component in instance.GetComponents(type))
                {
                    Object.DestroyImmediate(component);
                }
            }

            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        /// <summary>A player whose animator and spine are held still, so its pose is what the test left it in.</summary>
        private Character SpawnFrozenPlayer(Vector3 position, float yaw = 0f)
        {
            Character player = Spawn(PlayerPrefab, position, yaw);
            player.WeaponData = weaponData;
            ComboTestSupport.UseManualClock(player);
            return player;
        }

        private static void Freeze(Character character)
        {
            foreach (Animator animator in character.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            UpperBodyArticulation articulation = character.GetComponent<UpperBodyArticulation>();
            if (articulation != null) articulation.enabled = false;
        }

        private static HurtBox Region(Character body, string bone)
        {
            HurtBox box = body.GetComponentsInChildren<HurtBox>(true).FirstOrDefault(h => h.name == bone);
            Assert.IsNotNull(box, $"{body.name} has no hurtbox on '{bone}'.");
            return box;
        }

        private static Vector3 CentreOf(HurtBox box)
        {
            Physics.SyncTransforms();
            return box.Shapes[0].bounds.center;
        }

        // ----- Blades and swings -----

        private static Hitbox BladeOf(Character wielder) => wielder.GetComponentInChildren<Hitbox>(true);

        /// <summary>Puts the blade's collider centre at a world point with the blade's length along the given world direction.</summary>
        private static void PlaceBlade(Character wielder, Vector3 centre, Vector3 lengthAxis)
        {
            Hitbox blade = BladeOf(wielder);
            BoxCollider shape = blade.GetComponent<BoxCollider>();
            Transform t = blade.transform;
            t.rotation = Quaternion.FromToRotation(Vector3.up, lengthAxis.normalized);
            t.position = centre - t.rotation * Vector3.Scale(shape.center, t.lossyScale);
            Physics.SyncTransforms();
        }

        private bool Press(Character attacker) => attacker.GetComponent<AbilityUser>().TryActivateOnServer(melee);

        /// <summary>Opens the attacker's hit window with the blade where it is. Nothing is hit until the next physics step.</summary>
        private void OpenWindow(Character attacker)
        {
            Assert.IsTrue(Press(attacker), "the swing starts");
            ComboTestSupport.Advance(attacker, ComboTestSupport.InsideHitWindow);
            Assert.IsTrue(BladeOf(attacker).enabled, "the hit window is open");
        }

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        private int Dealt(Character victim, int before) => before - victim.Health;

        // ----- What each region is worth -----

        private IEnumerator StrikeSkeleton(string bone, BodyLocation location, int expected)
        {
            Character attacker = SpawnFrozenPlayer(Vector3.zero);
            Character skeleton = Spawn(SkeletonPrefab, new Vector3(0f, 0f, 4f), 180f);
            yield return null;
            Freeze(attacker);
            Freeze(skeleton);
            Assume.That(skeleton.MaxHealth, Is.GreaterThan(60));
            int before = skeleton.Health;

            PlaceBlade(attacker, CentreOf(Region(skeleton, bone)), Vector3.right);
            OpenWindow(attacker);
            yield return Steps(2);

            Assert.AreEqual(1, hits.Count, "one blow");
            Assert.AreEqual((int)location, hits[0].BodyLocation, "the body location that was struck");
            Assert.AreEqual(skeleton.Id, hits[0].Victim);
            Assert.AreEqual(attacker.Id, hits[0].Attacker);
            Assert.AreEqual(expected, Dealt(skeleton, before));
        }

        private IEnumerator StrikePlayer(string bone, BodyLocation location, int expected)
        {
            Character attacker = SpawnFrozenPlayer(Vector3.zero);
            Character victim = Spawn(PlayerPrefab, new Vector3(0f, 0f, 4f), 180f);
            yield return null;
            Freeze(attacker);
            Freeze(victim);
            int before = victim.Health;

            PlaceBlade(attacker, CentreOf(Region(victim, bone)), Vector3.right);
            OpenWindow(attacker);
            yield return Steps(2);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual((int)location, hits[0].BodyLocation);
            Assert.AreEqual(expected, Dealt(victim, before));
        }

        [UnityTest] public IEnumerator Skeleton_Skull_IsAHeadshot() => StrikeSkeleton("Head", BodyLocation.Head, 53);
        [UnityTest] public IEnumerator Skeleton_Chest_IsATorsoBlow() => StrikeSkeleton("Chest", BodyLocation.Torso, 30);
        [UnityTest] public IEnumerator Skeleton_Pelvis_IsATorsoBlow() => StrikeSkeleton("Hips", BodyLocation.Torso, 30);
        [UnityTest] public IEnumerator Skeleton_UpperArm_IsAnArmBlow() => StrikeSkeleton("LeftUpperArm", BodyLocation.Arms, 23);
        [UnityTest] public IEnumerator Skeleton_Forearm_IsAnArmBlow() => StrikeSkeleton("RightLowerArm", BodyLocation.Arms, 23);
        [UnityTest] public IEnumerator Skeleton_Shin_IsALegBlow() => StrikeSkeleton("LeftLowerLeg", BodyLocation.Legs, 23);
        [UnityTest] public IEnumerator Skeleton_Foot_IsALegBlow() => StrikeSkeleton("RightFoot", BodyLocation.Legs, 23);

        [UnityTest] public IEnumerator Player_Head_IsAHeadshot() => StrikePlayer("head.x", BodyLocation.Head, 53);
        [UnityTest] public IEnumerator Player_Chest_IsATorsoBlow() => StrikePlayer("spine_03.x", BodyLocation.Torso, 30);
        [UnityTest] public IEnumerator Player_UpperArm_IsAnArmBlow() => StrikePlayer("arm_twist.l", BodyLocation.Arms, 23);
        [UnityTest] public IEnumerator Player_Forearm_IsAnArmBlow() => StrikePlayer("forearm_twist.r", BodyLocation.Arms, 23);
        [UnityTest] public IEnumerator Player_Thigh_IsALegBlow() => StrikePlayer("thigh_twist.l", BodyLocation.Legs, 23);
        [UnityTest] public IEnumerator Player_Shin_IsALegBlow() => StrikePlayer("leg_twist.r", BodyLocation.Legs, 23);

        [UnityTest]
        public IEnumerator ADamageEvent_CarriesTheAttackerTheLocationAndTheFinalAmount()
        {
            Character attacker = SpawnFrozenPlayer(Vector3.zero);
            Character skeleton = Spawn(SkeletonPrefab, new Vector3(0f, 0f, 4f), 180f);
            yield return null;
            Freeze(attacker);
            Freeze(skeleton);

            PlaceBlade(attacker, CentreOf(Region(skeleton, "Head")), Vector3.right);
            OpenWindow(attacker);
            yield return Steps(2);

            DamageResolvedEvent resolved = damage.Single(d => d.Request.Target == skeleton.Id);
            Assert.AreEqual(attacker.Id, resolved.Request.Instigator);
            Assert.AreEqual((int)BodyLocation.Head, resolved.Request.BodyLocation);
            Assert.AreEqual(DamageProvenance.DirectWeaponHit, resolved.Request.Provenance);
            Assert.AreEqual(53, resolved.Request.Amount, "the event carries the amount that was dealt, adjusted once");
            Assert.AreEqual(resolved.HealthBefore - 53, resolved.HealthAfter);
        }

        // ----- One blow per swing -----

        [UnityTest]
        public IEnumerator ABladeThatCrossesManyRegions_DealsOneBlow_FromTheFirstRegionItTouches()
        {
            Character attacker = SpawnFrozenPlayer(Vector3.zero);
            Character skeleton = Spawn(SkeletonPrefab, new Vector3(0f, 0f, 4f), 180f);
            yield return null;
            Freeze(attacker);
            Freeze(skeleton);
            int before = skeleton.Health;

            Vector3 head = CentreOf(Region(skeleton, "Head"));
            Vector3 chest = CentreOf(Region(skeleton, "Chest"));
            Vector3 foot = CentreOf(Region(skeleton, "LeftFoot"));
            PlaceBlade(attacker, head + Vector3.up * 3f, Vector3.right);
            OpenWindow(attacker);
            yield return Steps(2);
            Assert.AreEqual(0, hits.Count, "the blade is still above the skeleton");

            // A downward chop through the head, then the chest, then the foot, each pose a physics step apart.
            foreach (Vector3 where in new[] { head, chest, foot, chest, head })
            {
                PlaceBlade(attacker, where, Vector3.right);
                yield return Steps(1);
            }

            yield return Steps(2);
            Assert.AreEqual(1, hits.Count, "one target, one blow per swing");
            Assert.AreEqual((int)BodyLocation.Head, hits[0].BodyLocation, "the first region the blade reached, not the one that pays most or least");
            Assert.AreEqual(53, Dealt(skeleton, before));
        }

        [UnityTest]
        public IEnumerator ABladeInsideTwoOverlappingRegions_ChoosesTheOneItIsMostInside()
        {
            Character attacker = SpawnFrozenPlayer(Vector3.zero);
            Character skeleton = Spawn(SkeletonPrefab, new Vector3(0f, 0f, 4f), 180f);
            yield return null;
            Freeze(attacker);
            Freeze(skeleton);

            // The skull box overlaps the top of the upper-chest box. Put the blade's centre where only the skull contains it, then where only the chest does.
            HurtBox skull = Region(skeleton, "Head");
            HurtBox chest = Region(skeleton, "UpperChest");
            Vector3 high = CentreOf(skull) + Vector3.up * 0.03f;
            Vector3 low = CentreOf(chest) - Vector3.up * 0.03f;
            Assert.IsTrue(skull.Shapes[0].bounds.Contains(high) && !chest.Shapes[0].bounds.Contains(high), "test set-up: the high point is only in the skull");
            Assert.IsTrue(chest.Shapes[0].bounds.Contains(low) && !skull.Shapes[0].bounds.Contains(low), "test set-up: the low point is only in the chest");

            PlaceBlade(attacker, high, Vector3.right);
            OpenWindow(attacker);
            yield return Steps(2);
            Assert.AreEqual((int)BodyLocation.Head, hits.Single().BodyLocation);
        }

        // ----- Death -----

        [UnityTest]
        public IEnumerator ADeadSkeleton_HasNoRegionLeftToStrike_AndTakesNoFurtherDamage()
        {
            Character attacker = SpawnFrozenPlayer(Vector3.zero);
            Character skeleton = Spawn(SkeletonPrefab, new Vector3(0f, 0f, 4f), 180f);
            yield return null;
            Freeze(attacker);
            Freeze(skeleton);
            HurtBox skull = Region(skeleton, "Head");
            Vector3 head = CentreOf(skull);

            skeleton.TakeDamage(new DamageInfo { Damage = skeleton.MaxHealth * 2 }, BodyLocation.Torso);
            yield return null;
            Assert.IsFalse(skeleton.IsAlive);
            hits.Clear();
            damage.Clear();

            foreach (HurtBox box in skeleton.GetComponentsInChildren<HurtBox>(true))
            {
                Assert.IsTrue(box.Shapes.All(s => !s.enabled), $"{box.name} is still strikable");
            }

            Physics.SyncTransforms();
            Assert.AreEqual(0, Physics.OverlapSphere(head, 0.5f, LayerMask.GetMask("HurtBox"), QueryTriggerInteraction.Collide).Count(c => c.GetComponentInParent<Character>() == skeleton),
                "nothing of the corpse answers a physics query");

            PlaceBlade(attacker, head, Vector3.right);
            OpenWindow(attacker);
            yield return Steps(3);

            Assert.AreEqual(0, hits.Count, "a corpse is not hit");
            Assert.AreEqual(0, damage.Count, "and takes no damage");
            Assert.AreEqual(0, skeleton.Health);
        }

        [UnityTest]
        public IEnumerator ARevivedPlayer_CanBeStruckAgain()
        {
            Character attacker = SpawnFrozenPlayer(Vector3.zero);
            Character victim = Spawn(PlayerPrefab, new Vector3(0f, 0f, 4f), 180f);
            yield return null;
            Freeze(attacker);
            Freeze(victim);

            victim.TakeDamage(new DamageInfo { Damage = victim.MaxHealth * 2 }, BodyLocation.Torso);
            yield return null;
            Assert.IsTrue(victim.GetComponentsInChildren<HurtBox>(true).All(h => h.Shapes.All(s => !s.enabled)), "dead: no region is strikable");

            Assert.IsTrue(services.Combat.Revive(victim.Id, victim.MaxHealth));
            yield return null;
            Assert.IsTrue(victim.GetComponentsInChildren<HurtBox>(true).All(h => h.Shapes.All(s => s.enabled)), "revived: every region is back");

            int before = victim.Health;
            PlaceBlade(attacker, CentreOf(Region(victim, "head.x")), Vector3.right);
            OpenWindow(attacker);
            yield return Steps(2);
            Assert.AreEqual(1, hits.Count(h => h.Victim == victim.Id), "one blow: " + string.Join(", ", damage.Select(d => d.Request.Amount + "@" + d.Request.BodyLocation)));
            Assert.AreEqual(53, Dealt(victim, before));
        }

        // ----- Poses -----

        [UnityTest]
        public IEnumerator TheSkeletonsRegions_FollowTheAnimatedBones()
        {
            Character skeleton = Spawn(SkeletonPrefab, Vector3.zero);
            Animator animator = skeleton.GetComponentInChildren<Animator>();
            AnimatorControllerClips(animator, out AnimationClip clip);
            Assume.That(clip, Is.Not.Null, "the skeleton has an animation to play");
            yield return null;

            // Hurtboxes are children of the bones, so wherever the animation takes a bone its region goes with it.
            HurtBox[] boxes = skeleton.GetComponentsInChildren<HurtBox>(true);
            Dictionary<HurtBox, Vector3> offsets = boxes.ToDictionary(b => b, b => b.transform.InverseTransformPoint(b.Shapes[0].bounds.center));

            for (int frame = 0; frame < 30; frame++) yield return null;
            Physics.SyncTransforms();

            foreach (HurtBox box in boxes)
            {
                Vector3 expected = box.transform.TransformPoint(offsets[box]);
                Assert.AreEqual(0f, Vector3.Distance(expected, box.Shapes[0].bounds.center), 0.01f, $"{box.name} stays on its bone");
            }

            Assert.AreEqual(0, boxes.Count(b => b.Shapes.Any(s => s.gameObject.layer != LayerMask.NameToLayer("HurtBox"))), "all on the HurtBox layer");
        }

        private static void AnimatorControllerClips(Animator animator, out AnimationClip clip)
        {
            clip = animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.animationClips.FirstOrDefault() : null;
        }

        [UnityTest]
        public IEnumerator Leaning_MovesTheHeadOutOfABladeThatWouldHaveHitIt()
        {
            Character control = SpawnFrozenPlayer(new Vector3(-3f, 0f, 0f));
            Character slasher = SpawnFrozenPlayer(new Vector3(3f, 0f, 0f));
            Character victim = Spawn(PlayerPrefab, new Vector3(0f, 0f, 4f), 180f);
            yield return null;
            Freeze(control);
            Freeze(slasher);
            foreach (Animator animator in victim.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            UpperBodyArticulation lean = victim.GetComponent<UpperBodyArticulation>();
            HurtBox head = Region(victim, "head.x");
            Vector3 upright = CentreOf(head);

            // Control: a horizontal slash at head height, with the victim standing, hits the head.
            PlaceBlade(control, upright, Vector3.right);
            OpenWindow(control);
            yield return Steps(2);
            Assert.AreEqual(1, hits.Count(h => h.Attacker == control.Id && h.BodyLocation == (int)BodyLocation.Head), "set-up: the slash would have hit the standing head");

            // The same slash after a deep lean.
            lean.SubmitLook(0f, 80f);
            for (int frame = 0; frame < 40; frame++) yield return null;
            Vector3 leaning = CentreOf(head);
            Assert.Greater(Vector3.Distance(upright, leaning), 0.4f, "a deep lean carries the head well away from where it stood");

            PlaceBlade(slasher, upright, Vector3.right);
            OpenWindow(slasher);
            yield return Steps(3);

            Assert.IsFalse(hits.Any(h => h.Attacker == slasher.Id && h.BodyLocation == (int)BodyLocation.Head), "the slash passes where the head was");
        }

        [Test]
        public void TheRegionsAreTriggers_OnTheHurtBoxLayer_AndTheBladesAreNot()
        {
            int hurt = LayerMask.NameToLayer("HurtBox");
            foreach (string path in new[] { PlayerPrefab, SkeletonPrefab })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (HurtBox box in prefab.GetComponentsInChildren<HurtBox>(true))
                {
                    Assert.AreEqual(hurt, box.gameObject.layer, $"{path}: {box.name}");
                    Assert.IsTrue(box.Shapes.All(s => s.isTrigger), $"{path}: {box.name} is a trigger");
                }

                foreach (Hitbox blade in prefab.GetComponentsInChildren<Hitbox>(true))
                {
                    Assert.AreNotEqual(hurt, blade.gameObject.layer, $"{path}: {blade.name} must not be a hurtbox");
                }
            }
        }
    }
}
