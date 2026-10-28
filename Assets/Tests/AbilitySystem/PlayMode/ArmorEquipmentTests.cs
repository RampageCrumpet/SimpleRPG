using NUnit.Framework;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using SimpleRPG.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Checks wearing armor on a real host: the equipment rules (equip, replace, unequip, refusals, ownership), the notifications other systems
    /// observe, and that the paper doll shows what is worn. Test armor is lightweight ScriptableObjects; no modeled assets are involved.
    /// </summary>
    public class ArmorEquipmentTests
    {
        private const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";

        private GameObject managerObject;
        private NetworkManager manager;
        private GameObject characterObject;
        private Equipment equipment;
        private Inventory inventory;
        private List<string> log;
        private DefinitionRegistry previousRegistry;
        private readonly List<Object> created = new List<Object>();

        private ArmorData helmet;
        private ArmorData otherHelmet;
        private ArmorData breastplate;
        private ArmorData gauntlets;
        private ArmorData greaves;
        private ArmorData boots;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameplayServices services = GameplayServices.Reset();
            log = new List<string>();
            services.Events.Subscribe<ItemEquippedEvent>(e => log.Add("equipped " + e.Item.Value + " in " + (EquipmentSlot)e.Slot));
            services.Events.Subscribe<ItemUnequippedEvent>(e => log.Add("unequipped " + e.Item.Value + " from " + (EquipmentSlot)e.Slot + " (" + e.Reason + ")"));

            helmet = Armor(EquipmentSlot.head, Vector2Int.one);
            otherHelmet = Armor(EquipmentSlot.head, Vector2Int.one);
            breastplate = Armor(EquipmentSlot.chest, new Vector2Int(2, 2));
            gauntlets = Armor(EquipmentSlot.hands, Vector2Int.one);
            greaves = Armor(EquipmentSlot.legs, Vector2Int.one);
            boots = Armor(EquipmentSlot.feet, Vector2Int.one);

            previousRegistry = DefinitionRegistry.Instance;
            DefinitionRegistry.Instance = ItemTestRegistry.With(new Item[] { helmet, otherHelmet, breastplate, gauntlets, greaves, boots });
            created.Add(DefinitionRegistry.Instance);

            managerObject = new GameObject("TestNetworkManager");
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };

            Assert.IsTrue(manager.StartHost(), "The test host should start.");
            yield return null;

#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefab);
            Assert.IsNotNull(prefab, $"Could not load test prefab {TargetPrefab}.");
            characterObject = Object.Instantiate(prefab);
            equipment = characterObject.GetComponent<Equipment>();
            inventory = characterObject.AddComponent<Inventory>();
            SetInventorySize(inventory, new Vector2Int(4, 4));
            characterObject.GetComponent<Character>().Initialize();
            characterObject.GetComponent<NetworkObject>().Spawn();
#else
            Assert.Ignore("The test prefab can only be loaded in the editor.");
