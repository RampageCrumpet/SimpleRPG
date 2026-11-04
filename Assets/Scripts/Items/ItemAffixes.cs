using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace SimpleRPG.InventorySystem
{
    /// <summary>One affix an item rolled: which affix (its network id in the <see cref="SimpleRPG.Abilities.DefinitionRegistry"/>) and the value of each of its effects.</summary>
    public readonly struct AffixRoll
    {
        /// <summary>Values are kept in thousandths so they travel and save as whole numbers and compare exactly.</summary>
        public const int Scale = 1000;

        private readonly int[] valuesMilli;

        public AffixRoll(uint affixId, params int[] valuesMilli)
        {
            AffixId = affixId;
            this.valuesMilli = valuesMilli ?? Array.Empty<int>();
        }

        public uint AffixId { get; }

        /// <summary>The rolled values in thousandths, in the order of the affix's effects.</summary>
        public IReadOnlyList<int> ValuesMilli => valuesMilli ?? Array.Empty<int>();

        public int ValueCount => ValuesMilli.Count;

        public float Value(int index) => ValuesMilli[index] / (float)Scale;

        /// <summary>The rolled values as numbers.</summary>
        public float[] Values()
        {
            var values = new float[ValueCount];
            for (int i = 0; i < values.Length; i++) values[i] = Value(i);
            return values;
        }

        public static int ToMilli(double value) => (int)Math.Round(value * Scale);
    }

    /// <summary>
    /// The affixes one item instance rolled when it came into the world. They are fixed for the life of the item: nothing rerolls or changes
    /// them, so what is stored and sent is the result, not a seed, and editing an affix's range later cannot change an item already rolled.
    /// <para>
    /// Stored as one flat run of whole numbers, <c>[affix id, value count, value, value, ...]</c> for each affix in turn, so an affix can have
    /// any number of effects and values with nothing in the format fixed to them. The run lives in a fixed number of words
    /// (<see cref="WordCapacity"/>) because it is replicated inside an inventory entry, which must be a fixed size; that is a limit on a whole
    /// item's affixes, not on one affix, and <see cref="AffixPool.Problems"/> reports a pool that could exceed it. Held as plain fields so it
    /// can be replicated and saved as it is.
    /// </para>
    /// </summary>
    public struct ItemAffixes : INetworkSerializable, IEquatable<ItemAffixes>
    {
        /// <summary>Whole numbers an item has for all its affixes. A stash database stores the same run.</summary>
        public const int WordCapacity = 32;

        private const int HeaderWords = 2;

        private WordBlock block0, block1, block2, block3;

        /// <summary>The words an affix with this many values takes.</summary>
        public static int Footprint(int valueCount) => HeaderWords + valueCount;

        /// <summary>How many words are used.</summary>
        public int WordsUsed
        {
            get
            {
                int at = 0;
                while (TryReadHeader(at, out _, out int count)) at += HeaderWords + count;
                return at;
            }
        }

        /// <summary>How many affixes there are.</summary>
        public int Count
        {
            get
            {
                int at = 0;
                int affixes = 0;
                while (TryReadHeader(at, out _, out int count))
                {
                    affixes++;
                    at += HeaderWords + count;
                }

                return affixes;
            }
        }

        public bool IsEmpty => Get(0) == 0;

        public AffixRoll this[int index]
        {
            get
            {
                int at = 0;
                for (int n = 0; TryReadHeader(at, out int id, out int count); n++)
                {
                    if (n == index)
                    {
                        var values = new int[count];
                        for (int v = 0; v < count; v++) values[v] = Get(at + HeaderWords + v);
                        return new AffixRoll((uint)id, values);
                    }

                    at += HeaderWords + count;
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        // The affix that starts at a word: false at the end of the run (no id) or if what follows does not fit, which only damaged data does.
        private bool TryReadHeader(int at, out int id, out int count)
        {
            id = 0;
            count = 0;
            if (at + HeaderWords > WordCapacity) return false;

            id = Get(at);
            count = Get(at + 1);
            return id != 0 && count >= 0 && at + HeaderWords + count <= WordCapacity;
        }

        /// <summary>
        /// Packs rolls into one value, in order. A roll with no affix id, or one that no longer fits in <see cref="WordCapacity"/>, is left out.
        /// </summary>
        public static ItemAffixes From(IReadOnlyList<AffixRoll> rolls)
        {
            var result = new ItemAffixes();
            int at = 0;
            for (int i = 0; rolls != null && i < rolls.Count; i++)
            {
                AffixRoll roll = rolls[i];
                if (roll.AffixId == 0 || at + Footprint(roll.ValueCount) > WordCapacity) continue;

                result.Set(at, (int)roll.AffixId);
                result.Set(at + 1, roll.ValueCount);
                for (int v = 0; v < roll.ValueCount; v++) result.Set(at + HeaderWords + v, roll.ValuesMilli[v]);
                at += Footprint(roll.ValueCount);
            }

            return result;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref block0);
            serializer.SerializeValue(ref block1);
            serializer.SerializeValue(ref block2);
            serializer.SerializeValue(ref block3);
        }

        public bool Equals(ItemAffixes other)
        {
            for (int i = 0; i < WordCapacity; i++)
            {
                if (Get(i) != other.Get(i)) return false;
            }

            return true;
        }

        public override bool Equals(object obj) => obj is ItemAffixes other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            for (int i = 0; i < WordCapacity; i++) hash.Add(Get(i));
            return hash.ToHashCode();
        }

        private int Get(int word)
        {
            switch (word >> 3)
            {
                case 0: return block0[word & 7];
                case 1: return block1[word & 7];
                case 2: return block2[word & 7];
                default: return block3[word & 7];
            }
        }

        private void Set(int word, int value)
        {
            switch (word >> 3)
            {
                case 0: block0[word & 7] = value; break;
                case 1: block1[word & 7] = value; break;
                case 2: block2[word & 7] = value; break;
                default: block3[word & 7] = value; break;
            }
        }

        // Eight whole numbers as plain fields: replicated as they are, with no heap and no unsafe code.
        private struct WordBlock : INetworkSerializable
        {
            private int w0, w1, w2, w3, w4, w5, w6, w7;

            public int this[int i]
            {
                get
                {
                    switch (i)
                    {
                        case 0: return w0;
                        case 1: return w1;
                        case 2: return w2;
                        case 3: return w3;
                        case 4: return w4;
                        case 5: return w5;
                        case 6: return w6;
                        default: return w7;
                    }
                }

                set
                {
                    switch (i)
                    {
                        case 0: w0 = value; break;
                        case 1: w1 = value; break;
                        case 2: w2 = value; break;
                        case 3: w3 = value; break;
                        case 4: w4 = value; break;
                        case 5: w5 = value; break;
                        case 6: w6 = value; break;
                        default: w7 = value; break;
                    }
                }
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref w0);
                serializer.SerializeValue(ref w1);
                serializer.SerializeValue(ref w2);
                serializer.SerializeValue(ref w3);
                serializer.SerializeValue(ref w4);
                serializer.SerializeValue(ref w5);
                serializer.SerializeValue(ref w6);
                serializer.SerializeValue(ref w7);
            }
        }
    }
}
