using System.Collections.Generic;
using System.Security.Cryptography;

namespace BSNet
{
    internal static class Cryptography
    {
        private static RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        private static readonly uint[] ChecksumTable;
        private static readonly uint Polynomial = 0xEDB88320;

        static Cryptography()
        {
            ChecksumTable = new uint[0x100];

            for (uint index = 0; index < 0x100; ++index)
            {
                uint item = index;
                for (int bit = 0; bit < 8; ++bit)
                    item = ((item & 1) != 0) ? (Polynomial ^ (item >> 1)) : (item >> 1);
                ChecksumTable[index] = item;
            }
        }

        public static uint CRC32UInt(IEnumerable<byte> bytes)
        {
            uint result = 0xFFFFFFFF;

            foreach (byte current in bytes)
                result = ChecksumTable[(result & 0xFF) ^ current] ^ (result >> 8);
            
            return ~result;
        }

        public static byte[] CRC32Bytes(IEnumerable<byte> bytes)
        {
            uint result = 0xFFFFFFFF;

            foreach (byte current in bytes)
                result = ChecksumTable[(result & 0xFF) ^ current] ^ (result >> 8);

            result = ~result;
            byte[] crc = new byte[4];
            crc[0] = (byte)(result >> 24);
            crc[1] = (byte)(result >> 16);
            crc[2] = (byte)(result >> 8);
            crc[3] = (byte)result;
            return crc;
        }

        public static void GetBytes(byte[] bytes) => rng.GetBytes(bytes);

        public static ulong GenerateToken()
        {
            byte[] token = new byte[8];
            GetBytes(token);
            return (ulong)token[0] << 56 |
                (ulong)token[1] << 48 |
                (ulong)token[2] << 40 |
                (ulong)token[3] << 32 |
                (ulong)token[4] << 24 |
                (ulong)token[5] << 16 |
                (ulong)token[6] << 8 |
                (ulong)token[7];
        }
    }
}
