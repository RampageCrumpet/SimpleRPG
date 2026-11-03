using System;
using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// The one place that says who the local player is. The owning character announces itself here and anything that presents or reacts to the
    /// local player (the HUD, menus, a camera) observes it, so the character never has to know what is listening and nothing has to search the
    /// scene for it. It also records whether a menu currently has the player's attention, so gameplay input can stand down while one is open.
    /// </summary>
    public static class LocalPlayer
    {
        /// <summary>The character the local player controls, or null before one has been announced.</summary>
        public static Character Current { get; private set; }

        /// <summary>Raised when a character is announced as the local player.</summary>
        public static event Action<Character> Ready;

        /// <summary>Raised when the local player's character is withdrawn, for example when it despawns.</summary>
        public static event Action<Character> Gone;

        /// <summary>True while a menu has the player's attention: the cursor is free and gameplay controls are not read.</summary>
        public static bool InMenu { get; private set; }

        /// <summary>Raised when <see cref="InMenu"/> changes.</summary>
        public static event Action<bool> MenuModeChanged;

        /// <summary>
        /// Makes a character the local player's. A previous one is withdrawn first.
        /// </summary>
        public static void Announce(Character character)
        {
            if (character == null || Current == character)
            {
                return;
            }

            if (Current != null)
            {
                Withdraw(Current);
            }

            Current = character;
            Ready?.Invoke(character);
        }

        /// <summary>
        /// Takes the character away as the local player's. Does nothing if it is not the current one.
        /// </summary>
        public static void Withdraw(Character character)
        {
            if (Current != character)
            {
                return;
            }

            Current = null;
            Gone?.Invoke(character);
        }

        /// <summary>
        /// Records that a menu has, or no longer has, the player's attention.
        /// </summary>
        public static void SetMenuMode(bool inMenu)
        {
            if (InMenu == inMenu)
            {
                return;
            }

            InMenu = inMenu;
            MenuModeChanged?.Invoke(inMenu);
        }

        /// <summary>
        /// Forgets everything. Runs at startup, so nothing survives from a previous play session when domain reload is off; tests call it too.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Current = null;
            InMenu = false;
            Ready = null;
            Gone = null;
            MenuModeChanged = null;
        }
    }
}
