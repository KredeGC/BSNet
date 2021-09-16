using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BSNetTest
{
    [TestClass]
    public class SocketTest
    {
        [TestMethod]
        public void ConnectGoodNetworkTest()
        {
            // Testing a P2P implementation
            TestPeer peer1 = new TestPeer(0, 10d, 0.05d, 0d);
            TestPeer peer2 = new TestPeer(0, 10d, 0.05d, 0d);

            // Construct peer endpoint
            IPAddress peer2Address = IPAddress.Parse("127.0.0.1");
            IPEndPoint peer2EndPoint = new IPEndPoint(peer2Address, peer2.Port);

            // Send a request to connect
            peer1.Connect(peer2EndPoint);

            // Check for connection message
            while (peer1.alive && peer2.alive)
            {
                peer1.Update();
                peer2.Update();
            }
            
            // Discard instances
            peer1.Dispose();
            peer2.Dispose();
        }
        
        [TestMethod]
        public void ConnectBadNetworkTest()
        {
            // Testing a P2P implementation
            TestPeer peer1 = new TestPeer(0, 350d, 0.5d, 0.01d);
            TestPeer peer2 = new TestPeer(0, 350d, 0.5d, 0.01d);

            // Construct peer endpoint
            IPAddress peer2Address = IPAddress.Parse("127.0.0.1");
            IPEndPoint peer2EndPoint = new IPEndPoint(peer2Address, peer2.Port);

            // Send a request to connect
            peer1.Connect(peer2EndPoint);

            // Check for connection message
            while (peer1.alive && peer2.alive)
            {
                peer1.Update();
                peer2.Update();
            }
            
            // Discard instances
            peer1.Dispose();
            peer2.Dispose();
        }
    }
}