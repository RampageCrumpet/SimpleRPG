using SimpleRPG.Gameplay;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Base of every reusable gameplay definition (abilities, statuses and so on). A definition has a permanent <see cref="DefinitionId"/>
    /// that does not change when the asset is renamed or moved. Duplicating the asset in the editor is given a new ID.
    /// Definitions are immutable during gameplay; anything that changes at runtime lives in runtime instances.
    /// </summary>
    public abstract class GameplayDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("The permanent identity of this definition. Assigned automatically; do not edit.")]
        private string definitionId;

        /// <summary>
        /// The permanent ID, or <see cref="DefinitionId.None"/> if none has been assigned.
        /// </summary>
        public DefinitionId Id => DefinitionId.TryParse(definitionId, out DefinitionId id) ? id : DefinitionId.None;

        /// <summary>
        /// True if the definition has a valid ID.
        /// </summary>
        public bool HasId => !Id.IsNone;

        /// <summary>
        /// Gives the definition a fresh ID, replacing any it has. Used by editor tooling when an asset is created or duplicated.
        /// </summary>
        public void AssignNewId()
        {
            definitionId = DefinitionId.NewId().ToString();
        }

        /// <summary>
        /// Gives the definition an ID if it has none. An existing valid ID is never changed.
        /// </summary>
        /// <returns>True if an ID was assigned.</returns>
        public bool EnsureId()
        {
            if (HasId)
            {
                return false;
            }

            AssignNewId();
            return true;
        }

#if UNITY_EDITOR
        // New assets get an ID as they are created, so a definition is never saved without one.
        protected virtual void Reset()
        {
            EnsureId();
        }

        protected virtual void OnValidate()
        {
            EnsureId();
        }
#endif
    }
}
