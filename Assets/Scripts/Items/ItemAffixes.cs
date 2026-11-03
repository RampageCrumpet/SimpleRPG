using System;
using Unity.Netcode;

namespace SimpleRPG.InventorySystem
{
    /// <summary>One affix an item rolled: which affix (its network id in the <see cref="SimpleRPG.Abilities.DefinitionRegistry"/>) and its value.</summary>
    public readonly struct AffixRoll
    {
        /// <summary>Values are kept in thousandths so they travel and save as whole numbers and compare exactly.</summary>
        public const int Scale = 1000;

        public AffixRoll(uint affixId, int valueMilli)
        {
            AffixId = affixId;
            ValueMilli = valueMilli;
        }

        public uint AffixId { get; }

        public int ValueMilli { get; }

        public float Value => ValueMilli / (float)Scale;

        public static int ToMilli(double value) => (int)Math.Round(value * Scale);
    }

    /// <summary>
    /// The affixes one item instance rolled when it came into the world. They are fixed for the life of the item: nothing rerolls or changes
    /// them, so what is stored and sent is the result, not a seed, and editing an affix's range later cannot change an item already rolled.
    /// Up to <see cref="MaxAffixes"/>. Held as plain fields so it can be replicated inside an inventory entry and saved as it is.
    /// </summary>
    public struct ItemAffixes : INetworkSerializable, IEquatable<ItemAffixes>
    {
        public const int MaxAffixes = 4;

        public uint Id0, Id1, Id2, Id3;
        public int Milli0, Milli1, Milli2, Milli3;

        /// <summary>How many affixes there are: the ones from the first, up to the first empty one.</summary>
        public int Count
        {
            get
            {
                if (Id0 == 0) return 0;
                if (Id1 == 0) return 1;
                if (Id2 == 0) return 2;
                return Id3 == 0 ? 3 : 4;
            }
        }

        public bool IsEmpty => Id0 == 0;

        public AffixRoll this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                switch (index)
                {
                    case 0: return new AffixRoll(Id0, Milli0);
                    case 1: return new AffixRoll(Id1, Milli1);
                    case 2: return new AffixRoll(Id2, Milli2);
                    default: return new AffixRoll(Id3, Milli3);
                }
            }
        }

        /// <summary>Packs rolls into one value. Rolls past <see cref="MaxAffixes"/>, and any with no affix id, are left out.</summary>
        public static ItemAffixes From(System.Collections.Generic.IReadOnlyList<AffixRoll> rolls)
        {
            var result = new ItemAffixes();
            int n = 0;
            for (int i = 0; rolls != null && i < rolls.Count && n < MaxAffixes; i++)
            {
                if (rolls[i].AffixId == 0)
                {
                    continue;
                }

                switch (n++)
                {
                    case 0: result.Id0 = rolls[i].AffixId; result.Milli0 = rolls[i].ValueMilli; break;
                    case 1: result.Id1 = rolls[i].AffixId; result.Milli1 = rolls[i].ValueMilli; break;
                    case 2: result.Id2 = rolls[i].AffixId; result.Milli2 = rolls[i].ValueMilli; break;
                    default: result.Id3 = rolls[i].AffixId; result.Milli3 = rolls[i].ValueMilli; break;
                }
            }

            return result;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id0);
            serializer.SerializeValue(ref Milli0);
            serializer.SerializeValue(ref Id1);
            serializer.SerializeValue(ref Milli1);
            serializer.SerializeValue(ref Id2);
            serializer.SerializeValue(ref Milli2);
            serializer.SerializeValue(ref Id3);
            serializer.SerializeValue(ref Milli3);
        }

        public bool Equals(ItemAffixes other)
        {
            return Id0 == other.Id0 && Milli0 == other.Milli0 && Id1 == other.Id1 && Milli1 == other.Milli1 &&
                   Id2 == other.Id2 && Milli2 == other.Milli2 && Id3 == other.Id3 && Milli3 == other.Milli3;
        }

        public override bool Equals(object obj) => obj is ItemAffixes other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Id0); hash.Add(Milli0); hash.Add(Id1); hash.Add(Milli1);
            hash.Add(Id2); hash.Add(Milli2); hash.Add(Id3); hash.Add(Milli3);
            return hash.ToHashCode();
        }
    }
}
