using System;
using System.Net;

namespace BSNet.Datagram
{
    public struct Packet
    {
        public EndPoint AddressPoint { private set; get; }
        public byte[] Bytes { private set; get; }
        public double Time { private set; get; }

        public Packet(EndPoint endPoint, byte[] bytes, int length, double time)
        {
            AddressPoint = endPoint;
            Bytes = new byte[length];
            Time = time;

            Buffer.BlockCopy(bytes, 0, Bytes, 0, length);
        }

        public Packet(EndPoint endPoint, byte[] bytes, double time)
        {
            AddressPoint = endPoint;
            Bytes = new byte[bytes.Length];
            Time = time;

            Buffer.BlockCopy(bytes, 0, Bytes, 0, bytes.Length);
        }
    }
}
