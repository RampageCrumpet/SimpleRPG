using NUnit.Framework;
using SimpleRPG.ObjectInteractions;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    public class InteractableManagerTests
    {
        private sealed class StubInteractable : Interactable
        {
            public override void ActivateInteraction(Interactor interactor) { }
            public override void FinishInteraction(Interactor interactor) { }
        }

        private GameObject host;

        [SetUp]
        public void SetUp() => InteractableManager.Clear();

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            InteractableManager.Clear();
        }

        private StubInteractable Make()
        {
            host = new GameObject("interactable");
            return host.AddComponent<StubInteractable>();
        }

        [Test]
        public void Registering_TwiceOrWithNull_KeepsOneEntry()
        {
            StubInteractable interactable = Make();
            InteractableManager.RegisterInteractable(interactable);
            InteractableManager.RegisterInteractable(interactable);
            InteractableManager.RegisterInteractable(null);
            Assert.AreEqual(1, InteractableManager.Interactables.Count);
        }

        [Test]
        public void Unregister_RemovesIt_AndClearForgetsEverything()
        {
            StubInteractable interactable = Make();
            InteractableManager.RegisterInteractable(interactable);
            InteractableManager.UnregisterInteractable(interactable);
            Assert.AreEqual(0, InteractableManager.Interactables.Count);

            InteractableManager.RegisterInteractable(interactable);
            InteractableManager.Clear();
            Assert.AreEqual(0, InteractableManager.Interactables.Count);
        }
    }
}
