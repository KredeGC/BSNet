using System.Net;

namespace BSNet.Datagram
{
    public struct ConnectionSequence
    {
        public IPEndPoint EndPoint { get; }
        public ushort Sequence { get; }

        public ConnectionSequence(IPEndPoint endPoint, ushort sequence)
        {
            EndPoint = endPoint;
            Sequence = sequence;
        }
    }
}
