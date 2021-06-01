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
        }

        // For error logging
        protected override void Log(object obj)
        {
            Console.WriteLine(obj);
        }

        // Called when a connection has been established with this IPEndPoint
        protected override void OnConnect(IPEndPoint endPoint)
        {
            Log($"{endPoint.ToString()} connected");

            // Send a message to the connected IPEndPoint
            SendMessageReliable(endPoint, writer =>
            {
                writer.SerializeString(encoding, "Hello network!");
            });
        }

        // Called when a connection has been lost with this IPEndPoint
        protected override void OnDisconnect(IPEndPoint endPoint)
        {
            Log($"{endPoint.ToString()} disconnected");
        }

        // Called when we receive a message from this IPEndPoint
        protected override void OnReceiveMessage(IPEndPoint endPoint, IBSStream reader)
        {
            // Receive the message, "Hello network!", from the other end
            string message = reader.SerializeString(encoding);
            Log(message);
        }

        /*protected override void OnNetworkStatistics(int outGoingBipS, int inComingBipS)
        {
            Log($"Outgoing bits in the last second: {outGoingBipS / 1000f} Kbits/S");
            Log($"Incoming bits in the last second: {inComingBipS / 1000f} Kbits/S");
        }*/
    }
}