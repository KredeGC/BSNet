using System.Collections.Generic;
using System.Security.Cryptography;

namespace BSNet
{
    internal static class Cryptography
    {
        private static RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        private static readonly uint[] ChecksumTable;
        private const uint POLYNOMIAL = 0xEDB88320;

        static Cryptography()
        {
            // Initiate the table for checksums
            ChecksumTable = new uint[0x100];

            for (uint index = 0; index < 0x100; ++index)
            {
                uint item = index;
                for (int bit = 0; bit < 8; ++bit)
                    item = ((item & 1) != 0) ? (POLYNOMIAL ^ (item >> 1)) : (item >> 1);
                ChecksumTable[index] = item;
            }
        }

        /// <summary>
        /// Calculate the checksum of a given byte array
        /// </summary>
        /// <param name="bytes">The byte array to calculate a checksum for</param>
        /// <returns>The checksum in uint format</returns>
        public static uint CRC32UInt(IEnumerable<byte> bytes)
        {
            uint result = 0xFFFFFFFF;

            foreach (byte current in bytes)
                result = ChecksumTable[(result & 0xFF) ^ current] ^ (result >> 8);
            
            return ~result;
        }

        /// <summary>
        /// Calculate the checksum of a given byte array
        /// </summary>
        /// <param name="bytes">The byte array to calculate a checksum for</param>
        /// <returns>The checksum in byte array format</returns>
        public static byte[] CRC32Bytes(IEnumerable<byte> bytes)
        {
            uint result = 0xFFFFFFFF;

            foreach (byte current in bytes)
                result = ChecksumTable[(result & 0xFF) ^ current] ^ (result >> 8);

            result = ~result;
            byte[] crc = BSPool.GetBuffer(4);
            crc[0] = (byte)(result >> 24);
            crc[1] = (byte)(result >> 16);
            crc[2] = (byte)(result >> 8);
            crc[3] = (byte)result;
            return crc;
        }

        /// <summary>
        /// Populate an array of cryptographically secure bytes
        /// </summary>
        /// <param name="bytes">The byte array to populate</param>
        public static void GetBytes(byte[] bytes) => rng.GetBytes(bytes);

        /// <summary>
        /// Generate a cryptographically secure token
        /// </summary>
        /// <returns>The generated token</returns>
        public static ulong GenerateToken()
        {
            byte[] tokenBytes = BSPool.GetBuffer(8);
            GetBytes(tokenBytes);

            ulong token = (ulong)tokenBytes[0] << 56 |
                (ulong)tokenBytes[1] << 48 |
                (ulong)tokenBytes[2] << 40 |
                (ulong)tokenBytes[3] << 32 |
                (ulong)tokenBytes[4] << 24 |
                (ulong)tokenBytes[5] << 16 |
                (ulong)tokenBytes[6] << 8 |
                (ulong)tokenBytes[7];

            BSPool.ReturnBuffer(tokenBytes);

            return token;
        }
    }
}
