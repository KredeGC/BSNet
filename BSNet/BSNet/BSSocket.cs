using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using BSNet.Stream;
using BSNet.Datagram;

#if (ENABLE_MONO || ENABLE_IL2CPP)
using UnityEngine;
#else
using System.Diagnostics;
#endif

namespace BSNet
{
    public abstract class BSSocket : IDisposable
    {
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

#if NETWORK_DEBUG
        // Network debugging
        public virtual int SimulatedPacketLoss { set; get; } // 0-1000
        public virtual int SimulatedPacketLatency { set; get; } // 0-1000

        protected System.Random random = new System.Random();

        protected List<Packet> latencyList = new List<Packet>();
#endif

        // Socket stuff
        protected double nextMessage;
        protected IPEndPoint localEndPoint;
        protected Socket socket;
        protected const int SIO_UDP_CONNRESET = -1744830452;
        protected bool _disposing;

        // P2P Protocol
        //protected const int headerSize =
        //    sizeof(uint) + // CRC32 of version + packet (4 bytes)
        //    sizeof(byte) + // ConnectionType (2 bits)
        //    sizeof(ushort) + // Sequence of this packet (2 bytes)
        //    sizeof(ushort) + // Acknowledgement for most recent received packet (2 bytes)
        //    sizeof(uint) + // Bitfield of acknowledgements before most recent (4 bytes)
        //    sizeof(ulong); // Token or LocalToken if not authenticated (8 bytes)

        // Network statistics
        protected double nextBip;
        protected int inComingBipS = 0;
        protected int outGoingBipS = 0;

        // Connections & reliable messages
        protected Dictionary<EndPoint, ClientConnection> connections = new Dictionary<EndPoint, ClientConnection>();
        protected Dictionary<ConnectionSequence, Packet> unsentMessages = new Dictionary<ConnectionSequence, Packet>();
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

        /// <summary>
        /// Disposes of this socket and tries to gracefully disconnect with any connected endPoints
        /// </summary>
        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of this socket and tries to gracefully disconnect with any connected endPoints
        /// </summary>
        /// <param name="disposing">Whether to disconnect gracefully</param>
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

        /// <summary>
        /// Handles incoming messages at the given tickrate
        /// </summary>
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
        /// Attempts to end the connection with the endPoint
        /// </summary>
        /// <param name="endPoint">The endPoint to end the connection with</param>
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
        /// Sends a reliable message to an endPoint
        /// </summary>
        /// <param name="endPoint">The endPoint to send it to</param>
        /// <param name="serializable">The serializable to fill the message</param>
        /// <returns>The sequence of the sent message</returns>
        public virtual ushort SendMessageUnreliable(EndPoint endPoint, IBSSerializable serializable)
        {
            return SendMessageUnreliable(endPoint, writer =>
            {
                serializable.Serialize(writer);
            });
        }

        /// <summary>
        /// Sends an unreliable message to an endPoint
        /// </summary>
        /// <param name="endPoint">The endPoint to send it to</param>
        /// <param name="action">The method to fill the buffer with data</param>
        /// <returns>The sequence of the sent message</returns>
        public virtual ushort SendMessageUnreliable(EndPoint endPoint, Action<IBSStream> action = null)
        {
            // Check if authenticated
            if (connections.TryGetValue(endPoint, out ClientConnection connection) && connection.Authenticated)
            {
                SendRawMessage(connection, ConnectionType.MESSAGE, connection.Token, writer =>
                {
                    action?.Invoke(writer);
                });

                return connection.LocalSequence;
            }

            return 0;
        }

        /// <summary>
        /// Sends a reliable message to an endPoint
        /// </summary>
        /// <param name="endPoint">The endPoint to send it to</param>
        /// <param name="serializable">The serializable to fill the message</param>
        /// <returns>The sequence of the sent message</returns>
        public virtual ushort SendMessageReliable(EndPoint endPoint, IBSSerializable serializable)
        {
            return SendMessageReliable(endPoint, writer =>
            {
                serializable.Serialize(writer);
            });
        }

        /// <summary>
        /// Sends a reliable message to an endPoint
        /// </summary>
        /// <param name="endPoint">The endPoint to send it to</param>
        /// <param name="action">The method to fill the buffer with data</param>
        /// <returns>The sequence of the sent message</returns>
        public virtual ushort SendMessageReliable(EndPoint endPoint, Action<IBSStream> action = null)
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

                return connection.LocalSequence;
            }

