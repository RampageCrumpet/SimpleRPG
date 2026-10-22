using System;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// The permanent identity of a gameplay definition (ability, status, item and so on). It is independent of the asset's name, path and C# type,
    /// and is a 128-bit value so independently created definitions never collide.
    /// </summary>
    public readonly struct DefinitionId : IEquatable<DefinitionId>
    {
        private readonly Guid value;

        private DefinitionId(Guid value)
        {
            this.value = value;
        }

        /// <summary>
        /// The empty ID, which identifies no definition.
        /// </summary>
        public static DefinitionId None => default;

        /// <summary>
        /// True if this ID identifies no definition.
        /// </summary>
        public bool IsNone => value == Guid.Empty;

        /// <summary>
        /// Creates a new, unique ID.
        /// </summary>
        public static DefinitionId NewId() => new DefinitionId(Guid.NewGuid());

        /// <summary>
        /// Creates an ID from an existing GUID.
        /// </summary>
        public static DefinitionId FromGuid(Guid guid) => new DefinitionId(guid);

        /// <summary>
        /// Reads an ID from its <see cref="ToString"/> form.
        /// </summary>
        /// <returns>True if <paramref name="text"/> was a valid ID. An empty or null string is not valid.</returns>
        public static bool TryParse(string text, out DefinitionId id)
        {
            if (!string.IsNullOrEmpty(text) && Guid.TryParseExact(text, "N", out Guid guid))
            {
                id = new DefinitionId(guid);
                return true;
            }

            id = None;
            return false;
        }

        /// <summary>
        /// The ID as a GUID.
        /// </summary>
        public Guid ToGuid() => value;

        /// <inheritdoc/>
        public bool Equals(DefinitionId other) => value == other.value;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is DefinitionId other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => value.GetHashCode();

        /// <summary>
        /// The ID as 32 lowercase hex digits, the form stored in assets.
        /// </summary>
        public override string ToString() => value.ToString("N");

        public static bool operator ==(DefinitionId a, DefinitionId b) => a.Equals(b);

        public static bool operator !=(DefinitionId a, DefinitionId b) => !a.Equals(b);
    }
}
