using System;
using System.Text;
using Coop.Core.Persistence;
using Xunit;

namespace Coop.Core.Tests.Persistence
{
    public class SchemaMigrationChainTests
    {
        private sealed class AppendSuffixMigration : ISchemaMigration
        {
            public uint FromVersion { get; }
            public uint ToVersion { get; }
            private readonly string _suffix;

            public AppendSuffixMigration(uint from, uint to, string suffix)
            {
                FromVersion = from;
                ToVersion = to;
                _suffix = suffix;
            }

            public byte[] Migrate(byte[] data) =>
                Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(data) + _suffix);
        }

        [Fact]
        public void SameVersion_ReturnsDataUnchanged()
        {
            var chain = new SchemaMigrationChain();

            byte[] result = chain.MigrateTo(1, Encoding.UTF8.GetBytes("data"), 1);

            Assert.Equal("data", Encoding.UTF8.GetString(result));
        }

        [Fact]
        public void SingleStep_AppliesOnce()
        {
            var chain = new SchemaMigrationChain();
            chain.Register(new AppendSuffixMigration(1, 2, "+v2"));

            byte[] result = chain.MigrateTo(1, Encoding.UTF8.GetBytes("data"), 2);

            Assert.Equal("data+v2", Encoding.UTF8.GetString(result));
        }

        [Fact]
        public void MultiStepChain_AppliesEachStepInOrder()
        {
            var chain = new SchemaMigrationChain();
            chain.Register(new AppendSuffixMigration(1, 2, "+v2"));
            chain.Register(new AppendSuffixMigration(2, 3, "+v3"));

            byte[] result = chain.MigrateTo(1, Encoding.UTF8.GetBytes("data"), 3);

            Assert.Equal("data+v2+v3", Encoding.UTF8.GetString(result));
        }

        [Fact]
        public void GapInTheChain_ThrowsRatherThanDefaulting()
        {
            var chain = new SchemaMigrationChain();
            chain.Register(new AppendSuffixMigration(1, 2, "+v2"));
            // No migration registered from 2 -> 3.

            Assert.Throws<InvalidOperationException>(() =>
                chain.MigrateTo(1, Encoding.UTF8.GetBytes("data"), 3));
        }

        [Fact]
        public void NewerThanSupported_RefusesToLoad()
        {
            var chain = new SchemaMigrationChain();

            Assert.Throws<InvalidOperationException>(() =>
                chain.MigrateTo(currentVersion: 5, Encoding.UTF8.GetBytes("data"), targetVersion: 1));
        }

        [Fact]
        public void RegisteringABackwardsOrEqualMigration_Throws()
        {
            var chain = new SchemaMigrationChain();

            Assert.Throws<ArgumentException>(() => chain.Register(new AppendSuffixMigration(2, 2, "noop")));
            Assert.Throws<ArgumentException>(() => chain.Register(new AppendSuffixMigration(3, 2, "backwards")));
        }
    }
}
