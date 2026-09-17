using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Coop.Core.Persistence
{
    /// <summary>
    /// Manages a rotating set of numbered generations of a named save file on disk, per
    /// SAVE_FORMAT.md §5 / CLAUDE.md §7: "Never overwrite the only valid save." Each write
    /// goes to a new generation number via a temp-file-then-rename (atomic on the same
    /// volume), so a crash mid-write can never corrupt or replace the previous good
    /// generation — the write either lands whole under its new name, or not at all, and the
    /// old file is untouched either way. Reads verify a checksum and fall back to older
    /// generations rather than ever returning silently-corrupt data.
    /// </summary>
    public sealed class SaveGenerationStore
    {
        private static readonly Regex GenerationFilePattern = new Regex(@"^(?<base>.+)\.(?<gen>\d+)\.sav$", RegexOptions.Compiled);

        private readonly string _directory;
        private readonly string _baseName;
        private readonly int _keepGenerations;

        public SaveGenerationStore(string directory, string baseName, int keepGenerations = 10)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Directory is required.", nameof(directory));
            if (string.IsNullOrEmpty(baseName)) throw new ArgumentException("Base name is required.", nameof(baseName));
            if (keepGenerations < 1) throw new ArgumentOutOfRangeException(nameof(keepGenerations), "Must keep at least 1 generation.");

            _directory = directory;
            _baseName = baseName;
            _keepGenerations = keepGenerations;
            Directory.CreateDirectory(_directory);
        }

        /// <summary>Writes a new, highest-numbered generation and prunes anything beyond the keep count. Returns the path written.</summary>
        public string WriteNewGeneration(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            int next = GetGenerationNumbers().DefaultIfEmpty(0).Max() + 1;
            string finalPath = PathFor(next);
            string tempPath = finalPath + ".tmp";

            byte[] framed = ChecksumFramedPayload.Wrap(data);
            File.WriteAllBytes(tempPath, framed);
            File.Move(tempPath, finalPath); // same-volume rename — atomic; finalPath never previously existed

            Prune();
            return finalPath;
        }

        /// <summary>
        /// Tries the newest generation first; if its checksum doesn't verify (corrupt/truncated),
        /// falls back to the next-newest, and so on. Never returns unverified data.
        /// </summary>
        public bool TryLoadLatestValid(out byte[] payload, out int generation)
        {
            foreach (int gen in GetGenerationNumbers().OrderByDescending(n => n))
            {
                byte[] raw;
                try
                {
                    raw = File.ReadAllBytes(PathFor(gen));
                }
                catch (IOException)
                {
                    continue;
                }

                if (ChecksumFramedPayload.TryUnwrap(raw, out payload))
                {
                    generation = gen;
                    return true;
                }
                // Checksum mismatch — this generation is corrupt. Fall back, don't stop here.
            }

            payload = null;
            generation = 0;
            return false;
        }

        /// <summary>All generation numbers currently on disk for this store, in no particular order.</summary>
        public IReadOnlyList<int> GetGenerationNumbers()
        {
            var numbers = new List<int>();
            if (!Directory.Exists(_directory))
            {
                return numbers;
            }

            foreach (string file in Directory.EnumerateFiles(_directory, $"{_baseName}.*.sav"))
            {
                Match match = GenerationFilePattern.Match(Path.GetFileName(file));
                if (match.Success && match.Groups["base"].Value == _baseName &&
                    int.TryParse(match.Groups["gen"].Value, out int gen))
                {
                    numbers.Add(gen);
                }
            }
            return numbers;
        }

        private void Prune()
        {
            foreach (int gen in GetGenerationNumbers().OrderByDescending(n => n).Skip(_keepGenerations))
            {
                try
                {
                    File.Delete(PathFor(gen));
                }
                catch (IOException)
                {
                    // Best-effort pruning; a leftover old generation is not a correctness problem.
                }
            }
        }

        private string PathFor(int generation) => Path.Combine(_directory, $"{_baseName}.{generation:D4}.sav");
    }
}
