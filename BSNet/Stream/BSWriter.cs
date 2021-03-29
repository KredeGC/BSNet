/*
 * The MIT License (MIT)
 * Copyright (c) 2015-2017 Bui
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of
 * this software and associated documentation files (the "Software"), to deal in
 * the Software without restriction, including without limitation the rights to
 * use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
 * the Software, and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
 * FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
 * COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
 * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
 * CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

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
    public class BSWriter
    {
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
        public void WriteByte(byte value, int bitCount = sizeof(byte) * BSUtility.BYTE_BITS)
        {
            WriteBytes(bitCount, new byte[] { value });
        }

        public void WriteUShort(ushort value, int bitCount = sizeof(ushort) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = new byte[2];
            bytes[0] = (byte)(value >> 8);
            bytes[1] = (byte)value;
            WriteBytes(bitCount, bytes);
        }

        public void WriteUInt(uint value, int bitCount = sizeof(uint) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(value >> 24);
            bytes[1] = (byte)(value >> 16);
            bytes[2] = (byte)(value >> 8);
            bytes[3] = (byte)value;
            WriteBytes(bitCount, bytes);
        }

        public void WriteULong(ulong value, int bitCount = sizeof(ulong) * BSUtility.BYTE_BITS)
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
            WriteBytes(bitCount, bytes);
        }

        // Signed
        public void WriteSByte(sbyte value, int bitCount = sizeof(sbyte) * BSUtility.BYTE_BITS)
        {
            byte zigzag = (byte)((value << 1) ^ (value >> 7));
            WriteByte(zigzag, bitCount);
        }

        public void WriteShort(short value, int bitCount = sizeof(short) * BSUtility.BYTE_BITS)
        {
            ushort zigzag = (ushort)((value << 1) ^ (value >> 15));
            WriteUShort(zigzag, bitCount);
        }

        public void WriteInt(int value, int bitCount = sizeof(int) * BSUtility.BYTE_BITS)
        {
            uint zigzag = (uint)((value << 1) ^ (value >> 31));
            WriteUInt(zigzag, bitCount);
        }

        public void WriteLong(long value, int bitCount = sizeof(long) * BSUtility.BYTE_BITS)
        {
            ulong zigzag = (ulong)((value << 1) ^ (value >> 63));
            WriteULong(zigzag, bitCount);
        }

        // Floating point
        public void WriteFloat(float value, BoundedRange range)
        {
            uint quanValue = range.Quantize(value);

            WriteUInt(quanValue, range.BitsRequired);
        }

        public void WriteHalf(float value)
        {
            ushort half = HalfPrecision.Quantize(value);

            WriteUShort(half);
        }

        // Vectors & Quaternions
        public void WriteVector2(Vector2 vec, BoundedRange[] range)
        {
            QuantizedVector2 quanVec = BoundedRange.Quantize(vec, range);

            WriteUInt(quanVec.x, range[0].BitsRequired);
            WriteUInt(quanVec.y, range[1].BitsRequired);
        }

        public void WriteVector3(Vector3 vec, BoundedRange[] range)
        {
            QuantizedVector3 quanVec = BoundedRange.Quantize(vec, range);

            WriteUInt(quanVec.x, range[0].BitsRequired);
            WriteUInt(quanVec.y, range[1].BitsRequired);
            WriteUInt(quanVec.z, range[2].BitsRequired);
        }

        public void WriteVector4(Vector4 vec, BoundedRange[] range)
        {
            QuantizedVector4 quanVec = BoundedRange.Quantize(vec, range);

            WriteUInt(quanVec.x, range[0].BitsRequired);
            WriteUInt(quanVec.y, range[1].BitsRequired);
            WriteUInt(quanVec.z, range[2].BitsRequired);
            WriteUInt(quanVec.w, range[3].BitsRequired);
        }

        public void WriteQuaternion(Quaternion quat, int bitsPerElement = 12)
        {
            QuantizedQuaternion quanQuat = SmallestThree.Quantize(quat, bitsPerElement);

            WriteUInt(quanQuat.m, 2);
            WriteUInt(quanQuat.a, bitsPerElement);
            WriteUInt(quanQuat.b, bitsPerElement);
            WriteUInt(quanQuat.c, bitsPerElement);
        }

        // String
        public void WriteString(string value, Encoding encoding)
        {
            if (value.Equals(null)) throw new ArgumentNullException(nameof(value));
            if (encoding.Equals(null)) throw new ArgumentNullException(nameof(encoding));
            byte[] bytes = encoding.GetBytes(value);
            WriteInt(bytes.Length);
            if (bytes.Length > 0)
                WriteBytes(bytes.Length * BSUtility.BYTE_BITS, bytes);
        }

        // IPs
        public void WriteIPAddress(IPAddress ipAddress)
        {
            WriteBytes(4 * BSUtility.BYTE_BITS, ipAddress.GetAddressBytes());
        }

        public void WriteIPEndPoint(IPEndPoint endPoint)
        {
            WriteIPAddress(endPoint.Address);
            WriteUShort((ushort)endPoint.Port);
        }


        public byte[] ToArray() => internalStream.ToArray();

        public void WriteBytes(int bitCount, byte[] data)
        {
            byte[] raw = new byte[data.Length];
            Buffer.BlockCopy(data, 0, raw, 0, data.Length);
            Write(bitCount, raw, 0, raw.Length);
        }

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
