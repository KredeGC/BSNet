using BSNet.Stream;
using System;
using System.Net;
using System.Text;

namespace BSNet.Example
{
    public class ExamplePeer : BSSocket<ClientConnection>
    {
        public override byte[] ProtocolVersion => new byte[] { 0xC0, 0xDE, 0xDE, 0xAD };

        public ExamplePeer(int localPort, double latency, double loss, double corruption) : base(localPort)
        {
#if NETWORK_DEBUG
            // Simulate bad network conditions
            SimulatedPacketLatency = latency; // 250ms latency
            SimulatedPacketLoss = loss; // 25% packet loss
            SimulatedPacketCorruption = corruption; // 0.1% packet corruption
#endif
        }

        // For error logging
        protected override void Log(object obj, LogLevel level)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"[{DateTime.Now.TimeOfDay}]");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"[{Port}] ");
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

        // Called when an endPoint wishes to connect, or we wish to connect to them
        protected override void OnRequestConnect(IPEndPoint endPoint, IBSStream reader, IBSStream writer)
        {
            writer.SerializeString(Encoding.ASCII, $"Hello from {Port}!");
        }

        // Called when a connection has been established with this IPEndPoint
        protected override void OnConnect(IPEndPoint endPoint, IBSStream reader)
        {
            Log($"{endPoint} connected", LogLevel.Info);

            Log($"Received initial message: \"{reader.SerializeString(Encoding.ASCII)}\"", LogLevel.Info);

            //// Send corrupt packet
            //SendMessageReliable(endPoint, writer =>
            //{
            //    byte[] rawBytes = new byte[20];
            //    Random rand = new Random();
            //    rand.NextBytes(rawBytes);
            //    writer.SerializeBytes(rawBytes);
            //});

            //// Create a packet
            //ExamplePacket serializable = new ExamplePacket($"Hello network from {socket.LocalEndPoint} to {endPoint}!", 3.1415f);

            //// Serialize the message and send it to the connected IPEndPoint
            //SendMessageReliable(endPoint, serializable);
        }

        // Called when a connection has been lost with this IPEndPoint
        protected override void OnDisconnect(IPEndPoint endPoint, IBSStream reader)
        {
            Log($"{endPoint.ToString()} disconnected", LogLevel.Info);

            // Attempt to reconnect
            Connect(endPoint);
        }

        // Called when we receive a message from this IPEndPoint
        protected override void OnReceiveMessage(IPEndPoint endPoint, ushort sequence, IBSStream reader)
        {
            // Create an empty message
            ExamplePacket emptySerializable = new ExamplePacket();

            // Deserialize the message
            emptySerializable.Serialize(reader);

            Log($"Received message: \"{emptySerializable.TestString}\"", LogLevel.Info);
        }

        // Called when we receive an acknowledgement for a packet from this IPEndPoint
        protected override void OnReceiveAcknowledgement(IPEndPoint endPoint, ushort sequence) { }
    }
}
