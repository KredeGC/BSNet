﻿using System;
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


        // Padding
        public byte[] PadToEnd()
        {
            int remaining = BSSocket.RECEIVE_BUFFER_SIZE * BSUtility.BYTE_BITS - TotalBits;
            byte[] padding = new byte[BSSocket.RECEIVE_BUFFER_SIZE];
            SerializeBytes(remaining, padding);
            return padding;
        }

        // Unsigned
        public byte SerializeByte(byte value = default(byte), int bitCount = sizeof(byte) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = new byte[] { value };
            SerializeBytes(bitCount, bytes);
            return value;
        }

        public ushort SerializeUShort(ushort value = default(ushort), int bitCount = sizeof(ushort) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = new byte[2];
            bytes[0] = (byte)(value >> 8);
            bytes[1] = (byte)value;
            SerializeBytes(bitCount, bytes);
            return value;
        }

        public uint SerializeUInt(uint value = default(uint), int bitCount = sizeof(uint) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(value >> 24);
            bytes[1] = (byte)(value >> 16);
            bytes[2] = (byte)(value >> 8);
            bytes[3] = (byte)value;
            SerializeBytes(bitCount, bytes);
            return value;
        }

        public ulong SerializeULong(ulong value = default(ulong), int bitCount = sizeof(ulong) * BSUtility.BYTE_BITS)
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
        public sbyte SerializeSByte(sbyte value = default(sbyte), int bitCount = sizeof(sbyte) * BSUtility.BYTE_BITS)
        {
            byte zigzag = (byte)((value << 1) ^ (value >> 7));
            SerializeByte(zigzag, bitCount);
            return value;
        }

        public short SerializeShort(short value = default(short), int bitCount = sizeof(short) * BSUtility.BYTE_BITS)
        {
            ushort zigzag = (ushort)((value << 1) ^ (value >> 15));
            SerializeUShort(zigzag, bitCount);
            return value;
        }

        public int SerializeInt(int value = default(int), int bitCount = sizeof(int) * BSUtility.BYTE_BITS)
        {
            uint zigzag = (uint)((value << 1) ^ (value >> 31));
            SerializeUInt(zigzag, bitCount);
            return value;
        }

        public long SerializeLong(long value = default(long), int bitCount = sizeof(long) * BSUtility.BYTE_BITS)
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
                SerializeBytes(bytes.Length * BSUtility.BYTE_BITS, bytes);

            return value;
        }

        // IPs
        public IPAddress SerializeIPAddress(IPAddress ipAddress)
        {
            byte[] bytes = ipAddress.GetAddressBytes();
            SerializeBytes(4 * BSUtility.BYTE_BITS, bytes);
            return ipAddress;
        }

        public IPEndPoint SerializeIPEndPoint(IPEndPoint endPoint)
        {
            SerializeIPAddress(endPoint.Address);
            SerializeUShort((ushort)endPoint.Port);

            return endPoint;
        }

        // Bytes
        public byte[] SerializeBytes(int bitCount, byte[] data = null)
        {
            byte[] raw = new byte[data.Length];
            Buffer.BlockCopy(data, 0, raw, 0, data.Length);
            Write(bitCount, raw, 0, raw.Length);
            return raw;
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
