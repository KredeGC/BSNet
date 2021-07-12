﻿using BSNet.Stream;
using System;
using System.Net;
using System.Text;

namespace BSNet.Example
{
    public class ExampleClient : BSSocket
    {
        protected Encoding encoding = new ASCIIEncoding();

        public override byte[] ProtocolVersion => new byte[] { 0x00, 0x00, 0x00, 0x01 };

        public ExampleClient(int localPort, string peerIP, int peerPort) : base(localPort)
        {
            // Construct peer endpoint
            IPAddress peerAddress = IPAddress.Parse(peerIP);
            IPEndPoint peerEndPoint = new IPEndPoint(peerAddress, peerPort);

            // Send a request to connect
            Connect(peerEndPoint);

#if NETWORK_DEBUG
            SimulatedPacketLatency = 250;
            SimulatedPacketLoss = 250;
#endif
        }

        // For error logging
        protected override void Log(object obj, LogLevel level)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"[{DateTime.Now.TimeOfDay}] ");
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

            // Create a packet
            ExamplePacket serializable = new ExamplePacket($"Hello network to {endPoint}!");

            // Serialize the message and send it to the connected IPEndPoint
            SendMessageReliable(endPoint, serializable);
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

        /*protected override void OnNetworkStatistics(int outGoingBipS, int inComingBipS)
        {
            Log($"Outgoing bits in the last second: {outGoingBipS / 1000f} Kbits/S");
            Log($"Incoming bits in the last second: {inComingBipS / 1000f} Kbits/S");
        }*/
    }
}