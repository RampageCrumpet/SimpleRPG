using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>One name a rare item can have, with a permanent id so an item that rolled it keeps it however the list is edited later.</summary>
    [Serializable]
    public struct NameWord
    {
        [Min(1)] public int id;
        public string text;
    }

    /// <summary>
    /// The names rare items are called by, Diablo II style ("Grave Bite", "Iron Requiem"), with the base item shown beneath. One is picked from the
    /// seeded gameplay stream when the item is made and kept as its id, so editing, adding, reordering or removing names never renames an item
    /// already rolled. The list is data the owner supplies.
    /// </summary>
    [CreateAssetMenu(fileName = "RareNames", menuName = "Items/Rare Names")]
    public class RareNames : ScriptableObject
    {
        [SerializeField, Tooltip("The names. Each needs its own permanent id (1 or more) that is never reused.")]
        private List<NameWord> names = new List<NameWord>();

        public IReadOnlyList<NameWord> Names => names;

        /// <summary>The id of the name a sample in [0, 1) lands on, or 0 if there are none.</summary>
        public int Pick(double sample)
        {
            if (names.Count == 0)
            {
                return 0;
            }

            int index = Math.Min(names.Count - 1, (int)Math.Floor(Math.Max(0d, Math.Min(0.999999d, sample)) * names.Count));
            return names[index].id;
        }

        /// <summary>The text of a name, or null if the list no longer has it.</summary>
        public string TextOf(int id)
        {
            foreach (NameWord word in names)
            {
                if (word.id == id) return word.text;
            }

            return null;
        }

        /// <summary>Why the list is not usable, or null.</summary>
        public string Problem()
        {
            if (names.Count == 0) return name + " has no names.";
            if (names.Any(w => w.id < 1 || string.IsNullOrWhiteSpace(w.text))) return name + " has a name with no id or no text.";
            if (names.Select(w => w.id).Distinct().Count() != names.Count) return name + " reuses an id.";
            return null;
        }
    }
}
