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
#if (ENABLE_MONO || ENABLE_IL2CPP)
    public class BSSocket<T> : MonoBehaviour where T : ClientConnection, new()
#else
    public abstract class BSSocket<T> : IDisposable where T : ClientConnection, new()
#endif
    {
        /// <summary>
        /// The elapsed time since the socket was created
        /// </summary>
#if !(ENABLE_MONO || ENABLE_IL2CPP)
        public double ElapsedTime => stopwatch.Elapsed.TotalSeconds;

        protected readonly Stopwatch stopwatch;
#else
        public double ElapsedTime { get; private set; }
#endif

        // Properties
        /// <summary>
        /// The version number for this connection
        /// </summary>
        public abstract byte[] ProtocolVersion { get; }

        /// <summary>
        /// The receiving tickrate of this socket
        /// </summary>
        public double TickRate { protected set; get; }

        /// <summary>
        /// The port this socket listens on and sends from
        /// </summary>
        public int Port => ((IPEndPoint)socket.LocalEndPoint).Port;

        /// <summary>
        /// The endPoint this socket listens on and sends from
        /// </summary>
        public IPEndPoint LocalEndPoint => (IPEndPoint)socket.LocalEndPoint;

#if NETWORK_DEBUG
        // Network debugging
        public virtual double SimulatedPacketLatency { set; get; } // 0-1000
        public virtual double SimulatedPacketLoss { set; get; } // 0-1
        public virtual double SimulatedPacketCorruption { set; get; } // 0-1

        protected readonly System.Random random = new System.Random();

        protected readonly List<Packet> latencyList = new List<Packet>();
#endif

        // Socket stuff
        protected double nextMessage;
        protected IPEndPoint localEndPoint;
        protected Socket socket;
        protected bool _disposing;
        protected const int SIO_UDP_CONNRESET = -1744830452;

        // Network statistics
        protected double nextBip;
        protected int inComingBipS;
        protected int outGoingBipS;

        // Connections & reliable messages
        protected readonly Dictionary<IPEndPoint, T> connections = new Dictionary<IPEndPoint, T>();
        protected readonly Dictionary<ConnectionSequence, Packet> unsentMessages = new Dictionary<ConnectionSequence, Packet>();
        protected readonly List<IPEndPoint> lastTimedOut = new List<IPEndPoint>();
        protected readonly List<IPEndPoint> lastHeartBeats = new List<IPEndPoint>();

#if (ENABLE_MONO || ENABLE_IL2CPP)
        protected void Init(int port, int ticksPerSecond = 50)
#else
        protected BSSocket(int port, int ticksPerSecond = 50)
#endif
        {
            TickRate = 1d / ticksPerSecond;

            // Create the socket and listen for packets
            localEndPoint = new IPEndPoint(IPAddress.Any, port);
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try // This won't work on Linux
            {
                // Disable ICMP exceptions
                socket.IOControl(SIO_UDP_CONNRESET, new byte[] {0, 0, 0, 0}, null);
            }
            catch
            {
                // ignored
            }

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(localEndPoint);

#if !(ENABLE_MONO || ENABLE_IL2CPP)
            // Without Unity's Update method
            stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
        }

#if !(ENABLE_MONO || ENABLE_IL2CPP)
        ~BSSocket()
        {
            Dispose(false);
        }
#else
        protected virtual void OnDestroy()
        {
            socket.Close();
            socket = null;
        }
#endif

        /// <summary>
        /// Disposes of this socket and tries to gracefully disconnect with any connected endPoints
        /// </summary>
        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of this socket and tries to gracefully disconnect from any connected endPoints
        /// </summary>
        /// <param name="disposing">Whether to disconnect gracefully</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposing) return;

            _disposing = true;

            if (disposing)
            {
                foreach (IPEndPoint endPoint in connections.Keys)
                    Disconnect(endPoint);
            }

            socket.Close();
            socket = null;
        }

        /// <summary>
        /// Call this in a loop to handle incoming and outgoing packets
        /// </summary>
        public virtual void Update()
        {
#if (ENABLE_MONO || ENABLE_IL2CPP)
            ElapsedTime += Time.deltaTime;
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
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="endPoint">The endPoint to establish a connection with</param>
        public virtual void Connect(IPEndPoint endPoint)
        {
            if (!connections.TryGetValue(endPoint, out T connection))
            {
                ulong localToken = Cryptography.GenerateToken();

                connection = new T();
                connection.Initialize(endPoint, ElapsedTime, localToken, 0);
                connections.Add(endPoint, connection);
            }

            // Send a message with the generated token
            byte[] rawBytes = SendRawMessage(connection, ConnectionType.Connect, connection.LocalToken, writer =>
            {
                OnRequestConnect(endPoint, null, writer);

                // Pad message to 1024 bytes
                writer.PadToEnd();
            });
            AddReliableMessage(connection, rawBytes);
        }

        /// <summary>
        /// Attempts to end the connection with the endPoint
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="endPoint">The endPoint to end the connection with</param>
        /// <param name="action">The method to fill the buffer with data</param>
        public virtual void Disconnect(IPEndPoint endPoint, Action<IBSStream> action = null)
        {
            if (connections.TryGetValue(endPoint, out T connection))
            {
                // Send an unreliable message with the generated token. We shouldn't wait around
                SendRawMessage(connection, ConnectionType.Disconnect, connection.Token, writer => action?.Invoke(writer));

                if (!_disposing)
                    connections.Remove(endPoint);
            }
        }

        /// <summary>
        /// Sends a reliable message to an endPoint
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="endPoint">The endPoint to send it to</param>
        /// <param name="serializable">The serializable to fill the message</param>
        /// <returns>The sequence of the sent message</returns>
        public virtual ushort SendMessageUnreliable(IPEndPoint endPoint, IBSSerializable serializable)
        {
            return SendMessageUnreliable(endPoint, serializable.Serialize);
        }

        /// <summary>
        /// Sends an unreliable message to an endPoint
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="endPoint">The endPoint to send it to</param>
        /// <param name="action">The method to fill the buffer with data</param>
        /// <returns>The sequence of the sent message</returns>
        public virtual ushort SendMessageUnreliable(IPEndPoint endPoint, Action<IBSStream> action = null)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            // Check if authenticated
            if (connections.TryGetValue(endPoint, out T connection) && connection.Authenticated)
            {
                SendRawMessage(connection, ConnectionType.Message, connection.Token, writer => action?.Invoke(writer));

                return connection.LocalSequence;
            }

            return 0;
        }

        /// <summary>
        /// Sends a reliable message to an endPoint
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="endPoint">The endPoint to send it to</param>
        /// <param name="serializable">The serializable to fill the message</param>
        /// <returns>The sequence of the sent message</returns>
        public virtual ushort SendMessageReliable(IPEndPoint endPoint, IBSSerializable serializable)
        {
            return SendMessageReliable(endPoint, serializable.Serialize);
        }

        /// <summary>
        /// Sends a reliable message to an endPoint
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="endPoint">The endPoint to send it to</param>
        /// <param name="action">The method to fill the buffer with data</param>
        /// <returns>The sequence of the sent message</returns>
        public virtual ushort SendMessageReliable(IPEndPoint endPoint, Action<IBSStream> action = null)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            // Check if authenticated
            if (connections.TryGetValue(endPoint, out T connection) && connection.Authenticated)
            {
                byte[] rawBytes = SendRawMessage(connection, ConnectionType.Message, connection.Token, writer => action?.Invoke(writer));

                // Add message to backlog
                AddReliableMessage(connection, rawBytes);

                return connection.LocalSequence;
            }

            return 0;
        }

        /// <summary>
        /// Sends a heartbeat message to an endPoint, keeping the connection alive
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="connection">The connection to send it to</param>
        protected virtual void SendHeartbeat(T connection)
        {
            if (connection.Authenticated)
                SendRawMessage(connection, ConnectionType.Heartbeat, connection.Token);
        }

        /// <summary>
        /// Sends a raw message to the given endPoint with a maximum size of 1024 bytes
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="connection">The connection to send it to</param>
        /// <param name="type">The type of connection to send</param>
        /// <param name="token">The token to send with it</param>
        /// <param name="action">The method to fill the buffer with data</param>
        /// <returns>The bytes that have been sent to the endPoint</returns>
        protected virtual byte[] SendRawMessage(T connection, ConnectionType type, ulong token, Action<IBSStream> action = null)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

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
                throw new OverflowException("Packet size too big");

            outGoingBipS += rawBytes.Length * 8;

