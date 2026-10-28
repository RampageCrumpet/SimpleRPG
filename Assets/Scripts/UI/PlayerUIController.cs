using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.UI
{
    public class PlayerUIController : MonoBehaviour
    {
        /// <summary>
        /// The MonoBehaviours that are currently requesting the mouse be shown and the camera controls be disabled..
        /// </summary>
        private HashSet<MonoBehaviour> lockingBehaviours = new();

        /// <summary>
        /// The FirstPersonCharacterController that is in charge of the player's camera.
        /// </summary>
        private FirstPersonCharacterController firstPersonCharacterController;

        /// <summary>
        /// The character this UI belongs to, if one has been set.
        /// </summary>
        public Character LocalCharacter { get; private set; }

        /// <summary>
        /// True while the local character is alive (or none is set yet), and so may use the world UI such as the inventory.
        /// </summary>
        public bool IsLocalCharacterAlive => LocalCharacter == null || LocalCharacter.IsAlive;

        /// <summary>
        /// Sets the character this UI belongs to.
        /// </summary>
        public void SetCharacter(Character character)
        {
            LocalCharacter = character;
        }

        /// <summary>
        /// Sets the <see cref="FirstPersonCharacterController"/> that is in charge of the player's camera.
        /// </summary>
        /// <param name="firstPersonCharacterController"> The character controller in charge of the camera.</param>
        public void SetCharacterController(FirstPersonCharacterController firstPersonCharacterController)
        {
            this.firstPersonCharacterController = firstPersonCharacterController;
        }

        /// <summary>
        /// Adds the given MonoBehavior to the collection of MonoBehaviors locking the rotation and hiding the mouse.
        /// </summary>
        /// <param name="lockingBehavior"> The v</param>
        public void LockRotationAndShowMouse(MonoBehaviour lockingBehavior)
        {
            lockingBehaviours.Add(lockingBehavior);
            SetMenuControlMode(true);
        }

        /// <summary>
        /// Removes the given MonoBehavior from the collection of MonoBehaviors locking the rotation and hiding the mouse. If the collection is empty, the rotation is unlocked and the mouse is shown.
        /// </summary>
        /// <param name="lockingBehavior"> The <see cref="MonoBehaviour"/> that wants to show the mouse and lock camera rotation.</param>
        public void UnlockRotationAndHideMouse(MonoBehaviour lockingBehavior)
        {
            lockingBehaviours.Remove(lockingBehavior);
            if (lockingBehaviours.Count == 0)
            {
                SetMenuControlMode(false);
            }
        }

        /// <summary>
        /// When enabled the mouse is shown and the camera is locked. When disabled the mouse is hidden and the camera is unlocked.
        /// </summary>
        /// <param name="value"> True to hide the mouse and lock camera rotation, false otherwise.</param>
        private void SetMenuControlMode(bool value)
        {
            if (value)
            {
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
            }

            Cursor.visible = value;
            if (firstPersonCharacterController != null) firstPersonCharacterController.enabled = !value;
        }
    }
}
