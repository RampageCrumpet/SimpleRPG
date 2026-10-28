using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The local-player channel: who the local player is, when that changes, and whether a menu has their attention.
    /// </summary>
    public class LocalPlayerTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<string> log = new List<string>();

        [SetUp]
        public void SetUp()
        {
            LocalPlayer.Reset();
            log.Clear();
            LocalPlayer.Ready += c => log.Add("ready " + c.name);
            LocalPlayer.Gone += c => log.Add("gone " + c.name);
        }

        [TearDown]
        public void TearDown()
        {
            LocalPlayer.Reset();
            foreach (GameObject go in created) Object.DestroyImmediate(go);
            created.Clear();
        }

        private Character Make(string name)
        {
            GameObject go = new GameObject(name);
            created.Add(go);
            return go.AddComponent<Character>();
        }

        [Test]
        public void Announce_MakesTheCharacterCurrent_AndRaisesReadyOnce()
        {
            Character a = Make("a");

            LocalPlayer.Announce(a);
            LocalPlayer.Announce(a);

            Assert.AreSame(a, LocalPlayer.Current);
            CollectionAssert.AreEqual(new[] { "ready a" }, log, "announcing the same character again is not news");
        }

        [Test]
        public void AnnouncingAnotherCharacter_WithdrawsTheFirst()
        {
            Character a = Make("a");
            Character b = Make("b");

            LocalPlayer.Announce(a);
            LocalPlayer.Announce(b);

            Assert.AreSame(b, LocalPlayer.Current);
            CollectionAssert.AreEqual(new[] { "ready a", "gone a", "ready b" }, log);
        }

        [Test]
        public void Withdraw_ClearsTheCurrentCharacter_ButOnlyForThatCharacter()
        {
            Character a = Make("a");
            Character b = Make("b");
            LocalPlayer.Announce(a);

            LocalPlayer.Withdraw(b);
            Assert.AreSame(a, LocalPlayer.Current, "withdrawing a character that is not current changes nothing");

            LocalPlayer.Withdraw(a);
            Assert.IsNull(LocalPlayer.Current);
            CollectionAssert.AreEqual(new[] { "ready a", "gone a" }, log);
        }

        [Test]
        public void AnnouncingNothing_IsIgnored()
        {
            LocalPlayer.Announce(null);

            Assert.IsNull(LocalPlayer.Current);
            Assert.IsEmpty(log);
        }

        [Test]
        public void MenuMode_RaisesAnEventOnlyWhenItChanges()
        {
            var changes = new List<bool>();
            LocalPlayer.MenuModeChanged += changes.Add;

            LocalPlayer.SetMenuMode(false);
            LocalPlayer.SetMenuMode(true);
            LocalPlayer.SetMenuMode(true);
            LocalPlayer.SetMenuMode(false);

            Assert.IsFalse(LocalPlayer.InMenu);
            CollectionAssert.AreEqual(new[] { true, false }, changes);
        }

        [Test]
        public void Reset_ForgetsTheCharacterTheMenuStateAndEveryListener()
        {
            LocalPlayer.Announce(Make("a"));
            LocalPlayer.SetMenuMode(true);
            log.Clear();

            LocalPlayer.Reset();
            LocalPlayer.Announce(Make("b"));

            Assert.IsFalse(LocalPlayer.InMenu);
            Assert.IsEmpty(log, "listeners from before the reset are gone");
        }
    }
}
