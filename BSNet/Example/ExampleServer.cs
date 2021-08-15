using BSNet.Stream;
using System;
using System.Net;
using System.Text;

namespace BSNet.Example
{
    public class ExampleServer : BSSocket
    {
        public override byte[] ProtocolVersion => new byte[] { 0xBE, 0xEB, 0xB0, 0x0B };

        public int OutgoingBipS { get; private set; }
        public int IncomingBipS { get; private set; }

        protected Encoding encoding = new ASCIIEncoding();

        public ExampleServer(int localPort) : base(localPort)
        {
            Log("Server starting", LogLevel.Info);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                Log("Server stopping", LogLevel.Info);
        }

        // Print outgoing + incoming bits per second
        public void PrintNetworkStats()
        {
            Log($"Outgoing bits in the last second: {OutgoingBipS / 1000d} Kbits", LogLevel.Info);
            Log($"Incoming bits in the last second: {IncomingBipS / 1000d} Kbits", LogLevel.Info);
        }

        // Print current connections and their stats
        public void PrintConnections(int amount)
        {
            int counter = 0;
            foreach (ClientConnection connection in connections.Values)
            {
                if (connection.Authenticated)
                {
                    Log($"{connection.AddressPoint} - {Math.Round(connection.RTT * 1000)}ms latency - {Math.Round(connection.PacketLoss * 100)}% packet loss", LogLevel.Info);
                    counter++;
                    if (counter >= amount)
                        break;
                }
            }
            Log($"Showing {counter} out of {connections.Count} connected players", LogLevel.Info);
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

            // Log the result
            //Log(emptySerializable.TestString, LogLevel.Info);
        }

        protected override void OnReceiveAcknowledgement(ushort sequence)
        {
            //Log($"Packet {sequence} acknowledged", LogLevel.Info);
        }

        protected override void OnNetworkStatistics(int outGoingBipS, int inComingBipS)
        {
            OutgoingBipS = outGoingBipS;
            IncomingBipS = inComingBipS;
        }
    }
}
