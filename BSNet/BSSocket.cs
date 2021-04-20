using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using BSNet.Stream;

#if (ENABLE_MONO || ENABLE_IL2CPP)
using UnityEngine;
#else
using System.Diagnostics;
#endif

namespace BSNet
{
    public abstract class BSSocket : IDisposable
    {
        protected struct ConnectionSequence
        {
            public EndPoint endPoint;
            public ushort sequence;

            public ConnectionSequence(EndPoint endPoint, ushort sequence)
            {
                this.endPoint = endPoint;
                this.sequence = sequence;
            }
        }

        protected struct ReliableMessage
        {
            public byte[] bytes;
            public double timeSent;

            public ReliableMessage(byte[] bytes, double timeSent)
            {
                this.bytes = bytes;
                this.timeSent = timeSent;
            }
        }

        // Constants
        public const int RECEIVE_BUFFER_SIZE = 1024;
        public const int TIMEOUT = 10;

        // Time
#if !(ENABLE_MONO || ENABLE_IL2CPP)
        public virtual double ElapsedTime => stopwatch.Elapsed.TotalSeconds;
        protected Stopwatch stopwatch;
#else
        public virtual double ElapsedTime => time;
        protected double time;
#endif

        // Properties
        public virtual double TickRate { private set; get; }
        public virtual byte[] ProtocolVersion { get; }

        // Socket stuff
        protected double nextMessage;
        protected IPEndPoint localEndPoint;
        protected Socket socket;
        protected const int SIO_UDP_CONNRESET = -1744830452;

        // Network statistics
        protected double nextBip;
        protected int inComingBipS = 0;
        protected int outGoingBipS = 0;

        // Connections & reliable messages
        protected Dictionary<EndPoint, ClientConnection> connections = new Dictionary<EndPoint, ClientConnection>();
        protected Dictionary<ConnectionSequence, ReliableMessage> unsentMessages = new Dictionary<ConnectionSequence, ReliableMessage>();

