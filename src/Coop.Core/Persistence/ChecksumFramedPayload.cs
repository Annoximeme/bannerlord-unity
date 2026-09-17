using System;
using System.Security.Cryptography;

namespace Coop.Core.Persistence
{
    /// <summary>
    /// Wraps a payload with a leading SHA-256 hash so corruption is detectable on load —
    /// SAVE_FORMAT.md §5 "Corruption detection". "Fail loudly, never silently": a mismatch
    /// makes <see cref="TryUnwrap"/> return <c>false</c> rather than returning corrupt data.
    /// </summary>
    public static class ChecksumFramedPayload
    {
        private const int HashLength = 32; // SHA-256

        public static byte[] Wrap(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            byte[] hash = Hash(payload);
            var framed = new byte[HashLength + payload.Length];
            Buffer.BlockCopy(hash, 0, framed, 0, HashLength);
            Buffer.BlockCopy(payload, 0, framed, HashLength, payload.Length);
            return framed;
        }

        public static bool TryUnwrap(byte[] framed, out byte[] payload)
        {
            if (framed == null || framed.Length < HashLength)
            {
                payload = null;
                return false;
            }

            var storedHash = new byte[HashLength];
            Buffer.BlockCopy(framed, 0, storedHash, 0, HashLength);

            var candidate = new byte[framed.Length - HashLength];
            Buffer.BlockCopy(framed, HashLength, candidate, 0, candidate.Length);

            byte[] actualHash = Hash(candidate);
            if (!TimingSafeEqual(storedHash, actualHash))
            {
                payload = null;
                return false;
            }

            payload = candidate;
            return true;
        }

        private static byte[] Hash(byte[] data)
        {
            using SHA256 sha = SHA256.Create();
            return sha.ComputeHash(data);
        }

        private static bool TimingSafeEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
