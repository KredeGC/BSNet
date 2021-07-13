using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using BSNet.Quantization;

#if !(ENABLE_MONO || ENABLE_IL2CPP)
using System.Numerics;
#else
using UnityEngine;
#endif

namespace BSNet.Stream
{
    public class BitWriter : IBSStream, IDisposable
    {
        private static Queue<BitWriter> writerPool = new Queue<BitWriter>();

        public bool Writing { get { return true; } }
        public bool Reading { get { return false; } }

        public int TotalBits
        {
            get
            {
                return BSUtility.BITS * bytePos + bitPos - 1;
            }
        }

        public int Remainder // 8 - (bitPos - 1)
        {
            get
            {
                return (9 - bitPos) % BSUtility.BITS;
            }
        }

        private byte[] internalStream;
        private int bytePos = 0;
        private int bitPos = 1;

        private BitWriter(int length)
        {
            internalStream = BSPool.GetBuffer(length);
        }

        ~BitWriter()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            Return(this);
        }

        /// <summary>
        /// Retrieves a writer from the pool, or creates a new if none exist
        /// </summary>
        /// <param name="length">The initial capacity in bytes</param>
        /// <returns>A new writer</returns>
        public static BitWriter Get(int length = 0)
        {
            lock (writerPool)
            {
                BitWriter writer;
                if (writerPool.Count > 0)
                {
                    writer = writerPool.Dequeue();
                    BSPool.ReturnBuffer(writer.internalStream);
                    writer.internalStream = BSPool.GetBuffer(length);
                }
                else
                {
                    writer = new BitWriter(length);
                }

                return writer;
            }
        }

        /// <summary>
        /// Returns the given writer into the pool for later use
        /// </summary>
        /// <param name="writer">The writer to return</param>
        public static void Return(BitWriter writer)
        {
            lock (writerPool)
            {
                writer.bytePos = 0;
                writer.bitPos = 1;
                writerPool.Enqueue(writer);
            }
        }


        // CRC Header
        public bool SerializeChecksum(byte[] version)
        {
            byte[] data = ToArray();

            byte[] combinedBytes = BSPool.GetBuffer(version.Length + data.Length);
            Buffer.BlockCopy(version, 0, combinedBytes, 0, version.Length);
            Buffer.BlockCopy(data, 0, combinedBytes, version.Length, data.Length);

            byte[] crcBytes = Cryptography.CRC32Bytes(combinedBytes);

            byte[] headerBytes = BSPool.GetBuffer(crcBytes.Length + data.Length);
            Buffer.BlockCopy(crcBytes, 0, headerBytes, 0, crcBytes.Length);
            Buffer.BlockCopy(data, 0, headerBytes, crcBytes.Length, data.Length);

            BSPool.ReturnBuffer(internalStream);

            internalStream = headerBytes;
            bytePos += crcBytes.Length;

            BSPool.ReturnBuffer(combinedBytes);
            BSPool.ReturnBuffer(crcBytes);

            return true;
        }

        // Padding
        public byte[] PadToEnd()
        {
            int remaining = (BSUtility.PACKET_MAX_SIZE - 4) * BSUtility.BITS - TotalBits;
            byte[] padding = new byte[BSUtility.PACKET_MAX_SIZE];
            SerializeBytes(remaining, padding);
            return padding;
        }

        // Unsigned
        public byte SerializeByte(byte value = default(byte), int bitCount = sizeof(byte) * BSUtility.BITS)
        {
            byte[] bytes = new byte[] { value };
            SerializeBytes(bitCount, bytes);
            return value;
        }

        public ushort SerializeUShort(ushort value = default(ushort), int bitCount = sizeof(ushort) * BSUtility.BITS)
        {
            byte[] bytes = new byte[2];
            bytes[0] = (byte)(value >> 8);
            bytes[1] = (byte)value;
            SerializeBytes(bitCount, bytes);
            return value;
        }