#if NETWORK_DEBUG
            if (SimulatedPacketLoss > 0 && random.NextDouble() < SimulatedPacketLoss)
                return rawBytes;
#endif

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
                    Log($"Network exception trying to send data to {connection.AddressPoint}", LogLevel.Error);
                    Log(e.ToString(), LogLevel.Error);
                }
            }
            catch (Exception e)
            {
                Log(e.ToString(), LogLevel.Error);
            }

            return rawBytes;
        }

        /// <summary>
        /// Add a message to the reliable list, awaiting acknowledgement
        /// </summary>
        /// <param name="connection">The connection of this endPoint</param>
        /// <param name="bytes">The payload of the packet</param>
        protected virtual void AddReliableMessage(T connection, byte[] bytes)
        {
            Packet msg = Packet.GetPacket(connection.AddressPoint, bytes, ElapsedTime);

            ConnectionSequence connSeq = new ConnectionSequence(connection.AddressPoint, connection.LocalSequence);
            if (!unsentMessages.ContainsKey(connSeq))
                unsentMessages.Add(connSeq, msg);
        }

        /// <summary>
        /// Handles a given message in raw byte format and returns it to the application if necessary
        /// </summary>
        /// <param name="endPoint">The endPoint that sent this message</param>
        /// <param name="rawBytes">The bytes to handle</param>
        /// <param name="length">Length of the byte array</param>
        protected virtual void HandleMessage(IPEndPoint endPoint, byte[] rawBytes, int length)
        {
            inComingBipS += length * 8;

            // The length is less than the header, certainly malicious
            if (length < BSUtility.PACKET_MIN_SIZE)
                return;

            // Read the buffer and determine CRC
            using (BSReader reader = BSReader.Get(rawBytes, length))
            {
                if (!reader.SerializeChecksum(ProtocolVersion))
                {
                    if (connections.TryGetValue(endPoint, out T connection))
                        connection.UpdateCorruption(true);

                    Log($"Mismatching checksum received from {endPoint}. Possibly corrupted", LogLevel.Warning);
                    return;
                }

                // Read header data
                using (Header header = Header.GetHeader(reader))
                {
                    // Handle the message
                    if (header.Type == ConnectionType.Connect) // If this endPoint wants to establish connection
                    {
                        if (length == BSUtility.PACKET_MAX_SIZE) // Make sure this message has been padded to avoid DDoS amplification
                        {
                            bool sendResponse = false;
                            if (connections.TryGetValue(endPoint, out T connection))
                            {
                                // Acknowledge this packet
                                connection.Acknowledge(header.Sequence);
                            }
                            else
                            {
                                // Add this connection to the list
                                ulong localToken = Cryptography.GenerateToken();
                                connection = new T();
                                connection.Initialize(endPoint, ElapsedTime, localToken, header.Token);
                                connections.Add(endPoint, connection);

                                // Acknowledge this packet
                                connection.Acknowledge(header.Sequence);

                                sendResponse = true;
                            }

                            connection.UpdateCorruption(false);

                            byte[] readerBytes = reader.ToArray();
                            //byte[] readerBytes = reader.SerializeStream(reader.TotalBits);

                            // If this connection is not authenticated
                            if (!connection.Authenticated)
                            {
                                connection.Authenticate(header.Token, ElapsedTime);
                                //using (BSReader reader2 = BSReader.Get(readerBytes))
                                OnConnect(endPoint, reader);
                            }

                            // Send a connection message to the sender
                            if (sendResponse && connections.ContainsKey(endPoint))
                            {
                                byte[] bytes = SendRawMessage(connection, ConnectionType.Connect, connection.LocalToken, writer =>
                                {
                                    using (BSReader reader2 = BSReader.Get(readerBytes))
                                        OnRequestConnect(endPoint, reader2, writer);

                                    // Pad message to 1024 bytes
                                    writer.PadToEnd();
                                });
                                AddReliableMessage(connection, bytes);
                            }
                        }
                        else
                        {
                            if (connections.TryGetValue(endPoint, out T connection))
                                connection.UpdateCorruption(true);
                        }
                    }
                    else if (connections.TryGetValue(endPoint, out T connection))
                    {
                        if (header.Type == ConnectionType.Disconnect) // If this endPoint wants to disconnect
                        {
                            // If this connection is authenticated
                            if (connection.Authenticated && connection.Authenticate(header.Token, ElapsedTime))
                            {
                                connection.UpdateCorruption(false);

                                connections.Remove(endPoint);
                                OnDisconnect(endPoint, reader);
                            }
                            else
                            {
                                connection.UpdateCorruption(true);
                            }
                        }
                        else if (connection.Authenticated) // Only allow messages/heartbeats from authenticated connections
                        {
                            // Compare the tokens
                            if (connection.Authenticate(header.Token, ElapsedTime))
                            {
                                connection.UpdateCorruption(false);

                                // Remove acknowledged messages
                                for (int i = 31; i >= 0; i--) // Shouldn't it be 32?
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

                                        if (!connection.ReceiveAcknowledgement(seq))
                                            OnReceiveAcknowledgement(endPoint, seq); // Return acknowledgement to application
                                    }
                                }

                                // Validate packet and return payload to application
                                if (!connection.IsAcknowledged(header.Sequence) && header.Type == ConnectionType.Message)
                                    OnReceiveMessage(endPoint, header.Sequence, reader);

                                // Acknowledge this packet
                                connection.Acknowledge(header.Sequence);
                            }
                            else
                            {
                                connection.UpdateCorruption(true);

                                Log($"Mismatching token received from {endPoint}", LogLevel.Warning);
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
            if (_disposing) return;

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

                try
                {
                    // Get packets from other endpoints
                    byte[] rawBytes = new byte[BSUtility.PACKET_MAX_SIZE];
                    int length = socket.ReceiveFrom(rawBytes, ref endPoint);

#if NETWORK_DEBUG
                    // Simulate packet loss
                    if (SimulatedPacketLoss > 0 && random.NextDouble() < SimulatedPacketLoss)
                        continue;

                    // Simulate packet corruption
                    if (SimulatedPacketCorruption > 0 && random.NextDouble() < SimulatedPacketCorruption)
                    {
                        int shiftAmount = random.Next(length * BSUtility.BITS);
                        byte bitMask = (byte)(1 << (shiftAmount % BSUtility.BITS));
                        int byteToFlip = length - (shiftAmount - 1) / BSUtility.BITS + 1;

                        rawBytes[byteToFlip] ^= bitMask;
                    }

                    // Simulate packet latency
                    if (SimulatedPacketLatency > 0)
                    {
                        Packet packet = Packet.GetPacket((IPEndPoint)endPoint, rawBytes, length, ElapsedTime + SimulatedPacketLatency / 1000d);
                        latencyList.Add(packet);
                    }
                    else
                    {
                        HandleMessage((IPEndPoint)endPoint, rawBytes, length);
                    }
#else
                    HandleMessage((IPEndPoint)endPoint, rawBytes, length);
#endif
                }
                catch (SocketException e)
                {
                    Log($"Network exception trying to receive data from {endPoint}", LogLevel.Error);
                    Log(e.ToString(), LogLevel.Error);
                }
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
            foreach (IPEndPoint ep in lastTimedOut)
            {
                connections.Remove(ep);
                OnDisconnect(ep, null);
            }


            // Resend lost reliable packets
            var resendMessages = unsentMessages.Where(i => i.Value.Time < resendTime).ToArray();
            foreach (var data in resendMessages)
            {
                IPEndPoint ep = data.Key.EndPoint;
                if (connections.TryGetValue(ep, out T connection))
                {
                    // Read the packet
                    byte[] rawBytes = unsentMessages[data.Key].Bytes;
                    using (BSReader reader = BSReader.Get(rawBytes))
                    {
                        // We don't really care about the checksum, as we're the ones who sent it in the first place
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
                            byte[] newBytes = SendRawMessage(connection, header.Type, header.Token, writer => writer.SerializeBytes(bits, payload));

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
            foreach (IPEndPoint ep in lastHeartBeats)
            {
                if (connections.TryGetValue(ep, out T connection) && connection.LastSent < beatTime)
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


        /// <summary>
        /// Called once per second, containing information about traffic in the last second
        /// </summary>
        /// <param name="outGoingBipS">The outgoing bits received in the last second</param>
        /// <param name="inComingBipS">The incoming bits received in the last second</param>
        protected virtual void OnNetworkStatistics(int outGoingBipS, int inComingBipS)
        {
        }

        /// <summary>
        /// Called when an event happens in the socket, such as warnings and errors
        /// <para/>Possible events can be, but are not limited to: packet corruption, packet loss, socket exceptions and connection drops
        /// <para/>Can also be user-created events like in the examples
        /// </summary>
        /// <param name="obj">The object that's been logged</param>
        /// <param name="level">The level of severity, ranging from least to most severe: Info, Warning, Error</param>
        protected abstract void Log(object obj, LogLevel level);

        /// <summary>
        /// Called when this socket receives an acknowledgement for a previously sent message, with the given sequence number
        /// </summary>
        /// <param name="endPoint">The address of the endPoint which acknowledged the message</param>
        /// <param name="sequence">The sequence number that was acknowledged</param>
        protected virtual void OnReceiveAcknowledgement(IPEndPoint endPoint, ushort sequence)
        {
        }

        /// <summary>
        /// Called when we want to send a connection message to the given endPoint
        /// </summary>
        /// <param name="endPoint">The endPoint we want to establish connection with</param>
        /// <param name="reader">The stream used to write a response message</param>
        /// <param name="writer">The stream used to write a response message</param>
        protected virtual void OnRequestConnect(IPEndPoint endPoint, IBSStream reader, IBSStream writer)
        {
        }

        /// <summary>
        /// Called when a connection is successfully established with a remote endPoint
        /// </summary>
        /// <param name="endPoint">The address of the endPoint which established connection</param>
        /// <param name="reader">The stream used to deserialize the contents of the connection message</param>
        protected abstract void OnConnect(IPEndPoint endPoint, IBSStream reader);

        /// <summary>
        /// Called when a connection with a remote endPoint is lost
        /// </summary>
        /// <param name="endPoint">The address of the lost endPoint</param>
        /// <param name="reader">The stream used to deserialize the contents of the disconnect message, or null if timed out</param>
        protected abstract void OnDisconnect(IPEndPoint endPoint, IBSStream reader);

        /// <summary>
        /// Called when this socket receives a packet from another, connected and authenticated endPoint
        /// </summary>
        /// <param name="endPoint">The address of the socket that sent the message</param>
        /// <param name="sequence">The sequence number of the received message</param>
        /// <param name="reader">The stream, used to deserialize the contents of the message</param>
        protected abstract void OnReceiveMessage(IPEndPoint endPoint, ushort sequence, IBSStream reader);
    }
}