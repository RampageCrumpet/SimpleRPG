using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>One word a rare item's name can use, with a permanent id so an item that rolled it keeps it however this list is edited later.</summary>
    [Serializable]
    public struct NameWord
    {
        [Min(1)] public int id;
        public string text;
    }

    /// <summary>
    /// The words that rare items are named from, Diablo II style: a rare item is called "{first word} {second word}" ("Grim Bulwark") with its
    /// base item beneath. The name is picked from the seeded gameplay stream when the item is made and kept as two ids, so editing, adding or
    /// removing words never renames an item already rolled. A list of words is data; the owner supplies it.
    /// </summary>
    [CreateAssetMenu(fileName = "RareNames", menuName = "Items/Rare Names")]
    public class RareNameComponents : ScriptableObject
    {
        [SerializeField, Tooltip("The first word of the name, such as 'Grim' or 'Beast'. Each needs its own permanent id (1 or more) that is never reused.")]
        private List<NameWord> firstWords = new List<NameWord>();

        [SerializeField, Tooltip("The second word of the name, such as 'Bulwark' or 'Shell'.")]
        private List<NameWord> secondWords = new List<NameWord>();

        public IReadOnlyList<NameWord> FirstWords => firstWords;

        public IReadOnlyList<NameWord> SecondWords => secondWords;

        /// <summary>The id of the word a sample in [0, 1) lands on, or 0 if there are none.</summary>
        public static int Pick(IReadOnlyList<NameWord> words, double sample)
        {
            if (words == null || words.Count == 0)
            {
                return 0;
            }

            int index = Math.Min(words.Count - 1, (int)Math.Floor(Math.Max(0d, Math.Min(0.999999d, sample)) * words.Count));
            return words[index].id;
        }

        /// <summary>The text of a word, or null if the list no longer has it.</summary>
        public static string TextOf(IReadOnlyList<NameWord> words, int id)
        {
            foreach (NameWord word in words)
            {
                if (word.id == id) return word.text;
            }

            return null;
        }

        /// <summary>The name for two ids, or null if either word is gone.</summary>
        public string NameOf(int firstId, int secondId)
        {
            string first = TextOf(firstWords, firstId);
            string second = TextOf(secondWords, secondId);
            return first != null && second != null ? first + " " + second : null;
        }

        /// <summary>Why the lists are not usable, or null.</summary>
        public string Problem()
        {
            if (firstWords.Count == 0 || secondWords.Count == 0) return name + " needs at least one first word and one second word.";
            foreach (List<NameWord> words in new[] { firstWords, secondWords })
            {
                if (words.Any(w => w.id < 1 || string.IsNullOrWhiteSpace(w.text))) return name + " has a word with no id or no text.";
                if (words.Select(w => w.id).Distinct().Count() != words.Count) return name + " reuses a word id.";
            }

            return null;
        }
    }
}
