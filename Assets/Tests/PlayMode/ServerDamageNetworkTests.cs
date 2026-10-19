using System.Collections;
using NUnit.Framework;
using SimpleRPG;
using Unity.Netcode;
using HitDetection;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// Checks the server-side damage path: a weapon hit on the host lowers the target's health by the scaled damage.
    /// Each test starts a host NetworkManager, spawns a character and a weapon with a hitbox, and tears everything down afterwards.
    /// </summary>
    public class ServerDamageNetworkTests
    {
        private const int BaseDamage = 10;
        private const int StartingHealth = 100;
        private const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";
        private const string WeaponPrefab = "Assets/Tests/PlayMode/Prefabs/TestWeapon.prefab";

        private GameObject managerObject;
        private NetworkManager manager;
        private GameObject characterObject;
        private Character character;
        private GameObject weaponObject;
        private Weapon weapon;
        private WeaponData weaponData;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            managerObject = new GameObject("TestNetworkManager");
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };

            Assert.IsTrue(manager.StartHost(), "The test host should start.");
            yield return null;
            Assert.IsTrue(manager.IsServer, "The test NetworkManager should be running as a server.");

            // Netcode only spawns objects that come from prefab assets, so both the target and the weapon are instantiated from prefabs.
            characterObject = Object.Instantiate(LoadPrefab(TargetPrefab));
            character = characterObject.GetComponent<Character>();
            character.Initialize();
            characterObject.GetComponent<NetworkObject>().Spawn();

            weaponObject = Object.Instantiate(LoadPrefab(WeaponPrefab));
            weapon = weaponObject.GetComponent<Weapon>();
            weaponObject.GetComponent<NetworkObject>().Spawn();

            weaponData = ScriptableObject.CreateInstance<WeaponData>();
            weaponData.damage = new DamageInfo { Damage = BaseDamage };

            // Let Start() run on the weapon and the spawns settle.
            yield return null;
        }

        private static GameObject LoadPrefab(string path)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Could not load test prefab {path}.");
            return prefab;
#else
            Assert.Ignore("The test prefabs can only be loaded in the editor.");
            return null;
#endif
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                yield return new WaitUntil(() => !manager.IsListening);
            }

            if (managerObject != null) Object.Destroy(managerObject);
            if (characterObject != null) Object.Destroy(characterObject);
            if (weaponObject != null) Object.Destroy(weaponObject);
            if (weaponData != null) Object.DestroyImmediate(weaponData);

            yield return null;
        }

        /// <summary>
        /// Applies a single weapon hit on the server, as the hitbox would.
        /// </summary>
        private IEnumerator HitOnServer(float damageScalar)
        {
            weapon.StartSwinging(weaponData);
            weapon.ActivateHitbox(damageScalar);
            weapon.TargetHit(character, BodyLocation.Torso);

            // The damage RPC runs on the server during the next frame.
            yield return null;
        }

        [UnityTest]
        public IEnumerator ServerHit_AtFullScalar_RemovesTheFullBaseDamage()
        {
            yield return HitOnServer(1f);

            Assert.AreEqual(StartingHealth - BaseDamage, character.Health);
        }

        [UnityTest]
        public IEnumerator ServerHit_ScalesTheDamageByTheAttackScalar()
        {
            // Attack 2 of the longsword: round(10 * 0.79) = 8.
            yield return HitOnServer(0.79f);

            Assert.AreEqual(StartingHealth - 8, character.Health);
        }

        [UnityTest]
        public IEnumerator ServerHits_AccumulateAcrossAttacks()
        {
            // Attack 1 (1.00) then attack 3 (0.84): 10 + round(10 * 0.84) = 10 + 8.
            yield return HitOnServer(1f);
            weapon.DeactivateHitbox();
            yield return HitOnServer(0.84f);

            Assert.AreEqual(StartingHealth - 18, character.Health);
        }

        [UnityTest]
        public IEnumerator ServerHit_RepeatedOnTheSameTargetInOneSwing_DamagesItOnce()
        {
            weapon.StartSwinging(weaponData);
            weapon.ActivateHitbox(1f);
            weapon.TargetHit(character, BodyLocation.Torso);
            weapon.TargetHit(character, BodyLocation.Head);
            weapon.ActivateHitbox(1f); // a doubled animation event must not start a fresh swing
            weapon.TargetHit(character, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - BaseDamage, character.Health);
        }

        [UnityTest]
        public IEnumerator ServerHit_ByTheWielderOnItself_DoesNothing()
        {
            // Parent the weapon under the character so the weapon knows who is swinging it, so it knows who is swinging it.
            weaponObject.transform.SetParent(characterObject.transform, false);
            weapon.StartSwinging(weaponData);
            weapon.ActivateHitbox(1f);
            weapon.TargetHit(character, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth, character.Health);
        }

        [UnityTest]
        public IEnumerator ServerHit_WithNoSwingInProgress_DoesNothing()
        {
            weapon.StartSwinging(weaponData);
            weapon.TargetHit(character, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth, character.Health);
        }

        [UnityTest]
        public IEnumerator ServerHit_OnACharacterOutOfReach_DoesNothing()
        {
            // A hit with no target should not change any health and should not throw.
            weapon.StartSwinging(weaponData);
            weapon.ActivateHitbox(1f);
            weapon.TargetHit(null, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth, character.Health);
        }
    }
}
