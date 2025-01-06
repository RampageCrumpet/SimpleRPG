using SimpleRPG.AI;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// The list of characters we can sense.
        /// </summary>
        private IEnumerable<Character> sensedCharacters = new List<Character>();

        /// <inheritdoc cref="PrimarySense.CanSense(Character)"/>
        public override bool CanSense(Character target)
        {
            return sensedCharacters.Contains(target);
        }

        public override IEnumerable<Character> Sense()
        {
            // Create an overlap sphere and return all of the characters within the range of this sense.
            sensedCharacters = Physics.OverlapSphere(transform.position, Range, sensableLayers, QueryTriggerInteraction.Ignore).Select(x => x.GetComponent<Character>()).Where(x => x != null);

            if(onlySensePlayers)
            {
                sensedCharacters = sensedCharacters.Where(x => x.gameObject.tag == "Player");
            }

            return sensedCharacters;
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
