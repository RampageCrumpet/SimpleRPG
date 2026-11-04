namespace SimpleRPG.Appearance
{
    /// <summary>
    /// The named attachment points on the character rig, besides the skeleton's bones. A socket is a transform authored in the body model (an
    /// empty under the bone it belongs to, posed in the art package) and carried into the rig prefab, so where a thing sits on the body is body
    /// art data, never computed per item. A rigid <see cref="BodyPartPiece"/> attaches to a socket exactly as it does to a bone, by name.
    /// </summary>
    public static class RigSockets
    {
        /// <summary>
        /// Where headgear sits, under the <c>Head</c> bone. Its origin is where a helmet's own origin goes (the bottom centre of the helmet) and
        /// its axes are the helmet's: up, and forward the way the face looks. Head pieces are authored with that origin and orientation.
        /// </summary>
        public const string Headgear = "Socket_Headgear";
    }
}
