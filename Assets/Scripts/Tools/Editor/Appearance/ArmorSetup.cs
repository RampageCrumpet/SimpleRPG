using SimpleRPG.Abilities;
using SimpleRPG.Appearance;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.AppearanceTools
{
    /// <summary>
    /// Turns the armor art into game content: an <see cref="ArmorData"/> item for each piece and a <see cref="BodyPartDefinition"/> for each
    /// skinned mesh in its prefab, marked as the look of that item, so wearing the item dresses the character (see <c>EquipmentAppearance</c>).
    /// <para>
    /// Safe to run again. A part is updated in place from the art, so ids and references survive a re-export. An item that already exists is
    /// <b>never</b> changed: once it is made, its numbers (size, armor rating) belong to the asset and the designer, and this only creates
    /// what is missing. The starting numbers below are a seed to tune from, not a decision.
    /// </para>
    /// A piece it cannot convert (no skinned mesh, a hand or foot with no left or right in its name) is reported and left for hand work.
    /// </summary>
    public static class ArmorSetup
    {
        private const string ArtFolder = "Assets/Art/3d Models/Items";
        public const string ItemsFolder = "Assets/Items/Armor";
        public const string PartsFolder = "Assets/Art/BodyParts/Armor";

        /// <summary>One piece of armor as the seed describes it.</summary>
        public readonly struct Piece
        {
            public Piece(string folder, string displayName, EquipmentSlot slot, int width, int height, float armorRating)
            {
                Folder = folder;
                DisplayName = displayName;
                Slot = slot;
                Size = new Vector2Int(width, height);
                ArmorRating = armorRating;
            }

            /// <summary>The art folder, and the name of its prefab and icon.</summary>
            public string Folder { get; }

            public string DisplayName { get; }

            public EquipmentSlot Slot { get; }

            public Vector2Int Size { get; }

            /// <summary>What wearing it adds to Armor Rating: a starting point, tuned in the asset afterwards.</summary>
            public float ArmorRating { get; }
        }

        /// <summary>The seed. Legs and feet join it when their art exists.</summary>
        public static readonly Piece[] Pieces =
        {
            new Piece("PaddedCoif", "Padded Coif", EquipmentSlot.head, 2, 2, 5f),
            new Piece("KettleHelm", "Kettle Helm", EquipmentSlot.head, 2, 2, 12f),
            new Piece("VisoredSallet", "Visored Sallet", EquipmentSlot.head, 2, 2, 18f),
            new Piece("LinenTunic", "Linen Tunic", EquipmentSlot.chest, 2, 2, 2f),
            new Piece("Gambeson", "Gambeson", EquipmentSlot.chest, 3, 2, 10f),
            new Piece("MailHauberk", "Mail Hauberk", EquipmentSlot.chest, 3, 3, 28f),
            new Piece("PlateArmor", "Plate Armor", EquipmentSlot.chest, 3, 3, 45f),
            new Piece("LeatherGloves", "Leather Gloves", EquipmentSlot.hands, 2, 1, 3f),
            new Piece("ReinforcedGloves", "Reinforced Gloves", EquipmentSlot.hands, 2, 2, 6f),
            new Piece("PlateGauntlets", "Plate Gauntlets", EquipmentSlot.hands, 2, 2, 12f),
        };

        /// <summary>
        /// Which place on the body a mesh in a piece of armor covers. Head, chest and legs have one place each; hands and feet are left or right
        /// by the mesh's name (ending in _L, .L, _R or .R, or containing Left or Right). False if it cannot be told.
        /// </summary>
        public static bool TryPartSlot(EquipmentSlot equipmentSlot, string meshName, out BodyPartSlot slot)
        {
            slot = default;
            switch (equipmentSlot)
            {
                case EquipmentSlot.head: slot = BodyPartSlot.Head; return true;
                case EquipmentSlot.chest: slot = BodyPartSlot.Torso; return true;
                case EquipmentSlot.legs: slot = BodyPartSlot.Legs; return true;
                case EquipmentSlot.hands: return TrySide(meshName, BodyPartSlot.HandLeft, BodyPartSlot.HandRight, out slot);
                case EquipmentSlot.feet: return TrySide(meshName, BodyPartSlot.FootLeft, BodyPartSlot.FootRight, out slot);
                default: return false;
            }
        }

        private static bool TrySide(string meshName, BodyPartSlot left, BodyPartSlot right, out BodyPartSlot slot)
        {
            string name = meshName ?? string.Empty;
            bool isLeft = name.IndexOf("left", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.EndsWith("_L") || name.EndsWith(".L");
            bool isRight = name.IndexOf("right", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.EndsWith("_R") || name.EndsWith(".R");

            // A name that says both, or neither, does not say which.
            slot = left;
            if (isLeft == isRight)
            {
                return false;
            }

            slot = isLeft ? left : right;
            return true;
        }

        [MenuItem("SimpleRPG/Items/Build Armor Items And Parts")]
        public static void Build()
        {
            Directory.CreateDirectory(ItemsFolder);
            Directory.CreateDirectory(PartsFolder);

            int itemsMade = 0;
            int partsWritten = 0;
            var problems = new List<string>();
            foreach (Piece piece in Pieces)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtFolder}/{piece.Folder}/{piece.Folder}.prefab");
                if (prefab == null)
                {
                    problems.Add($"{piece.Folder}: no prefab at {ArtFolder}/{piece.Folder}/{piece.Folder}.prefab.");
                    continue;
                }

                ArmorData item = LoadOrCreateItem(piece, problems, out bool created);
                if (created) itemsMade++;

                SkinnedMeshRenderer[] meshes = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (meshes.Length == 0)
                {
                    problems.Add($"{piece.Folder}: its prefab has no skinned mesh (a rigid piece, such as one with a moving part, needs its part made by hand), so the item has no look yet.");
                    continue;
                }

                foreach (SkinnedMeshRenderer mesh in meshes)
                {
                    if (WritePart(piece, item, mesh, problems)) partsWritten++;
                }
            }

            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("SimpleRPG/Definitions/Rebuild Definition Registry");
            Report(itemsMade, partsWritten, problems);
        }

        private static ArmorData LoadOrCreateItem(Piece piece, List<string> problems, out bool created)
        {
            string path = $"{ItemsFolder}/{piece.Folder}.asset";
            ArmorData item = AssetDatabase.LoadAssetAtPath<ArmorData>(path);
            created = item == null;
            if (!created)
            {
                // Authored numbers are the designer's from here on: an existing item is left exactly as it is.
                return item;
            }

            item = ScriptableObject.CreateInstance<ArmorData>();
            item.EnsureId();
            AssetDatabase.CreateAsset(item, path);

            string iconPath = $"{ArtFolder}/{piece.Folder}/{piece.Folder}_Icon.png";
            Sprite icon = LoadSprite(iconPath);
            if (icon == null) problems.Add($"{piece.Folder}: no icon at {iconPath}; the item has none.");

            var serialized = new SerializedObject(item);
            serialized.FindProperty("slot").intValue = (int)piece.Slot;
            serialized.FindProperty("<ItemName>k__BackingField").stringValue = piece.DisplayName;
            serialized.FindProperty("<ItemSize>k__BackingField").vector2IntValue = piece.Size;
            serialized.FindProperty("<Icon>k__BackingField").objectReferenceValue = icon;

            SerializedProperty grants = serialized.FindProperty("grants");
            grants.arraySize = 1;
            SerializedProperty grant = grants.GetArrayElementAtIndex(0);
            grant.FindPropertyRelative("AttributeKey").stringValue = DerivedStats.ArmorRating.Key;
            grant.FindPropertyRelative("Layer").intValue = (int)ModifierLayer.Add;
            grant.FindPropertyRelative("Value").floatValue = piece.ArmorRating;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        // An icon has to be imported as a sprite to be shown in the inventory; the importer is set the first time.
        private static Sprite LoadSprite(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static bool WritePart(Piece piece, ArmorData item, SkinnedMeshRenderer mesh, List<string> problems)
        {
            if (!TryPartSlot(piece.Slot, mesh.name, out BodyPartSlot slot))
            {
                problems.Add($"{piece.Folder}: cannot tell which side of the body '{mesh.name}' is for ({piece.Slot}); name it with a trailing _L or _R.");
                return false;
            }

            if (mesh.sharedMesh == null || mesh.bones == null || mesh.bones.Length == 0 || mesh.rootBone == null)
            {
                problems.Add($"{piece.Folder}: '{mesh.name}' has no mesh, bones or root bone, so it cannot be bound to the rig.");
                return false;
            }

            string path = $"{PartsFolder}/{piece.Folder}_{mesh.name}.asset";
            BodyPartDefinition part = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(path);
            if (part == null)
            {
                part = ScriptableObject.CreateInstance<BodyPartDefinition>();
                part.EnsureId();
                AssetDatabase.CreateAsset(part, path);
            }

            part.Configure(slot, mesh.sharedMesh, mesh.sharedMaterials, mesh.bones.Select(b => b != null ? b.name : "").ToArray(), mesh.rootBone.name,
                mesh.localBounds, "", Vector3.zero, Quaternion.identity, Vector3.one);
            part.Dress(item);
            string problem = part.Problem();
            if (problem != null) problems.Add($"{piece.Folder}: {problem}");
            EditorUtility.SetDirty(part);
            return true;
        }

        private static void Report(int itemsMade, int partsWritten, List<string> problems)
        {
            var text = new StringBuilder();
            text.AppendLine($"Armor: {itemsMade} new item(s), {partsWritten} part(s) written or updated, {problems.Count} to look at.");
            foreach (string problem in problems) text.AppendLine("  - " + problem);
            Debug.Log("[Armor] " + text);
            EditorUtility.DisplayDialog("Armor items and parts", text.ToString(), "OK");
        }
    }
}
