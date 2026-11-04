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
    /// A piece it cannot convert (a hand or foot with no left or right in its name, a rigid piece that is not a helmet) is reported and left for hand work.
    /// </summary>
    public static class ArmorSetup
    {
        private const string ArtFolder = "Assets/Art/3d Models/Items";
        public const string ItemsFolder = "Assets/Items/Armor";
        public const string PartsFolder = "Assets/Art/BodyParts/Armor";
        private const string HeadBone = "Head";

        /// <summary>One piece of armor as the seed describes it.</summary>
        public readonly struct Piece
        {
            /// <summary>A piece of the default size for its slot (see <see cref="TryDefaultSize"/>).</summary>
            public Piece(string folder, string displayName, EquipmentSlot slot, float armorRating)
                : this(folder, displayName, slot, TryDefaultSize(slot, out Vector2Int size) ? size : Vector2Int.one, armorRating)
            {
            }

            /// <summary>A piece with a size of its own: an item's size is its own data, and the slot's default is only where it starts.</summary>
            public Piece(string folder, string displayName, EquipmentSlot slot, Vector2Int size, float armorRating)
            {
                Folder = folder;
                DisplayName = displayName;
                Slot = slot;
                Size = size;
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

        /// <summary>
        /// The size a new piece worn in a slot starts with, as in the games this one follows: 2 by 2 for head, hands and feet, 2 wide and 3 tall
        /// for chest and legs. This is only a default for the tool: nothing enforces it. An item's size is its own data in its asset, a piece in
        /// the seed may give its own, and a larger or smaller piece in any slot is fine.
        /// </summary>
        public static bool TryDefaultSize(EquipmentSlot slot, out Vector2Int size)
        {
            switch (slot)
            {
                case EquipmentSlot.head:
                case EquipmentSlot.hands:
                case EquipmentSlot.feet:
                    size = new Vector2Int(2, 2);
                    return true;
                case EquipmentSlot.chest:
                case EquipmentSlot.legs:
                    size = new Vector2Int(2, 3);
                    return true;
                default:
                    size = default;
                    return false;
            }
        }

        /// <summary>The seed. Legs and feet join it when their art exists.</summary>
        public static readonly Piece[] Pieces =
        {
            new Piece("PaddedCoif", "Padded Coif", EquipmentSlot.head, 5f),
            new Piece("KettleHelm", "Kettle Helm", EquipmentSlot.head, 12f),
            new Piece("VisoredSallet", "Visored Sallet", EquipmentSlot.head, 18f),
            new Piece("LinenTunic", "Linen Tunic", EquipmentSlot.chest, 2f),
            new Piece("Gambeson", "Gambeson", EquipmentSlot.chest, 10f),
            new Piece("MailHauberk", "Mail Hauberk", EquipmentSlot.chest, 28f),
            new Piece("PlateArmor", "Plate Armor", EquipmentSlot.chest, 45f),
            new Piece("LeatherGloves", "Leather Gloves", EquipmentSlot.hands, 3f),
            new Piece("ReinforcedGloves", "Reinforced Gloves", EquipmentSlot.hands, 6f),
            new Piece("PlateGauntlets", "Plate Gauntlets", EquipmentSlot.hands, 12f),
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
                case EquipmentSlot.head: slot = BodyPartSlot.Headgear; return true;
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
                    // A rigid piece (a helmet that is meshes on a pivot, not skinned to the rig) is combined into one part on the head bone.
                    if (WriteRigidPart(piece, item, prefab, problems)) partsWritten++;
                    continue;
                }

                // A character shows one part per slot, so the meshes of a slot (plate with its mail undercoat) become the pieces of one part.
                var bySlot = new Dictionary<BodyPartSlot, List<(SkinnedMeshRenderer mesh, BodyPartPiece piece)>>();
                foreach (SkinnedMeshRenderer mesh in meshes)
                {
                    if (!TryMakePiece(piece, mesh, problems, out BodyPartSlot slot, out BodyPartPiece made))
                    {
                        continue;
                    }

                    if (!bySlot.TryGetValue(slot, out var group)) bySlot[slot] = group = new List<(SkinnedMeshRenderer, BodyPartPiece)>();
                    group.Add((mesh, made));
                }

                var written = new HashSet<BodyPartDefinition>();
                foreach (var pair in bySlot)
                {
                    BodyPartDefinition part = WritePart(piece, item, pair.Key, pair.Value, problems);
                    if (part == null) continue;
                    written.Add(part);
                    partsWritten++;
                }

                DetachStaleParts(piece, item, written, problems);
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

        /// <summary>
        /// The bone names, out of those given, that the body rig does not have. Empty means the art can be bound to the body.
        /// </summary>
        public static List<string> BonesMissingFromRig(IEnumerable<string> boneNames)
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(HumanMaleSetup.RigPrefabPath);
            var have = new HashSet<string>(rig != null ? rig.GetComponentsInChildren<Transform>(true).Select(t => t.name) : Enumerable.Empty<string>());
            return boneNames.Where(name => !have.Contains(name)).Distinct().ToList();
        }

        // Validates one skinned mesh and describes it as a piece. False (with a problem reported) if it cannot be worn.
        private static bool TryMakePiece(Piece piece, SkinnedMeshRenderer mesh, List<string> problems, out BodyPartSlot slot, out BodyPartPiece made)
        {
            made = null;
            if (!TryPartSlot(piece.Slot, mesh.name, out slot))
            {
                problems.Add($"{piece.Folder}: cannot tell which side of the body '{mesh.name}' is for ({piece.Slot}); name it with a trailing _L or _R.");
                return false;
            }

            if (mesh.sharedMesh == null || mesh.bones == null || mesh.bones.Length == 0 || mesh.rootBone == null)
            {
                problems.Add($"{piece.Folder}: '{mesh.name}' has no mesh, bones or root bone, so it cannot be bound to the rig.");
                return false;
            }

            // Art skinned to bones the body does not have cannot be worn. Say so here, and do not write a part the game would throw on.
            string[] boneNames = mesh.bones.Select(b => b != null ? b.name : "").ToArray();
            List<string> missingBones = BonesMissingFromRig(boneNames);
            if (missingBones.Count > 0)
            {
                problems.Add($"{piece.Folder}: '{mesh.name}' is skinned to bones the body rig does not have ({string.Join(", ", missingBones)}). Export it from the authoring scene with Authoring/export_armor.py.");
                return false;
            }

            made = new BodyPartPiece(mesh.sharedMesh, mesh.sharedMaterials, boneNames, mesh.rootBone.name, mesh.localBounds,
                "", Vector3.zero, Quaternion.identity, Vector3.one);
            return true;
        }

        // One part for the slot: the first mesh is the primary piece and names the asset (so an existing single-mesh part keeps its id and
        // references), and any further meshes are its additional pieces.
        private static BodyPartDefinition WritePart(Piece piece, ArmorData item, BodyPartSlot slot, List<(SkinnedMeshRenderer mesh, BodyPartPiece piece)> group, List<string> problems)
        {
            string path = $"{PartsFolder}/{piece.Folder}_{group[0].mesh.name}.asset";
            BodyPartDefinition part = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(path);
            if (part == null)
            {
                part = ScriptableObject.CreateInstance<BodyPartDefinition>();
                part.EnsureId();
                AssetDatabase.CreateAsset(part, path);
            }

            BodyPartPiece primary = group[0].piece;
            part.Configure(slot, primary.Mesh, primary.Materials, primary.BoneNames, primary.RootBoneName, primary.LocalBounds,
                "", Vector3.zero, Quaternion.identity, Vector3.one);
            part.SetAdditionalPieces(group.Skip(1).Select(g => g.piece).ToArray());
            part.Dress(item);
            string problem = part.Problem();
            if (problem != null) problems.Add($"{piece.Folder}: {problem}");
            EditorUtility.SetDirty(part);
            return part;
        }

        // A part an earlier run wrote for this item that this run did not (an old one-mesh-per-part export, now merged into another part) would
        // still dress the item and fight the merged part for the slot. It is detached from the item, not deleted, so nothing referencing it breaks.
        private static void DetachStaleParts(Piece piece, ArmorData item, HashSet<BodyPartDefinition> written, List<string> problems)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:BodyPartDefinition", new[] { PartsFolder }))
            {
                var part = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (part == null || part.DressesItem != item || written.Contains(part)) continue;

                part.Dress(null);
                EditorUtility.SetDirty(part);
                problems.Add($"{piece.Folder}: '{part.name}' is no longer part of this item's look (its mesh is now a piece of another part) and was detached from it. It can be deleted.");
            }
        }

        // A piece that is not skinned: every mesh in its prefab is combined, in the prefab's own space, into one mesh worn rigidly on the head
        // bone. Parts are one per slot, so a shell and a visor have to be one part; the visor is as the prefab has it (closed). The pose of the
        // prefab relative to the head bone is taken the way the eyebrows are: the art is assumed to share the body model's space. If the piece
        // sits wrong on the head, adjust the part's attach position and rotation in its asset.
        private static bool WriteRigidPart(Piece piece, ArmorData item, GameObject prefab, List<string> problems)
        {
            if (piece.Slot != EquipmentSlot.head)
            {
                problems.Add($"{piece.Folder}: its prefab has no skinned mesh, and only head pieces can be worn rigidly so far; its part has to be made by hand.");
                return false;
            }

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null).ToArray();
            if (filters.Length == 0)
            {
                problems.Add($"{piece.Folder}: its prefab has no skinned mesh and no plain meshes, so the item has no look yet.");
                return false;
            }

            GameObject body = AssetDatabase.LoadAssetAtPath<GameObject>(HumanMaleSetup.ModelPath);
            Transform head = body != null ? body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == HeadBone) : null;
            if (head == null)
            {
                problems.Add($"{piece.Folder}: no '{HeadBone}' bone found in {HumanMaleSetup.ModelPath}, so a rigid piece cannot be placed on the head.");
                return false;
            }

            string partPath = $"{PartsFolder}/{piece.Folder}_Rigid.asset";
            string meshPath = $"{PartsFolder}/{piece.Folder}_RigidMesh.asset";
            Mesh combined;
            var materials = new List<Material>();
            try
            {
                var instances = new List<CombineInstance>();
                Matrix4x4 intoPrefab = prefab.transform.worldToLocalMatrix;
                foreach (MeshFilter filter in filters)
                {
                    Material[] shared = filter.GetComponent<MeshRenderer>().sharedMaterials;
                    for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                    {
                        instances.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = intoPrefab * filter.transform.localToWorldMatrix });
                        materials.Add(sub < shared.Length ? shared[sub] : null);
                    }
                }

                combined = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                bool isNew = combined == null;
                if (isNew) combined = new Mesh { name = piece.Folder + "_RigidMesh" };
                else combined.Clear();

                combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                combined.CombineMeshes(instances.ToArray(), false, true);
                combined.RecalculateBounds();
                if (isNew) AssetDatabase.CreateAsset(combined, meshPath);
                EditorUtility.SetDirty(combined);
            }
            catch (System.Exception e)
            {
                problems.Add($"{piece.Folder}: could not combine its meshes ({e.Message}). If its model is not readable, turn on Read/Write in the model's import settings.");
                return false;
            }

            BodyPartDefinition part = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(partPath);
            if (part == null)
            {
                part = ScriptableObject.CreateInstance<BodyPartDefinition>();
                part.EnsureId();
                AssetDatabase.CreateAsset(part, partPath);
            }

            Matrix4x4 local = head.worldToLocalMatrix;
            part.Configure(BodyPartSlot.Headgear, combined, materials.ToArray(), null, "", combined.bounds, HeadBone, local.GetPosition(), local.rotation, local.lossyScale);
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
