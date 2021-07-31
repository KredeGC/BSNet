using System;
using System.Collections.Generic;
using System.Net;

namespace BSNet.Datagram
{
    public class Packet
    {
        private static Queue<Packet> packetPool = new Queue<Packet>();

        public IPEndPoint AddressPoint { private set; get; }
        public byte[] Bytes { private set; get; }
        public double Time { private set; get; }

        private Packet() { }

        /// <summary>
        /// Retrieves a packet from the pool, or creates a new if none exist
        /// </summary>
        /// <param name="endPoint">The endPoint that sent this packet</param>
        /// <param name="bytes">The data in the packet</param>
        /// <param name="length">The size of the packet</param>
        /// <param name="time">The time this packet was received/sent</param>
        /// <returns>A new packet</returns>
        public static Packet GetPacket(IPEndPoint endPoint, byte[] bytes, int length, double time)
        {
            lock (packetPool)
            {
                Packet packet;
                if (packetPool.Count > 0)
                    packet = packetPool.Dequeue();
                else
                    packet = new Packet();
                
                packet.AddressPoint = endPoint;
                packet.Bytes = new byte[length];
                packet.Time = time;
                Buffer.BlockCopy(bytes, 0, packet.Bytes, 0, length);

                return packet;
            }
        }

        /// <summary>
        /// Retrieves a packet from the pool, or creates a new if none exist
        /// </summary>
        /// <param name="endPoint">The endPoint that sent this packet</param>
        /// <param name="bytes">The data in the packet</param>
        /// <param name="time">The time this packet was received/sent</param>
        /// <returns>A new packet</returns>
        public static Packet GetPacket(IPEndPoint endPoint, byte[] bytes, double time)
        {
            lock (packetPool)
            {
                Packet packet;
                if (packetPool.Count > 0)
                    packet = packetPool.Dequeue();
                else
                    packet = new Packet();

                packet.AddressPoint = endPoint;
                packet.Bytes = new byte[bytes.Length];
                packet.Time = time;
                Buffer.BlockCopy(bytes, 0, packet.Bytes, 0, bytes.Length);

                return packet;
            }
        }

        /// <summary>
        /// Returns the given packet into the pool for later use
        /// </summary>
        /// <param name="packet">The packet to return</param>
        public static void ReturnPacket(Packet packet)
        {
            lock (packetPool)
            {
                if (packetPool.Count < BSUtility.MAX_POOLING)
                    packetPool.Enqueue(packet);
            }
        }
    }
}
