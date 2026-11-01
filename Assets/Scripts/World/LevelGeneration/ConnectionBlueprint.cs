using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG
{
    public class ConnectionBlueprint : MonoBehaviour
    {
        /// <summary>
        /// The length of the connection in unity units away from the room it exists on..
        /// </summary>
        [Tooltip("The length of the connection in unity units away from the room it exists on.")]
        [field: SerializeField]
        public float ConnectionLength { get; set; }

        [Tooltip("The width of the connection in unity units.")]
        [field:SerializeField]
        public float ConnectionWidth { get; set; }

        [Tooltip("The object that will be used to seal the connection when it is closed.")]
        [field: SerializeField]
        public GameObject SealingObject;

        [Tooltip("What kind of door this is. Doors only join doors of the same type. Leave empty for a standard door.")]
        public LevelGeneration.ConnectionType Type;

        [Tooltip("Which floor of the room the door stands on, counted up from the room's lowest floor.")]
        [Min(0)]
        public int Level;

        [Tooltip("For a door of a vertical type: true if the door it joins is on a higher floor, false if lower. Ignored for level doors.")]
        public bool ClimbsUp = true;

        /// <summary>
        /// How many floors higher the door this one joins stands (negative for lower). Zero unless the door has a vertical type.
        /// </summary>
        public int FloorDelta
        {
            get
            {
                int climb = Type == null ? 0 : Mathf.Abs(Type.FloorDelta);
                return ClimbsUp ? climb : -climb;
            }
        }

        /// <summary>
        /// Gets a <see cref="Vector2Int"/> representing which direction this connection is pointing.
        /// </summary>
        public Vector2Int Forward
        {
            get
            {
                return new Vector2Int(
                    Mathf.RoundToInt(this.gameObject.transform.forward.x),
                    Mathf.RoundToInt(this.gameObject.transform.forward.z)
                );
            }
        }

        /// <summary>
        /// The location of the connection relative to it's parent.
        /// </summary>
        public Vector2Int location;

        [Tooltip("The door's own mesh (frame and leaf). Needed on a door with a length of zero: when two rooms join shared, the generator hides the DoorObject of one of them so the pair is drawn once instead of z-fighting.")]
        public GameObject DoorObject;

        /// <summary>
        /// Hide this door's own mesh, because the door it joins draws the pair.
        /// </summary>
        /// <returns> False if there is no door object to hide.</returns>
        public bool TryHideDoor()
        {
            if (DoorObject == null)
            {
                return false;
            }

            DoorObject.SetActive(false);
            return true;
        }

        [Tooltip("The object switched on when the generator locks this door (a gate, a padlock). Leave empty if the room has no locked look.")]
        public GameObject LockObject;

        /// <summary>
        /// Show this door as locked.
        /// </summary>
        /// <returns> False if there is no lock object to activate.</returns>
        public bool TryLock()
        {
            if (LockObject == null)
            {
                return false;
            }

            LockObject.SetActive(true);
            return true;
        }

        /// <summary>
        /// Seal the connection with a wall.
        /// </summary>
        public void Seal()
        {
            TrySeal();
        }

        /// <summary>
        /// Seal the connection with a wall if it has a sealing object.
        /// </summary>
        /// <returns> False if there is no sealing object to activate.</returns>
        public bool TrySeal()
        {
            if (SealingObject == null)
            {
                return false;
            }

            SealingObject.SetActive(true);
            return true;
        }

        void OnDrawGizmosSelected()
        {
            // Draws a 5 unit long red line in front of the object
            Gizmos.color = Color.red;
            Vector3 direction = transform.TransformDirection(Vector3.forward) * 5;

            Gizmos.DrawRay(transform.position, direction);
        }

        /// <summary>
        /// Returns true if a connection is facing in the opposite direction as another connection.
        /// </summary>
        /// <param name="otherConnection"> The connection we want to see if we can connect to.</param>
        /// <returns> True if the connections are facing opposite directions, false otherwise.</returns>
        public bool CanConnect(ConnectionBlueprint otherConnection)
        {
            if (this.Forward * -1 == otherConnection.Forward)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
