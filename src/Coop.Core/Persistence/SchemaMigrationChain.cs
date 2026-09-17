using System;
using System.Collections.Generic;

namespace Coop.Core.Persistence
{
    /// <summary>
    /// SAVE_FORMAT.md §5: "Refuse to load an unknown-newer schema; migrate a known-older
    /// one." "Explicit, ordered migration steps vN → vN+1. A save may only be loaded via a
    /// complete migration chain; no implicit field defaulting."
    /// </summary>
    public sealed class SchemaMigrationChain
    {
        private readonly Dictionary<uint, ISchemaMigration> _byFromVersion = new Dictionary<uint, ISchemaMigration>();

        public void Register(ISchemaMigration migration)
        {
            if (migration == null) throw new ArgumentNullException(nameof(migration));
            if (migration.ToVersion <= migration.FromVersion)
            {
                throw new ArgumentException(
                    $"A migration must move forward: got FromVersion={migration.FromVersion}, ToVersion={migration.ToVersion}.",
                    nameof(migration));
            }
            _byFromVersion[migration.FromVersion] = migration;
        }

        /// <summary>
        /// Applies as many registered steps as needed to reach <paramref name="targetVersion"/>.
        /// Throws if the current version is newer than the target (refuse to load an
        /// unknown-newer schema) or if there's a gap in the chain (no implicit defaulting).
        /// </summary>
        public byte[] MigrateTo(uint currentVersion, byte[] data, uint targetVersion)
        {
            if (currentVersion > targetVersion)
            {
                throw new InvalidOperationException(
                    $"Save schema v{currentVersion} is newer than this build supports (v{targetVersion}) — refusing to load.");
            }

            while (currentVersion < targetVersion)
            {
                if (!_byFromVersion.TryGetValue(currentVersion, out ISchemaMigration step))
                {
                    throw new InvalidOperationException(
                        $"No migration registered from schema v{currentVersion} — cannot reach v{targetVersion}.");
                }

                data = step.Migrate(data);
                currentVersion = step.ToVersion;
            }

            return data;
        }
    }
}
