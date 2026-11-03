using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// One kind of bonus an item can roll: a change to one attribute of the wearer, with a range its value is drawn from when the item is made.
    /// Like every definition it is never changed while the game runs; what an item actually rolled is stored on the item (see <see cref="ItemAffixes"/>).
    /// Run <b>SimpleRPG &gt; Definitions &gt; Rebuild Definition Registry</b> after creating one, so it has a network id.
    /// </summary>
    [CreateAssetMenu(fileName = "Affix", menuName = "Items/Affix")]
    public class AffixDefinition : GameplayDefinition
    {
        [SerializeField]
        [Tooltip("What the player reads, for example 'Strength'. The amount is put in front: '+3 Strength', '+10% Strength'.")]
        private string label = "";

        [SerializeField]
        [Tooltip("The attribute it changes, by stable key, for example simplerpg.attr.strength.")]
        private string attributeKey = "";

        [SerializeField]
        [Tooltip("Where the change applies: a flat amount, a percentage bonus (0.1 is +10%), or a final factor.")]
        private ModifierLayer layer = ModifierLayer.Add;

        [SerializeField]
        [Tooltip("The lowest value it can roll.")]
        private float minimum = 1f;

        [SerializeField]
        [Tooltip("The highest value it can roll.")]
        private float maximum = 3f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How likely this affix is to be picked, relative to the others in the pool. 0 never.")]
        private float weight = 1f;

        [SerializeField]
        [Tooltip("Two affixes with the same non-empty group never roll on one item, so an item cannot get two kinds of the same bonus.")]
        private string exclusiveGroup = "";

        [SerializeField]
        [Tooltip("The slots this affix can roll on. Empty means any.")]
        private List<EquipmentSlot> allowedSlots = new List<EquipmentSlot>();

        public string Label => label;

        public string AttributeKey => attributeKey;

        public ModifierLayer Layer => layer;

        public float Minimum => minimum;

        public float Maximum => maximum;

        public float Weight => weight;

        public string ExclusiveGroup => exclusiveGroup;

        public bool AllowsSlot(EquipmentSlot slot) => allowedSlots == null || allowedSlots.Count == 0 || allowedSlots.Contains(slot);

        /// <summary>The attribute change this affix makes at a rolled value, in the form the grant engine applies.</summary>
        public GrantSpec ToGrant(float value) => new GrantSpec { AttributeKey = attributeKey, Layer = layer, Value = value };

        /// <summary>The line a player reads for this affix at a rolled value, such as "+3 Strength", "+10% Strength" or "x1.5 Strength".</summary>
        public string Describe(float value)
        {
            string amount;
            switch (layer)
            {
                case ModifierLayer.PercentAdd:
                    amount = (value >= 0f ? "+" : "") + (value * 100f).ToString("0.#") + "%";
                    break;
                case ModifierLayer.Multiply:
                    amount = "x" + value.ToString("0.##");
                    break;
                default:
                    amount = (value >= 0f ? "+" : "") + value.ToString("0.##");
                    break;
            }

            return amount + " " + label;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (maximum < minimum)
            {
                Debug.LogWarning($"{name}: the maximum is below the minimum, so it will always roll the minimum.", this);
            }

            if (string.IsNullOrWhiteSpace(attributeKey))
            {
                Debug.LogWarning($"{name}: no attribute key, so it changes nothing.", this);
            }
        }
#endif
    }
}
