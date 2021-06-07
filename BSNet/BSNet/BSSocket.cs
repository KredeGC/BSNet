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
        public virtual double TickRate { protected set; get; }
        public abstract byte[] ProtocolVersion { get; }

        // Socket stuff
        protected double nextMessage;
        protected IPEndPoint localEndPoint;
        protected Socket socket;
        protected const int SIO_UDP_CONNRESET = -1744830452;
        protected bool _disposing;

        // P2P Protocol
        protected const int headerSize =
            sizeof(uint) + // CRC32 of version + packet (4 bytes)
            sizeof(byte) + // ConnectionType (2 bits)
            sizeof(ushort) + // Sequence of this packet (2 bytes)
            sizeof(ushort) + // Acknowledgement for most recent received packet (2 bytes)
            sizeof(uint) + // Bitfield of acknowledgements before most recent (4 bytes)
            sizeof(ulong); // Token or LocalToken if not authenticated (8 bytes)

        // Network statistics
        protected double nextBip;
        protected int inComingBipS = 0;
        protected int outGoingBipS = 0;

        // Connections & reliable messages
        protected Dictionary<EndPoint, ClientConnection> connections = new Dictionary<EndPoint, ClientConnection>();
        protected Dictionary<ConnectionSequence, ReliableMessage> unsentMessages = new Dictionary<ConnectionSequence, ReliableMessage>();
        protected List<EndPoint> lastTimedOut = new List<EndPoint>();
        protected List<EndPoint> lastHeartBeats = new List<EndPoint>();

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
            Dispose(false);
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposing)
            {
                _disposing = true;

                if (disposing)
                {
                    foreach (EndPoint endPoint in connections.Keys)
                        Disconnect(endPoint);
                }

                socket.Close();
                socket = null;
            }
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

                connection = new ClientConnection(endPoint, ElapsedTime, localToken, 0);
                connections.Add(endPoint, connection);
            }

            // Send a message with the generated token
            byte[] rawBytes = SendRawMessage(connection, ConnectionType.CONNECT, connection.LocalToken, writer =>
            {
                // Pad message to 1024 bytes
                writer.PadToEnd();
            });
            AddReliableMessage(connection, rawBytes);
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
                SendRawMessage(connection, ConnectionType.DISCONNECT, connection.Token);

                if (!_disposing)
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
                SendRawMessage(connection, ConnectionType.MESSAGE, connection.Token, writer =>
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
                byte[] rawBytes = SendRawMessage(connection, ConnectionType.MESSAGE, connection.Token, writer =>
                {
                    action?.Invoke(writer);
                });

                // Add message to backlog
                AddReliableMessage(connection, rawBytes);
            }
        }

        /// <summary>
        /// Send an acknowledgement back to the given endPoint
        /// </summary>
        /// <param name="connection">The connection to send it to</param>
        protected virtual void SendHeartbeat(ClientConnection connection)
        {
            if (connection.Authenticated)
                SendRawMessage(connection, ConnectionType.HEARTBEAT, connection.Token);
        }

        /// <summary>
        /// Sends a raw message to the given endPoint
        /// </summary>
        /// <param name="connection">The connection to send it to</param>
        /// <param name="type">The type of connection to send</param>
        /// <param name="token">The token to send with it</param>
        /// <param name="action">The method to fill the buffer with data</param>
        /// <returns>The bytes that have been sent to the endPoint</returns>
        protected virtual byte[] SendRawMessage(ClientConnection connection, byte type, ulong token, Action<IBSStream> action = null)
        {
            // Increment sequence
            connection.IncrementSequence(ElapsedTime);

            // Get values for header
            ushort sequence = connection.LocalSequence;
            ushort ack = connection.RemoteSequence;
            uint ackBits = connection.AckBits;

            byte[] rawBytes;
            using (BSWriter writer = new BSWriter(headerSize))
            {
                // Write header
                SerializeHeader(writer,
                    ref type,
                    ref sequence,
                    ref ack,
                    ref ackBits,
                    ref token);
                action?.Invoke(writer);
                writer.SerializeChecksum(ProtocolVersion);

                rawBytes = writer.ToArray();
            }

            if (rawBytes.Length > RECEIVE_BUFFER_SIZE)
                throw new ArgumentOutOfRangeException("Packet size too big");

            try
            {
                socket.SendTo(rawBytes, rawBytes.Length, SocketFlags.None, connection.AddressPoint);
            }
            catch (SocketException e)
            {
                // Suppress warning about ICMP closed ports
                // Idea: Disconnect client if we receive this error? Might interfer with NAT punch-through
                if (e.ErrorCode != 10054)
                {
                    Log($"Network exception trying to receive data from {connection.AddressPoint}", LogLevel.Error);
                    Log(e.ToString(), LogLevel.Error);
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
            type = stream.SerializeByte(type, 2);
            sequence = stream.SerializeUShort(sequence);
            ack = stream.SerializeUShort(ack);
            ackBits = stream.SerializeUInt(ackBits);
            token = stream.SerializeULong(token);
        }

        /// <summary>
        /// Add a message to the reliable list, awaiting acknowledgement
        /// </summary>
        /// <param name="connection">The connection of this endPoint</param>
        /// <param name="bytes">The payload of the packet</param>
        protected virtual void AddReliableMessage(ClientConnection connection, byte[] bytes)
        {
            ReliableMessage msg = new ReliableMessage(bytes, ElapsedTime);

            ConnectionSequence connSeq = new ConnectionSequence(connection.AddressPoint, connection.LocalSequence);
            if (!unsentMessages.ContainsKey(connSeq))
                unsentMessages.Add(connSeq, msg);
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
                byte[] rawBytes = BufferPool.GetBuffer(RECEIVE_BUFFER_SIZE);
                int length = socket.ReceiveFrom(rawBytes, ref endPoint);
                //if (random.Next(100) < 25) return; // DEBUG

                inComingBipS += length * 8;

                // The length is less than the header, certainly malicious
                if (length < headerSize)
                {
                    BufferPool.ReturnBuffer(rawBytes);
                    continue;
                }

                // Read the buffer and determine CRC
                using (BSReader reader = new BSReader(rawBytes, length))
                {
                    if (!reader.SerializeChecksum(ProtocolVersion))
                        continue;

                    // Read header
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
                    
                    // Handle the message
                    if (type == ConnectionType.CONNECT) // If this endPoint wants to establish connection
                    {
                        if (length == RECEIVE_BUFFER_SIZE) // Make sure this message has been padded
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
                                connection = new ClientConnection(endPoint, ElapsedTime, localToken, token);
                                connections.Add(endPoint, connection);

                                // Acknowledge this packet
                                connection.Acknowledge(sequence);

                                // Send a connection message to the sender
                                byte[] bytes = SendRawMessage(connection, ConnectionType.CONNECT, connection.LocalToken, writer =>
                                {
                                    // Pad message to 1024 bytes
                                    writer.PadToEnd();
                                });
                                AddReliableMessage(connection, bytes);
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
                            // If this connection is authenticated
                            if (connection.Authenticated && connection.Authenticate(token, ElapsedTime))
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
                                for (int i = 31; i >= 0; i--)
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
                                if (!connection.IsAcknowledged(sequence) && type == ConnectionType.MESSAGE)
                                    OnReceiveMessage((IPEndPoint)endPoint, reader);

                                // Acknowledge this packet
                                connection.Acknowledge(sequence);
                            }
                            else
                            {
                                Log($"Mismatching token for {endPoint}", LogLevel.Warning);
                            }
                        }
                    }
                }

                BufferPool.ReturnBuffer(rawBytes);
            }

            double timeout = ElapsedTime - TIMEOUT;
            double resendTime = ElapsedTime - TickRate * 32d;
            double beatTime = ElapsedTime;

            // Set up lists
            lastTimedOut.Clear();
            lastHeartBeats.Clear();
            foreach (var data in connections)
            {
                if (data.Value.LastReceived < timeout)
                    lastTimedOut.Add(data.Key);
                else if (data.Value.LastSent < beatTime)
                    lastHeartBeats.Add(data.Key);
            }


            // Clean up old endPoints
            foreach (EndPoint ep in lastTimedOut)
            {
                connections.Remove(ep);
                OnDisconnect((IPEndPoint)ep);
            }


            // Resend lost reliable packets
            var resendMessages = unsentMessages.Where(i => i.Value.timeSent < resendTime).ToArray();
            foreach (var data in resendMessages)
            {
                EndPoint ep = data.Key.endPoint;
                if (connections.TryGetValue(ep, out ClientConnection connection))
                {
                    // Get CRC
                    byte[] rawBytes = unsentMessages[data.Key].bytes;
                    using (BSReader reader = new BSReader(rawBytes))
                    {
                        reader.SerializeChecksum(ProtocolVersion);

                        // Get header
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

                        // Remove message from backlog
                        unsentMessages.Remove(data.Key);

                        // Send new message
                        byte[] newBytes = SendRawMessage(connection, type, token, writer =>
                        {
                            writer.SerializeBytes(bits, payload);
                        });

                        // Add message to backlog
                        AddReliableMessage(connection, newBytes);
                    }

                    Log($"Packet {data.Key.sequence} to {data.Key.endPoint} has been resent as {connection.LocalSequence}", LogLevel.Warning);
                }
                else
                {
                    // Client has disconnected
                    Log($"Packet {data.Key.sequence} to {data.Key.endPoint} dropped due to client disconnect", LogLevel.Warning);
                    unsentMessages.Remove(data.Key);
                }
            }


            // Send a heartbeat if nothing has been sent this tick
            foreach (EndPoint ep in lastHeartBeats)
            {
                if (connections.TryGetValue(ep, out ClientConnection connection) && connection.LastSent < beatTime)
                    SendHeartbeat(connection);
            }


            // Calculate network statistics
            if (nextBip < ElapsedTime)
            {
                nextBip = ElapsedTime + 1d;

                OnNetworkStatistics(outGoingBipS, inComingBipS);

                outGoingBipS = 0;
                inComingBipS = 0;
            }
        }


        protected abstract void Log(object obj, LogLevel level);

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