            return 0;
        }

        /// <summary>
        /// Sends a heartbeat message to an endPoint, keeping the connection alive
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
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="connection">The connection to send it to</param>
        /// <param name="type">The type of connection to send</param>
        /// <param name="token">The token to send with it</param>
        /// <param name="action">The method to fill the buffer with data</param>
        /// <returns>The bytes that have been sent to the endPoint</returns>
        protected virtual byte[] SendRawMessage(ClientConnection connection, byte type, ulong token, Action<IBSStream> action = null)
        {
            // Increment sequence
            connection.IncrementSequence(ElapsedTime);

            byte[] rawBytes;
            using (BSWriter writer = BSWriter.Get(BSUtility.PACKET_MIN_SIZE))
            {
                // Write header data
                using (Header header = Header.GetHeader(type,
                    connection.LocalSequence,
                    connection.RemoteSequence,
                    connection.AckBits,
                    token))
                {
                    header.Serialize(writer);
                }

                // Write message data
                action?.Invoke(writer);
                writer.SerializeChecksum(ProtocolVersion);

                rawBytes = writer.ToArray();
            }

            if (rawBytes.Length > BSUtility.PACKET_MAX_SIZE)
                throw new ArgumentOutOfRangeException("Packet size too big");

            try
            {
                socket.SendTo(rawBytes, rawBytes.Length, SocketFlags.None, connection.AddressPoint);
            }
            catch (SocketException e)
            {
                // Suppress warning about ICMP closed ports
                // Idea: Disconnect client if we receive this exception? Might interfer with NAT punch-through
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
        /// Add a message to the reliable list, awaiting acknowledgement
        /// </summary>
        /// <param name="connection">The connection of this endPoint</param>
        /// <param name="bytes">The payload of the packet</param>
        protected virtual void AddReliableMessage(ClientConnection connection, byte[] bytes)
        {
            Packet msg = Packet.GetPacket(connection.AddressPoint, bytes, ElapsedTime);

            ConnectionSequence connSeq = new ConnectionSequence(connection.AddressPoint, connection.LocalSequence);
            if (!unsentMessages.ContainsKey(connSeq))
                unsentMessages.Add(connSeq, msg);
        }

        /// <summary>
        /// Handles a given message in raw byte format and returns it to the application if necessary
        /// </summary>
        /// <param name="rawBytes">The bytes to handle</param>
        /// <param name="length">Length of the byte array</param>
        protected virtual void HandleMessage(EndPoint endPoint, byte[] rawBytes, int length)
        {
            inComingBipS += length * 8;

            // The length is less than the header, certainly malicious
            if (length < BSUtility.PACKET_MIN_SIZE)
            {
                // Don't return the buffer, we hopefully won't be needing it again
                // BSPool.ReturnBuffer(rawBytes);
                return;
            }

            // Read the buffer and determine CRC
            using (BSReader reader = BSReader.Get(rawBytes, length))
            {
                if (!reader.SerializeChecksum(ProtocolVersion))
                {
                    Log($"Mismatching CRC checksum for {endPoint}", LogLevel.Warning);
                    return;
                }

                // Read header data
                using (Header header = Header.GetHeader(reader))
                {
                    // Handle the message
                    if (header.Type == ConnectionType.CONNECT) // If this endPoint wants to establish connection
                    {
                        if (length == BSUtility.PACKET_MAX_SIZE) // Make sure this message has been padded to avoid DDos amplification
                        {
                            if (connections.TryGetValue(endPoint, out ClientConnection connection))
                            {
                                // Acknowledge this packet
                                connection.Acknowledge(header.Sequence);
                            }
                            else
                            {
                                // Add this connection to the list
                                ulong localToken = Cryptography.GenerateToken();
                                connection = new ClientConnection(endPoint, ElapsedTime, localToken, header.Token);
                                connections.Add(endPoint, connection);

                                // Acknowledge this packet
                                connection.Acknowledge(header.Sequence);

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
                                connection.Authenticate(header.Token, ElapsedTime);
                                OnConnect((IPEndPoint)endPoint);
                            }
                        }
                    }
                    else if (connections.TryGetValue(endPoint, out ClientConnection connection))
                    {
                        if (header.Type == ConnectionType.DISCONNECT) // If this endPoint wants to disconnect
                        {
                            // If this connection is authenticated
                            if (connection.Authenticated && connection.Authenticate(header.Token, ElapsedTime))
                            {
                                connections.Remove(endPoint);
                                OnDisconnect((IPEndPoint)endPoint);
                            }
                        }
                        else if (connection.Authenticated) // If this endPoint has been authenticated
                        {
                            // Compare the tokens
                            if (connection.Authenticate(header.Token, ElapsedTime))
                            {
                                // Remove acknowledged messages
                                for (int i = 31; i >= 0; i--)
                                {
                                    ushort seq = (ushort)(header.Ack - i);
                                    if (BSUtility.IsAcknowledged(header.AckBits, header.Ack, seq))
                                    {
                                        connection.UpdateRTT(seq, ElapsedTime);
                                        ConnectionSequence conSeq = new ConnectionSequence(endPoint, seq);
                                        if (unsentMessages.TryGetValue(conSeq, out Packet packet))
                                        {
                                            Packet.ReturnPacket(packet);
                                            unsentMessages.Remove(conSeq);
                                        }
                                        OnMessageAcknowledged(seq);
                                    }
                                }

                                // Validate packet and return payload to application
                                if (!connection.IsAcknowledged(header.Sequence) && header.Type == ConnectionType.MESSAGE)
                                    OnReceiveMessage((IPEndPoint)endPoint, header.Sequence, reader);

                                // Acknowledge this packet
                                connection.Acknowledge(header.Sequence);
                            }
                            else
                            {
                                Log($"Mismatching token for {endPoint}", LogLevel.Warning);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Receives packets from other endPoints and handles them
        /// </summary>
        protected virtual void ReceiveMessages()
        {
#if NETWORK_DEBUG
            for (int i = latencyList.Count - 1; i >= 0; i--)
            {
                Packet packet = latencyList[i];
                if (ElapsedTime > packet.Time)
                {
                    HandleMessage(packet.AddressPoint, packet.Bytes, packet.Bytes.Length);
                    latencyList.RemoveAt(i);
                    Packet.ReturnPacket(packet);
                }
            }
#endif

            while (socket.Available > 0)
            {
                EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

                // Get packets from other endpoints
                byte[] rawBytes = BSPool.GetBuffer(BSUtility.PACKET_MAX_SIZE);
                int length = socket.ReceiveFrom(rawBytes, ref endPoint);

#if NETWORK_DEBUG
                // Simulate packet loss
                if (SimulatedPacketLoss > 0 && random.Next(1000) < SimulatedPacketLoss)
                    continue;

                // Simulate packet latency
                if (SimulatedPacketLatency > 0)
                {
                    Packet packet = Packet.GetPacket(endPoint, rawBytes, length, ElapsedTime + SimulatedPacketLatency / 1000d);
                    latencyList.Add(packet);
                }
                else
                {
                    HandleMessage(endPoint, rawBytes, length);
                }
#else
                HandleMessage(endPoint, rawBytes, length);
#endif
            }

            double timeout = ElapsedTime - BSUtility.TIMEOUT;
            double resendTime = ElapsedTime - TickRate * 32d;
            double beatTime = ElapsedTime - TickRate;

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
            var resendMessages = unsentMessages.Where(i => i.Value.Time < resendTime).ToArray();
            foreach (var data in resendMessages)
            {
                EndPoint ep = data.Key.EndPoint;
                if (connections.TryGetValue(ep, out ClientConnection connection))
                {
                    // Get CRC
                    byte[] rawBytes = unsentMessages[data.Key].Bytes;
                    using (BSReader reader = BSReader.Get(rawBytes))
                    {
                        reader.SerializeChecksum(ProtocolVersion);

                        // Read header data
                        using (Header header = Header.GetHeader(reader))
                        {
                            // Get payload
                            int bits = reader.TotalBits;
                            byte[] payload = reader.SerializeBytes(bits);

                            // Remove message from backlog
                            Packet.ReturnPacket(data.Value);
                            unsentMessages.Remove(data.Key);

                            // Send new message
                            byte[] newBytes = SendRawMessage(connection, header.Type, header.Token, writer =>
                            {
                                writer.SerializeBytes(bits, payload);
                            });

                            // Add message to backlog
                            AddReliableMessage(connection, newBytes);
                        }
                    }

                    Log($"Packet {data.Key.Sequence} to {data.Key.EndPoint} has been resent as {connection.LocalSequence}", LogLevel.Warning);
                }
                else
                {
                    // Client has disconnected
                    Log($"Packet {data.Key.Sequence} to {data.Key.EndPoint} dropped due to client disconnect", LogLevel.Warning);
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
            /*foreach (ClientConnection connection in connections.Values)
                Log(connection.RTT, LogLevel.Info);
            Log($"Outgoing bits in the last second: {outGoingBipS / 1000f} Kbits/S", LogLevel.Info);
            Log($"Incoming bits in the last second: {inComingBipS / 1000f} Kbits/S", LogLevel.Info);*/
        }

        /// <summary>
        /// Called when this socket receives an acknowledgement for a previously sent message, with the given sequence number
        /// <para/>Note: This callback runs every time an acknowledgement is received, which can happen up to 33 times per message
        /// </summary>
        /// <param name="sequence">The sequence number that was acknowledged</param>
        protected virtual void OnMessageAcknowledged(ushort sequence) { }

        /// <summary>
        /// Called when a connection is established with a remote endPoint
        /// </summary>
        /// <param name="endPoint">The address of the endPoint which established connection</param>
        protected abstract void OnConnect(IPEndPoint endPoint);

        /// <summary>
        /// Called when a connection with a remote endPoint is lost
        /// </summary>
        /// <param name="endPoint">The address of the lost endPoint</param>
        protected abstract void OnDisconnect(IPEndPoint endPoint);

        /// <summary>
        /// Called when this socket receives a packet from another, connected and authenticated endPoint
        /// </summary>
        /// <param name="endPoint">The address of the socket that sent the message</param>
        /// <param name="sequence">The sequence number of the received message</param>
        /// <param name="reader">The stream, used to deserialize the contents of the message</param>
        protected abstract void OnReceiveMessage(IPEndPoint endPoint, ushort sequence, IBSStream reader);
    }
}