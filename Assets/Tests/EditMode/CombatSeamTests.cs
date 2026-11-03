using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The small interfaces that let combat code stay below the code that carries items, runs delves and tracks players: where the wielded
    /// weapon comes from, who decides a respawn, and how a player's character is found. Each one has to behave as the concrete thing it
    /// replaced did.
    /// </summary>
    public class CombatSeamTests
    {
        private sealed class FakeWeaponSource : MonoBehaviour, IEquippedWeaponSource
        {
            public WeaponData Weapon;

            public WeaponData MainHandWeapon => Weapon;
        }

        private sealed class FakeRespawnPolicy : IRespawnPolicy
        {
            public bool Allows;

            public bool AllowsRespawn(Character character) => Allows;
        }

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                if (item != null) Object.DestroyImmediate(item);
            }

            created.Clear();
        }

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }

        private WeaponData MakeWeapon() => Track(ScriptableObject.CreateInstance<WeaponData>());

        // ----- The weapon a combatant attacks with -----

        [Test]
        public void TheWeaponInTheMainHand_IsTheOneUsed_WhateverHoldsTheEquipment()
        {
            GameObject go = Track(new GameObject("fighter"));
            CombatLoadout loadout = go.AddComponent<Character>().Loadout;
            WeaponData defaultWeapon = MakeWeapon();
            WeaponData wielded = MakeWeapon();
            loadout.DefaultWeapon = defaultWeapon;
            go.AddComponent<FakeWeaponSource>().Weapon = wielded;

            Assert.AreSame(wielded, loadout.Weapon);
        }

        [Test]
        public void AnEmptyMainHand_FallsBackToTheDefaultWeapon()
        {
            GameObject go = Track(new GameObject("fighter"));
            CombatLoadout loadout = go.AddComponent<Character>().Loadout;
            WeaponData defaultWeapon = MakeWeapon();
            loadout.DefaultWeapon = defaultWeapon;
            go.AddComponent<FakeWeaponSource>().Weapon = null;

            Assert.AreSame(defaultWeapon, loadout.Weapon);
        }

        [Test]
        public void NoEquipmentAtAll_UsesTheDefaultWeapon()
        {
            GameObject go = Track(new GameObject("monster"));
            CombatLoadout loadout = go.AddComponent<Character>().Loadout;
            WeaponData defaultWeapon = MakeWeapon();
            loadout.DefaultWeapon = defaultWeapon;

            Assert.AreSame(defaultWeapon, loadout.Weapon);
        }

        [Test]
        public void ADestroyedWeaponSource_IsNotUsed_AndTheDefaultWeaponTakesOver()
        {
            GameObject go = Track(new GameObject("fighter"));
            CombatLoadout loadout = go.AddComponent<Character>().Loadout;
            WeaponData defaultWeapon = MakeWeapon();
            loadout.DefaultWeapon = defaultWeapon;
            FakeWeaponSource source = go.AddComponent<FakeWeaponSource>();
            source.Weapon = MakeWeapon();
            Assert.AreSame(source.Weapon, loadout.Weapon, "the source is found and remembered");

            Object.DestroyImmediate(source);

            Assert.AreSame(defaultWeapon, loadout.Weapon, "a source that is gone is looked for again, not read");
        }

        // ----- Who decides a respawn -----

        [Test]
        public void WithoutAPolicy_RespawningIsAllowed()
        {
            GameObject go = Track(new GameObject("manager"));
            NetworkManager manager = go.AddComponent<NetworkManager>();

            Assert.IsNull(RespawnPolicies.Find(manager));
            Assert.IsNull(RespawnPolicies.Find(null));
            Assert.IsTrue(RespawnPolicies.AllowsRespawn(null));
        }

        [Test]
        public void ARegisteredPolicy_IsFoundThroughItsNetworkManager_AndOnlyThroughIt()
        {
            NetworkManager first = Track(new GameObject("first")).AddComponent<NetworkManager>();
            NetworkManager second = Track(new GameObject("second")).AddComponent<NetworkManager>();
            var policy = new FakeRespawnPolicy();

            RespawnPolicies.Register(first, policy);
            try
            {
                Assert.AreSame(policy, RespawnPolicies.Find(first));
                Assert.IsNull(RespawnPolicies.Find(second), "a second session in the same process has its own rule");
            }
            finally
            {
                RespawnPolicies.Unregister(first, policy);
            }

            Assert.IsNull(RespawnPolicies.Find(first));
        }

        [Test]
        public void ARetiredPolicy_DoesNotRemoveTheOneThatReplacedIt()
        {
            NetworkManager manager = Track(new GameObject("manager")).AddComponent<NetworkManager>();
            var retired = new FakeRespawnPolicy();
            var current = new FakeRespawnPolicy();

            RespawnPolicies.Register(manager, retired);
            RespawnPolicies.Register(manager, current);
            try
            {
                RespawnPolicies.Unregister(manager, retired);

                Assert.AreSame(current, RespawnPolicies.Find(manager));
            }
            finally
            {
                RespawnPolicies.Unregister(manager, current);
            }
        }

        // ----- Finding a player's character -----

        [Test]
        public void NoClient_HasNoCharacter()
        {
            Assert.IsNull(PlayerCharacters.Of(null));
        }
    }
}
