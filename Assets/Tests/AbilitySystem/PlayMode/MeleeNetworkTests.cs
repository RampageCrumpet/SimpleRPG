using SimpleRPG.HitDetection;
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
    /// Runs the melee ability through the real melee mechanics on a real host: real characters, the real weapon, combo machine and melee
    /// delivery, and the real combat pipeline. The combo runs on a clock the tests move by hand, so hit windows open and close exactly when
    /// a test says (see <see cref="ComboTestSupport"/> for the timeline), independent of frame rate and clip timing.
    /// </summary>
    public class MeleeNetworkTests
    {
        private const string MeleeAttack = "Assets/Abilities/Abilities/MeleeAttack.asset";
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";
        private const int StartingHealth = 100;
        private const int WeaponDamage = 10;
        // Melee Power multiplies a weapon's damage by 1.5 at the default attributes, so a weapon of 10 deals 15.
        private const int Blow = 15;

        private GameObject managerObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> created = new List<Object>();
        private GameplayServices services;
        private WeaponData weaponData;
        private AbilityDefinition melee;
        private ComboDefinition combo;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            services = GameplayServices.Reset();
            AbilityServices.Reset();

            weaponData = ScriptableObject.CreateInstance<WeaponData>();
            weaponData.damage = new DamageInfo { Damage = WeaponDamage };
            created.Add(weaponData);

            combo = ComboTestSupport.CreateCombo();
            created.Add(combo);
            ComboTestSupport.GiveCombo(weaponData, combo);

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
        /// Spawns a character on the host. Player-only scripts that need a camera, input or the game's UI are removed, as are the animation scripts,
        /// so the tests decide when the combo's hit windows happen.
        /// </summary>
        private Character Spawn(string path, Vector3 position, AnimatorCullingMode? culling = null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Could not load test prefab {path}.");

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            spawned.Add(instance);

            foreach (System.Type type in new[] { typeof(LocalPlayerControls), typeof(FirstPersonCharacterController) })
            {
                Component component = instance.GetComponent(type);
                if (component != null) Object.DestroyImmediate(component);
            }

            AnimatableCharacter animatable = instance.GetComponent<AnimatableCharacter>();
            if (animatable != null) Object.DestroyImmediate(animatable);

            if (culling.HasValue)
            {
                foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true)) animator.cullingMode = culling.Value;
            }

            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        private Character SpawnAttacker(AnimatorCullingMode? culling = null)
        {
            Character attacker = Spawn(PlayerPrefab, Vector3.zero, culling);
            attacker.WeaponData = weaponData;
            ComboTestSupport.UseManualClock(attacker);
            return attacker;
        }

        private Character SpawnVictim() => Spawn(TargetPrefab, new Vector3(0f, 0f, 2f));

        private static AbilityUser UserOf(Character character) => character.GetComponent<AbilityUser>();

        private static ComboMeleeDelivery MeleeOf(Character character) => character.GetComponent<ComboMeleeDelivery>();

        private static Weapon WeaponOf(Character character) => character.GetComponentInChildren<Weapon>();

        /// <summary>
        /// Uses the melee ability the way a player's press does. A press is accepted if it starts a chain or lands in a chain window.
        /// </summary>
        private bool Press(Character attacker) => UserOf(attacker).TryActivateOnServer(melee);

        private static void Advance(Character attacker, double seconds) => ComboTestSupport.Advance(attacker, seconds);

        private static int Pending() => AbilityServices.Current.Executor.PendingContinuations;

        // ----- A swing and its hits -----

        [UnityTest]
        public IEnumerator Swing_HitInsideTheWindow_DealsTheWeaponsDamage()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;

            Assert.IsTrue(Press(attacker));
            Advance(attacker, ComboTestSupport.InsideHitWindow);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - Blow, victim.Health);
        }

        [UnityTest]
        public IEnumerator SecondAttackOfTheCombo_DealsTheScaledDamage()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;
            Press(attacker);
            Advance(attacker, ComboTestSupport.InsideHitWindow);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);

            Advance(attacker, ComboTestSupport.InsideChainWindow - ComboTestSupport.InsideHitWindow);
            Assert.IsTrue(Press(attacker), "A press inside the chain window continues the combo.");
            Advance(attacker, ComboTestSupport.InsideHitWindow);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            // Attack 1 is 15 (10 x Melee Power 1.5), attack 2 is 15 x 0.8 = 12.
            Assert.AreEqual(StartingHealth - Blow - 12, victim.Health);
        }

        [UnityTest]
        public IEnumerator TheContinuedSwing_DoesNotChargeTheCooldownAgain()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            Press(attacker);

            Advance(attacker, ComboTestSupport.InsideChainWindow);
            Assert.IsTrue(Press(attacker), "The combo continues even though the ability's cooldown has not passed.");
            Assert.AreEqual(1, Pending(), "The first swing closed its hit window; only the second is open.");

            Advance(attacker, 1.5);
            Assert.AreEqual(0, MeleeOf(attacker).CurrentAttack, "The chain has run out.");
            Assert.IsFalse(Press(attacker), "A fresh use is still inside its cooldown.");
        }

        [UnityTest]
        public IEnumerator APressBeforeTheChainWindow_IsDiscarded_AndNotRemembered()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            Press(attacker);

            Advance(attacker, ComboTestSupport.InsideHitWindow);
            Assert.IsFalse(Press(attacker), "Too early to chain.");

            Advance(attacker, ComboTestSupport.InsideChainWindow - ComboTestSupport.InsideHitWindow);
            Assert.AreEqual(1, MeleeOf(attacker).CurrentAttack, "The early press must not chain once the window opens.");
        }

        [UnityTest]
        public IEnumerator APressAfterTheChainWindow_IsDiscarded_AndTheSwingRunsOut()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            Press(attacker);

            Advance(attacker, 0.95);
            Assert.IsFalse(Press(attacker), "The chain window has closed.");
            Assert.AreEqual(1, MeleeOf(attacker).CurrentAttack);

            Advance(attacker, 0.1);
            Assert.AreEqual(0, MeleeOf(attacker).CurrentAttack);
        }

        [UnityTest]
        public IEnumerator PressesInTheSameWindow_NeverSkipAnAttack()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            Press(attacker);
            Advance(attacker, ComboTestSupport.InsideChainWindow);

            Assert.IsTrue(Press(attacker));
            Assert.IsFalse(Press(attacker), "The second attack's own window has not opened.");
            Assert.AreEqual(2, MeleeOf(attacker).CurrentAttack);
        }

        [UnityTest]
        public IEnumerator Hits_InOneSwing_LandOnEachTargetOnce()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;
            Press(attacker);
            Advance(attacker, ComboTestSupport.InsideHitWindow);

            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Head);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - Blow, victim.Health);
        }

        [UnityTest]
        public IEnumerator Swing_OnTheWielder_DoesNothing()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            Press(attacker);
            Advance(attacker, ComboTestSupport.InsideHitWindow);

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
            Press(attacker);

            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth, victim.Health);
        }

        [UnityTest]
        public IEnumerator Hit_AfterTheWindowCloses_DoesNothing()
        {
            Character attacker = SpawnAttacker();
            Character victim = SpawnVictim();
            yield return null;
            Press(attacker);
            Advance(attacker, 0.6);

            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth, victim.Health);
        }

        [UnityTest]
        public IEnumerator ADeadAttacker_CannotSwing()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            attacker.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, BodyLocation.Torso);
            yield return null;

            Assert.IsFalse(Press(attacker));
            Assert.AreEqual(0, Pending());
        }

        [UnityTest]
        public IEnumerator AWeaponWithoutACombo_CannotSwing()
        {
            Character attacker = SpawnAttacker();
            WeaponData plain = ScriptableObject.CreateInstance<WeaponData>();
            created.Add(plain);
            attacker.WeaponData = plain;
            yield return null;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no weapon with a combo"));

            Assert.IsFalse(Press(attacker));
            Assert.AreEqual(0, Pending());
        }

        // ----- Presses stamped with a time -----

        [UnityTest]
        public IEnumerator AStampedPress_MadeInTheWindow_IsCreditedEvenIfItArrivesLate()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            Press(attacker);
            Advance(attacker, 0.95); // the window closed at 0.9 s

            // The press was made at 0.7 s and took 0.25 s to arrive; the server credits up to 0.15 s of delay, which is enough here.
            MeleePressOutcome outcome = MeleeOf(attacker).Press(pressTick: 21, freshAllowed: false);
            MeleeOf(attacker).CancelPress();

            Assert.AreEqual(MeleePressOutcome.Continued, outcome);
        }

        [UnityTest]
        public IEnumerator AStampedPress_MadeBeforeTheWindow_IsNotCredited()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            Press(attacker);
            Advance(attacker, 0.5);

            MeleePressOutcome outcome = MeleeOf(attacker).Press(pressTick: 6, freshAllowed: false);

            Assert.AreEqual(MeleePressOutcome.Rejected, outcome);
        }

        [UnityTest]
        public IEnumerator AStampedPress_FromTheFuture_IsTreatedAsNow()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            Press(attacker);
            Advance(attacker, ComboTestSupport.InsideHitWindow);

            MeleePressOutcome outcome = MeleeOf(attacker).Press(pressTick: 300, freshAllowed: false);

            Assert.AreEqual(MeleePressOutcome.Rejected, outcome, "A client cannot claim a time later than the server's.");
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

            Press(attacker);
            Advance(attacker, ComboTestSupport.InsideHitWindow);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Head);
            yield return null;

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(attacker.Id, hits[0].Attacker);
            Assert.AreEqual(victim.Id, hits[0].Victim);
            Assert.AreEqual((int)BodyLocation.Head, hits[0].BodyLocation);
            Assert.AreEqual(1, hits[0].AttackNumber);
            Assert.AreEqual(DamageProvenance.DirectWeaponHit, hits[0].Provenance);
            Assert.AreEqual(StartingHealth - Blow, healthWhenConfirmed, "The blow's damage is already applied when the event arrives.");
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

            Press(attacker);
            Advance(attacker, ComboTestSupport.InsideHitWindow);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(1, confirmed, "The proc's own damage must not count as a weapon hit.");
            Assert.AreEqual(StartingHealth - Blow - 5, victim.Health);
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

            Press(attacker);
            Advance(attacker, ComboTestSupport.InsideHitWindow);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            Assert.AreEqual(1, confirmed, "The killing blow is a confirmed hit.");
            Assert.IsFalse(victim.IsAlive);

            Advance(attacker, ComboTestSupport.InsideChainWindow - ComboTestSupport.InsideHitWindow);
            Press(attacker);
            Advance(attacker, ComboTestSupport.InsideHitWindow);
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
            Press(attacker);
            Assert.AreEqual(1, Pending());

            Advance(attacker, 0.6);

            Assert.AreEqual(0, Pending());
        }

        [UnityTest]
        public IEnumerator TheAttackersDeath_ReleasesItsOpenSwings_AndEndsTheChain()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            Press(attacker);
            Advance(attacker, ComboTestSupport.InsideHitWindow);
            Assert.AreEqual(1, Pending());

            attacker.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(0, Pending());
            Assert.AreEqual(0, MeleeOf(attacker).CurrentAttack);
        }

        [UnityTest]
        public IEnumerator ADeadAttacker_CannotChainAnotherSwing()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            Press(attacker);
            Advance(attacker, ComboTestSupport.InsideChainWindow);
            attacker.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, BodyLocation.Torso);
            yield return null;

            Assert.IsFalse(Press(attacker));
            Assert.AreEqual(0, Pending());
        }

        [UnityTest]
        public IEnumerator ChangingWeapons_EndsTheChain()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            Press(attacker);
            Advance(attacker, ComboTestSupport.InsideHitWindow);
            WeaponData other = ScriptableObject.CreateInstance<WeaponData>();
            ComboDefinition otherCombo = ComboTestSupport.CreateCombo();
            ComboTestSupport.GiveCombo(other, otherCombo);
            created.Add(other);
            created.Add(otherCombo);
            attacker.WeaponData = other;

            Advance(attacker, ComboTestSupport.InsideChainWindow - ComboTestSupport.InsideHitWindow);
            // The new weapon's combo starts fresh, so the press is a fresh use and the cooldown applies.
            Assert.IsFalse(Press(attacker));
            Assert.AreEqual(0, Pending(), "The old weapon's swing ended when it was put away.");
        }

        // ----- Dedicated servers -----

        [UnityTest]
        public IEnumerator OnTheServer_AnimatorsAlwaysAnimate_SoTheBladeKeepsMoving()
        {
            // A dedicated server has no screen, so a culled animator would never move the weapon the hitbox rides on.
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
            Advance(attacker, ComboTestSupport.InsideHitWindow);
            WeaponOf(attacker).TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.IsTrue(activated);
            Assert.Greater(slot.CooldownTimeLeft, 0f);
            Assert.AreEqual(StartingHealth - Blow, victim.Health);
        }

        [UnityTest]
        public IEnumerator TheMeleeSlot_ChainsDuringTheCooldown()
        {
            Character attacker = SpawnAttacker();
            yield return null;
            GraphAbilitySlot slot = attacker.AbilitySlots.OfType<GraphAbilitySlot>().Single(x => x.Definition == melee);

            slot.Activate();
            Advance(attacker, ComboTestSupport.InsideChainWindow);
            slot.Activate();

            Assert.AreEqual(2, MeleeOf(attacker).CurrentAttack, "The slot must not refuse a chain press while the ability's cooldown shows.");
        }
    }
}
