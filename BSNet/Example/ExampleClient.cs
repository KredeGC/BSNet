using BSNet.Stream;
using System;
using System.Net;
using System.Text;

namespace BSNet.Example
{
    public class ExampleClient : BSSocket<ClientConnection>
    {
        protected Encoding encoding = new ASCIIEncoding();

        public override byte[] ProtocolVersion => new byte[] { 0xBE, 0xEB, 0xB0, 0x0B };

        public ExampleClient(int localPort, string peerIP, int peerPort) : base(localPort)
        {
            // Construct peer endpoint
            IPAddress serverAddress = IPAddress.Parse(peerIP);
            IPEndPoint serverEndPoint = new IPEndPoint(serverAddress, peerPort);

            // Send a request to connect
            Connect(serverEndPoint);
        }

        // For error logging
        protected override void Log(object obj, LogLevel level)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"[{DateTime.Now.TimeOfDay}]");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"[Bot {((IPEndPoint)socket.LocalEndPoint).Port}] ");
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
        protected override void OnConnect(IPEndPoint endPoint, IBSStream reader)
        {
            //// Send corrupt packet
            //SendMessageReliable(endPoint, writer =>
            //{
            //    byte[] rawBytes = new byte[20];
            //    Random rand = new Random();
            //    rand.NextBytes(rawBytes);
            //    writer.SerializeBytes(rawBytes);
            //});

            // Create a packet
            ExamplePacket serializable = new ExamplePacket($"Hello network to {endPoint}!", 3.1415f);

            // Serialize the message and send it to the connected IPEndPoint
            SendMessageReliable(endPoint, serializable);
        }

        // Called when a connection has been lost with this IPEndPoint
        protected override void OnDisconnect(IPEndPoint endPoint, IBSStream reader) { }

        // Called when we receive a message from this IPEndPoint
        protected override void OnReceiveMessage(IPEndPoint endPoint, ushort sequence, IBSStream reader)
        {
            // Create an empty message
            ExamplePacket emptySerializable = new ExamplePacket();

            // Deserialize the message
            emptySerializable.Serialize(reader);
        }

        // Called when we receive an acknowledgement for a packet from this IPEndPoint
        protected override void OnReceiveAcknowledgement(IPEndPoint endPoint, ushort sequence) { }
    }
}
