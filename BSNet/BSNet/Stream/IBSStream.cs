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

        // Padding
        int PadToEnd();
        int PadToByte();

        // Unsigned
        byte SerializeByte(byte value = default(byte), int bitCount = sizeof(byte) * BSUtility.BITS);
        ushort SerializeUShort(ushort value = default(ushort), int bitCount = sizeof(ushort) * BSUtility.BITS);
        uint SerializeUInt(uint value = default(uint), int bitCount = sizeof(uint) * BSUtility.BITS);
        ulong SerializeULong(ulong value = default(ulong), int bitCount = sizeof(ulong) * BSUtility.BITS);

        // Signed
        sbyte SerializeSByte(sbyte value = default(sbyte), int bitCount = sizeof(sbyte) * BSUtility.BITS);
        short SerializeShort(short value = default(short), int bitCount = sizeof(short) * BSUtility.BITS);
        int SerializeInt(int value = default(int), int bitCount = sizeof(int) * BSUtility.BITS);
        long SerializeLong(long value = default(long), int bitCount = sizeof(long) * BSUtility.BITS);

        // Floating point
        float SerializeFloat(BoundedRange range, float value = default(float));
        float SerializeHalf(float value = default(float));

        // Vectors & Quaternions
        Vector2 SerializeVector2(BoundedRange[] range, Vector2 vec = default(Vector2));
        Vector3 SerializeVector3(BoundedRange[] range, Vector3 vec = default(Vector3));
        Vector4 SerializeVector4(BoundedRange[] range, Vector4 vec = default(Vector4));
        Quaternion SerializeQuaternion(int bitsPerElement = 12, Quaternion quat = default(Quaternion));

        // String
        string SerializeString(Encoding encoding, string value = null);

        // IPs
        IPAddress SerializeIPAddress(IPAddress ipAddress = default(IPAddress));
        IPEndPoint SerializeIPEndPoint(IPEndPoint endPoint = default(IPEndPoint));

        // Bytes
        byte[] SerializeBytes(byte[] bytes = null);
        byte[] SerializeBytes(int bitCount, byte[] bytes = null, bool trimRight = false);
    }
}
