using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// The base class of an abillity.
    /// </summary>
    public abstract class Ability : ScriptableObject
    {
        /// <summary>
        /// The name of this abillity.
        /// </summary>
        [Tooltip("The name of this abillity.")]
        public string abillityName = "New Abillity";

        /// <summary>
        /// The tooltip the user sees when they hover over this ability.
        /// </summary>
        [Tooltip("The tooltip we want to display when the user hovers over this ability.")]
        public string toolTip;

        /// <summary>
        /// This abillitys sprite.
        /// </summary>
        [Tooltip("The icon this ability displays in the GUI.")]
        public Sprite abillitySprite;

        /// <summary>
        /// The sound we play when this abillity is activated.
        /// </summary>
        [Tooltip("The sound to be played when this ability is activated.")]
        public AudioClip abillitySound;

        /// <summary>
        /// The time delay in seconds before this ability can be used again.
        /// </summary>
        [Tooltip("The time delay in seconds before this abillity can be used again.")]
        public float cooldownTime = 1f;

        [HideInInspector]
        /// <summary>
        /// The last time this abillity was used.
        /// </summary>
        public float LastActivationTime
        {
            get;
            private set;
        }

        /// <summary>
        /// Activate the abillity.
        /// </summary>
        public virtual void Activate()
        {
            if (Time.time > LastActivationTime + this.cooldownTime)
            {
                LastActivationTime = Time.time;
            }
        }
    }
}