using System.Net;

namespace BSNet.Datagram
{
    public struct ConnectionSequence
    {
        public IPEndPoint EndPoint { private set; get; }
        public ushort Sequence { private set; get; }

        public ConnectionSequence(IPEndPoint endPoint, ushort sequence)
        {
            EndPoint = endPoint;
            Sequence = sequence;
        }
    }
}
