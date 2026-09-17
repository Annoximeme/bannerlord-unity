namespace Coop.Core.Identity
{
    /// <summary>
    /// Monotonic <see cref="CoopShipId"/> allocator. Ids start at 1 — 0 is
    /// <see cref="CoopShipId.None"/> — and are never reused, including across a server
    /// restart: <see cref="ObserveMinimumNext"/> fast-forwards past whatever "next id" was
    /// persisted before this allocator instance existed for the current run
    /// (docs/SYNCHRONIZATION_MODEL.md §4.3's <c>nextId</c> field).
    /// </summary>
    public sealed class CoopShipIdAllocator
    {
        private ulong _next;

        public CoopShipIdAllocator(ulong startAt = 1)
        {
            _next = startAt == 0 ? 1 : startAt;
        }

        /// <summary>The value the next <see cref="Allocate"/> call will hand out.</summary>
        public ulong NextValue => _next;

        public CoopShipId Allocate()
        {
            var id = new CoopShipId(_next);
            _next++;
            return id;
        }

        /// <summary>Ensures future allocations never go below a previously persisted value.</summary>
        public void ObserveMinimumNext(ulong atLeast)
        {
            if (atLeast > _next)
            {
                _next = atLeast;
            }
        }
    }
}
