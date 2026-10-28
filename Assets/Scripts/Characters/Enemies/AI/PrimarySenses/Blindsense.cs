using SimpleRPG.AI;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.AI
{
    /// <summary>
    /// This class represents an enemies ability to immediately detect a player within a certain range.
    /// </summary>
    public class Blindsense : PrimarySense
    {
        [field: SerializeField]
        [Tooltip("The range of this sense.")]
        public float Range { get; set; }

        [SerializeField]
        [Tooltip("The layers this sense can detect.")]
        public LayerMask sensableLayers;

        [SerializeField]
        [Tooltip("Should this sense only return the player?")]
        private bool onlySensePlayers;

        private Collider[] overlaps = new Collider[64];
        private readonly List<Character> found = new List<Character>();

        /// <inheritdoc cref="PrimarySense.CanSense(Character)"/>
        public override bool CanSense(Character target)
        {
            if (target == null) return false;
            found.Clear();
            Sense(found);
            return found.Contains(target);
        }

        public override IEnumerable<Character> Sense()
        {
            var results = new List<Character>();
            Sense(results);
            return results;
        }

        /// <inheritdoc/>
        public override void Sense(List<Character> results)
        {
            // Every living character within the range of this sense, each once.
            // A full buffer may have dropped colliders (each character has many hurtboxes), so grow it and look again.
            int count = Physics.OverlapSphereNonAlloc(transform.position, Range, overlaps, sensableLayers, QueryTriggerInteraction.Collide);
            while (count == overlaps.Length)
            {
                overlaps = new Collider[overlaps.Length * 2];
                count = Physics.OverlapSphereNonAlloc(transform.position, Range, overlaps, sensableLayers, QueryTriggerInteraction.Collide);
            }

            for (int i = 0; i < count; i++)
            {
                Character character = overlaps[i].GetComponentInParent<Character>();
                if (character == null || !character.IsAlive) continue;
                if (onlySensePlayers && !character.CompareTag("Player")) continue;
                if (!results.Contains(character)) results.Add(character);
            }
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
