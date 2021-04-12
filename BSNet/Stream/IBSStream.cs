using BSNet.Quantization;
using System.Net;
using System.Text;

#if !(ENABLE_MONO || ENABLE_IL2CPP)
using System.Numerics;
#else
using UnityEngine;
#endif

namespace BSNet.Stream
{
    public interface IBSStream
    {
        bool Writing { get; }
        bool Reading { get; }
        int TotalBits { get; }

        // Unsigned
        bool SerializeByte(ref byte value, int bitCount = sizeof(byte) * BSUtility.BYTE_BITS);
        bool SerializeUShort(ref ushort value, int bitCount = sizeof(ushort) * BSUtility.BYTE_BITS);
        bool SerializeUInt(ref uint value, int bitCount = sizeof(uint) * BSUtility.BYTE_BITS);
        bool SerializeULong(ref ulong value, int bitCount = sizeof(ulong) * BSUtility.BYTE_BITS);

        // Signed
        bool SerializeSByte(ref sbyte value, int bitCount = sizeof(sbyte) * BSUtility.BYTE_BITS);
        bool SerializeShort(ref short value, int bitCount = sizeof(short) * BSUtility.BYTE_BITS);
        bool SerializeInt(ref int value, int bitCount = sizeof(int) * BSUtility.BYTE_BITS);
        bool SerializeLong(ref long value, int bitCount = sizeof(long) * BSUtility.BYTE_BITS);

        // Floating point
        bool SerializeFloat(ref float value, BoundedRange range);
        bool SerializeHalf(ref float value);

        // Vectors & Quaternions
        bool SerializeVector2(ref Vector2 vec, BoundedRange[] range);
        bool SerializeVector3(ref Vector3 vec, BoundedRange[] range);
        bool SerializeVector4(ref Vector4 vec, BoundedRange[] range);
        bool SerializeQuaternion(ref Quaternion quat, int bitsPerElement = 12);

        // String
        bool SerializeString(ref string value, Encoding encoding);

        // IPs
        bool SerializeIPAddress(ref IPAddress ipAddress);
        bool SerializeIPEndPoint(ref IPEndPoint endPoint);

        // Bytes
        bool SerializeBytes(ref byte[] bytes, int bitCount);
    }
}
