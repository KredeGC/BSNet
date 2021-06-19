using System.Net;

namespace BSNet.Datagram
{
    public struct ConnectionSequence
    {
        public EndPoint EndPoint { private set; get; }
        public ushort Sequence { private set; get; }

        public ConnectionSequence(EndPoint endPoint, ushort sequence)
        {
            EndPoint = endPoint;
            Sequence = sequence;
        }
    }
}
