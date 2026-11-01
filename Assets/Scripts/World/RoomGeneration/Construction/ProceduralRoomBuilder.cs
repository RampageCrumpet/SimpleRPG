using System.Collections.Generic;
using SimpleRPG.LevelGeneration;
using UnityEngine;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// Builds the structure of the procedural rooms of a finished layout. It runs after the layout is final, once per accepted room, so the
    /// layout search never generates geometry. Everything it needs is in the placed room and the layout's seed: the same layout builds the same
    /// plans on every machine, in any order.
    /// </summary>
    public sealed class ProceduralRoomBuilder
    {
        private readonly float cellSize;
        private readonly float floorHeight;
        private static Material fallbackMaterial;

        public ProceduralRoomBuilder(float cellSize, float floorHeight)
        {
            this.cellSize = cellSize;
            this.floorHeight = floorHeight;
        }

        /// <summary>
        /// One entry per procedural room built, in layout order.
        /// </summary>
        public List<(PlacedRoom room, RoomGenerationResult result)> Results { get; } = new List<(PlacedRoom, RoomGenerationResult)>();

        /// <summary>
        /// The context of a placed room, in the room's own unturned frame.
        /// </summary>
        public static RoomGenerationContext ContextFor(Layout layout, PlacedRoom room, ProceduralRoomSettings settings, IRoomGenerator generator, float cellSize, float floorHeight)
        {
            RoomDefinition definition = room.Definition;
            int seed = RoomSeed.ForRoom(layout.Seed, layout.Attempt, room.Id, definition.Index, generator.Id, generator.Version);
            return new RoomGenerationContext(definition.Name, definition.Size, definition.Height, definition.Doors, seed,
                settings.ToGenerationSettings(cellSize, floorHeight), settings.GeneratorSettings());
        }

        /// <summary>
        /// Generates and builds a room's structure if its prefab asks for it. Authored rooms are left alone.
        /// </summary>
        /// <returns> True if the room is procedural (whether or not generation succeeded).</returns>
        public bool Build(Layout layout, PlacedRoom room, RoomBlueprint instance)
        {
            ProceduralRoomSettings settings = instance.GetComponent<ProceduralRoomSettings>();
            if (settings == null)
            {
                return false;
            }

            IRoomGenerator generator = settings.CreateGenerator();
            if (generator == null)
            {
                Debug.LogError($"Procedural room '{instance.name}' names the generator '{settings.GeneratorId}', which does not exist.", instance);
                return true;
            }

            if (room.Definition.ExtraCells.Count > 0)
            {
                Debug.LogError($"Procedural room '{instance.name}' claims cells beyond its rectangle, which generated rooms do not support. It has no generated geometry.", instance);
                return true;
            }

            RoomGenerationContext context = ContextFor(layout, room, settings, generator, cellSize, floorHeight);
            RoomGenerationResult result = RoomGenerationPipeline.Generate(context, generator, settings.Retries);
            Results.Add((room, result));

            foreach (string diagnostic in result.Diagnostics)
            {
                Debug.LogWarning($"Procedural room {room}: {diagnostic}", instance);
            }

            if (!result.Succeeded)
            {
                Debug.LogError($"Procedural room {room} has no geometry, so its connections lead nowhere. {result.Room.Failure}", instance);
                return true;
            }

            if (result.UsedFallback)
            {
                Debug.LogWarning($"Procedural room {room} (seed {context.Seed}) used the fallback open chamber after {settings.GeneratorId} failed.", instance);
            }

            BuildMesh(instance, room, result.Room, settings);
            return true;
        }

        /// <summary>
        /// Constructs the meshes and collider for a plan under the room's root, with the plan's lowest corner where authored geometry's would be.
        /// </summary>
        public GameObject BuildMesh(RoomBlueprint instance, PlacedRoom room, GeneratedRoom plan, ProceduralRoomSettings settings)
        {
            return Construct(instance.transform, plan, settings, room.Definition.OriginOffset, cellSize);
        }

        public static GameObject Construct(Transform parent, GeneratedRoom plan, ProceduralRoomSettings settings, Vector2Int originOffset, float cellSize)
        {
            RoomMeshBuilder.Result built = RoomMeshBuilder.Build(plan.ResolvePieces(), settings.MetresPerUvTile);

            var geometry = new GameObject("Procedural geometry");
            geometry.transform.SetParent(parent, false);
            geometry.transform.localPosition = new Vector3(originOffset.x * cellSize, 0f, originOffset.y * cellSize);
            geometry.isStatic = true;

            geometry.AddComponent<MeshFilter>().sharedMesh = built.Mesh;
            var materials = new Material[built.SubmeshKinds.Count];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = settings.MaterialFor(built.SubmeshKinds[i]) ?? Fallback();
            }

            geometry.AddComponent<MeshRenderer>().sharedMaterials = materials;
            geometry.AddComponent<MeshCollider>().sharedMesh = built.Mesh;
            return geometry;
        }

        private static Material Fallback()
        {
            if (fallbackMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                fallbackMaterial = new Material(shader) { name = "Procedural room fallback" };
            }

            return fallbackMaterial;
        }
    }
}
