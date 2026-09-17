namespace Coop.Core.Persistence
{
    /// <summary>One explicit, ordered migration step for a persisted blob's schema (SAVE_FORMAT.md §5): <c>FromVersion → ToVersion</c>. No implicit field defaulting.</summary>
    public interface ISchemaMigration
    {
        uint FromVersion { get; }
        uint ToVersion { get; }
        byte[] Migrate(byte[] data);
    }
}
