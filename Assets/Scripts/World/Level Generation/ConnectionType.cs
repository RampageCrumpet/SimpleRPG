using UnityEngine;

namespace LevelGeneration
{
    /// <summary>
    /// A kind of door (a gate, a sewer pipe, stairs, ...). Doors only join doors of the same type, so a gate never opens onto a sewer.
    /// A door with no type is a standard door. Types are told apart by asset name, so names must be unique within a roomset.
    /// </summary>
    [CreateAssetMenu(menuName = "World generation/Connection type")]
    public class ConnectionType : ScriptableObject
    {
        /// <summary>
        /// The id the layout generator uses for standard doors, which have no <see cref="ConnectionType"/>.
        /// </summary>
        public const int StandardId = 0;

        /// <summary>
        /// A stable id for this type, the same on every machine. Derived from the asset name, never from object identity.
        /// </summary>
        public int Id => IdFor(name);

        /// <summary>
        /// The id for a door's type, where null means a standard door.
        /// </summary>
        public static int IdFor(ConnectionType type)
        {
            return type == null ? StandardId : type.Id;
        }

        /// <summary>
        /// A stable id for a type name: 32 bit FNV-1a, never <see cref="StandardId"/>.
        /// </summary>
        public static int IdFor(string typeName)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char character in typeName)
                {
                    hash = (hash ^ character) * 16777619u;
                }

                return hash == (uint)StandardId ? 1 : (int)hash;
            }
        }
    }
}
