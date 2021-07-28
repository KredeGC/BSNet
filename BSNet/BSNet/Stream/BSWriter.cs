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
    public class BSWriter : IBSStream, IDisposable
    {
        private static Queue<BSWriter> writerPool = new Queue<BSWriter>();

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

        private BSWriter(int length)
        {
            internalStream = BSPool.GetBuffer(length);
        }

        ~BSWriter()
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
        public static BSWriter Get(int length = 0)
        {
            lock (writerPool)
            {
                BSWriter writer;
                if (writerPool.Count > 0)
                {
                    writer = writerPool.Dequeue();
                    writer.internalStream = BSPool.GetBuffer(length);
                }
                else
                {
                    writer = new BSWriter(length);
                }

                return writer;
            }
        }

        /// <summary>
        /// Returns the given writer into the pool for later use
        /// </summary>
        /// <param name="writer">The writer to return</param>
        public static void Return(BSWriter writer)
        {
            lock (writerPool)
            {
                BSPool.ReturnBuffer(writer.internalStream);
                if (writerPool.Count < 4)
                {
                    writer.bytePos = 0;
                    writer.bitPos = 1;
                    writerPool.Enqueue(writer);
                }
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
        public int PadToEnd()
        {
            int remaining = (BSUtility.PACKET_MAX_SIZE - 4) * BSUtility.BITS - TotalBits;
            byte[] padding = new byte[BSUtility.PACKET_MAX_SIZE];
            SerializeBytes(remaining, padding);
            return remaining;
        }

        public int PadToByte()
        {
            int remaining = (8 - TotalBits % 8) % 8;
            byte[] padding = new byte[1];
            SerializeBytes(remaining, padding);
            return remaining;
        }

        // Unsigned
        public byte SerializeByte(byte value = default(byte), int bitCount = sizeof(byte) * BSUtility.BITS)
        {
            byte[] bytes = BSPool.GetBuffer(1);
            bytes[0] = value;
            Write(bitCount, bytes);
            BSPool.ReturnBuffer(bytes);
            return value;
        }

        public ushort SerializeUShort(ushort value = default(ushort), int bitCount = sizeof(ushort) * BSUtility.BITS)
        {
            byte[] bytes = BSPool.GetBuffer(2);
            bytes[0] = (byte)(value >> 8);
            bytes[1] = (byte)value;
            Write(bitCount, bytes);
            BSPool.ReturnBuffer(bytes);
            return value;
        }

        public uint SerializeUInt(uint value = default(uint), int bitCount = sizeof(uint) * BSUtility.BITS)
        {
            byte[] bytes = BSPool.GetBuffer(4);
            bytes[0] = (byte)(value >> 24);
            bytes[1] = (byte)(value >> 16);
            bytes[2] = (byte)(value >> 8);
            bytes[3] = (byte)value;
            Write(bitCount, bytes);
            BSPool.ReturnBuffer(bytes);
            return value;
        }

        public ulong SerializeULong(ulong value = default(ulong), int bitCount = sizeof(ulong) * BSUtility.BITS)
        {
            byte[] bytes = BSPool.GetBuffer(8);
            bytes[0] = (byte)(value >> 56);
            bytes[1] = (byte)(value >> 48);
            bytes[2] = (byte)(value >> 40);
            bytes[3] = (byte)(value >> 32);
            bytes[4] = (byte)(value >> 24);
            bytes[5] = (byte)(value >> 16);
            bytes[6] = (byte)(value >> 8);
            bytes[7] = (byte)value;
            Write(bitCount, bytes);
            BSPool.ReturnBuffer(bytes);
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
            int offset = trimRight ? data.Length * BSUtility.BITS - bitCount : 0;
            byte[] raw = BSPool.GetBuffer(data.Length);
            Buffer.BlockCopy(data, 0, raw, 0, data.Length);
            Write(bitCount, raw, offset);
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

        // Return internal stream
        public byte[] ToArray()
        {
            int size = (TotalBits - 1) / BSUtility.BITS + 1;
            byte[] rawBytes = new byte[size];
            Buffer.BlockCopy(internalStream, 0, rawBytes, 0, size);

            return rawBytes;
        }

        // Write internally
        private void Write(int bitCount, byte[] data, int offset = 0)
        {
            if (bitCount == 0) return;

            if (bitCount < 0)
                throw new ArgumentOutOfRangeException("Attempting to write a negative bitCount");

            int expansion = bytePos + (bitPos - 1 + bitCount - 1) / BSUtility.BITS + 1;

            if (expansion > internalStream.Length)
            {
                byte[] bytes = BSPool.GetBuffer(expansion);
                Buffer.BlockCopy(internalStream, 0, bytes, 0, internalStream.Length);

                BSPool.ReturnBuffer(internalStream);

                internalStream = bytes;
            }
            
            if (!BitConverter.IsLittleEndian)
	            Array.Reverse(data);

            // Length of the data
            int byteCountCeil = (bitCount - 1 + bitPos - 1) / BSUtility.BITS + 1;
            int totalOffset = data.Length * BSUtility.BITS - bitCount - offset; // 21

            // The offset to shift by
            int leftShift = totalOffset % BSUtility.BITS; // 5
            int rightShift = BSUtility.BITS - leftShift; // 3
            int skip = totalOffset / BSUtility.BITS; // 2
            byte leftValue = 0;

            // Go through the data
            for (int i = 0; i < byteCountCeil; i++)
            {
                // Add bits from the firs part, shifted by offset
                byte value = 0;
                if (i + skip < data.Length)
                    value |= (byte)(data[i + skip] << leftShift);

                // Add bits from the second part, shifted by 8 - offset
                if (i + skip + 1 < data.Length)
                    value |= (byte)(data[i + skip + 1] >> rightShift);

                if (bitPos > 1)
                {
                    byte original = value;

                    // Shift to the right to correct the data
                    value = (byte)(value >> bitPos - 1);

                    // Shift part of the value on the left into this value
                    if (i + skip - 1 >= 0)
                        value |= (byte)(leftValue << BSUtility.BITS - (bitPos - 1));

                    leftValue = original;
                }

                internalStream[bytePos + i] |= value;
            }

            // Offset read count by bitCount
            bytePos += bitCount / BSUtility.BITS;
            bitPos += bitCount % BSUtility.BITS;
            if (bitPos > BSUtility.BITS)
            {
                bitPos -= BSUtility.BITS;
                bytePos++;
            }
        }
    }
}