using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.UI
{
    /// <summary>
    /// The root of the player's HUD and menus. When the local player is announced (see <see cref="LocalPlayer"/>) it binds every
    /// <see cref="ILocalPlayerView"/> beneath it to that character, and unbinds them when it goes, so the character never needs to find the UI
    /// and a new piece of UI only has to implement the interface. It also owns the cursor and tells the rest of the game when a menu has the
    /// player's attention.
    /// </summary>
    public class PlayerUIController : MonoBehaviour
    {
        /// <summary>
        /// The MonoBehaviours that are currently requesting the mouse be shown and the camera controls be disabled..
        /// </summary>
        private HashSet<MonoBehaviour> lockingBehaviours = new();

        /// <summary>
        /// The character this UI belongs to, if one has been set.
        /// </summary>
        public Character LocalCharacter { get; private set; }

        /// <summary>
        /// True while the local character is alive (or none is set yet), and so may use the world UI such as the inventory.
        /// </summary>
        public bool IsLocalCharacterAlive => LocalCharacter == null || LocalCharacter.IsAlive;

        private void OnEnable()
        {
            LocalPlayer.Ready += BindViews;
            LocalPlayer.Gone += UnbindViews;

            // The character may have been announced before this UI came up.
            if (LocalPlayer.Current != null)
            {
                BindViews(LocalPlayer.Current);
            }
        }

        private void OnDisable()
        {
            LocalPlayer.Ready -= BindViews;
            LocalPlayer.Gone -= UnbindViews;

            if (LocalCharacter != null)
            {
                UnbindViews(LocalCharacter);
            }
        }

        /// <summary>
        /// Sets the character this UI belongs to.
        /// </summary>
        public void SetCharacter(Character character)
        {
            LocalCharacter = character;
        }

        private void BindViews(Character character)
        {
            // The controller must know the character before the views bind: a character that joined already dead shows the death screen at once.
            LocalCharacter = character;
            foreach (ILocalPlayerView view in GetComponentsInChildren<ILocalPlayerView>(true))
            {
                view.BindLocalPlayer(character);
            }
        }

        private void UnbindViews(Character character)
        {
            foreach (ILocalPlayerView view in GetComponentsInChildren<ILocalPlayerView>(true))
            {
                view.UnbindLocalPlayer();
            }

            LocalCharacter = null;
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
        /// When enabled the mouse is shown and gameplay input and the camera are held back. When disabled the mouse is hidden and they resume.
        /// The camera and the player's controls listen to <see cref="LocalPlayer.MenuModeChanged"/>, so this does not know what they are.
        /// </summary>
        /// <param name="value"> True to show the mouse and hold gameplay input back, false otherwise.</param>
        private void SetMenuControlMode(bool value)
        {
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
            LocalPlayer.SetMenuMode(value);
        }
    }
}
