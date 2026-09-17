using Coop.Core.Identity;
using Xunit;

namespace Coop.Core.Tests.Identity
{
    public class PartyBaseIdTests
    {
        [Fact]
        public void Equality_SameOwnerAndKind_AreEqual()
        {
            var a = new PartyBaseId(new EngineObjectId(7), OwnerKind.MobileParty);
            var b = new PartyBaseId(new EngineObjectId(7), OwnerKind.MobileParty);

            Assert.Equal(a, b);
        }

        [Fact]
        public void Equality_SameOwnerIdDifferentKind_AreNotEqual()
        {
            // A MobileParty and a Settlement could coincidentally share the same raw
            // MBGUID value space distinction shouldn't collapse — kind is part of identity.
            var asParty = new PartyBaseId(new EngineObjectId(7), OwnerKind.MobileParty);
            var asSettlement = new PartyBaseId(new EngineObjectId(7), OwnerKind.Settlement);

            Assert.NotEqual(asParty, asSettlement);
        }

        [Fact]
        public void Equality_DifferentOwnerId_AreNotEqual()
        {
            var a = new PartyBaseId(new EngineObjectId(1), OwnerKind.Settlement);
            var b = new PartyBaseId(new EngineObjectId(2), OwnerKind.Settlement);

            Assert.NotEqual(a, b);
        }
    }
}
