using BSNet.Stream;
using System;
using System.Net;
using System.Text;

namespace BSNet.Example
{
    public class ExampleClient : BSSocket
    {
        protected Encoding encoding = new ASCIIEncoding();

        protected bool verbose = false;

        public override byte[] ProtocolVersion => new byte[] { 0x00, 0x00, 0x00, 0x01 };

        public ExampleClient(int localPort, string peerIP, int peerPort, bool verbose = false) : base(localPort)
        {
            this.verbose = verbose;

            // Construct peer endpoint
            IPAddress peerAddress = IPAddress.Parse(peerIP);
            IPEndPoint peerEndPoint = new IPEndPoint(peerAddress, peerPort);

            // Send a request to connect
            Connect(peerEndPoint);

#if NETWORK_DEBUG
            SimulatedPacketLatency = 250; // 250ms
            SimulatedPacketLoss = 250; // 25%
            SimulatedPacketCorruption = 1; // 0.1%
#endif
        }

        // For error logging
        protected override void Log(object obj, LogLevel level)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"[{DateTime.Now.TimeOfDay}]");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"[Bot] ");
            switch (level)
            {
                case LogLevel.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogLevel.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogLevel.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
            }
            Console.WriteLine(obj);
        }

        // Called when a connection has been established with this IPEndPoint
        protected override void OnConnect(IPEndPoint endPoint)
        {
            if (verbose)
                Log($"{endPoint.ToString()} connected", LogLevel.Info);

            //// Send corrupt packet
            //SendMessageReliable(endPoint, writer =>
            //{
            //    byte[] rawBytes = new byte[20];
            //    Cryptography.GetBytes(rawBytes);
            //    writer.SerializeBytes(rawBytes);
            //});

            // Create a packet
            ExamplePacket serializable = new ExamplePacket($"Hello network to {endPoint}!", 3.1415f);

            // Serialize the message and send it to the connected IPEndPoint
            SendMessageReliable(endPoint, serializable);
        }

        // Called when a connection has been lost with this IPEndPoint
        protected override void OnDisconnect(IPEndPoint endPoint)
        {
            if (verbose)
                Log($"{endPoint.ToString()} disconnected", LogLevel.Info);
        }

        // Called when we receive a message from this IPEndPoint
        protected override void OnReceiveMessage(IPEndPoint endPoint, ushort sequence, IBSStream reader)
        {
            // Create an empty message
            ExamplePacket emptySerializable = new ExamplePacket();

            // Deserialize the message
            emptySerializable.Serialize(reader);

            if (verbose)
                Log(emptySerializable.TestString, LogLevel.Info);
        }

        protected override void OnReceiveAcknowledgement(ushort sequence)
        {
            //if (verbose)
            //    Log($"Packet {sequence} acknowledged", LogLevel.Info);
        }

        protected override void OnNetworkStatistics(int outGoingBipS, int inComingBipS)
        {
            if (verbose)
            {
                foreach (ClientConnection connection in connections.Values)
                    Log($"{connection.AddressPoint} - {Math.Round(connection.RTT * 1000)}ms latency - {Math.Round(connection.PacketLoss * 100)}% packet loss", LogLevel.Info);

                Log($"Outgoing bits in the last second: {outGoingBipS / 1000f} Kbits/S", LogLevel.Info);
                Log($"Incoming bits in the last second: {inComingBipS / 1000f} Kbits/S", LogLevel.Info);
            }
        }
    }
}
