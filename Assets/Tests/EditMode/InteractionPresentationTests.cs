using System.Linq;
using NUnit.Framework;
using SimpleRPG.Controls;
using SimpleRPG.ObjectInteractions;
using SimpleRPG.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace SimpleRPG.Tests.EditMode
{
    public class InteractionPresentationTests
    {
        [Test]
        public void EveryUniversalRenderer_CarriesTheInteractableOutlineWithBothShaders()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" });
            Assert.IsNotEmpty(guids);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                var feature = renderer.rendererFeatures.OfType<InteractableOutlineFeature>().SingleOrDefault();
                Assert.IsNotNull(feature, $"{path} has no InteractableOutlineFeature.");

                var serialized = new SerializedObject(feature);
                Assert.IsNotNull(serialized.FindProperty("maskShader").objectReferenceValue, $"{path}: outline mask shader is unset.");
                Assert.IsNotNull(serialized.FindProperty("compositeShader").objectReferenceValue, $"{path}: outline composite shader is unset.");
            }
        }

        [Test]
        public void EveryInteractablePrefab_HasItsOwnNameAndActionText()
        {
            var seen = new System.Collections.Generic.Dictionary<string, string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (Interactable interactable in prefab.GetComponentsInChildren<Interactable>(true))
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(interactable.InteractionName), $"{path}: no InteractionName.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(interactable.InteractionText), $"{path}: no InteractionText.");

                    if (seen.TryGetValue(interactable.InteractionName, out string otherPath) && otherPath != path)
                    {
                        Assert.Fail($"{path} and {otherPath} share the interaction name '{interactable.InteractionName}'.");
                    }

                    seen[interactable.InteractionName] = path;
                }
            }
        }

        [Test]
        public void InteractAction_DefaultsToF_AndEveryGameplayActionExists()
        {
            Assert.AreEqual("F", GameInput.BindingDisplay(GameInput.Interact));
            Assert.IsNotNull(GameInput.Move);
            Assert.IsNotNull(GameInput.Look);
            Assert.IsNotNull(GameInput.Inventory);
            Assert.IsNotNull(GameInput.Cancel);
            for (int slot = 1; slot <= 10; slot++)
            {
                Assert.IsNotNull(GameInput.Ability(slot), $"Ability{slot} is missing.");
            }
        }

        [Test]
        public void RebindingInteract_ChangesTheDisplayedKey_AndResetRestoresIt()
        {
            GameInput.Interact.ApplyBindingOverride(0, "<Keyboard>/e");
            try
            {
                Assert.AreEqual("E", GameInput.BindingDisplay(GameInput.Interact));
            }
            finally
            {
                GameInput.ResetBindingOverrides();
            }

            Assert.AreEqual("F", GameInput.BindingDisplay(GameInput.Interact));
        }
    }
}
