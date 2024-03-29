﻿using BSNet.Datagram;
using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace BSNet
{
    public static class BSUtility
    {
        public const int TIMEOUT = 10; // Timeout for connections
        public const int BITS = 8; // Bits in a byte
        public const int RTT_BUFFER_SIZE = 128; // Needs to be larger than AckBits
        public const int MAX_POOLING = 4; // Max amount of objects to pool

        public const int PACKET_MIN_SIZE =
            sizeof(uint) + // CRC32 of version + packet (4 bytes)
            Header.HEADER_SIZE; // Header
        public const int PACKET_MAX_SIZE = 1024; // 1472

        /// <summary>
        /// Compares 2 byte arrays
        /// </summary>
        /// <param name="baseBytes">The first byte array to compare</param>
        /// <param name="compareBytes">The byte array to compare against</param>
        /// <returns>Whether the byte arrays are equal</returns>
        public static bool CompareBytes(byte[] baseBytes, byte[] compareBytes)
        {
            if (baseBytes.Length != compareBytes.Length) return false;

            for (int i = 0; i < baseBytes.Length; i++)
            {
                if (!baseBytes[i].Equals(compareBytes[i]))
                    return false;
            }

            return true;
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
            
            // return (from networkInterface in NetworkInterface.GetAllNetworkInterfaces()
            //     where networkInterface.OperationalStatus == OperationalStatus.Up
            //     select networkInterface.GetIPProperties()
            //     into adapterProperties
            //     where adapterProperties.GatewayAddresses.FirstOrDefault() != null
            //     from ip in adapterProperties.UnicastAddresses
            //     where ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            //     select ip.Address).FirstOrDefault();
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

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                for (int j = 0; j < fields.Length; j++)
                    builder.Append((data[i] & fields[j]) > 0 ? 1 : 0);
                if (i < data.Length - 1)
                    builder.Append(" ");
            }

            return builder.ToString();
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
