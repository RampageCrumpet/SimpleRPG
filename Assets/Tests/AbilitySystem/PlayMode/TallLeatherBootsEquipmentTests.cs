using NUnit.Framework;
using SimpleRPG.Appearance;
using SimpleRPG.InventorySystem;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// The tall leather boots through the real equipment path on a host and a genuinely separate client: wearing them puts the left and right
    /// boot on the character in place of the bare feet, and taking them off returns the bare feet.
    /// </summary>
    public class TallLeatherBootsEquipmentTests : RemoteClientTestBase
    {
        private const string BootsItemPath = "Assets/Items/Armor/TallLeatherBoots.asset";
        private ArmorData boots;

        [UnitySetUp]
        public IEnumerator UseRealDefinitions()
        {
            DefinitionRegistry.Instance = Resources.Load<DefinitionRegistry>("DefinitionRegistry");
            boots = AssetDatabase.LoadAssetAtPath<ArmorData>(BootsItemPath);
            Assert.IsNotNull(boots);
            yield return null;
        }

        private static SkinnedMeshRenderer[] BootRenderers(Character body) =>
            body.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null && r.sharedMesh.name.StartsWith("TallLeatherBoots_")).ToArray();

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator WearingTheBoots_ShowsBothOnTheServerAndTheClient_AndTakingThemOffRestoresTheBareFeet()
        {
            Character onServer = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            Character onClient = null;
            yield return ClientCopyOf(onServer, copy => onClient = copy);
            Assert.AreEqual(0, BootRenderers(onServer).Length, "no boots before they are worn");

            Assert.IsTrue(onServer.GetComponent<Equipment>().Equip(EquipmentSlot.feet, boots, 600), "the equipment accepts the boots");
            yield return Frames(3);
            yield return Until(() => BootRenderers(onClient).Length == 2, 5f, "the client to show both boots");

            foreach (Character body in new[] { onServer, onClient })
            {
                SkinnedMeshRenderer[] shown = BootRenderers(body);
                Assert.AreEqual(2, shown.Length, body.name);
                CollectionAssert.AreEquivalent(new[] { "TallLeatherBoots_L", "TallLeatherBoots_R" }, shown.Select(r => r.sharedMesh.name).ToArray(), body.name);
                Assert.AreEqual(1, shown.Select(r => r.sharedMaterial).Distinct().Count(), "both boots share one material");
                Assert.IsTrue(shown.All(r => r.bones.Length == 52 && r.bones.All(b => b != null)), "skinned to the wearer's own 52 bones");
                Assert.IsTrue(shown.All(r => r.bones.All(b => b.IsChildOf(body.transform))), "the bones belong to the wearer");
            }

            onServer.GetComponent<Equipment>().Unequip(EquipmentSlot.feet);
            yield return Frames(3);
            yield return Until(() => BootRenderers(onClient).Length == 0, 5f, "the client to drop the boots");
            Assert.AreEqual(0, BootRenderers(onServer).Length, "the server drops them too");
        }
    }
}
