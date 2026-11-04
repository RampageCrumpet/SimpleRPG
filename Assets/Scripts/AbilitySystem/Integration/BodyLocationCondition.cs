using SimpleRPG.HitDetection;
using System;
using UnityEngine;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// Passes when the blow struck one of the chosen parts of the body: a bonus on a headshot, a penalty on a limb. It lives with the game because
    /// the body locations are the game's own; the effect system only passes the location as a number.
    /// </summary>
    [Serializable]
    public sealed class BodyLocationCondition : EffectCondition
    {
        [SerializeField]
        [Tooltip("The parts of the body that pass.")]
        private BodyLocation[] locations = { BodyLocation.Head };

        public BodyLocationCondition()
        {
        }

        public BodyLocationCondition(params BodyLocation[] locations) => this.locations = locations;

        public override bool Passes(in TriggerContext context)
        {
            if (context.BodyLocation == TriggerContext.NoBodyLocation)
            {
                return false;
            }

            foreach (BodyLocation location in locations)
            {
                if ((int)location == context.BodyLocation) return true;
            }

            return false;
        }

        public override string Problem() => locations == null || locations.Length == 0 ? "a body location condition names no body location" : null;
    }
}
