using BSNet.Datagram;
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
        public const int TIMEOUT = 10; // Timeout for connections
        public const int BITS = 8; // Bits in a byte
        public const int RTT_BUFFER_SIZE = 64; // Double the size of AckBits
        
        public const int PACKET_MIN_SIZE =
            sizeof(uint) + // CRC32 of version + packet (4 bytes)
            Header.HEADER_SIZE; // Header
        public const int PACKET_MAX_SIZE = 1024;

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
        /// Visualize the bits in the given byte-array
        /// </summary>
        /// <param name="data">The byte-array to show the bits of</param>
        /// <returns>A string visualizing the bits in the byte-array</returns>
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

        /// <summary>
        /// Print the bits in the given byte-array to the console output
        /// </summary>
        /// <param name="data">The byte-array to show the bits of</param>
        public static void PrintBits(byte[] data)
        {
            Console.WriteLine(GetBits(data));
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