        public BSSocket(int port, int ticksPerSecond)
        {
            TickRate = 1d / ticksPerSecond;

            // Create the socket and listen for packets
            localEndPoint = new IPEndPoint(IPAddress.Any, port);
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try // This won't work on Linux
            {
                socket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch { }

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(localEndPoint);

#if !(ENABLE_MONO || ENABLE_IL2CPP)
            // Without Unity's Update method
            stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
        }

        public BSSocket(int port) : this(port, 50) { }

        ~BSSocket()
        {
            Dispose();
        }

        public virtual void Dispose()
        {
            socket.Close();
            socket = null;
            GC.SuppressFinalize(this);
        }

        public virtual void Update()
        {
#if (ENABLE_MONO || ENABLE_IL2CPP)
            time += Time.deltaTime;
#endif
            if (ElapsedTime > nextMessage)
            {
                nextMessage += TickRate;
                ReceiveMessages();
            }
        }

        // P2P Protocol:
        // byte - connection type
        // ushort - sequence
        // ushort - an acknowledgement for most recent packet
        // uint - bitfield of acknowledgements before recent
        // ulong - token if logged in, otherwise localToken
        // any other payload...

        /// <summary>
        /// Attempts to establish a connection with the endPoint, if one doesn't already exist
        /// <para>Note: For NAT hole-punching, both ends are required to call this</para>
        /// </summary>
        /// <param name="endPoint">The endPoint to establish a connection with</param>
        public virtual void Connect(EndPoint endPoint)
        {
            if (!connections.TryGetValue(endPoint, out ClientConnection connection))
            {
                ulong localToken = Cryptography.GenerateToken();

                connection = new ClientConnection(ElapsedTime, localToken, 0);
                connections.Add(endPoint, connection);
            }

            // Send a message with the generated token
            byte[] rawBytes = SendRawMessage(endPoint, ConnectionType.CONNECT, connection.LocalToken, writer =>
            {
                // Pad message to 1024 bytes
                writer.PadToEnd();
            });
            AddReliableMessage(endPoint, connection, rawBytes);
        }

        /// <summary>
        /// Sends a packet, wanting to disconnect
        /// </summary>
        /// <param name="endPoint">The endPoint to establish a connection with</param>
        public virtual void Disconnect(EndPoint endPoint)
        {
            if (connections.TryGetValue(endPoint, out ClientConnection connection))
            {
                // Send an unreliable message with the generated token. We shouldn't wait around
                byte[] rawBytes = SendRawMessage(endPoint, ConnectionType.DISCONNECT, connection.Token);

                connections.Remove(endPoint);
            }
        }

        /// <summary>
        /// Sends an unreliable message to an endPoint
        /// </summary>
        /// <param name="endPoint">The endPoint to send it to</param>
        /// <param name="action">The method to fill the buffer with data</param>
        public virtual void SendMessageUnreliable(EndPoint endPoint, Action<IBSStream> action = null)
        {
            // Check if authenticated
            if (connections.TryGetValue(endPoint, out ClientConnection connection) && connection.Authenticated)
            {
                // Increment sequence
                connection.IncrementSequence();
                connection.UpdateLastSent(ElapsedTime);

                SendRawMessage(endPoint, ConnectionType.UNRELIABLE, connection.Token, writer =>
                {
                    action?.Invoke(writer);
                });
            }
        }

        /// <summary>
        /// Sends a reliable message to an endPoint
        /// </summary>
        /// <param name="endPoint">The endPoint to send it to</param>
        /// <param name="action">The method to fill the buffer with data</param>
        public virtual void SendMessageReliable(EndPoint endPoint, Action<IBSStream> action = null)
        {
            // Check if authenticated
            if (connections.TryGetValue(endPoint, out ClientConnection connection) && connection.Authenticated)
            {
                // Increment sequence
                connection.IncrementSequence();
                connection.UpdateLastSent(ElapsedTime);

                byte[] rawBytes = SendRawMessage(endPoint, ConnectionType.RELIABLE, connection.Token, writer =>
                {
                    action?.Invoke(writer);
                });

                // Add message to backlog
                AddReliableMessage(endPoint, connection, rawBytes);
            }
        }

        /// <summary>
        /// Send an acknowledgement back to the given endPoint
        /// </summary>
        /// <param name="endPoint">The endPoint to send it to</param>
        protected virtual void SendHeartbeat(EndPoint endPoint)
        {
            if (connections.TryGetValue(endPoint, out ClientConnection connection) && connection.Authenticated)
            {
                // Increment sequence
                connection.IncrementSequence();
                connection.UpdateLastSent(ElapsedTime);

                SendRawMessage(endPoint, ConnectionType.HEARTBEAT, connection.Token);
            }
        }

        /// <summary>
        /// Sends a raw message to the given endPoint
        /// </summary>
        /// <param name="endPoint">The endPoint to send it to</param>
        /// <param name="type">The type of connection to send</param>
        /// <param name="token">The token to send with it</param>
        /// <param name="action">The method to fill the buffer with data</param>
        /// <returns>The bytes that have been sent to the endPoint</returns>
        protected virtual byte[] SendRawMessage(EndPoint endPoint, byte type, ulong token, Action<IBSStream> action = null)
        {
            BSWriter writer = new BSWriter();

            if (connections.TryGetValue(endPoint, out ClientConnection connection))
            {
                ushort sequence = connection.LocalSequence;
                ushort ack = connection.RemoteSequence;
                uint ackBits = connection.AckBits;
                SerializeHeader(writer,
                    ref type,
                    ref sequence,
                    ref ack,
                    ref ackBits,
                    ref token);
            }
            action?.Invoke(writer);
            writer.SerializeChecksum(ProtocolVersion);

            byte[] rawBytes = writer.ToArray();

            if (rawBytes.Length > 1472)
                throw new OverflowException("Packet size too big");

            try
            {
                socket.SendTo(rawBytes, rawBytes.Length, SocketFlags.None, endPoint);
            }
            catch (SocketException e)
            {
                if (e.ErrorCode != 10054)
                {
                    Log($"Network exception trying to receive data from: {endPoint}");
                    Log(e.ToString());
                }
            }

            outGoingBipS += rawBytes.Length * 8;

            return rawBytes;
        }

        /// <summary>
        /// Serializes a header from an IBSStream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="type"></param>
        /// <param name="sequence"></param>
        /// <param name="ack"></param>
        /// <param name="ackBits"></param>
        /// <param name="token"></param>
        protected virtual void SerializeHeader(IBSStream stream, ref byte type, ref ushort sequence, ref ushort ack, ref uint ackBits, ref ulong token)
        {
            type = stream.SerializeByte(type, 3);
            sequence = stream.SerializeUShort(sequence);
            ack = stream.SerializeUShort(ack);
            ackBits = stream.SerializeUInt(ackBits);
            token = stream.SerializeULong(token);
        }

        /// <summary>
        /// Add a message to the reliable list, awaiting acknowledgement
        /// </summary>
        /// <param name="endPoint">The endPoint to send it to</param>
        /// <param name="connection">The connection of this endPoint</param>
        /// <param name="bytes">The payload of the packet</param>
        protected virtual void AddReliableMessage(EndPoint endPoint, ClientConnection connection, byte[] bytes)
        {
            ReliableMessage msg = new ReliableMessage(bytes, ElapsedTime);

            ConnectionSequence connSeq = new ConnectionSequence(endPoint, connection.LocalSequence);
            if (!unsentMessages.ContainsKey(connSeq))
                unsentMessages.Add(connSeq, msg);

            connection.AddRTT(connection.LocalSequence, ElapsedTime);
        }

        //System.Random random = new System.Random();

        /// <summary>
        /// Called, preferably in another thread, to receive packets from other endPoints
        /// </summary>
        protected virtual void ReceiveMessages()
        {
            while (socket.Available > 0)
            {
                EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

                // Get packets from other endpoints
                byte[] rawBytes = new byte[RECEIVE_BUFFER_SIZE];
                int length = socket.ReceiveFrom(rawBytes, ref endPoint);
                //if (random.Next(100) < 25) return; // DEBUG

                // The length is less than the header, certainly malicious
                if (length < sizeof(byte) + 2 * sizeof(ushort) + sizeof(uint) + sizeof(ulong))
                    continue;

                inComingBipS += length * 8;

                // Read the buffer and extract header
                BSReader reader = new BSReader(rawBytes, length);
                if (!reader.SerializeChecksum(ProtocolVersion))
                    continue;

                byte type = 0;
                ushort sequence = 0;
                ushort ack = 0;
                uint ackBits = 0;
                ulong token = 0;
                SerializeHeader(reader,
                    ref type,
                    ref sequence,
                    ref ack,
                    ref ackBits,
                    ref token);

                // Check if a connection already exists
                if (type == ConnectionType.CONNECT) // If this endPoint wants to establish connection
                {
                    // Make sure this message has been padded
                    if (length >= RECEIVE_BUFFER_SIZE)
                    {
                        if (connections.TryGetValue(endPoint, out ClientConnection connection))
                        {
                            // Acknowledge this packet
                            connection.Acknowledge(sequence);
                        }
                        else
                        {
                            // Add this connection to the list
                            ulong localToken = Cryptography.GenerateToken();
                            connection = new ClientConnection(ElapsedTime, localToken, token);
                            connections.Add(endPoint, connection);

                            // Acknowledge this packet
                            connection.Acknowledge(sequence);

                            // Send a connection message to the sender
                            byte[] bytes = SendRawMessage(endPoint, ConnectionType.CONNECT, connection.LocalToken, writer =>
                            {
                                // Pad message to 1024 bytes
                                writer.PadToEnd();
                            });
                            AddReliableMessage(endPoint, connection, bytes);
                        }

                        // If this connection is not authenticated
                        if (!connection.Authenticated)
                        {
                            connection.Authenticate(token, ElapsedTime);
                            OnConnect((IPEndPoint)endPoint);
                        }
                    }
                }
                else if (connections.TryGetValue(endPoint, out ClientConnection connection))
                {
                    if (type == ConnectionType.DISCONNECT) // If this endPoint wants to disconnect
                    {
                        // If this connection is not authenticated
                        if (connection.Authenticate(token, ElapsedTime))
                        {
                            connections.Remove(endPoint);
                            OnDisconnect((IPEndPoint)endPoint);
                        }
                    }
                    else if (connection.Authenticated) // If this endPoint has been authenticated
                    {
                        // Compare the tokens
                        if (connection.Authenticate(token, ElapsedTime))
                        {
                            // Remove acknowledged messages
                            for (int i = 0; i < 33; i++)
                            {
                                ushort seq = (ushort)(ack - i);
                                if (BSUtility.IsAcknowledged(ackBits, ack, seq))
                                {
                                    connection.UpdateRTT(seq, ElapsedTime);
                                    unsentMessages.Remove(new ConnectionSequence(endPoint, seq));
                                    // OnMessageAcknowledged(seq);
                                }
                            }

                            // Validate packet and return payload to application
                            if (!connection.IsAcknowledged(sequence) && (type == ConnectionType.RELIABLE || type == ConnectionType.UNRELIABLE))
                                OnReceiveMessage((IPEndPoint)endPoint, reader);

                            // Acknowledge this packet
                            connection.Acknowledge(sequence);
                        }
                        else
                        {
                            Log("Tokens differ.... FUCK");
                        }
                    }
                }
            }

            double timeout = ElapsedTime - TIMEOUT;
            double resendTime = ElapsedTime - TickRate * 33d;
            double beatTime = ElapsedTime - TickRate;

            // Clean up old endPoints
            var timedOut = connections.Where(i => i.Value.LastReceived < timeout).ToArray();
            foreach (var data in timedOut)
            {
                connections.Remove(data.Key);
                OnDisconnect((IPEndPoint)data.Key);
            }


            // Resend lost reliable packets
            var resendMessages = unsentMessages.Where(i => i.Value.timeSent < resendTime).ToArray();
            foreach (var data in resendMessages)
            {
                EndPoint ep = data.Key.endPoint;
                if (connections.TryGetValue(ep, out ClientConnection connection))
                {
                    // Get header
                    byte[] rawBytes = unsentMessages[data.Key].bytes;
                    BSReader reader = new BSReader(rawBytes);
                    reader.SerializeChecksum(ProtocolVersion);

                    byte type = 0;
                    ushort sequence = 0;
                    ushort ack = 0;
                    uint ackBits = 0;
                    ulong token = 0;
                    SerializeHeader(reader,
                        ref type,
                        ref sequence,
                        ref ack,
                        ref ackBits,
                        ref token);

                    // Get payload
                    int bits = reader.TotalBits;
                    byte[] payload = reader.SerializeBytes(bits);

                    // Remove message and clear RTT
                    connection.ClearRTT(data.Key.sequence);
                    unsentMessages.Remove(data.Key);

                    // Increment sequence
                    connection.IncrementSequence();
                    connection.UpdateLastSent(ElapsedTime);

                    byte[] newBytes = SendRawMessage(data.Key.endPoint, type, token, writer =>
                    {
                        writer.SerializeBytes(bits, payload);
                    });

                    // Add message to backlog
                    AddReliableMessage(data.Key.endPoint, connection, newBytes);

                    Log($"Packet {data.Key.sequence} has been resent as {connection.LocalSequence}");
                }
                else
                {
                    // Client has disconnected
                    Log($"Packet {data.Key.sequence} dropped due to client disconnect");
                    unsentMessages.Remove(data.Key);
                }
            }


            // Send a heartbeat if nothing has been sent this tick
            var endPoints = connections.Where(i => i.Value.LastSent < beatTime).ToArray();
            foreach (var data in endPoints)
                SendHeartbeat(data.Key);


            // Calculate network statistics
            if (nextBip < ElapsedTime)
            {
                nextBip = ElapsedTime + 1d;

                OnNetworkStatistics(outGoingBipS, inComingBipS);

                outGoingBipS = 0;
                inComingBipS = 0;
            }
        }


        protected abstract void Log(object obj);

        protected virtual void OnNetworkStatistics(int outGoingBipS, int inComingBipS)
        {
            //Log($"Outgoing bits in the last second: {outGoingBipS / 1000f} Kbits/S");
            //Log($"Incoming bits in the last second: {inComingBipS / 1000f} Kbits/S");
        }

        protected abstract void OnConnect(IPEndPoint endPoint);

        protected abstract void OnDisconnect(IPEndPoint endPoint);

        protected abstract void OnReceiveMessage(IPEndPoint endPoint, IBSStream reader);
    }
}