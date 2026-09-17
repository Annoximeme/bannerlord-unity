using System;
using System.IO;
using System.Linq;
using System.Text;
using Coop.Core.Persistence;
using Xunit;

namespace Coop.Core.Tests.Persistence
{
    /// <summary>Uses a real temp directory and real file I/O — this is exactly the mechanism CLAUDE.md §7 / SAVE_FORMAT.md §5 describe, not a simulation of it.</summary>
    public class SaveGenerationStoreTests : IDisposable
    {
        private readonly string _tempDir;

        public SaveGenerationStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "coop-save-tests-" + Guid.NewGuid());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);
        private static string Str(byte[] b) => Encoding.UTF8.GetString(b);

        [Fact]
        public void FirstWrite_CreatesGenerationOne()
        {
            var store = new SaveGenerationStore(_tempDir, "world");

            store.WriteNewGeneration(Bytes("v1"));

            Assert.Equal(new[] { 1 }, store.GetGenerationNumbers());
        }

        [Fact]
        public void SuccessiveWrites_IncrementGenerationNumbers()
        {
            var store = new SaveGenerationStore(_tempDir, "world");

            store.WriteNewGeneration(Bytes("v1"));
            store.WriteNewGeneration(Bytes("v2"));
            store.WriteNewGeneration(Bytes("v3"));

            Assert.Equal(new[] { 1, 2, 3 }, store.GetGenerationNumbers().OrderBy(n => n));
        }

        [Fact]
        public void LoadLatestValid_ReturnsTheNewestGeneration()
        {
            var store = new SaveGenerationStore(_tempDir, "world");
            store.WriteNewGeneration(Bytes("older"));
            store.WriteNewGeneration(Bytes("newest"));

            bool ok = store.TryLoadLatestValid(out byte[] payload, out int generation);

            Assert.True(ok);
            Assert.Equal("newest", Str(payload));
            Assert.Equal(2, generation);
        }

        [Fact]
        public void PreviousGenerationSurvives_WhenNewestWriteIsInterruptedMidWrite()
        {
            // This is the literal Phase 1.9 exit criterion: "a save interrupted mid-write
            // leaves the previous generation intact and loadable." We simulate the
            // interruption directly: a real crash mid-write would leave only the .tmp file
            // (WriteNewGeneration renames it into place only after the write completes), so
            // there would be no new "world.0002.sav" — exactly reproduced here by creating
            // the .tmp file and nothing else, then confirming generation 1 still loads.
            var store = new SaveGenerationStore(_tempDir, "world");
            store.WriteNewGeneration(Bytes("good generation"));

            File.WriteAllBytes(Path.Combine(_tempDir, "world.0002.sav.tmp"), Bytes("truncated garbage"));

            bool ok = store.TryLoadLatestValid(out byte[] payload, out int generation);

            Assert.True(ok);
            Assert.Equal("good generation", Str(payload));
            Assert.Equal(1, generation); // the interrupted write never became generation 2
        }

        [Fact]
        public void CorruptedNewestGeneration_FallsBackToThePreviousValidOne()
        {
            var store = new SaveGenerationStore(_tempDir, "world");
            store.WriteNewGeneration(Bytes("good"));
            string secondPath = store.WriteNewGeneration(Bytes("will be corrupted"));

            byte[] raw = File.ReadAllBytes(secondPath);
            raw[raw.Length - 1] ^= 0xFF; // corrupt the newest generation after the fact
            File.WriteAllBytes(secondPath, raw);

            bool ok = store.TryLoadLatestValid(out byte[] payload, out int generation);

            Assert.True(ok);
            Assert.Equal("good", Str(payload));
            Assert.Equal(1, generation);
        }

        [Fact]
        public void AllGenerationsCorrupted_ReturnsFalse_NeverFabricatesData()
        {
            var store = new SaveGenerationStore(_tempDir, "world");
            string path = store.WriteNewGeneration(Bytes("only generation"));
            byte[] raw = File.ReadAllBytes(path);
            raw[raw.Length - 1] ^= 0xFF;
            File.WriteAllBytes(path, raw);

            bool ok = store.TryLoadLatestValid(out byte[] payload, out _);

            Assert.False(ok);
            Assert.Null(payload);
        }

        [Fact]
        public void NoGenerationsYet_ReturnsFalse()
        {
            var store = new SaveGenerationStore(_tempDir, "world");

            bool ok = store.TryLoadLatestValid(out _, out _);

            Assert.False(ok);
        }

        [Fact]
        public void Prune_KeepsOnlyTheConfiguredNumberOfNewestGenerations()
        {
            var store = new SaveGenerationStore(_tempDir, "world", keepGenerations: 2);

            store.WriteNewGeneration(Bytes("1"));
            store.WriteNewGeneration(Bytes("2"));
            store.WriteNewGeneration(Bytes("3"));

            Assert.Equal(new[] { 2, 3 }, store.GetGenerationNumbers().OrderBy(n => n));
        }
    }
}