#endif
            yield return null;
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
            DefinitionRegistry.Instance = previousRegistry;
            foreach (Object item in created)
            {
                if (item != null) Object.DestroyImmediate(item);
            }

            created.Clear();
            GameplayServices.Reset();
            yield return null;
        }

        private ArmorData Armor(EquipmentSlot slot, Vector2Int size)
        {
            ArmorData armor = ScriptableObject.CreateInstance<ArmorData>();
            typeof(ArmorData).GetField("slot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(armor, slot);
            typeof(Item).GetField("<ItemSize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(armor, size);
            created.Add(armor);
            return armor;
        }

        private static void SetInventorySize(Inventory target, Vector2Int size)
        {
            typeof(Inventory).GetField("<InventorySize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, size);
            target.Awake();
        }

        /// <summary>
        /// Puts a copy of a catalog item in the inventory, as loot would, and returns the copy.
        /// </summary>
        private Item Give(Item template)
        {
            HashSet<Item> before = new HashSet<Item>(inventory.GetItems().Select(pair => pair.item));
            Assert.IsTrue(inventory.AddItemInstance(template), "The test inventory should have room for " + template.name);
            return inventory.GetItems().Select(pair => pair.item).Single(item => !before.Contains(item));
        }

        private int InventoryCount(Item item) => inventory.GetItems().Count(pair => pair.item == item);

        private int EquippedCount(Item item) => equipment.Equipped.Count(entry => entry.item == item);

        /// <summary>
        /// An item is held by the inventory or by an equipment slot, never both and never neither.
        /// </summary>
        private void AssertOwnedOnce(Item item)
        {
            Assert.AreEqual(1, InventoryCount(item) + EquippedCount(item), item.name + " must be in exactly one place.");
        }

        [Test]
        public void Equip_IntoAnEmptySlot_MovesTheItemFromInventoryToTheSlot()
        {
            Item worn = Give(helmet);
            uint instanceId = (uint)inventory.GetInstanceId(worn).Value;

            Assert.AreEqual(EquipResult.Success, EquipmentController.TryEquip(inventory, equipment, worn));

            Assert.AreSame(worn, equipment.Get(EquipmentSlot.head));
            Assert.AreEqual(instanceId, equipment.GetInstanceId(EquipmentSlot.head), "The item keeps its identity.");
            Assert.IsFalse(inventory.Contains(worn));
            AssertOwnedOnce(worn);
            Assert.IsNull(equipment.Get(EquipmentSlot.chest), "Other slots are untouched.");
        }

        [Test]
        public void Equip_GoesToTheSlotTheArmorDeclares()
        {
            foreach (ArmorData armor in new[] { helmet, breastplate, gauntlets, greaves, boots })
            {
                Assert.AreEqual(EquipResult.Success, EquipmentController.TryEquip(inventory, equipment, Give(armor)));
                Assert.AreEqual(armor, equipment.Get(armor.Slot).SourceAsset);
            }

            Assert.AreEqual(5, equipment.Equipped.Count());
        }

        [Test]
        public void Equip_ReplacingArmor_ReturnsThePreviousItemToTheInventory()
        {
            Item first = Give(helmet);
            Item second = Give(otherHelmet);
            uint firstId = (uint)inventory.GetInstanceId(first).Value;
            EquipmentController.TryEquip(inventory, equipment, first);

            Assert.AreEqual(EquipResult.Success, EquipmentController.TryEquip(inventory, equipment, second));

            Assert.AreSame(second, equipment.Get(EquipmentSlot.head));
            Assert.IsTrue(inventory.Contains(first));
            Assert.AreEqual(firstId, (uint)inventory.GetInstanceId(first).Value, "The returned item keeps its identity.");
            Assert.IsFalse(inventory.Contains(second));
            AssertOwnedOnce(first);
            AssertOwnedOnce(second);
        }

        [Test]
        public void Equip_ReplacingArmor_CanUseTheRoomTheNewItemLeaves()
        {
            // A 2x2 inventory holding one 2x2 breastplate has no free cell, but equipping the other breastplate frees four.
            SetInventorySize(inventory, new Vector2Int(2, 2));
            ArmorData spare = Armor(EquipmentSlot.chest, new Vector2Int(2, 2));
            DefinitionRegistry.Instance = ItemTestRegistry.With(new Item[] { helmet, otherHelmet, breastplate, gauntlets, greaves, boots, spare });
            created.Add(DefinitionRegistry.Instance);
            Item worn = Give(breastplate);
            EquipmentController.TryEquip(inventory, equipment, worn);
            Item replacement = Give(spare);

            Assert.AreEqual(EquipResult.Success, EquipmentController.TryEquip(inventory, equipment, replacement));

            Assert.AreSame(replacement, equipment.Get(EquipmentSlot.chest));
            Assert.IsTrue(inventory.Contains(worn));
            AssertOwnedOnce(worn);
            AssertOwnedOnce(replacement);
        }

        [Test]
        public void Equip_ReplacingArmor_WithNoRoomForThePreviousItem_ChangesNothing()
        {
            // The worn breastplate needs a free 2x2 block. Once the one-cell replacement leaves, a filler item still blocks every such block.
            SetInventorySize(inventory, new Vector2Int(2, 2));
            ArmorData small = Armor(EquipmentSlot.chest, Vector2Int.one);
            DefinitionRegistry.Instance = ItemTestRegistry.With(new Item[] { helmet, otherHelmet, breastplate, gauntlets, greaves, boots, small });
            created.Add(DefinitionRegistry.Instance);
            Item worn = Give(breastplate);
            EquipmentController.TryEquip(inventory, equipment, worn);
            Item replacement = Give(small);
            Give(helmet);
            log.Clear();
            int changes = 0;
            equipment.Changed += () => changes++;
            int inventoryChanges = 0;
            inventory.Changed += () => inventoryChanges++;

            Assert.AreEqual(EquipResult.NoRoomInInventory, EquipmentController.TryEquip(inventory, equipment, replacement));

            Assert.AreSame(worn, equipment.Get(EquipmentSlot.chest));
            Assert.IsTrue(inventory.Contains(replacement));
            AssertOwnedOnce(worn);
            AssertOwnedOnce(replacement);
            Assert.AreEqual(0, changes + inventoryChanges, "A refused request is invisible.");
            Assert.IsEmpty(log);
        }

        [Test]
        public void Unequip_ReturnsTheItemToTheInventory_AndEmptiesTheSlot()
        {
            Item worn = Give(helmet);
            uint instanceId = (uint)inventory.GetInstanceId(worn).Value;
            EquipmentController.TryEquip(inventory, equipment, worn);

            Assert.AreEqual(EquipResult.Success, EquipmentController.TryUnequip(inventory, equipment, EquipmentSlot.head));

            Assert.IsNull(equipment.Get(EquipmentSlot.head));
            Assert.IsTrue(inventory.Contains(worn));
            Assert.AreEqual(instanceId, (uint)inventory.GetInstanceId(worn).Value);
            AssertOwnedOnce(worn);
        }

        [Test]
        public void Unequip_WithAFullInventory_IsRefused_AndTheItemStaysWorn()
        {
            SetInventorySize(inventory, new Vector2Int(1, 1));
            Item worn = Give(helmet);
            EquipmentController.TryEquip(inventory, equipment, worn);
            Give(boots);

            Assert.AreEqual(EquipResult.NoRoomInInventory, EquipmentController.TryUnequip(inventory, equipment, EquipmentSlot.head));

            Assert.AreSame(worn, equipment.Get(EquipmentSlot.head));
            AssertOwnedOnce(worn);
        }

        [Test]
        public void Unequip_AnEmptySlot_IsRefused()
        {
            Assert.AreEqual(EquipResult.SlotEmpty, EquipmentController.TryUnequip(inventory, equipment, EquipmentSlot.feet));
            Assert.AreEqual(EquipResult.SlotEmpty, EquipmentController.TryUnequip(inventory, equipment, EquipmentSlot.mainHand), "Only armor slots are handled here.");
        }

        [Test]
        public void ItemsThatAreNotArmor_AreRefused_AndStayInTheInventory()
        {
            Item plain = ScriptableObject.CreateInstance<Item>();
            WeaponData sword = ScriptableObject.CreateInstance<WeaponData>();
            created.Add(plain);
            created.Add(sword);
            DefinitionRegistry.Instance = ItemTestRegistry.With(new Item[] { helmet, plain, sword });
            created.Add(DefinitionRegistry.Instance);
            Item plainCopy = plain.CloneInstance();
            Item swordCopy = sword.CloneInstance();
            typeof(Item).GetField("<ItemSize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(plainCopy, Vector2Int.one);
            typeof(Item).GetField("<ItemSize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(swordCopy, Vector2Int.one);
            inventory.AddItem(plainCopy);
            inventory.AddItem(swordCopy);

            Assert.AreEqual(EquipResult.NotArmor, EquipmentController.TryEquip(inventory, equipment, plainCopy));
            Assert.AreEqual(EquipResult.NotArmor, EquipmentController.TryEquip(inventory, equipment, swordCopy));

            Assert.IsTrue(inventory.Contains(plainCopy));
            Assert.IsTrue(inventory.Contains(swordCopy));
            Assert.IsEmpty(equipment.Equipped);
        }

        [Test]
        public void ArmorDeclaringANonArmorSlot_IsRefused()
        {
            ArmorData misconfigured = Armor(EquipmentSlot.mainHand, Vector2Int.one);
            DefinitionRegistry.Instance = ItemTestRegistry.With(new Item[] { misconfigured });
            created.Add(DefinitionRegistry.Instance);
            Item copy = Give(misconfigured);

            Assert.AreEqual(EquipResult.NotArmor, EquipmentController.TryEquip(inventory, equipment, copy));
            Assert.IsEmpty(equipment.Equipped);
            Assert.IsTrue(inventory.Contains(copy));
        }

        [Test]
        public void Equip_AnItemThatIsNotInTheInventory_IsRefused()
        {
            Item stranger = helmet.CloneInstance();

            Assert.AreEqual(EquipResult.ItemNotInInventory, EquipmentController.TryEquip(inventory, equipment, stranger));
            Assert.AreEqual(EquipResult.NoItem, EquipmentController.TryEquip(inventory, equipment, null));
            Assert.AreEqual(EquipResult.NoEquipment, EquipmentController.TryEquip(inventory, null, Give(helmet)));
            Assert.AreEqual(EquipResult.NoEquipment, EquipmentController.TryEquip(null, equipment, stranger));
            Assert.IsEmpty(equipment.Equipped);
            Object.DestroyImmediate(stranger);
        }

        [Test]
        public void Equip_AnItemFromAnotherInventory_IsRefused()
        {
            GameObject chestObject = new GameObject("Chest");
            Inventory chest = chestObject.AddComponent<Inventory>();
            SetInventorySize(chest, new Vector2Int(2, 2));
            Item loot = helmet.CloneInstance();
            chest.AddItem(loot);

            Assert.AreEqual(EquipResult.ItemNotInInventory, EquipmentController.TryEquip(inventory, equipment, loot));
            Assert.IsTrue(chest.Contains(loot));
            Assert.IsEmpty(equipment.Equipped);
            Object.DestroyImmediate(chestObject);
            Object.DestroyImmediate(loot);
        }

        [Test]
        public void Validate_ChangesNothing()
        {
            Item worn = Give(helmet);
            Item replacement = Give(otherHelmet);
            EquipmentController.TryEquip(inventory, equipment, worn);
            log.Clear();

            Assert.AreEqual(EquipResult.Success, EquipmentController.ValidateEquip(inventory, equipment, replacement));
            Assert.AreEqual(EquipResult.Success, EquipmentController.ValidateUnequip(inventory, equipment, EquipmentSlot.head));

            Assert.AreSame(worn, equipment.Get(EquipmentSlot.head));
            Assert.IsTrue(inventory.Contains(replacement));
            Assert.IsEmpty(log);
        }

        [Test]
        public void RequestEquip_AndRequestUnequip_RunTheSameRulesOnTheServer()
        {
            Item worn = Give(gauntlets);

            Assert.IsTrue(equipment.RequestEquip(worn));
            Assert.AreSame(worn, equipment.Get(EquipmentSlot.hands));

            Assert.IsTrue(equipment.RequestUnequip(EquipmentSlot.hands));
            Assert.IsNull(equipment.Get(EquipmentSlot.hands));
            Assert.IsTrue(inventory.Contains(worn));
            Assert.IsFalse(equipment.RequestUnequip(EquipmentSlot.hands), "Nothing left to take off.");
        }

        [Test]
        public void Changed_IsRaisedOncePerAcceptedChange_AndNeverForARefusedOne()
        {
            int changes = 0;
            equipment.Changed += () => changes++;
            Item worn = Give(helmet);
            Item replacement = Give(otherHelmet);

            EquipmentController.TryEquip(inventory, equipment, worn);
            Assert.AreEqual(1, changes, "Equipping.");

            EquipmentController.TryEquip(inventory, equipment, replacement);
            Assert.AreEqual(2, changes, "Replacing.");

            EquipmentController.TryUnequip(inventory, equipment, EquipmentSlot.head);
            Assert.AreEqual(3, changes, "Unequipping.");

            EquipmentController.TryUnequip(inventory, equipment, EquipmentSlot.head);
            EquipmentController.TryEquip(inventory, equipment, helmet.CloneInstance());
            Assert.AreEqual(3, changes, "Refusals.");
        }

        [Test]
        public void GameplayEvents_ReportEquippingReplacingAndUnequipping_WithoutAnyUi()
        {
            Item worn = Give(helmet);
            Item replacement = Give(otherHelmet);
            uint wornId = (uint)inventory.GetInstanceId(worn).Value;
            uint replacementId = (uint)inventory.GetInstanceId(replacement).Value;

            EquipmentController.TryEquip(inventory, equipment, worn);
            EquipmentController.TryEquip(inventory, equipment, replacement);
            EquipmentController.TryUnequip(inventory, equipment, EquipmentSlot.head);

            CollectionAssert.AreEqual(new[]
            {
                "equipped " + wornId + " in head",
                "unequipped " + wornId + " from head (Replaced)",
                "equipped " + replacementId + " in head",
                "unequipped " + replacementId + " from head (Removed)"
            }, log);
        }

        // --- Paper doll ---

        private PaperDollUI CreateDoll()
        {
            GameObject canvas = new GameObject("Doll", typeof(RectTransform));
            created.Add(canvas);
            PaperDollUI doll = canvas.AddComponent<PaperDollUI>();
            doll.Bind(equipment);
            return doll;
        }

        private Item GiveWithIcon(ArmorData template)
        {
            Texture2D texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
            created.Add(texture);
            created.Add(sprite);
            typeof(Item).GetField("<Icon>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(template, sprite);
            return Give(template);
        }

        [Test]
        public void PaperDoll_ShowsAllFiveSlotsEmpty()
        {
            PaperDollUI doll = CreateDoll();

            foreach (EquipmentSlot slot in ArmorData.ArmorSlots)
            {
                EquipmentSlotView view = doll.GetView(slot);
                Assert.IsNotNull(view, slot + " slot");
                Assert.IsNull(view.DisplayedItem);
                Assert.IsTrue(view.ShowsEmptyIndicator);
            }

            Assert.IsNull(doll.GetView(EquipmentSlot.mainHand), "Weapons have no paper doll slot.");
        }

        [Test]
        public void PaperDoll_ArrangesSlotsAsABody()
        {
            PaperDollUI doll = CreateDoll();
            Vector2 Position(EquipmentSlot slot) => ((RectTransform)doll.GetView(slot).transform).anchoredPosition;

            // The wireframe's body: head over chest, hands to the left of the chest, legs to its right, feet below the chest.
            Assert.AreEqual(Position(EquipmentSlot.chest).x, Position(EquipmentSlot.head).x, "Head over chest.");
            Assert.AreEqual(Position(EquipmentSlot.chest).x, Position(EquipmentSlot.feet).x, "Feet under chest.");
            Assert.Greater(Position(EquipmentSlot.head).y, Position(EquipmentSlot.chest).y);
            Assert.Greater(Position(EquipmentSlot.chest).y, Position(EquipmentSlot.feet).y);
            Assert.Less(Position(EquipmentSlot.hands).x, Position(EquipmentSlot.chest).x, "Hands beside the chest.");
            Assert.Greater(Position(EquipmentSlot.legs).x, Position(EquipmentSlot.chest).x, "Legs on the other side.");
            Assert.AreEqual(Position(EquipmentSlot.chest).y, Position(EquipmentSlot.hands).y);
        }

        [Test]
        public void PaperDoll_ReflectsEquippingReplacingAndUnequippingImmediately()
        {
            PaperDollUI doll = CreateDoll();
            Item first = GiveWithIcon(helmet);
            Item second = GiveWithIcon(otherHelmet);
            EquipmentSlotView head = doll.GetView(EquipmentSlot.head);

            EquipmentController.TryEquip(inventory, equipment, first);
            Assert.AreSame(first, head.DisplayedItem);
            Assert.IsFalse(head.ShowsEmptyIndicator);

            EquipmentController.TryEquip(inventory, equipment, second);
            Assert.AreSame(second, head.DisplayedItem, "Replacing updates the slot.");

            EquipmentController.TryUnequip(inventory, equipment, EquipmentSlot.head);
            Assert.IsNull(head.DisplayedItem);
            Assert.IsTrue(head.ShowsEmptyIndicator);
        }

        [Test]
        public void PaperDoll_ClickingAWornSlot_UnequipsIt()
        {
            PaperDollUI doll = CreateDoll();
            Item worn = GiveWithIcon(boots);
            EquipmentController.TryEquip(inventory, equipment, worn);

            doll.GetView(EquipmentSlot.feet).Click();

            Assert.IsNull(equipment.Get(EquipmentSlot.feet));
            Assert.IsTrue(inventory.Contains(worn));
            Assert.IsTrue(doll.GetView(EquipmentSlot.feet).ShowsEmptyIndicator);
        }

        [Test]
        public void PaperDoll_ClickingAnEmptySlot_DoesNothing()
        {
            PaperDollUI doll = CreateDoll();
            int changes = 0;
            equipment.Changed += () => changes++;

            doll.GetView(EquipmentSlot.chest).Click();

            Assert.AreEqual(0, changes);
        }

        [Test]
        public void PaperDoll_WhenUnbound_ShowsNothing_AndEquipmentStillWorks()
        {
            PaperDollUI doll = CreateDoll();
            doll.Bind(null);
            Item worn = Give(helmet);

            EquipmentController.TryEquip(inventory, equipment, worn);

            Assert.IsNull(doll.GetView(EquipmentSlot.head).DisplayedItem);
            Assert.AreSame(worn, equipment.Get(EquipmentSlot.head), "Equipment does not depend on any view.");
        }

        [Test]
        public void PaperDoll_BoundLate_ShowsWhatIsAlreadyWorn()
        {
            Item worn = Give(gauntlets);
            EquipmentController.TryEquip(inventory, equipment, worn);

            PaperDollUI doll = CreateDoll();

            Assert.AreSame(worn, doll.GetView(EquipmentSlot.hands).DisplayedItem);
        }
    }
}
