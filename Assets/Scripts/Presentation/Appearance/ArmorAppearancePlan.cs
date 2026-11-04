using System;
using System.Collections.Generic;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// What a character should look like, given its bare body and the parts of the armor it wears. Plain code with no engine behaviour, so the
    /// rule is tested exactly. Worn armor shows in place of the bare part in the same slot; take it off and the bare part is back.
    /// </summary>
    public static class ArmorAppearancePlan
    {
        /// <summary>
        /// The part each slot shows. A slot with neither armor nor a bare part has no entry and shows nothing.
        /// </summary>
        /// <param name="bareParts">What the character wears with nothing on: its default parts.</param>
        /// <param name="armorParts">The parts of every worn piece, in the order they should take effect. If two claim a slot the later one wins.</param>
        public static Dictionary<BodyPartSlot, BodyPartDefinition> Desired(IEnumerable<BodyPartDefinition> bareParts, IEnumerable<BodyPartDefinition> armorParts)
        {
            var desired = new Dictionary<BodyPartSlot, BodyPartDefinition>();
            foreach (BodyPartDefinition part in bareParts ?? Array.Empty<BodyPartDefinition>())
            {
                if (part != null) desired[part.Slot] = part;
            }

            foreach (BodyPartDefinition part in armorParts ?? Array.Empty<BodyPartDefinition>())
            {
                if (part != null) desired[part.Slot] = part;
            }

            return desired;
        }
    }
}
