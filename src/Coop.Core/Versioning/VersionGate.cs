namespace Coop.Core.Versioning
{
    /// <summary>
    /// CLAUDE.md §2 / docs/VERSION_SUPPORT.md §7.3: refuse to run on an unpinned game
    /// version rather than silently targeting whatever happens to be installed.
    /// </summary>
    public sealed class VersionGate
    {
        public GameVersion Pinned { get; }

        public VersionGate(GameVersion pinned)
        {
            Pinned = pinned;
        }

        public bool Accepts(GameVersion running) => running == Pinned;
    }
}
