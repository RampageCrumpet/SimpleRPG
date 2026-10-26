namespace SimpleRPG.Appearance
{
    /// <summary>
    /// A place on a character's body that holds at most one visual part. Values are sent over the network, so append new slots and never renumber.
    /// </summary>
    public enum BodyPartSlot
    {
        Head = 0,
        Torso = 1,
        Legs = 2,
        FootLeft = 3,
        FootRight = 4,
        HandLeft = 5,
        HandRight = 6,
        Eyebrows = 7,
    }
}
