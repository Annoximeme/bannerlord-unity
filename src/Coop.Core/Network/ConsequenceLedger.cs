using System;
using System.Collections.Generic;

namespace Coop.Core.Network
{
    /// <summary>
    /// ARCHITECTURE.md §10: "Effects are applied exactly once, inside a guard that
    /// checks-and-records atomically." A duplicated or replayed packet must never duplicate
    /// loot, gold, XP, casualties, ship changes, or any other campaign consequence.
    ///
    /// In-memory only for now — persisting the applied-set into the save of record (so
    /// idempotency survives a server restart, per the same section) is Phase 1.9 wiring;
    /// <see cref="Snapshot"/>/<see cref="Restore"/> exist so that wiring has something to call.
    /// </summary>
    public sealed class ConsequenceLedger
    {
        private readonly HashSet<ConsequenceId> _applied = new HashSet<ConsequenceId>();
        private readonly object _sync = new object();

        /// <summary>
        /// Applies <paramref name="apply"/> and records the id — but only the first time this
        /// id is seen. A replayed id is recognized and <paramref name="apply"/> is never
        /// invoked again. Returns whether this call actually applied it (false = it was a
        /// replay). The record only happens after <paramref name="apply"/> returns without
        /// throwing, so a failed attempt can legitimately be retried with the same id later.
        /// </summary>
        public bool TryApply(ConsequenceId id, Action apply)
        {
            if (apply == null) throw new ArgumentNullException(nameof(apply));

            lock (_sync)
            {
                if (_applied.Contains(id))
                {
                    return false;
                }

                apply();
                _applied.Add(id);
                return true;
            }
        }

        public bool HasApplied(ConsequenceId id)
        {
            lock (_sync)
            {
                return _applied.Contains(id);
            }
        }

        public IReadOnlyCollection<ConsequenceId> Snapshot()
        {
            lock (_sync)
            {
                return new List<ConsequenceId>(_applied);
            }
        }

        public void Restore(IEnumerable<ConsequenceId> previouslyApplied)
        {
            if (previouslyApplied == null) throw new ArgumentNullException(nameof(previouslyApplied));

            lock (_sync)
            {
                foreach (ConsequenceId id in previouslyApplied)
                {
                    _applied.Add(id);
                }
            }
        }
    }
}
