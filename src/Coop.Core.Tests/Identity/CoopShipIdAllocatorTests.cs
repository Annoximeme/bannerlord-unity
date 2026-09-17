using Coop.Core.Identity;
using Xunit;

namespace Coop.Core.Tests.Identity
{
    public class CoopShipIdAllocatorTests
    {
        [Fact]
        public void Allocate_StartsAtOne_NeverZero()
        {
            var allocator = new CoopShipIdAllocator();

            CoopShipId first = allocator.Allocate();

            Assert.Equal(1ul, first.Value);
            Assert.False(first.IsNone);
        }

        [Fact]
        public void Allocate_NeverRepeats()
        {
            var allocator = new CoopShipIdAllocator();

            CoopShipId a = allocator.Allocate();
            CoopShipId b = allocator.Allocate();
            CoopShipId c = allocator.Allocate();

            Assert.NotEqual(a, b);
            Assert.NotEqual(b, c);
            Assert.NotEqual(a, c);
        }

        [Fact]
        public void ObserveMinimumNext_FastForwardsPastAPersistedValue()
        {
            var allocator = new CoopShipIdAllocator();
            allocator.Allocate(); // 1
            allocator.Allocate(); // 2

            allocator.ObserveMinimumNext(500);

            Assert.Equal(500ul, allocator.Allocate().Value);
        }

        [Fact]
        public void ObserveMinimumNext_NeverMovesBackward()
        {
            var allocator = new CoopShipIdAllocator(startAt: 100);

            allocator.ObserveMinimumNext(50); // lower than current — must be ignored

            Assert.Equal(100ul, allocator.Allocate().Value);
        }

        [Fact]
        public void ZeroStartAt_TreatedAsOne_SinceZeroIsNone()
        {
            var allocator = new CoopShipIdAllocator(startAt: 0);

            Assert.Equal(1ul, allocator.Allocate().Value);
        }
    }
}
