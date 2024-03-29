﻿using BSNet.Stream;
using System;
using System.Collections.Generic;

namespace BSNet.Datagram
{
    public class Header : IBSSerializable, IDisposable
    {
        private static Queue<Header> headerPool = new Queue<Header>();

        public ConnectionType Type { private set; get; }
        public ushort Sequence { private set; get; }
        public ushort Ack { private set; get; }
        public uint AckBits { private set; get; }
        public ulong Token { private set; get; }

        public const int HEADER_SIZE =
            sizeof(byte) + // ConnectionType (2 bits)
            sizeof(ushort) + // Sequence of this packet (2 bytes)
            sizeof(ushort) + // Acknowledgement for most recent received packet (2 bytes)
            sizeof(uint) + // Bitfield of acknowledgements before most recent (4 bytes)
            sizeof(ulong); // Token or LocalToken if not authenticated (8 bytes)

        private Header() { }

        ~Header()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            ReturnHeader(this);
        }

        /// <summary>
        /// Retrieves a header from the pool, or creates a new if none exist
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <returns>A new header</returns>
        public static Header GetHeader(IBSStream stream)
        {
            lock (headerPool)
            {
                Header header;
                if (headerPool.Count > 0)
                    header = headerPool.Dequeue();
                else
                    header = new Header();

                if (stream.Reading)
                    header.Serialize(stream);
                else
                    throw new InvalidOperationException("Cannot initialize header from writable stream");

                return header;
            }
        }

        /// <summary>
        /// Retrieves a header from the pool, or creates a new if none exist
        /// </summary>
        /// <param name="type"></param>
        /// <param name="sequence"></param>
        /// <param name="ack"></param>
        /// <param name="ackBits"></param>
        /// <param name="token"></param>
        /// <returns>A new header</returns>
        public static Header GetHeader(ConnectionType type, ushort sequence, ushort ack, uint ackBits, ulong token)
        {
            lock (headerPool)
            {
                Header header;
                if (headerPool.Count > 0)
                    header = headerPool.Dequeue();
                else
                    header = new Header();

                header.Type = type;
                header.Sequence = sequence;
                header.Ack = ack;
                header.AckBits = ackBits;
                header.Token = token;

                return header;
            }
        }

        /// <summary>
        /// Returns the given header into the pool for later use
        /// </summary>
        /// <param name="header">The header to return</param>
        public static void ReturnHeader(Header header)
        {
            lock (headerPool)
            {
                if (headerPool.Count < BSUtility.MAX_POOLING)
                    headerPool.Enqueue(header);
            }
        }

        /// <summary>
        /// Use to serialize the header of a packet
        /// </summary>
        /// <param name="stream">The stream to serialize into</param>
        public void Serialize(IBSStream stream)
        {
            Type = (ConnectionType)stream.SerializeByte((byte)Type, 2);
            Sequence = stream.SerializeUShort(Sequence);
            Ack = stream.SerializeUShort(Ack);
            AckBits = stream.SerializeUInt(AckBits);
            Token = stream.SerializeULong(Token);
        }
    }
}
