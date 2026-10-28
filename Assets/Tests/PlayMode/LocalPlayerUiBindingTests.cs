using NUnit.Framework;
using SimpleRPG.InventorySystem;
using SimpleRPG.ObjectInteractions;
using SimpleRPG.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// The UI finds the local player by observing it, not the other way round: the controller binds every view beneath it when the player is
    /// announced (or already was), unbinds them when it goes, and tells the game when a menu has the player's attention.
    /// </summary>
    public class LocalPlayerUiBindingTests
    {
        private sealed class RecordingView : MonoBehaviour, ILocalPlayerView
        {
            public readonly List<Character> Bound = new List<Character>();
            public int Unbinds;

            public void BindLocalPlayer(Character character) => Bound.Add(character);

            public void UnbindLocalPlayer() => Unbinds++;
        }

        private sealed class TestContainer : LootContainer
        {
        }

        private readonly List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void ResetStaticState() => LocalPlayer.Reset();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
            LocalPlayer.Reset();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private Character MakeCharacter()
        {
            GameObject go = new GameObject("character");
            spawned.Add(go);
            return go.AddComponent<Character>();
        }

        private PlayerUIController MakeUi(out RecordingView active, out RecordingView inactive)
        {
            GameObject root = new GameObject("PlayerUI");
            spawned.Add(root);
            GameObject activeChild = new GameObject("activeView");
            activeChild.transform.SetParent(root.transform);
            active = activeChild.AddComponent<RecordingView>();

            // Some views (the death screen) sit on objects that are switched off until they are needed.
            GameObject inactiveChild = new GameObject("inactiveView");
            inactiveChild.transform.SetParent(root.transform);
            inactiveChild.SetActive(false);
            inactive = inactiveChild.AddComponent<RecordingView>();

            return root.AddComponent<PlayerUIController>();
        }

        [UnityTest]
        public IEnumerator EveryViewBeneathTheController_IsBound_WhenThePlayerIsAnnounced_IncludingInactiveOnes()
        {
            PlayerUIController ui = MakeUi(out RecordingView active, out RecordingView inactive);
            yield return null;
            Character player = MakeCharacter();

            LocalPlayer.Announce(player);

            CollectionAssert.AreEqual(new[] { player }, active.Bound);
            CollectionAssert.AreEqual(new[] { player }, inactive.Bound, "a view on an inactive object is bound too");
            Assert.AreSame(player, ui.LocalCharacter);
        }

        [UnityTest]
        public IEnumerator AUiThatComesUpAfterThePlayer_BindsToTheCurrentOne()
        {
            Character player = MakeCharacter();
            LocalPlayer.Announce(player);

            MakeUi(out RecordingView active, out RecordingView inactive);
            yield return null;

            CollectionAssert.AreEqual(new[] { player }, active.Bound);
            CollectionAssert.AreEqual(new[] { player }, inactive.Bound);
        }

        [UnityTest]
        public IEnumerator WhenThePlayerGoes_EveryViewIsUnbound_AndTheControllerForgetsTheCharacter()
        {
            PlayerUIController ui = MakeUi(out RecordingView active, out RecordingView inactive);
            yield return null;
            Character player = MakeCharacter();
            LocalPlayer.Announce(player);

            LocalPlayer.Withdraw(player);

            Assert.AreEqual(1, active.Unbinds);
            Assert.AreEqual(1, inactive.Unbinds);
            Assert.IsNull(ui.LocalCharacter);
        }

        [UnityTest]
        public IEnumerator ADisabledController_StopsListening()
        {
            PlayerUIController ui = MakeUi(out RecordingView active, out _);
            yield return null;
            ui.enabled = false;

            LocalPlayer.Announce(MakeCharacter());

            Assert.IsEmpty(active.Bound, "a disabled UI is not bound");
        }

        [UnityTest]
        public IEnumerator OpeningAMenu_TellsTheGame_AndTheLastMenuToCloseReleasesIt()
        {
            PlayerUIController ui = MakeUi(out RecordingView first, out RecordingView second);
            yield return null;
            Assert.IsFalse(LocalPlayer.InMenu);

            ui.LockRotationAndShowMouse(first);
            ui.LockRotationAndShowMouse(second);
            Assert.IsTrue(LocalPlayer.InMenu);

            ui.UnlockRotationAndHideMouse(first);
            Assert.IsTrue(LocalPlayer.InMenu, "another menu is still open");

            ui.UnlockRotationAndHideMouse(second);
            Assert.IsFalse(LocalPlayer.InMenu);
            Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
        }

        [UnityTest]
        public IEnumerator AContainer_AsksForTheLootMenuThroughAnEvent_AndNeedsNoUi()
        {
            GameObject go = new GameObject("container");
            spawned.Add(go);
            TestContainer container = go.AddComponent<TestContainer>();
            yield return null;

            // No listener at all (a dedicated server, or a scene with no UI): nothing happens and nothing throws.
            Assert.DoesNotThrow(() => container.ActivateInteraction(null));
            Assert.DoesNotThrow(() => container.FinishInteraction(null));

            Inventory opened = null;
            int closed = 0;
            System.Action<Inventory> onOpen = inventory => opened = inventory;
            System.Action onClose = () => closed++;
            LootContainer.OpenRequested += onOpen;
            LootContainer.CloseRequested += onClose;
            try
            {
                container.ActivateInteraction(null);
                container.FinishInteraction(null);
            }
            finally
            {
                LootContainer.OpenRequested -= onOpen;
                LootContainer.CloseRequested -= onClose;
            }

            Assert.AreSame(go.GetComponent<Inventory>(), opened);
            Assert.AreEqual(1, closed);
        }
    }
}
