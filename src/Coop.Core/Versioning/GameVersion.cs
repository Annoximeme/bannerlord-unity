using System;
using System.Text.RegularExpressions;

namespace Coop.Core.Versioning
{
    /// <summary>
    /// A game-agnostic major.minor.revision triple. Deliberately does not know about
    /// TaleWorlds.Library.ApplicationVersion or its "v" prefix / changeset / branch
    /// concepts — Coop.GameInterface adapts those into this shape.
    /// </summary>
    public readonly struct GameVersion : IEquatable<GameVersion>
    {
        private static readonly Regex Pattern = new Regex(
            @"^v?(?<major>\d+)\.(?<minor>\d+)\.(?<revision>\d+)",
            RegexOptions.Compiled);

        public int Major { get; }
        public int Minor { get; }
        public int Revision { get; }

        public GameVersion(int major, int minor, int revision)
        {
            Major = major;
            Minor = minor;
            Revision = revision;
        }

        /// <summary>
        /// Parses the leading "major.minor.revision" of a version string, tolerating an
        /// optional "v" prefix and ignoring anything after the third component (e.g. a
        /// trailing ".119303" changeset) — mirrors what module SubModule.xml versions and
        /// TaleWorlds.Library.ApplicationVersion.ToString() both produce.
        /// </summary>
        public static bool TryParse(string text, out GameVersion version)
        {
            if (text != null)
            {
                Match match = Pattern.Match(text);
                if (match.Success)
                {
                    version = new GameVersion(
                        int.Parse(match.Groups["major"].Value),
                        int.Parse(match.Groups["minor"].Value),
                        int.Parse(match.Groups["revision"].Value));
                    return true;
                }
            }

            version = default;
            return false;
        }

        public bool Equals(GameVersion other) =>
            Major == other.Major && Minor == other.Minor && Revision == other.Revision;

        public override bool Equals(object obj) => obj is GameVersion other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Major;
                hash = hash * 31 + Minor;
                hash = hash * 31 + Revision;
                return hash;
            }
        }

        public override string ToString() => $"v{Major}.{Minor}.{Revision}";

        public static bool operator ==(GameVersion left, GameVersion right) => left.Equals(right);
        public static bool operator !=(GameVersion left, GameVersion right) => !left.Equals(right);
    }
}