        public uint SerializeUInt(uint value = default(uint), int bitCount = sizeof(uint) * BSUtility.BITS)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(value >> 24);
            bytes[1] = (byte)(value >> 16);
            bytes[2] = (byte)(value >> 8);
            bytes[3] = (byte)value;
            SerializeBytes(bitCount, bytes);
            return value;
        }

        public ulong SerializeULong(ulong value = default(ulong), int bitCount = sizeof(ulong) * BSUtility.BITS)
        {
            byte[] bytes = new byte[8];
            bytes[0] = (byte)(value >> 56);
            bytes[1] = (byte)(value >> 48);
            bytes[2] = (byte)(value >> 40);
            bytes[3] = (byte)(value >> 32);
            bytes[4] = (byte)(value >> 24);
            bytes[5] = (byte)(value >> 16);
            bytes[6] = (byte)(value >> 8);
            bytes[7] = (byte)value;
            SerializeBytes(bitCount, bytes);
            return value;
        }

        // Signed
        public sbyte SerializeSByte(sbyte value = default(sbyte), int bitCount = sizeof(sbyte) * BSUtility.BITS)
        {
            byte zigzag = (byte)((value << 1) ^ (value >> 7));
            SerializeByte(zigzag, bitCount);
            return value;
        }

        public short SerializeShort(short value = default(short), int bitCount = sizeof(short) * BSUtility.BITS)
        {
            ushort zigzag = (ushort)((value << 1) ^ (value >> 15));
            SerializeUShort(zigzag, bitCount);
            return value;
        }

        public int SerializeInt(int value = default(int), int bitCount = sizeof(int) * BSUtility.BITS)
        {
            uint zigzag = (uint)((value << 1) ^ (value >> 31));
            SerializeUInt(zigzag, bitCount);
            return value;
        }

        public long SerializeLong(long value = default(long), int bitCount = sizeof(long) * BSUtility.BITS)
        {
            ulong zigzag = (ulong)((value << 1) ^ (value >> 63));
            SerializeULong(zigzag, bitCount);
            return value;
        }

        // Floating point
        public float SerializeFloat(BoundedRange range, float value = default(float))
        {
            uint quanValue = range.Quantize(value);

            SerializeUInt(quanValue, range.BitsRequired);
            return value;
        }

        public float SerializeHalf(float value = default(float))
        {
            ushort half = HalfPrecision.Quantize(value);

            SerializeUShort(half);
            return value;
        }

        // Vectors & Quaternions
        public Vector2 SerializeVector2(BoundedRange[] range, Vector2 vec = default(Vector2))
        {
            QuantizedVector2 quanVec = BoundedRange.Quantize(vec, range);

            SerializeUInt(quanVec.x, range[0].BitsRequired);
            SerializeUInt(quanVec.y, range[1].BitsRequired);

            return vec;
        }

        public Vector3 SerializeVector3(BoundedRange[] range, Vector3 vec = default(Vector3))
        {
            QuantizedVector3 quanVec = BoundedRange.Quantize(vec, range);

            SerializeUInt(quanVec.x, range[0].BitsRequired);
            SerializeUInt(quanVec.y, range[1].BitsRequired);
            SerializeUInt(quanVec.z, range[2].BitsRequired);

            return vec;
        }

        public Vector4 SerializeVector4(BoundedRange[] range, Vector4 vec = default(Vector4))
        {
            QuantizedVector4 quanVec = BoundedRange.Quantize(vec, range);

            SerializeUInt(quanVec.x, range[0].BitsRequired);
            SerializeUInt(quanVec.y, range[1].BitsRequired);
            SerializeUInt(quanVec.z, range[2].BitsRequired);
            SerializeUInt(quanVec.w, range[3].BitsRequired);

            return vec;
        }

        public Quaternion SerializeQuaternion(int bitsPerElement = 12, Quaternion quat = default(Quaternion))
        {
            QuantizedQuaternion quanQuat = SmallestThree.Quantize(quat, bitsPerElement);

            SerializeUInt(quanQuat.m, 2);
            SerializeUInt(quanQuat.a, bitsPerElement);
            SerializeUInt(quanQuat.b, bitsPerElement);
            SerializeUInt(quanQuat.c, bitsPerElement);

            return quat;
        }

        // String
        public string SerializeString(Encoding encoding, string value = null)
        {
            if (value.Equals(null)) throw new ArgumentNullException(nameof(value));
            if (encoding.Equals(null)) throw new ArgumentNullException(nameof(encoding));

            byte[] bytes = encoding.GetBytes(value);
            SerializeInt(bytes.Length);

            if (bytes.Length > 0)
                SerializeBytes(bytes.Length * BSUtility.BITS, bytes);

            return value;
        }

        // IPs
        public IPAddress SerializeIPAddress(IPAddress ipAddress)
        {
            byte[] bytes = ipAddress.GetAddressBytes();
            SerializeBytes(4 * BSUtility.BITS, bytes);
            return ipAddress;
        }

        public IPEndPoint SerializeIPEndPoint(IPEndPoint endPoint)
        {
            SerializeIPAddress(endPoint.Address);
            SerializeUShort((ushort)endPoint.Port);

            return endPoint;
        }

        // Bytes
        public byte[] SerializeBytes(int bitCount, byte[] data = null, bool trimRight = false)
        {
            int size = (bitCount - 1) / BSUtility.BITS + 1;
            byte[] raw = BSPool.GetBuffer(size);
            Buffer.BlockCopy(data, 0, raw, 0, size);
            Write(bitCount, raw);
            BSPool.ReturnBuffer(raw);
            return raw;
        }

        public byte[] SerializeBytes(byte[] data = null)
        {
            byte[] raw = BSPool.GetBuffer(data.Length);
            Buffer.BlockCopy(data, 0, raw, 0, data.Length);
            Write(data.Length * BSUtility.BITS, raw);
            BSPool.ReturnBuffer(raw);
            return raw;
        }

        // TODO: Do the opposite
        // This doesn't work for more than single bytes
        // Make a method like SerializeBytes that appends spaces in front, if bitCount isn't right
        public byte[] ToBytes()
        {
            int remainder = 8 - (bitPos - 1);

            if (remainder < 8)
            {
                byte[] rawBytes;
                using (BitWriter writer = Get())
                {
                    writer.SerializeByte(0, remainder);
                    writer.SerializeBytes(ToArray());

                    int size = (TotalBits - remainder - 1) / BSUtility.BITS + 1;

                    byte[] raw = writer.ToArray();
                    rawBytes = new byte[size];

                    Buffer.BlockCopy(raw, 0, rawBytes, 0, size);
                }

                return rawBytes;
            }

            return ToArray();
        }

        public byte[] ToArray()
        {
            int size = (TotalBits - 1) / BSUtility.BITS + 1;
            byte[] rawBytes = new byte[size];
            Buffer.BlockCopy(internalStream, 0, rawBytes, 0, size);

            return rawBytes;
        }

        private void Write(int bitCount, byte[] data)
        {
            // Expand the stream
            int expansion = bytePos + (bitPos - 1 + bitCount - 1) / BSUtility.BITS + 1;

            if (expansion > internalStream.Length)
            {
                byte[] bytes = BSPool.GetBuffer(expansion);
                Buffer.BlockCopy(internalStream, 0, bytes, 0, internalStream.Length);

                BSPool.ReturnBuffer(internalStream);

                internalStream = bytes;
            }

            // Write in little-endian
            int byteCountCeil = (bitCount - 1) / BSUtility.BITS + 1;
            int maxBytes = data.Length - byteCountCeil;
            int consumedBits = 0;

            // Optimization if the stream isn't offset by bits
            if (bitPos == 1)
            {
                // Reverse array
                if (data.Length > 1 && BitConverter.IsLittleEndian)
                    Array.Reverse(data);

                // Copy byte array into stream
                Buffer.BlockCopy(data, 0, internalStream, bytePos, byteCountCeil);
                bytePos += byteCountCeil;

                // If bitcount isn't whole, set the last byte to the correct bits
                int endBits = bitCount % BSUtility.BITS;
                if (endBits != 0)
                {
                    bytePos--;
                    internalStream[bytePos] = (byte)(data[bitCount / BSUtility.BITS] << (BSUtility.BITS - endBits));
                    bitPos += endBits;
                }

                return;
            }

            // Pack the bits into the stream
            for (int i = data.Length - 1; i >= maxBytes; i--)
            {
                int bitsToConsume = Math.Min(bitCount - consumedBits, BSUtility.BITS);
                int remainingBits = BSUtility.BITS - (bitPos - 1);

                byte value;
                if (BitConverter.IsLittleEndian)
                    value = (byte)(data[i] & BSUtility.GetNarrowingMask(bitsToConsume));
                else
                    value = (byte)(data[data.Length - 1 - i] & BSUtility.GetNarrowingMask(bitsToConsume));

                if (bitsToConsume > remainingBits)
                {
                    // Add the first part of the value
                    internalStream[bytePos++] |= (byte)((byte)(value >> (bitsToConsume - remainingBits)) & BSUtility.GetNarrowingMask(remainingBits));
                    bitPos = 1;
                    remainingBits = bitsToConsume - remainingBits;

                    // Add the second part of the value, doesn't need the '|'
                    internalStream[bytePos] |= (byte)(value << (BSUtility.BITS - remainingBits));
                    bitPos += remainingBits;
                }
                else
                {
                    // Offset and add the value
                    internalStream[bytePos] |= (byte)(value << (remainingBits - bitsToConsume));
                    bitPos += bitsToConsume;
                    if (bitPos > BSUtility.BITS)
                    {
                        bitPos = 1;
                        bytePos++;
                    }
                }

                consumedBits += bitsToConsume;
            }
        }
    }
}
