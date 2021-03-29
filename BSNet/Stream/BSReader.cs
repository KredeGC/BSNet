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
    public class BSReader
    {
        public bool IsFinished
        {
            get
            {
                return RemainingBits == 0;
            }
        }
        public int RemainingBits
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
        public byte ReadByte(int bitCount = sizeof(byte) * BSUtility.BYTE_BITS)
        {
            return ReadBytes(bitCount)[0];
        }

        public ushort ReadUShort(int bitCount = sizeof(ushort) * BSUtility.BYTE_BITS)
        {
            return (ushort)ReadULong(bitCount);
        }

        public uint ReadUInt(int bitCount = sizeof(uint) * BSUtility.BYTE_BITS)
        {
            return (uint)ReadULong(bitCount);
        }

        public ulong ReadULong(int bitCount = sizeof(ulong) * BSUtility.BYTE_BITS)
        {
            byte[] bytes = ReadBytes(bitCount);
            ulong val = 0;
            int shift = (bytes.Length - 1) * 8;
            for (int i = 0; i < bytes.Length; i++, shift -= 8)
                val |= (ulong)bytes[i] << shift;
            return val;
        }

        // Signed
        public short ReadShort(int bitCount = sizeof(short) * BSUtility.BYTE_BITS)
        {
            ushort value = ReadUShort(bitCount);
            short zagzig = (short)((value >> 1) ^ (-(short)(value & 1)));

            return zagzig;
        }

        public int ReadInt(int bitCount = sizeof(int) * BSUtility.BYTE_BITS)
        {
            uint value = ReadUInt(bitCount);
            int zagzig = (int)((value >> 1) ^ (-(int)(value & 1)));

            return zagzig;
        }

        public long ReadLong(int bitCount = sizeof(long) * BSUtility.BYTE_BITS)
        {
            ulong value = ReadULong(bitCount);
            long zagzig = (long)(value >> 1) ^ (-(long)(value & 1));

            return zagzig;
        }

        // Floating point
        public float ReadFloat(BoundedRange range)
        {
            uint quanValue = ReadUInt(range.BitsRequired);

            return range.Dequantize(quanValue);
        }

        public float ReadHalf()
        {
            ushort quanValue = ReadUShort();

            return HalfPrecision.Dequantize(quanValue);
        }

        // Vectors & Quaternions
        public Vector2 ReadVector2(BoundedRange[] range)
        {
            float x = ReadFloat(range[0]);
            float y = ReadFloat(range[1]);

            return new Vector2(x, y);
        }

        public Vector3 ReadVector3(BoundedRange[] range)
        {
            float x = ReadFloat(range[0]);
            float y = ReadFloat(range[1]);
            float z = ReadFloat(range[2]);

            return new Vector3(x, y, z);
        }

        public Vector4 ReadVector4(BoundedRange[] range)
        {
            float x = ReadFloat(range[0]);
            float y = ReadFloat(range[1]);
            float z = ReadFloat(range[2]);
            float w = ReadFloat(range[3]);

            return new Vector4(x, y, z, w);
        }

        public Quaternion ReadQuaternion(int bitsPerElement = 12)
        {
            uint m = ReadUInt(2);
            uint a = ReadUInt(bitsPerElement);
            uint b = ReadUInt(bitsPerElement);
            uint c = ReadUInt(bitsPerElement);

            QuantizedQuaternion quanQuat = new QuantizedQuaternion(m, a, b, c);

            return SmallestThree.Dequantize(quanQuat, bitsPerElement);
        }

        // String
        public string ReadString(Encoding encoding)
        {
            if (encoding.Equals(null)) throw new ArgumentNullException(nameof(encoding));

            int length = ReadInt();
            if (length == 0)
                return string.Empty;

            byte[] bytes = ReadBytes(length * BSUtility.BYTE_BITS);
            return encoding.GetString(bytes);
        }

        // IPs
        public IPAddress ReadIPAddress()
        {
            byte[] addressBytes = ReadBytes(4 * BSUtility.BYTE_BITS);

            return new IPAddress(addressBytes);
        }

        public IPEndPoint ReadIPEndPoint()
        {
            IPAddress ipAddress = ReadIPAddress();
            ushort port = ReadUShort();

            return new IPEndPoint(ipAddress, port);
        }


        public byte[] ReadBytes(int bitCount)
        {
            Read(bitCount, out byte[] data, (int)Math.Ceiling((double)bitCount / BSUtility.BYTE_BITS));
            return data;
        }

        private bool Read(int bitCount, out byte[] data, int typeBytes)
        {
            if (bitCount > RemainingBits)
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
