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
    public class BSReader : IBSStream
    {
        public bool Writing { get { return false; } }
        public bool Reading { get { return true; } }

        public bool IsFinished
        {
            get
            {
                return TotalBits == 0;
            }
        }
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
            internalStream = new byte[byteStream.Length];
            Buffer.BlockCopy(byteStream, 0, internalStream, 0, length);
        }

        public BSReader(byte[] byteStream) : this(byteStream, byteStream.Length) { }


        // Unsigned
        public bool SerializeByte(ref byte value, int bitCount = sizeof(byte) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = null;
            if (!SerializeBytes(ref bytes, bitCount))
                return false;

            value = bytes[0];
            return true;
        }

        public bool SerializeUShort(ref ushort value, int bitCount = sizeof(ushort) * BSUtility.BYTE_BITS)
        {
            ulong lValue = 0;
            if (!SerializeULong(ref lValue, bitCount))
                return false;

            value = (ushort)lValue;
            return true;
        }

        public bool SerializeUInt(ref uint value, int bitCount = sizeof(uint) * BSUtility.BYTE_BITS)
        {
            ulong lValue = 0;
            if (!SerializeULong(ref lValue, bitCount))
                return false;

            value = (uint)lValue;
            return true;
        }

        public bool SerializeULong(ref ulong value, int bitCount = sizeof(ulong) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = null;
            if (!SerializeBytes(ref bytes, bitCount))
                return false;

            value = 0;
            int shift = (bytes.Length - 1) * 8;
            for (int i = 0; i < bytes.Length; i++, shift -= 8)
                value |= (ulong)bytes[i] << shift;

            return true;
        }

        // Signed
        public bool SerializeSByte(ref sbyte value, int bitCount = sizeof(sbyte) * BSUtility.BYTE_BITS)
        {
            byte uValue = 0;
            if (!SerializeByte(ref uValue, bitCount))
                return false;

            value = (sbyte)((uValue >> 1) ^ (-(sbyte)(uValue & 1)));
            return true;
        }

        public bool SerializeShort(ref short value, int bitCount = sizeof(short) * BSUtility.BYTE_BITS)
        {
            ushort uValue = 0;
            if (!SerializeUShort(ref uValue, bitCount))
                return false;

            value = (short)((uValue >> 1) ^ (-(short)(uValue & 1)));
            return true;
        }

        public bool SerializeInt(ref int value, int bitCount = sizeof(int) * BSUtility.BYTE_BITS)
        {
            uint uValue = 0;
            if (!SerializeUInt(ref uValue, bitCount))
                return false;

            value = (int)((uValue >> 1) ^ (-(int)(uValue & 1)));
            return true;
        }

        public bool SerializeLong(ref long value, int bitCount = sizeof(long) * BSUtility.BYTE_BITS)
        {
            ulong uValue = 0;
            if (!SerializeULong(ref uValue, bitCount))
                return false;

            value = (long)(uValue >> 1) ^ (-(long)(uValue & 1));
            return true;
        }

        // Floating point
        public bool SerializeFloat(ref float value, BoundedRange range)
        {
            uint quanValue = 0;
            if (!SerializeUInt(ref quanValue, range.BitsRequired))
                return false;

            value = range.Dequantize(quanValue);
            return true;
        }

        public bool SerializeHalf(ref float value)
        {
            ushort quanValue = 0;
            if (!SerializeUShort(ref quanValue))
                return false;

            value = HalfPrecision.Dequantize(quanValue);
            return true;
        }

        // Vectors & Quaternions
        public bool SerializeVector2(ref Vector2 value, BoundedRange[] range)
        {
            float x = 0;
            if (SerializeFloat(ref x, range[0]))
                return false;

            float y = 0;
            if (SerializeFloat(ref y, range[1]))
                return false;

            value = new Vector2(x, y);
            return true;
        }

        public bool SerializeVector3(ref Vector3 value, BoundedRange[] range)
        {
            float x = 0;
            if (SerializeFloat(ref x, range[0]))
                return false;

            float y = 0;
            if (SerializeFloat(ref y, range[1]))
                return false;

            float z = 0;
            if (SerializeFloat(ref z, range[2]))
                return false;

            value = new Vector3(x, y, z);
            return true;
        }

        public bool SerializeVector4(ref Vector4 value, BoundedRange[] range)
        {
            float x = 0;
            if (SerializeFloat(ref x, range[0]))
                return false;

            float y = 0;
            if (SerializeFloat(ref y, range[1]))
                return false;

            float z = 0;
            if (SerializeFloat(ref z, range[2]))
                return false;

            float w = 0;
            if (SerializeFloat(ref w, range[3]))
                return false;

            value = new Vector4(x, y, z, w);
            return true;
        }

        public bool SerializeQuaternion(ref Quaternion value, int bitsPerElement = 12)
        {
            uint m = 0;
            if (!SerializeUInt(ref m, 2))
                return false;

            uint a = 0;
            if (!SerializeUInt(ref a, bitsPerElement))
                return false;

            uint b = 0;
            if (!SerializeUInt(ref b, bitsPerElement))
                return false;

            uint c = 0;
            if (!SerializeUInt(ref c, bitsPerElement))
                return false;

            QuantizedQuaternion quanQuat = new QuantizedQuaternion(m, a, b, c);

            value = SmallestThree.Dequantize(quanQuat, bitsPerElement);

            return true;
        }

        // String
        public bool SerializeString(ref string value, Encoding encoding)
        {
            if (encoding.Equals(null)) throw new ArgumentNullException(nameof(encoding));

            int length = 0;
            if (!SerializeInt(ref length))
                return false;

            if (length == 0)
            {
                value = string.Empty;
            }
            else
            {
                byte[] bytes = null;
                if (!SerializeBytes(ref bytes, length * BSUtility.BYTE_BITS))
                    return false;
                value = encoding.GetString(bytes);
            }
            return true;
        }

        // IPs
        public bool SerializeIPAddress(ref IPAddress ipAddress)
        {
            byte[] addressBytes = null;
            if (!SerializeBytes(ref addressBytes, 4 * BSUtility.BYTE_BITS))
                return false;

            ipAddress = new IPAddress(addressBytes);
            return true;
        }

        public bool SerializeIPEndPoint(ref IPEndPoint endPoint)
        {
            IPAddress ipAddress = null;
            if (!SerializeIPAddress(ref ipAddress))
                return false;

            ushort port = 0;
            if (!SerializeUShort(ref port))
                return false;
            
            endPoint.Address = ipAddress;
            endPoint.Port = port;
            return true;
        }

        // Bytes
        public bool SerializeBytes(ref byte[] bytes, int bitCount)
        {
            return Read(bitCount, ref bytes, (int)Math.Ceiling((double)bitCount / BSUtility.BYTE_BITS));
        }


        private bool Read(int bitCount, ref byte[] data, int typeBytes)
        {
            if (bitCount > TotalBits)
                return false;

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
