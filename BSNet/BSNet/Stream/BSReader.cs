using System;
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
    public class BSReader : IBSStream, IDisposable
    {
        public bool Writing { get { return false; } }
        public bool Reading { get { return true; } }

        public int TotalBits
        {
            get
            {
                return BSUtility.BYTE_BITS * internalStream.Length - BSUtility.BYTE_BITS * bytePos - bitPos + 1;
            }
        }

        private byte[] internalStream;
        private int bytePos = 0;
        private int bitPos = 1;

        public BSReader(byte[] byteStream, int length)
        {
            internalStream = BufferPool.GetBuffer(length);
            Buffer.BlockCopy(byteStream, 0, internalStream, 0, length);
        }

        public BSReader(byte[] byteStream) : this(byteStream, byteStream.Length) { }

        ~BSReader()
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
            BufferPool.ReturnBuffer(internalStream);
        }


        // CRC Header
        public bool SerializeChecksum(byte[] version)
        {
            byte[] crcBytes = BufferPool.GetBuffer(4);
            byte[] headerBytes = internalStream;

            byte[] data = BufferPool.GetBuffer(headerBytes.Length - crcBytes.Length);
            Buffer.BlockCopy(headerBytes, 0, crcBytes, 0, crcBytes.Length);
            Buffer.BlockCopy(headerBytes, crcBytes.Length, data, 0, data.Length);

            byte[] combinedBytes = BufferPool.GetBuffer(version.Length + data.Length);
            Buffer.BlockCopy(version, 0, combinedBytes, 0, version.Length);
            Buffer.BlockCopy(data, 0, combinedBytes, version.Length, data.Length);

            byte[] generatedBytes = Cryptography.CRC32Bytes(combinedBytes);

            for (int i = 0; i < crcBytes.Length; i++)
            {
                if (!crcBytes[i].Equals(generatedBytes[i]))
                {
                    BufferPool.ReturnBuffer(data);
                    BufferPool.ReturnBuffer(crcBytes);
                    BufferPool.ReturnBuffer(combinedBytes);

                    return false;
                }
            }

            BufferPool.ReturnBuffer(internalStream);

            internalStream = data;

            BufferPool.ReturnBuffer(crcBytes);
            BufferPool.ReturnBuffer(combinedBytes);

            return true;
        }

        // Padding
        public byte[] PadToEnd()
        {
            int remaining = (BSSocket.RECEIVE_BUFFER_SIZE - 4) * BSUtility.BYTE_BITS - TotalBits;
            byte[] padding = SerializeBytes(remaining);
            return padding;
        }

        // Unsigned
        public byte SerializeByte(byte value = default(byte), int bitCount = sizeof(byte) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = SerializeBytes(bitCount);
            return bytes[0];
        }

        public ushort SerializeUShort(ushort value = default(ushort), int bitCount = sizeof(ushort) * BSUtility.BYTE_BITS)
        {
            ulong val = SerializeULong(value, bitCount);
            return (ushort)val;
        }

        public uint SerializeUInt(uint value = default(uint), int bitCount = sizeof(uint) * BSUtility.BYTE_BITS)
        {
            ulong val = SerializeULong(value, bitCount);
            return (uint)val;
        }

        public ulong SerializeULong(ulong value = default(ulong), int bitCount = sizeof(ulong) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = SerializeBytes(bitCount);

            ulong val = 0;
            int shift = (bytes.Length - 1) * 8;
            for (int i = 0; i < bytes.Length; i++, shift -= 8)
                val |= (ulong)bytes[i] << shift;

            return val;
        }

        // Signed
        public sbyte SerializeSByte(sbyte value = default(sbyte), int bitCount = sizeof(sbyte) * BSUtility.BYTE_BITS)
        {
            byte val = SerializeByte(0, bitCount);
            sbyte zagzig = (sbyte)((val >> 1) ^ (-(sbyte)(val & 1)));
            return zagzig;
        }

        public short SerializeShort(short value = default(short), int bitCount = sizeof(short) * BSUtility.BYTE_BITS)
        {
            ushort val = SerializeUShort(0, bitCount);
            short zagzig = (short)((val >> 1) ^ (-(short)(val & 1)));
            return zagzig;
        }

        public int SerializeInt(int value = default(int), int bitCount = sizeof(int) * BSUtility.BYTE_BITS)
        {
            uint val = SerializeUInt(0, bitCount);
            int zagzig = (int)((val >> 1) ^ (-(int)(val & 1)));
            return zagzig;
        }

        public long SerializeLong(long value = default(long), int bitCount = sizeof(long) * BSUtility.BYTE_BITS)
        {
            ulong val = SerializeULong(0, bitCount);
            long zagzig = (long)(val >> 1) ^ (-(long)(val & 1));
            return zagzig;
        }

        // Floating point
        public float SerializeFloat(BoundedRange range, float value = default(float))
        {
            uint quanValue = SerializeUInt(0, range.BitsRequired);

            return range.Dequantize(quanValue);
        }

        public float SerializeHalf(float value = default(float))
        {
            ushort quanValue = SerializeUShort();

            return HalfPrecision.Dequantize(quanValue);
        }

        // Vectors & Quaternions
        public Vector2 SerializeVector2(BoundedRange[] range, Vector2 value = default(Vector2))
        {
            float x = SerializeFloat(range[0]);
            float y = SerializeFloat(range[1]);

            return new Vector2(x, y);
        }

        public Vector3 SerializeVector3(BoundedRange[] range, Vector3 value = default(Vector3))
        {
            float x = SerializeFloat(range[0]);
            float y = SerializeFloat(range[1]);
            float z = SerializeFloat(range[2]);

            return new Vector3(x, y, z);
        }

        public Vector4 SerializeVector4(BoundedRange[] range, Vector4 value = default(Vector4))
        {
            float x = SerializeFloat(range[0]);
            float y = SerializeFloat(range[1]);
            float z = SerializeFloat(range[2]);
            float w = SerializeFloat(range[3]);

            return new Vector4(x, y, z, w);
        }

        public Quaternion SerializeQuaternion(int bitsPerElement = 12, Quaternion value = default(Quaternion))
        {
            uint m = SerializeUInt(0, 2);
            uint a = SerializeUInt(0, bitsPerElement);
            uint b = SerializeUInt(0, bitsPerElement);
            uint c = SerializeUInt(0, bitsPerElement);

            QuantizedQuaternion quanQuat = new QuantizedQuaternion(m, a, b, c);

            return SmallestThree.Dequantize(quanQuat, bitsPerElement);
        }

        // String
        public string SerializeString(Encoding encoding, string value = null)
        {
            if (encoding.Equals(null)) throw new ArgumentNullException(nameof(encoding));

            int length = SerializeInt();

            if (length > 0)
            {
                byte[] bytes = SerializeBytes(length * BSUtility.BYTE_BITS);
                return encoding.GetString(bytes);
            }
            return string.Empty;
        }

        // IPs
        public IPAddress SerializeIPAddress(IPAddress ipAddress)
        {
            byte[] addressBytes = SerializeBytes(4 * BSUtility.BYTE_BITS);

            return new IPAddress(addressBytes);
        }

        public IPEndPoint SerializeIPEndPoint(IPEndPoint endPoint)
        {
            IPAddress ipAddress = SerializeIPAddress(null);

            ushort port = SerializeUShort();

            return new IPEndPoint(ipAddress, port);
        }

        // Bytes
        public byte[] SerializeBytes(int bitCount, byte[] data = null)
        {
            Read(bitCount, out byte[] raw, (int)Math.Ceiling((double)bitCount / BSUtility.BYTE_BITS));
            return raw;
        }


        private bool Read(int bitCount, out byte[] data, int typeBytes)
        {
            if (bitCount > TotalBits)
            {
                data = null;
                return false;
            }

            data = new byte[typeBytes];

            int destBytePos = data.Length - 1;
            int destBitPos = 1;
            int consumedBits = 0;

            while (consumedBits < bitCount)
            {
                int bitsToConsume = Math.Min(bitCount - consumedBits, BSUtility.BYTE_BITS);
                int remainingBits = BSUtility.BYTE_BITS - (bitPos - 1);
                int attemptConsumeBits = Math.Min(bitsToConsume, remainingBits);
                byte rawValue = (byte)(internalStream[bytePos] & BSUtility.GetWideningMask(attemptConsumeBits, bitPos - 1));

                bitPos += attemptConsumeBits;
                if (bitPos > BSUtility.BYTE_BITS)
                {
                    bitPos = 1;
                    bytePos++;
                }

                if (bitsToConsume > attemptConsumeBits)
                {
                    data[destBytePos] |= (byte)(rawValue << (bitsToConsume - attemptConsumeBits));
                    destBitPos += attemptConsumeBits;
                    if (destBitPos > BSUtility.BYTE_BITS)
                    {
                        destBitPos = 1;
                        destBytePos--;
                    }

                    remainingBits = bitsToConsume - attemptConsumeBits;
                    rawValue = (byte)(internalStream[bytePos] & BSUtility.GetWideningMask(remainingBits, bitPos - 1));
                    data[destBytePos] |= (byte)(rawValue >> (BSUtility.BYTE_BITS - remainingBits));

                    destBitPos += remainingBits;
                    if (destBitPos > BSUtility.BYTE_BITS)
                    {
                        destBitPos = 1;
                        destBytePos--;
                    }

                    bitPos += remainingBits;
                    if (bitPos > BSUtility.BYTE_BITS)
                    {
                        bitPos = 1;
                        bytePos++;
                    }
                }
                else
                {
                    data[destBytePos] |= (byte)(rawValue >> (remainingBits - bitsToConsume));
                    destBitPos += bitsToConsume;
                    if (destBitPos > BSUtility.BYTE_BITS)
                    {
                        destBitPos = 1;
                        destBytePos--;
                    }
                }

                consumedBits += bitsToConsume;
            }

            //if (BitConverter.IsLittleEndian) Array.Reverse(data);
            return true;
        }
    }
}
