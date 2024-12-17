using UnityEngine;
using System.Collections.Generic;

namespace SimpleRPG.ObjectInteractions
{
    public static class InteractableManager
    {
        /// <summary>
        /// A collection of all <see cref="Interactable"/> objects.
        /// </summary>
        public static IReadOnlyCollection<Interactable> Interactables
        { 
            get
            {
                if(interactables == null)
                {
                    interactables = new List<Interactable>();
                }

                return interactables;
            }
        }

        /// <summary>
        /// Registers a new <see cref="Interactable"/> in the scene so that other objects know it can be interacted with.
        /// </summary>
        /// <param name="interactable"> The <see cref="Interactable"/> we want to register.</param>
        public static void RegisterInteractable(Interactable interactable)
        {
            interactables.Add(interactable);
        }

        /// <summary>
        /// Removes a <see cref="Interactable"/> from the list of registered interactables so that it can no longer be interacted with.
        /// </summary>
        /// <param name="interactable"> The <see cref="Interactable"/> we want to remove from our list of interactables.</param>
        public static void UnregisterInteractable(Interactable interactable)
        {
            interactables.Remove(interactable);
        }

        /// <summary>
        /// Backing collection of <see cref="Interactable"/> for the <see cref="Interactables"/> property.
        /// </summary>
        private static List<Interactable> interactables = new List<Interactable>();
    }
}
