using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// The base class of an abillity. This class represents the information required to cast this ability.
    /// </summary>
    public abstract class Ability : NetworkSerializableScriptableObject
    {
        /// <summary>
        /// Gets the type of behavior we want our ability to invoke.
        /// </summary>
        public abstract Type AbilityBehavior
        {
            get;
        }

        /// <summary>
        /// The name of this abillity.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The name of this abillity.")]
        public string AbillityName { get; private set; } = "New Abillity";

        /// <summary>
        /// The tooltip the user sees when they hover over this ability.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The tooltip we want to display when the user hovers over this ability.")]
        public string ToolTip { get; private set; }

        /// <summary>
        /// This abillitys sprite.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The icon this ability displays in the GUI.")]
        public Sprite AbilityIcon { get; private set; }

        /// <summary>
        /// The sound we play when this abillity is activated.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The sound to be played when this ability is activated.")]
        public AudioClip AbillitySound { get; private set; }

        /// <summary>
        /// The time delay in seconds before this ability can be used again.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The time delay in seconds before this abillity can be used again.")]
        public float CooldownTime { get; private set; } = 1f;

        protected override void DeserializeData<T>(BufferSerializer<T> serializer)
        {
            var tooltip = string.Empty;
            serializer.SerializeValue(ref tooltip);
            ToolTip = tooltip;

            var cooldownTime = 0f;
            serializer.SerializeValue(ref cooldownTime);
            CooldownTime = cooldownTime;

            var abilityIconName = string.Empty;
            serializer.SerializeValue(ref abilityIconName);
            if (!string.IsNullOrEmpty(abilityIconName))
            {
                AbilityIcon = Resources.Load<Sprite>(abilityIconName);
            }

            var abilitySoundName = string.Empty;
            serializer.SerializeValue(ref abilitySoundName);
            if (!string.IsNullOrEmpty(abilitySoundName))
            {
                AbillitySound = Resources.Load<AudioClip>(abilitySoundName);
            }
        }

        protected override void SerializeData<T>(BufferSerializer<T> serializer)
        {
            var tooltip = ToolTip;
            serializer.SerializeValue(ref tooltip);

            var cooldownTime = CooldownTime;
            serializer.SerializeValue(ref cooldownTime);

            // Serialize the icon's name instead of the icon itself so we don't have to send images across the network.
            string abilityIconName = AbilityIcon != null ? AbilityIcon.name : string.Empty;
            serializer.SerializeValue(ref abilityIconName);

            string abilitySoundName = AbillitySound != null ? AbillitySound.name : string.Empty;
            serializer.SerializeValue(ref abilitySoundName); ;
        }
    }
}