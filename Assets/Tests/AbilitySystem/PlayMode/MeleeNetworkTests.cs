using HitDetection;
using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Animations;
using SimpleRPG.Gameplay;
using SimpleRPG.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Runs the melee ability through the real melee mechanics on a real host: real characters, the real weapon, combo tracker and melee
    /// delivery, and the real combat pipeline. The animation is driven by hand (the animation events are called directly) so the tests do not
    /// depend on clip timing; one test at the end lets the real animation do it.
    /// </summary>
    public class MeleeNetworkTests
    {
        private const string MeleeAttack = "Assets/Abilitities/Abillities/MeleeAttack.asset";
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";
        private const int StartingHealth = 100;
        private const int WeaponDamage = 10;

        private GameObject managerObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> created = new List<Object>();
        private GameplayServices services;
        private WeaponData weaponData;
        private AbilityDefinition melee;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            services = GameplayServices.Reset();
            AbilityServices.Reset();

            weaponData = ScriptableObject.CreateInstance<WeaponData>();
            weaponData.damage = new DamageInfo { Damage = WeaponDamage };
            created.Add(weaponData);

            melee = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(MeleeAttack);
            Assert.IsNotNull(melee, $"Could not load {MeleeAttack}.");

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
        /// Spawns a character on the host. Player-only scripts that need a camera, input or the game's UI are removed, as are the animation scripts
        /// unless <paramref name="withAnimation"/> is set, so the tests decide when the swing's animation events happen.
        /// </summary>
        private Character Spawn(string path, Vector3 position, bool withAnimation = false, AnimatorCullingMode? culling = null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Could not load test prefab {path}.");

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            spawned.Add(instance);

            foreach (System.Type type in new[] { typeof(RegisterWithUI), typeof(FirstPersonCharacterController), typeof(ThirdPersonCharacterController) })
            {
                Component component = instance.GetComponent(type);
                if (component != null) Object.DestroyImmediate(component);
            }

            if (!withAnimation)
            {
                AnimatableCharacter animatable = instance.GetComponent<AnimatableCharacter>();
                if (animatable != null) Object.DestroyImmediate(animatable);
            }

            if (culling.HasValue)
            {
                foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true)) animator.cullingMode = culling.Value;
            }

            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        private Character SpawnAttacker(bool withAnimation = false, AnimatorCullingMode? culling = null)
        {
            Character attacker = Spawn(PlayerPrefab, Vector3.zero, withAnimation, culling);
            attacker.WeaponData = weaponData;
            return attacker;
        }

        private Character SpawnVictim() => Spawn(TargetPrefab, new Vector3(0f, 0f, 2f));

        private static AbilityUser UserOf(Character character) => character.GetComponent<AbilityUser>();

        private static MeleeDelivery MeleeOf(Character character) => character.GetComponent<MeleeDelivery>();

        private static Weapon WeaponOf(Character character) => character.GetComponentInChildren<Weapon>();

        /// <summary>
        /// Accepts a queued press the way the server does when a client sends one inside the current attack's window.
        /// </summary>
        private static void QueueNextSwing(Character attacker, AbilityDefinition ability)
        {
            MethodInfo queue = typeof(MeleeDelivery).GetMethod("QueueOnServer", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(queue, "MeleeDelivery.QueueOnServer should exist.");
            queue.Invoke(MeleeOf(attacker), new object[] { ability });
        }

        private static int Pending() => AbilityServices.Current.Executor.PendingContinuations;

        // ----- A swing and its hits -----

        [UnityTest]
        public IEnumerator Swing_HitInsideTheWindow_DealsTheWeaponsDamage()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;

            Assert.IsTrue(UserOf(attacker).TryActivateOnServer(melee));
            MeleeOf(attacker).ActivateWeapon(1);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - WeaponDamage, victim.Health);
        }

        [UnityTest]
        public IEnumerator SecondAttackOfTheCombo_DealsTheScaledDamage()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;
            UserOf(attacker).TryActivateOnServer(melee);
            MeleeOf(attacker).ActivateWeapon(1);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            MeleeOf(attacker).DeactivateWeaponH();

            QueueNextSwing(attacker, melee);
            MeleeOf(attacker).ActivateWeapon(2);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            // Attack 1 is 10, attack 2 is round(10 x 0.79) = 8.
            Assert.AreEqual(StartingHealth - WeaponDamage - 8, victim.Health);
        }

        [UnityTest]
        public IEnumerator TheQueuedSwing_DoesNotChargeTheCooldownAgain()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            UserOf(attacker).TryActivateOnServer(melee);

            QueueNextSwing(attacker, melee);

            Assert.IsFalse(UserOf(attacker).TryActivateOnServer(melee), "A fresh use is still inside its cooldown.");
            Assert.AreEqual(2, Pending(), "The first swing and the queued one are both open.");
        }

        [UnityTest]
        public IEnumerator AFreshChain_IsBlockedWhileAQueuedOneIsRunning()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            UserOf(attacker).TryActivateOnServer(melee);
            Assert.IsTrue(MeleeOf(attacker).CanStartFreshChain, "Nothing is queued yet.");

            QueueNextSwing(attacker, melee);

            Assert.IsFalse(MeleeOf(attacker).CanStartFreshChain);
        }

        [UnityTest]
        public IEnumerator Hits_InOneSwing_LandOnEachTargetOnce()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;
            UserOf(attacker).TryActivateOnServer(melee);
            MeleeOf(attacker).ActivateWeapon(1);

            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Head);
            MeleeOf(attacker).ActivateWeapon(1); // a doubled animation event must not start a fresh swing
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - WeaponDamage, victim.Health);
        }

        [UnityTest]
        public IEnumerator Swing_OnTheWielder_DoesNothing()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            UserOf(attacker).TryActivateOnServer(melee);
            MeleeOf(attacker).ActivateWeapon(1);

            WeaponOf(attacker).TargetHit(attacker, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth, attacker.Health);
        }

        [UnityTest]
        public IEnumerator Hit_WithNoWindowOpen_DoesNothing()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;
            UserOf(attacker).TryActivateOnServer(melee);

            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth, victim.Health);
        }

        [UnityTest]
        public IEnumerator Window_OpenedWithNoSwingStarted_DoesNothing()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;

            MeleeOf(attacker).ActivateWeapon(1);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth, victim.Health, "A hit window that no ability asked for is not a hit.");
        }

        [UnityTest]
        public IEnumerator ClipsWithNoAttackNumber_UseTheOnlySwing()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;
            UserOf(attacker).TryActivateOnServer(melee);

            // The enemy clip's event passes 0.
            MeleeOf(attacker).ActivateWeapon(0);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - WeaponDamage, victim.Health);
        }

        [UnityTest]
        public IEnumerator ADeadAttacker_CannotSwing()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            attacker.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, BodyLocation.Torso);
            yield return null;

            Assert.IsFalse(UserOf(attacker).TryActivateOnServer(melee));
            Assert.AreEqual(0, Pending());
        }

        // ----- Weapon hit events -----

        [UnityTest]
        public IEnumerator ALandedBlow_RaisesOneWeaponHitConfirmed_AfterItsDamage()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;
            List<WeaponHitConfirmedEvent> hits = new List<WeaponHitConfirmedEvent>();
            int healthWhenConfirmed = -1;
            services.Events.Subscribe<WeaponHitConfirmedEvent>(e =>
            {
                hits.Add(e);
                healthWhenConfirmed = victim.Health;
            });

            UserOf(attacker).TryActivateOnServer(melee);
            MeleeOf(attacker).ActivateWeapon(1);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Head);
            yield return null;

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(attacker.Id, hits[0].Attacker);
            Assert.AreEqual(victim.Id, hits[0].Victim);
            Assert.AreEqual((int)BodyLocation.Head, hits[0].BodyLocation);
            Assert.AreEqual(1, hits[0].AttackNumber);
            Assert.AreEqual(DamageProvenance.DirectWeaponHit, hits[0].Provenance);
            Assert.AreEqual(StartingHealth - WeaponDamage, healthWhenConfirmed, "The blow's damage is already applied when the event arrives.");
        }

        [UnityTest]
        public IEnumerator AProcsOwnSpellDamage_IsNotAnotherWeaponHit()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;
            int confirmed = 0;
            services.Events.Subscribe<WeaponHitConfirmedEvent>(e =>
            {
                confirmed++;

                // A rule reacting to the blow deals spell damage of its own.
                services.Combat.Apply(new DamageRequest(e.Victim, 5, e.Attacker, provenance: DamageProvenance.Spell | DamageProvenance.Proc));
            });

            UserOf(attacker).TryActivateOnServer(melee);
            MeleeOf(attacker).ActivateWeapon(1);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(1, confirmed, "The proc's own damage must not count as a weapon hit.");
            Assert.AreEqual(StartingHealth - WeaponDamage - 5, victim.Health);
        }

        [UnityTest]
        public IEnumerator ABlowThatKillsATarget_StillConfirmsTheHit_ButABlowOnACorpseDoesNot()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;
            victim.TakeDamage(new DamageInfo { Damage = StartingHealth - 5 }, BodyLocation.Torso);
            int confirmed = 0;
            services.Events.Subscribe<WeaponHitConfirmedEvent>(e => confirmed++);

            UserOf(attacker).TryActivateOnServer(melee);
            MeleeOf(attacker).ActivateWeapon(1);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            Assert.AreEqual(1, confirmed, "The killing blow is a confirmed hit.");
            Assert.IsFalse(victim.IsAlive);

            MeleeOf(attacker).DeactivateWeaponH();
            QueueNextSwing(attacker, melee);
            MeleeOf(attacker).ActivateWeapon(2);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(1, confirmed, "A blow on a corpse is not a confirmed hit.");
        }

        // ----- The swing's lifetime -----

        [UnityTest]
        public IEnumerator ClosingTheWindow_EndsTheSwing_AndReleasesTheAbility()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            UserOf(attacker).TryActivateOnServer(melee);
            Assert.AreEqual(1, Pending());

            MeleeOf(attacker).ActivateWeapon(1);
            MeleeOf(attacker).DeactivateWeaponH();

            Assert.AreEqual(0, Pending());
        }

        [UnityTest]
        public IEnumerator ASwingWhoseWindowNeverCloses_IsReleasedAfterItsExpectedLength()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            UserOf(attacker).TryActivateOnServer(melee);
            Assert.AreEqual(1, Pending());

            // Attack 1 is about 1.17 s plus 0.28 s of combo, plus the 2 s of slack: an interrupted animation must not hold the ability forever.
            yield return new WaitForSeconds(3.7f);

            Assert.AreEqual(0, Pending());
        }

        [UnityTest]
        public IEnumerator TheAttackersDeath_ReleasesItsOpenSwings()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            UserOf(attacker).TryActivateOnServer(melee);
            QueueNextSwing(attacker, melee);
            Assert.AreEqual(2, Pending());

            attacker.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(0, Pending());
        }

        [UnityTest]
        public IEnumerator ADeadAttacker_CannotQueueAnotherSwing()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            UserOf(attacker).TryActivateOnServer(melee);
            attacker.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, BodyLocation.Torso);
            yield return null;

            QueueNextSwing(attacker, melee);

            Assert.AreEqual(0, Pending());
        }

        // ----- Dedicated servers -----

        [UnityTest]
        public IEnumerator OnTheServer_AnimatorsAlwaysAnimate_SoHitWindowEventsStillFire()
        {
            // A dedicated server has no screen, so a culled animator would never play the clips whose events open the hit window.
            Character attacker = SpawnAttacker(culling: AnimatorCullingMode.CullCompletely);
            yield return null;

            Animator[] animators = attacker.GetComponentsInChildren<Animator>(true);
            Assert.IsNotEmpty(animators);
            foreach (Animator animator in animators)
            {
                Assert.AreEqual(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode, animator.name);
            }
        }

        // ----- The ability bar -----

        [UnityTest]
        public IEnumerator TheMeleeSlot_UsesTheAbilityThroughTheCharactersBar()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;
            GraphAbilitySlot slot = attacker.AbilitySlots.OfType<GraphAbilitySlot>().Single(x => x.Definition == melee);
            bool activated = false;
            slot.Activated += _ => activated = true;

            slot.Activate();
            MeleeOf(attacker).ActivateWeapon(1);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.IsTrue(activated);
            Assert.Greater(slot.CooldownTimeLeft, 0f);
            Assert.AreEqual(StartingHealth - WeaponDamage, victim.Health);
        }

        // ----- The real animation -----

        [UnityTest]
        public IEnumerator RepeatedSlotPresses_DuringEachSwing_BufferTheRealAnimatedCombo()
        {
            Character attacker = SpawnAttacker(withAnimation: true);
            yield return null;
            var started = new List<int>();
            MeleeOf(attacker).MeleeAttackStarted += started.Add;
            GraphAbilitySlot slot = attacker.AbilitySlots.OfType<GraphAbilitySlot>().Single(x => x.Definition == melee);
            Animator animator = attacker.GetComponentsInChildren<Animator>(true).Single(a => a.runtimeAnimatorController != null);

            slot.Activate();
            foreach (int attack in new[] { 1, 2 })
            {
                float deadline = Time.time + 3f;
                while (!animator.GetCurrentAnimatorStateInfo(0).IsName($"Longsword_A{attack}_Attack") && Time.time < deadline)
                    yield return null;
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName($"Longsword_A{attack}_Attack"));
                yield return new WaitForSeconds(.2f);
                slot.Activate();
            }

            float finish = Time.time + 3f;
            while ((started.Count < 3 || Pending() > 0) && Time.time < finish) yield return null;
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, started);
            Assert.AreEqual(0, Pending());
        }

        [UnityTest]
        public IEnumerator RepeatedSlotPresses_AfterEachSwing_CompleteTheRealAnimatedCombo()
        {
            Character attacker = SpawnAttacker(withAnimation: true);
            yield return null;
            var started = new List<int>();
            MeleeOf(attacker).MeleeAttackStarted += started.Add;
            GraphAbilitySlot slot = attacker.AbilitySlots.OfType<GraphAbilitySlot>().Single(x => x.Definition == melee);
            Animator animator = attacker.GetComponentsInChildren<Animator>(true).Single(a => a.runtimeAnimatorController != null);

            slot.Activate();
            foreach (int attack in new[] { 1, 2 })
            {
                float deadline = Time.time + 3f;
                while (!animator.GetCurrentAnimatorStateInfo(0).IsName($"Longsword_A{attack}_Recovery") && Time.time < deadline)
                    yield return null;
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName($"Longsword_A{attack}_Recovery"), "The actual swing must finish before the next press.");
                yield return new WaitForSeconds(.12f);
                slot.Activate();
            }

            float finish = Time.time + 3f;
            while ((started.Count < 3 || Pending() > 0) && Time.time < finish) yield return null;
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, started, "Normal ability-bar presses must advance the combo after each swing.");
            Assert.AreEqual(0, Pending(), "All three real clip events must close their swings.");
        }

        [UnityTest]
        public IEnumerator ARealSwing_PlaysItsAnimation_OpensAndClosesTheHitWindow_AndReleasesTheAbility()
        {
            Character attacker = SpawnAttacker(withAnimation: true);
            yield return null;
            int started = 0;
            MeleeOf(attacker).MeleeAttackStarted += _ => started++;

            Assert.IsTrue(UserOf(attacker).TryActivateOnServer(melee));
            yield return null;
            Assert.AreEqual(1, started, "Every machine is told which attack to play.");
            Assert.AreEqual(1, Pending());

            // The attack clip's own events open and close the window; when it has played out the swing is complete.
            float began = Time.time;
            float deadline = began + 4f;
            while (Pending() > 0 && Time.time < deadline) yield return null;

            Assert.AreEqual(0, Pending(), "The clip's DeactivateWeaponH event should end the swing.");
            Assert.Less(Time.time - began, 2.5f, "It must end because the clip closed its window, not because the safety timeout (about 3.5 s) released it.");
        }
    }
}
