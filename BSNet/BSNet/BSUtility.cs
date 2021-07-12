using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace BSNet
{
    public static class BSUtility
    {
        public const int TIMEOUT = 10;
        public const int BITS = 8;
        public const int RTT_BUFFER_SIZE = 256;
        public const int RECEIVE_BUFFER_SIZE = 1024;

        private static readonly byte[] sBitMasks;

        static BSUtility()
        {
            // Bit 0 isn't used
            int maskCount = BITS + 1;
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
            bitMask <<= (BITS - bitCount);
            bitMask >>= startBit;
            return bitMask;
        }

        /// <summary>
        /// Return this NAT's external IPAddress, sometimes
        /// </summary>
        /// <returns></returns>
        public static IPAddress GetExternalIP()
        {
            IEnumerable<IPAddress> gateways = from networkInterface in NetworkInterface.GetAllNetworkInterfaces()
                                              where
                                                  networkInterface.OperationalStatus == OperationalStatus.Up ||
                                                  networkInterface.OperationalStatus == OperationalStatus.Unknown
                                              from address in networkInterface.GetIPProperties().GatewayAddresses
                                              where address.Address.AddressFamily == AddressFamily.InterNetwork
                                              select address.Address;


            IEnumerable<IPAddress> unicastAddresses = from networkInterface in NetworkInterface.GetAllNetworkInterfaces()
                                                      where
                                                          networkInterface.OperationalStatus == OperationalStatus.Up ||
                                                          networkInterface.OperationalStatus == OperationalStatus.Unknown
                                                      from address in networkInterface.GetIPProperties().UnicastAddresses
                                                      where address.Address.AddressFamily == AddressFamily.InterNetwork
                                                      select address.Address;


            foreach (IPAddress uniAddress in unicastAddresses)
            {
                try
                {
                    using (UdpClient client = new UdpClient(new IPEndPoint(uniAddress, 0)))
                    {
                        foreach (IPAddress gateway in gateways)
                        {
                            IPEndPoint gatewayEndPoint = new IPEndPoint(gateway, 5351); // PMP Port is 5351

                            // Send a request
                            try
                            {
                                byte[] buffer = new byte[] { 0, 0 };
                                client.Send(buffer, buffer.Length, gatewayEndPoint);
                            }
                            catch (SocketException) { continue; }

                            // Receive external IP
                            IPEndPoint receivedFrom = new IPEndPoint(IPAddress.None, 0);
                            byte[] response = client.Receive(ref receivedFrom);

                            if (response.Length != 12 || response[0] != 0 || response[1] != 128) // 128 is server NOOP
                                continue;

                            byte[] addressBytes = new[] { response[8], response[9], response[10], response[11] };
                            return new IPAddress(addressBytes);
                        }
                    }
                }
                catch (SocketException) { continue; }
            }

            return null;
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
        /// Shifts the bits in an array of bytes to the left.
        /// </summary>
        /// <param name="bytes">The byte array to shift.</param>
        public static void ShiftLeft(byte[] bytes)
        {
            // Iterate through the elements of the array from left to right.
            for (int index = 0; index < bytes.Length; index++)
            {
                // If the leftmost bit of the current byte is 1 then we have a carry.
                bool carryFlag = (bytes[index] & 0x80) > 0;

                if (index > 0)
                {
                    if (carryFlag == true)
                    {
                        // Apply the carry to the rightmost bit of the current bytes neighbor to the left.
                        bytes[index - 1] = (byte)(bytes[index - 1] | 0x01);
                    }
                }

                bytes[index] = (byte)(bytes[index] << 1);
            }
        }

        /// <summary>
        /// Shifts the bits in an array of bytes to the right.
        /// </summary>
        /// <param name="bytes">The byte array to shift.</param>
        public static void ShiftRight(byte[] bytes)
        {
            int rightEnd = bytes.Length - 1;

            // Iterate through the elements of the array right to left.
            for (int index = rightEnd; index >= 0; index--)
            {
                // If the rightmost bit of the current byte is 1 then we have a carry.
                bool carryFlag = (bytes[index] & 0x01) > 0;

                if (index < rightEnd)
                {
                    if (carryFlag == true)
                    {
                        // Apply the carry to the leftmost bit of the current bytes neighbor to the right.
                        bytes[index + 1] = (byte)(bytes[index + 1] | 0x80);
                    }
                }

                bytes[index] = (byte)(bytes[index] >> 1);
            }
        }

        public static byte[] Trim(byte[] rawBytes, int start, int bitCount)
        {
            int length = (bitCount - 1) / BITS + 1;
            byte[] shiftedBytes = new byte[length];
            int leftShift = start % 8;
            int rightShift = 8 - leftShift;
            int skip = start / 8;

            for (int i = 0; i < length; i++)
            {
                if (i + skip < rawBytes.Length) // Remove
                    shiftedBytes[i] = (byte)(rawBytes[i + skip] << leftShift);

                if (i + skip + 1 < rawBytes.Length)
                    shiftedBytes[i] |= (byte)(rawBytes[i + skip + 1] >> rightShift);
            }

            return shiftedBytes;
        }

        public static byte[] TrimLeft(byte[] rawBytes, int bitCount)
        {
            int length = (bitCount - 1) / BITS + 1;
            byte[] shiftedBytes = new byte[length];
            int leftShift = (8 - bitCount % 8) % 8;
            int rightShift = 8 - leftShift;
            int skip = (rawBytes.Length * BITS - bitCount) / 8;

            for (int i = 0; i < length; i++)
            {
                shiftedBytes[i] = (byte)(rawBytes[i + skip] << leftShift);

                int t1 = shiftedBytes[i];
                int t = shiftedBytes[i];

                if (i < length - 1)
                    shiftedBytes[i] |= (byte)(rawBytes[i + skip + 1] >> rightShift);
            }

            return shiftedBytes;
        }

        public static string GetBits(byte[] data)
        {
            int[] fields = new int[8];
            for (int i = 0; i < 8; i++)
                fields[i] = 1 << (7 - i);

            string str = string.Empty;
            for (int i = 0; i < data.Length; i++)
            {
                for (int j = 0; j < fields.Length; j++)
                    str += (data[i] & fields[j]) > 0 ? 1 : 0;
                if (i < data.Length - 1)
                    str += " ";
            }

            return str;
        }

        public static void PrintBits(byte[] data)
        {
            Console.WriteLine(GetBits(data));
        }

        public static byte[] BitShiftLeft(byte[] rawBytes, int length, int shift)
        {
            byte[] shiftedBytes = new byte[length];
            int shiftRemainder = shift % 8;
            int skip = shift / 8;

            for (int i = 0; i < length; i++)
            {
                shiftedBytes[i] = (byte)(rawBytes[i + skip] << shiftRemainder);

                if (i < length - 1)
                    shiftedBytes[i] |= (byte)(rawBytes[i + skip + 1] >> 8 - shiftRemainder);
            }

            return shiftedBytes;
        }

        public static byte[] BitShiftRight(byte[] rawBytes, int length, int shift)
        {
            byte[] shiftedBytes = new byte[length];
            int shiftRemainder = shift % 8;

            for (int i = 0; i < length; i++)
            {
                shiftedBytes[i] = (byte)(rawBytes[i] >> shiftRemainder);

                if (i > 0)
                    shiftedBytes[i] |= (byte)(rawBytes[i - 1] << 8 - shift);
            }

            return shiftedBytes;
        }

        /// <summary>
        /// Returns whether a sequence is newer than another
        /// </summary>
        /// <param name="seq1">The first sequence</param>
        /// <param name="seq2">The second sequence</param>
        /// <returns>Whether seq1 is newer than seq2</returns>
        public static bool IsGreaterThan(ushort seq1, ushort seq2)
        {
            return ((seq1 > seq2) && (seq1 - seq2 <= 32768)) ||
               ((seq1 < seq2) && (seq2 - seq1 > 32768));
        }

        /// <summary>
        /// Returns whether the given sequence is acknowledged
        /// </summary>
        /// <param name="bits">The bitfield to test within</param>
        /// <param name="lastSequence">The highest sequence of the bitfield</param>
        /// <param name="sequence">The sequence to test</param>
        /// <returns>Whether the given sequence is contained within the bitfield</returns>
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
