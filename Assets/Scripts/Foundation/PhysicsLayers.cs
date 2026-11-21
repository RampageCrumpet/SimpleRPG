using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// Layer masks for unfiltered physics queries. The <c>ClothCollision</c> layer holds the colliders worn garments' Cloth collides with. The collision matrix keeps it
    /// from touching anything, but raycasts and overlap queries ignore the matrix, so a query that does not name its layers must use one of these masks.
    /// Queries that already name their layers (hit boxes, drops, senses) cannot reach the cloth colliders and need nothing.
    /// </summary>
    public static class PhysicsLayers
    {
        public const string ClothCollisionName = "ClothCollision";

        /// <summary>The layer index of <see cref="ClothCollisionName"/>, or -1 if the project has no such layer.</summary>
        public static int ClothCollision => LayerMask.NameToLayer(ClothCollisionName);

        /// <summary>Every layer except the cloth colliders: the mask for a query that means "everything".</summary>
        public static int AllExceptCloth => Without(~0);

        /// <summary>What <see cref="Physics.Raycast(Ray, out RaycastHit)"/> hits by default (everything except Ignore Raycast), without the cloth colliders.</summary>
        public static int DefaultRaycastExceptCloth => Without(Physics.DefaultRaycastLayers);

        private static int Without(int mask)
        {
            int layer = ClothCollision;
            return layer < 0 ? mask : mask & ~(1 << layer);
        }
    }
}
