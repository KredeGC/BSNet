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
    public class BSWriter : IBSStream
    {
        public bool Writing { get { return true; } }
        public bool Reading { get { return false; } }

        public int TotalBits
        {
            get
            {
                return BSUtility.BYTE_BITS * internalStream.Count + bitPos - BSUtility.BYTE_BITS;
            }
        }

        private List<byte> internalStream = new List<byte>();
        private int bitPos = 1;
        private bool forceAddByte;


        // Unsigned
        public bool SerializeByte(ref byte value, int bitCount = sizeof(byte) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = new byte[] { value };
            return SerializeBytes(ref bytes, bitCount);
        }

        public bool SerializeUShort(ref ushort value, int bitCount = sizeof(ushort) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = new byte[2];
            bytes[0] = (byte)(value >> 8);
            bytes[1] = (byte)value;
            return SerializeBytes(ref bytes, bitCount);
        }

        public bool SerializeUInt(ref uint value, int bitCount = sizeof(uint) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(value >> 24);
            bytes[1] = (byte)(value >> 16);
            bytes[2] = (byte)(value >> 8);
            bytes[3] = (byte)value;
            return SerializeBytes(ref bytes, bitCount);
        }

        public bool SerializeULong(ref ulong value, int bitCount = sizeof(ulong) * BSUtility.BYTE_BITS)
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
            return SerializeBytes(ref bytes, bitCount);
        }

        // Signed
        public bool SerializeSByte(ref sbyte value, int bitCount = sizeof(sbyte) * BSUtility.BYTE_BITS)
        {
            byte zigzag = (byte)((value << 1) ^ (value >> 7));
            return SerializeByte(ref zigzag, bitCount);
        }

        public bool SerializeShort(ref short value, int bitCount = sizeof(short) * BSUtility.BYTE_BITS)
        {
            ushort zigzag = (ushort)((value << 1) ^ (value >> 15));
            return SerializeUShort(ref zigzag, bitCount);
        }

        public bool SerializeInt(ref int value, int bitCount = sizeof(int) * BSUtility.BYTE_BITS)
        {
            uint zigzag = (uint)((value << 1) ^ (value >> 31));
            return SerializeUInt(ref zigzag, bitCount);
        }

        public bool SerializeLong(ref long value, int bitCount = sizeof(long) * BSUtility.BYTE_BITS)
        {
            ulong zigzag = (ulong)((value << 1) ^ (value >> 63));
            return SerializeULong(ref zigzag, bitCount);
        }

        // Floating point
        public bool SerializeFloat(ref float value, BoundedRange range)
        {
            uint quanValue = range.Quantize(value);

            return SerializeUInt(ref quanValue, range.BitsRequired);
        }

        public bool SerializeHalf(ref float value)
        {
            ushort half = HalfPrecision.Quantize(value);

            return SerializeUShort(ref half);
        }

        // Vectors & Quaternions
        public bool SerializeVector2(ref Vector2 vec, BoundedRange[] range)
        {
            QuantizedVector2 quanVec = BoundedRange.Quantize(vec, range);

            if (!SerializeUInt(ref quanVec.x, range[0].BitsRequired))
                return false;

            if (!SerializeUInt(ref quanVec.y, range[1].BitsRequired))
                return false;

            return true;
        }

        public bool SerializeVector3(ref Vector3 vec, BoundedRange[] range)
        {
            QuantizedVector3 quanVec = BoundedRange.Quantize(vec, range);

            if (!SerializeUInt(ref quanVec.x, range[0].BitsRequired))
                return false;

            if (!SerializeUInt(ref quanVec.y, range[1].BitsRequired))
                return false;

            if (!SerializeUInt(ref quanVec.z, range[2].BitsRequired))
                return false;

            return true;
        }

        public bool SerializeVector4(ref Vector4 vec, BoundedRange[] range)
        {
            QuantizedVector4 quanVec = BoundedRange.Quantize(vec, range);

            if (!SerializeUInt(ref quanVec.x, range[0].BitsRequired))
                return false;
            if (!SerializeUInt(ref quanVec.y, range[1].BitsRequired))
                return false;
            if (!SerializeUInt(ref quanVec.z, range[2].BitsRequired))
                return false;
            if (!SerializeUInt(ref quanVec.w, range[3].BitsRequired))
                return false;

            return true;
        }

        public bool SerializeQuaternion(ref Quaternion quat, int bitsPerElement = 12)
        {
            QuantizedQuaternion quanQuat = SmallestThree.Quantize(quat, bitsPerElement);

            if (!SerializeUInt(ref quanQuat.m, 2))
                return false;
            if (!SerializeUInt(ref quanQuat.a, bitsPerElement))
                return false;
            if (!SerializeUInt(ref quanQuat.b, bitsPerElement))
                return false;
            if (!SerializeUInt(ref quanQuat.c, bitsPerElement))
                return false;

            return true;
        }

        // String
        public bool SerializeString(ref string value, Encoding encoding)
        {
            if (value.Equals(null)) throw new ArgumentNullException(nameof(value));
            if (encoding.Equals(null)) throw new ArgumentNullException(nameof(encoding));

            byte[] bytes = encoding.GetBytes(value);
            int length = bytes.Length;
            if (!SerializeInt(ref length))
                return false;

            if (bytes.Length > 0)
            {
                if (!SerializeBytes(ref bytes, bytes.Length * BSUtility.BYTE_BITS))
                    return false;
            }

            return true;
        }

        // IPs
        public bool SerializeIPAddress(ref IPAddress ipAddress)
        {
            byte[] bytes = ipAddress.GetAddressBytes();
            return SerializeBytes(ref bytes, 4 * BSUtility.BYTE_BITS);
        }

        public bool SerializeIPEndPoint(ref IPEndPoint endPoint)
        {
            IPAddress ipAddress = endPoint.Address;
            if (!SerializeIPAddress(ref ipAddress))
                return false;

            ushort port = (ushort)endPoint.Port;
            if (!SerializeUShort(ref port))
                return false;

            return true;
        }

        // Bytes
        public bool SerializeBytes(ref byte[] data, int bitCount)
        {
            byte[] raw = new byte[data.Length];
            Buffer.BlockCopy(data, 0, raw, 0, data.Length);
            Write(bitCount, raw, 0, raw.Length);
            return true;
        }


        public byte[] ToArray() => internalStream.ToArray();

        private int ExpandBuffer(int bitCount)
        {
            if (internalStream.Count == 0) internalStream.Add(0x00);

            int oldPos = internalStream.Count - 1;
            int bytesToAdd = 0;

            if ((bitCount + (bitPos - 1)) > BSUtility.BYTE_BITS)
            {
                int adjustedBitCount = bitCount - (BSUtility.BYTE_BITS - (bitPos - 1));
                bytesToAdd = adjustedBitCount / BSUtility.BYTE_BITS;
                if (adjustedBitCount % BSUtility.BYTE_BITS != 0) bytesToAdd++;
            }
            if (forceAddByte)
            {
                bytesToAdd++;
                oldPos++;
            }

            for (int i = 0; i < bytesToAdd; i++) internalStream.Add(0x00);

            forceAddByte = false;
            return oldPos;
        }

        private void Write(int bitCount, byte[] data, int offset, int length)
        {
            length = length - offset - 1;

            //if (BitConverter.IsLittleEndian) Array.Reverse(data);

            int bytePos = ExpandBuffer(bitCount);
            int srcBytePos = offset + length;
            int srcBitPos = 1;
            int consumedBits = 0;

            while (consumedBits < bitCount)
            {
                int bitsToConsume = Math.Min(bitCount - consumedBits, BSUtility.BYTE_BITS);
                byte rawValue = (byte)(data[srcBytePos] & BSUtility.GetNarrowingMask(bitsToConsume));
                int remainingBits = BSUtility.BYTE_BITS - (bitPos - 1);

                // Extract only the bits we need for the current byte
                // Assuming we have more bits than our current byte boundary, we have to apply some bits to the next byte
                if (bitsToConsume > remainingBits)
                {
                    internalStream[bytePos++] |= (byte)((byte)(rawValue >> (bitsToConsume - remainingBits)) & BSUtility.GetNarrowingMask(remainingBits));
                    bitPos = 1;
                    remainingBits = bitsToConsume - remainingBits;

                    internalStream[bytePos] |= (byte)(rawValue << (BSUtility.BYTE_BITS - remainingBits));
                    bitPos += remainingBits;
                    forceAddByte = false;
                }
                else
                {
                    internalStream[bytePos] |= (byte)(rawValue << (remainingBits - bitsToConsume));
                    bitPos += bitsToConsume;
                    if (bitPos > BSUtility.BYTE_BITS)
                    {
                        bitPos = 1;
                        bytePos++;
                        // If the bits are directly on the border of a byte boundary (e.g. packed 32 bits)
                        // Then we must indicate to the expansion function that it must add another byte
                        // Because it uses the position in the current byte to determine how many are needed
                        // But only if we end on this byte
                        forceAddByte = true;
                    }
                    else forceAddByte = false;
                }

                srcBitPos += bitsToConsume;
                if (srcBitPos > BSUtility.BYTE_BITS)
                {
                    srcBitPos = 1;
                    srcBytePos--;
                }

                consumedBits += bitsToConsume;
            }
        }
    }
}
