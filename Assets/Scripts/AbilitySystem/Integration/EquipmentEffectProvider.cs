using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using Unity.Netcode;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// Tells the effect hosts what a worn item brings (its triggered effects and its grants), by looking at the wearer's equipment. Server only.
    /// </summary>
    public sealed class EquipmentEffectProvider : IItemEffectProvider, IItemGrantProvider
    {
        /// <inheritdoc/>
        public bool TryGetGrants(EntityId wearer, ItemInstanceId item, out IReadOnlyList<GrantSpec> grants)
        {
            grants = null;
            Item equipped = Find(wearer, item);
            IReadOnlyList<GrantSpec> all = equipped != null ? equipped.AllGrants : null;
            if (all == null || all.Count == 0)
            {
                return false;
            }

            grants = all;
            return true;
        }

        /// <inheritdoc/>
        public bool TryGetFixedAttributes(EntityId wearer, ItemInstanceId item, out IReadOnlyList<AttributeValueSpec> fixedAttributes)
        {
            fixedAttributes = null;
            Item equipped = Find(wearer, item);
            if (equipped == null || equipped.FixedAttributes.Count == 0)
            {
                return false;
            }

            fixedAttributes = equipped.FixedAttributes;
            return true;
        }

        private static Item Find(EntityId wearer, ItemInstanceId item)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || !manager.SpawnManager.SpawnedObjects.TryGetValue(wearer.Value, out NetworkObject spawned))
            {
                return null;
            }

            Equipment equipment = spawned.GetComponent<Equipment>();
            if (equipment == null)
            {
                return null;
            }

            foreach ((EquipmentSlot slot, uint instanceId, Item equipped) in equipment.Equipped)
            {
                if (instanceId == item.Value)
                {
                    return equipped;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public bool TryGetEffects(EntityId wearer, ItemInstanceId item, out IReadOnlyList<TriggeredEffect> effects)
        {
            effects = null;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || !manager.SpawnManager.SpawnedObjects.TryGetValue(wearer.Value, out NetworkObject spawned))
            {
                return false;
            }

            Equipment equipment = spawned.GetComponent<Equipment>();
            if (equipment == null)
            {
                return false;
            }

            foreach ((EquipmentSlot slot, uint instanceId, Item equipped) in equipment.Equipped)
            {
                if (instanceId == item.Value && equipped != null && equipped.AllTriggeredEffects.Count > 0)
                {
                    effects = equipped.AllTriggeredEffects;
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<EntityId, ItemInstanceId>> EnumerateEquipped()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
            {
                yield break;
            }

            foreach (NetworkObject spawned in manager.SpawnManager.SpawnedObjectsList)
            {
                Equipment equipment = spawned.GetComponent<Equipment>();
                if (equipment == null)
                {
                    continue;
                }

                foreach ((EquipmentSlot slot, uint instanceId, Item equipped) in equipment.Equipped)
                {
                    yield return new KeyValuePair<EntityId, ItemInstanceId>(new EntityId(spawned.NetworkObjectId), new ItemInstanceId(instanceId));
                }
            }
        }
    }
}
