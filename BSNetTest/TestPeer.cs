using System.Net;
using System.Text;
using BSNet;
using BSNet.Stream;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BSNetTest
{
    public class TestPeer : BSSocket<ClientConnection>
    {
        public override byte[] ProtocolVersion => new byte[] {0xC0, 0xDE, 0xDE, 0xAD};

        public bool alive = true;

        public TestPeer(int localPort, double latency, double loss, double corruption) : base(localPort)
        {
#if NETWORK_DEBUG
                    // Simulate bad network conditions
                    SimulatedPacketLatency = latency;
                    SimulatedPacketLoss = loss;
                    SimulatedPacketCorruption = corruption;
#endif
        }

        // Called when an endPoint wishes to connect, or we wish to connect to them
        protected override void OnRequestConnect(IPEndPoint endPoint, IBSStream reader, IBSStream writer)
        {
            writer.SerializeUInt(21U, 7);
            writer.SerializeString(Encoding.ASCII, "Hello world!");
        }

        // Called when a connection has been established with this IPEndPoint
        protected override void OnConnect(IPEndPoint endPoint, IBSStream reader)
        {
            Assert.IsNotNull(reader);

            Assert.AreEqual(21U, reader.SerializeUInt(0, 7));
            Assert.AreEqual("Hello world!", reader.SerializeString(Encoding.ASCII));

            alive = false;
        }

        // Called when a connection has been lost with this IPEndPoint
        protected override void OnDisconnect(IPEndPoint endPoint, IBSStream reader)
        {
            Assert.IsNotNull(reader); // Assert no timeout occurs
        }

        // Unused
        protected override void Log(object obj, LogLevel level)
        {
        }

        // Unused
        protected override void OnReceiveMessage(IPEndPoint endPoint, ushort sequence, IBSStream reader)
        {
            Assert.Fail();
        }
    }
}