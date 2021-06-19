using System;
using System.Collections.Generic;
using System.Net;

namespace BSNet.Datagram
{
    public class Packet
    {
        private static Queue<Packet> packetPool = new Queue<Packet>();

        public EndPoint AddressPoint { private set; get; }
        public byte[] Bytes { private set; get; }
        public double Time { private set; get; }

        private Packet() { }

        public static Packet GetPacket(EndPoint endPoint, byte[] bytes, int length, double time)
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

        public static Packet GetPacket(EndPoint endPoint, byte[] bytes, double time)
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
                packetPool.Enqueue(packet);
            }
        }
    }
}
