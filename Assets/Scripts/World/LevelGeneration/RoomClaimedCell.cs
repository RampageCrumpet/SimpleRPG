using UnityEngine;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// Marks a cell outside a room's rectangle that the room still needs, such as an alcove that sticks out through a wall.
    /// Place the object anywhere inside the cell. The packer stores the cell on the room, and the generator keeps every other room out of it.
    /// The object's own position does not count towards the room's size.
    /// </summary>
    public class RoomClaimedCell : MonoBehaviour
    {
    }
}
