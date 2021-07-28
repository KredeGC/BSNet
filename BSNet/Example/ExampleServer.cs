using BSNet.Stream;
using System;
using System.Net;
using System.Text;

namespace BSNet.Example
{
    public class ExampleServer : BSSocket
    {
        protected Encoding encoding = new ASCIIEncoding();

        public override byte[] ProtocolVersion => new byte[] { 0x00, 0x00, 0x00, 0x01 };

        public ExampleServer(int localPort) : base(localPort)
        {
            Log("Server starting", LogLevel.Info);

#if NETWORK_DEBUG
            SimulatedPacketLatency = 250; // 250ms
            SimulatedPacketLoss = 250; // 25%
            SimulatedPacketCorruption = 1; // 0.1%
#endif
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                Log("Server stopping", LogLevel.Info);
        }

        // Print current connections and their stats
        public void PrintConnections(int amount)
        {
            Log($"{connections.Count} players connected", LogLevel.Info);
            int counter = 0;
            foreach (ClientConnection connection in connections.Values)
            {
                Log($"{connection.AddressPoint} - {Math.Round(connection.RTT * 1000)}ms latency - {Math.Round(connection.PacketLoss * 100)}% packet loss", LogLevel.Info);
                counter++;
                if (counter > amount)
                    break;
            }
        }

        // For error logging
        protected override void Log(object obj, LogLevel level)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"[{DateTime.Now.TimeOfDay}]");
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write($"[Server] ");
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
            Log($"{endPoint.ToString()} connected", LogLevel.Info);
        }

        // Called when a connection has been lost with this IPEndPoint
        protected override void OnDisconnect(IPEndPoint endPoint)
        {
            Log($"{endPoint.ToString()} disconnected", LogLevel.Info);
        }

        // Called when we receive a message from this IPEndPoint
        protected override void OnReceiveMessage(IPEndPoint endPoint, ushort sequence, IBSStream reader)
        {
            // Create an empty message
            ExamplePacket emptySerializable = new ExamplePacket();

            // Deserialize the message
            emptySerializable.Serialize(reader);

            Log(emptySerializable.TestString, LogLevel.Info);
        }

        protected override void OnMessageAcknowledged(ushort sequence)
        {
            //if (verbose)
            //    Log($"Packet {sequence} acknowledged", LogLevel.Info);
        }

        protected override void OnNetworkStatistics(int outGoingBipS, int inComingBipS)
        {
            if (false)
            {
                foreach (ClientConnection connection in connections.Values)
                    Log($"{connection.AddressPoint} - {Math.Round(connection.RTT * 1000)}ms latency - {Math.Round(connection.PacketLoss * 100)}% packet loss", LogLevel.Info);

                Log($"Outgoing bits in the last second: {outGoingBipS / 1000f} Kbits/S", LogLevel.Info);
                Log($"Incoming bits in the last second: {inComingBipS / 1000f} Kbits/S", LogLevel.Info);
            }
        }
    }
}
