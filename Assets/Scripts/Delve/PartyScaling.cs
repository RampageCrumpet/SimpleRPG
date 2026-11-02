using System;

namespace SimpleRPG.Delve
{
    /// <summary>
    /// How much more the dungeon holds for a bigger party. There is no levelling, so a monster stays as dangerous as it always was; what grows is
    /// how many of them wait in a room. Plain code so the curve can be tested exactly.
    /// </summary>
    public static class PartyScaling
    {
        /// <summary>
        /// How many times as many encounters a party of this size meets as a lone player: 1 for one player, plus <paramref name="perExtraPlayer"/>
        /// for each player beyond the first. A party size of 0 or less means unknown and counts as one.
        /// </summary>
        public static float Multiplier(int partySize, float perExtraPlayer)
        {
            return 1f + Math.Max(0, partySize - 1) * Math.Max(0f, perExtraPlayer);
        }

        /// <summary>
        /// How many groups of enemies to place where a lone player would meet one. A fraction of a group is a chance of one more, so a
        /// multiplier of 1.5 gives one or two, two or three of every four on average. Draws from <paramref name="random"/> only when the
        /// multiplier has a fraction, so a party of one reaches exactly the same dungeon it always did.
        /// </summary>
        public static int Groups(int partySize, float perExtraPlayer, Random random)
        {
            float multiplier = Multiplier(partySize, perExtraPlayer);
            int whole = (int)Math.Floor(multiplier);
            float fraction = multiplier - whole;
            if (fraction > 1e-6f && random.NextDouble() < fraction)
            {
                whole++;
            }

            return whole;
        }
    }
}
