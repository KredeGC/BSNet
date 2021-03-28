using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace BSNet
{
    public static class BSUtility
    {
        public const int BYTE_BITS = 8;

        private static byte[] sBitMasks;

        static BSUtility()
        {
            // Bit 0 isn't used
            int maskCount = BYTE_BITS + 1;
            sBitMasks = new byte[maskCount];

            for (int i = 1; i < maskCount; i++)
            {
                byte bitMask = 0;
                int bitCount = 0;
                for (byte j = 1; bitCount < i; j <<= 1, bitCount++)
                    bitMask |= j;
                sBitMasks[i] = bitMask;
            }
        }

        public static byte GetNarrowingMask(int bitCount) => sBitMasks[bitCount];

        public static byte GetWideningMask(int bitCount, int startBit)
        {
            byte bitMask = sBitMasks[bitCount];
            bitMask <<= (BYTE_BITS - bitCount);
            bitMask >>= startBit;
            return bitMask;
        }

        /// <summary>
        /// Return this machines local IPAddress, hopefully
        /// </summary>
        /// <returns></returns>
        public static IPAddress GetLocalIP()
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties adapterProperties = networkInterface.GetIPProperties();
                    if (adapterProperties.GatewayAddresses.FirstOrDefault() != null)
                    {
                        foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                return ip.Address;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Returns whether a sequence is newer than another
        /// </summary>
        /// <param name="seq1">The first sequence</param>
        /// <param name="seq2">The second sequence</param>
        /// <returns></returns>
        public static bool IsGreaterThan(ushort seq1, ushort seq2)
        {
            return ((seq1 > seq2) && (seq1 - seq2 <= 32768)) ||
               ((seq1 < seq2) && (seq2 - seq1 > 32768));
        }

        /// <summary>
        /// Returns whether the given sequence is acknowledged
        /// </summary>
        /// <param name="bits"></param>
        /// <param name="lastSequence"></param>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static bool IsAcknowledged(uint bits, ushort lastSequence, ushort sequence)
        {
            if (lastSequence == sequence)
                return true;
            if (IsGreaterThan(sequence, lastSequence))
                return false;

            int shift = lastSequence - sequence - 1;
            if (shift < 0)
                shift += 65536 + 1;

            uint add = 1U << shift;

            return (bits & add) != 0;
        }
    }
}
