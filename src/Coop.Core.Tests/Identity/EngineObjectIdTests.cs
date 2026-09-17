using Coop.Core.Identity;
using Xunit;

namespace Coop.Core.Tests.Identity
{
    public class EngineObjectIdTests
    {
        [Fact]
        public void Equality_SameValue_AreEqual()
        {
            Assert.Equal(new EngineObjectId(42), new EngineObjectId(42));
            Assert.True(new EngineObjectId(42) == new EngineObjectId(42));
        }

        [Fact]
        public void Equality_DifferentValue_AreNotEqual()
        {
            Assert.NotEqual(new EngineObjectId(1), new EngineObjectId(2));
            Assert.True(new EngineObjectId(1) != new EngineObjectId(2));
        }
    }
}
