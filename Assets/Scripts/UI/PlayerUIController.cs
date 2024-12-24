using NUnit.Framework;
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

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {
        
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
            firstPersonCharacterController.enabled = !value;
        }
    }
}
