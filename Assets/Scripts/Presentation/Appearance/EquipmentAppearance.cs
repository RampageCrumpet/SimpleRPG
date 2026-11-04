using SimpleRPG.InventorySystem;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// Dresses a character in the armor it wears. The server watches the character's <see cref="Equipment"/> and puts the parts that are the look
    /// of each worn piece on its <see cref="CharacterAppearance"/>, in place of the bare parts; take the armor off and the bare parts return.
    /// Clients do nothing here: appearance is already replicated, so every machine, a late joiner included, shows what the server decided.
    /// The armor is only a look. What it does in combat is the stats it grants; nothing here changes them.
    /// </summary>
    public sealed class EquipmentAppearance : NetworkBehaviour
    {
        private Equipment equipment;
        private CharacterAppearance appearance;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            equipment = GetComponent<Equipment>();
            appearance = GetComponent<CharacterAppearance>();
            if (equipment == null || appearance == null)
            {
                return;
            }

            equipment.Changed += Refresh;

            // Armor already worn when the character spawns, such as a returning player's, is dressed straight away.
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            if (equipment != null)
            {
                equipment.Changed -= Refresh;
            }

            equipment = null;
            appearance = null;
        }

        private void Refresh()
        {
            if (appearance == null || equipment == null)
            {
                return;
            }

            // In equipment-slot order, so the result does not depend on the order things were put on.
            var armorParts = new List<BodyPartDefinition>();
            foreach ((EquipmentSlot slot, uint instanceId, Item item) in equipment.Equipped.OrderBy(worn => (int)worn.slot))
            {
                armorParts.AddRange(ArmorParts.For(item));
            }

            Dictionary<BodyPartSlot, BodyPartDefinition> desired = ArmorAppearancePlan.Desired(appearance.DefaultParts, armorParts);
            Dictionary<BodyPartSlot, BodyPartDefinition> shown = appearance.CurrentParts.Where(p => p != null).ToDictionary(p => p.Slot);

            foreach (BodyPartSlot slot in Enum.GetValues(typeof(BodyPartSlot)))
            {
                desired.TryGetValue(slot, out BodyPartDefinition want);
                shown.TryGetValue(slot, out BodyPartDefinition have);
                if (want == have)
                {
                    continue;
                }

                if (want != null) appearance.ServerEquip(want);
                else appearance.ServerClear(slot);
            }
        }
    }
}
