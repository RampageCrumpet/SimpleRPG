using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.AppearanceTools
{
    /// <summary>
    /// How worn clothing becomes something that can lie on a floor. Armor is modelled on a standing body with its arms out; dropped on the ground
    /// as it is, it would be a person-shaped shell with sleeves in the air. A garment is instead tucked (the sleeves drawn in towards the body),
    /// laid face up, brought down to a size that fits a floor pickup, and set on its base, so it reads as a folded piece of armor. A hat or helmet is
    /// not folded: it only stands on its base. Pure geometry on vertex positions, so it is the same every time and can be tested without a mesh.
    /// </summary>
    public static class GarmentFold
    {
        /// <summary>The numbers the fold uses. Defaults are a starting point; a piece with a hand-made world form does not use them.</summary>
        public readonly struct Settings
        {
            public Settings(float bodyHalfWidth, float sleeveRetention, float largestSide)
            {
                BodyHalfWidth = bodyHalfWidth;
                SleeveRetention = sleeveRetention;
                LargestSide = largestSide;
            }

            /// <summary>How far from the middle the body ends and a sleeve begins, in metres.</summary>
            public float BodyHalfWidth { get; }

            /// <summary>How much of a sleeve's length past the body is kept (0.25 draws it in to a quarter).</summary>
            public float SleeveRetention { get; }

            /// <summary>The longest the folded piece may be on the floor, in metres. It is never enlarged to reach it.</summary>
            public float LargestSide { get; }

            public static Settings Default => new Settings(0.22f, 0.25f, 0.55f);
        }

        /// <summary>
        /// Folds the pieces of a garment together, in place: all of them share one tuck, one turn, one scale and one resting place, so a pair of
        /// gloves stays a pair. Vertices are in the body's space (up is +Y, forward +Z). Afterwards the pieces lie in the floor's plane with their
        /// lowest point at zero and their middle over the origin.
        /// </summary>
        public static void Fold(IList<Vector3[]> pieces, Settings settings)
        {
            foreach (Vector3[] piece in pieces)
            {
                for (int i = 0; i < piece.Length; i++)
                {
                    Vector3 v = piece[i];
                    float side = Mathf.Abs(v.x);
                    if (side > settings.BodyHalfWidth)
                    {
                        v.x = Mathf.Sign(v.x) * (settings.BodyHalfWidth + (side - settings.BodyHalfWidth) * settings.SleeveRetention);
                    }

                    // Laid face up: the body's forward becomes up and its up becomes the floor's far side.
                    piece[i] = new Vector3(v.x, v.z, -v.y);
                }
            }

            Rest(pieces, settings.LargestSide);
        }

        /// <summary>Stands the pieces on their base without turning them, brought to size if too large. For a helmet.</summary>
        public static void Stand(IList<Vector3[]> pieces, Settings settings) => Rest(pieces, settings.LargestSide);

        // Scales down to fit if needed, centres over the origin and sets the lowest point at zero.
        private static void Rest(IList<Vector3[]> pieces, float largestSide)
        {
            if (!TryBounds(pieces, out Bounds bounds))
            {
                return;
            }

            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = largest > largestSide && largest > 0f ? largestSide / largest : 1f;
            Vector3 centre = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (Vector3[] piece in pieces)
            {
                for (int i = 0; i < piece.Length; i++)
                {
                    piece[i] = (piece[i] - centre) * scale;
                }
            }
        }

        private static bool TryBounds(IList<Vector3[]> pieces, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Vector3[] piece in pieces)
            {
                foreach (Vector3 v in piece)
                {
                    if (!any)
                    {
                        bounds = new Bounds(v, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(v);
                    }
                }
            }

            return any;
        }
    }
}
