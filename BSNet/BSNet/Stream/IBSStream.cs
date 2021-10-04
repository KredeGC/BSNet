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
    /// <summary>
    /// A stream of bytes that can be read or written to, based on the appropriate flags
    /// <para/>Includes methods to pack bits tightly
    /// </summary>
    public interface IBSStream
    {
        /// <summary>
        /// Whether this stream is writing
        /// </summary>
        bool Writing { get; }
        
        /// <summary>
        /// Whether this stream is reading
        /// </summary>
        bool Reading { get; }
        
        /// <summary>
        /// The total amount of bits read / written
        /// </summary>
        int TotalBits { get; }
        
        /// <summary>
        /// Whether this stream has been corrupted while writing / reading
        /// </summary>
        bool Corrupt { get; }

        #region Padding
        
        /// <summary>
        /// Adds padding to the end of the stream
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <returns>The amount of padding added</returns>
        int PadToEnd();

        /// <summary>
        /// Adds padding until next even byte
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <returns>The amount of padding added</returns>
        int PadToByte();
        
        #endregion

        #region Bool
        
        /// <summary>
        /// Serializes a bool as a single bit
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="value">The bool to serialize</param>
        /// <returns>The serialized bool</returns>
        bool SerializeBool(bool value = default(bool));
        
        #endregion

        #region Unsigned
        
        /// <summary>
        /// Serializes a byte, using the given bitCount
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="value">The byte to serialize</param>
        /// <param name="bitCount">The amount of bits to use, from little-endian</param>
        /// <returns>The serialized byte</returns>
        byte SerializeByte(byte value = default(byte), int bitCount = sizeof(byte) * BSUtility.BITS);

        /// <summary>
        /// Serializes an unsigned short, using the given bitCount
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="value">The unsigned short to serialize</param>
        /// <param name="bitCount">The amount of bits to use, from little-endian</param>
        /// <returns>The serialized unsigned short</returns>
        ushort SerializeUShort(ushort value = default(ushort), int bitCount = sizeof(ushort) * BSUtility.BITS);

        /// <summary>
        /// Serializes an unsigned int, using the given bitCount
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="value">The unsigned int to serialize</param>
        /// <param name="bitCount">The amount of bits to use, from little-endian</param>
        /// <returns>The serialized unsigned int</returns>
        uint SerializeUInt(uint value = default(uint), int bitCount = sizeof(uint) * BSUtility.BITS);

        /// <summary>
        /// Serializes an unsigned long, using the given bitCount
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="value">The unsigned long to serialize</param>
        /// <param name="bitCount">The amount of bits to use, from little-endian</param>
        /// <returns>The serialized unsigned long</returns>
        ulong SerializeULong(ulong value = default(ulong), int bitCount = sizeof(ulong) * BSUtility.BITS);
        
        #endregion

        #region Signed
        
        /// <summary>
        /// Serializes a signed byte, using the given bitCount
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="value">The signed byte to serialize</param>
        /// <param name="bitCount">The amount of bits to use, from little-endian</param>
        /// <returns>The serialized signed byte</returns>
        sbyte SerializeSByte(sbyte value = default(sbyte), int bitCount = sizeof(sbyte) * BSUtility.BITS);

        /// <summary>
        /// Serializes a short, using the given bitCount
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="value">The short to serialize</param>
        /// <param name="bitCount">The amount of bits to use, from little-endian</param>
        /// <returns>The serialized short</returns>
        short SerializeShort(short value = default(short), int bitCount = sizeof(short) * BSUtility.BITS);

        /// <summary>
        /// Serializes a int, using the given bitCount
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="value">The int to serialize</param>
        /// <param name="bitCount">The amount of bits to use, from little-endian</param>
        /// <returns>The serialized int</returns>
        int SerializeInt(int value = default(int), int bitCount = sizeof(int) * BSUtility.BITS);

        /// <summary>
        /// Serializes a long, using the given bitCount
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="value">The long to serialize</param>
        /// <param name="bitCount">The amount of bits to use, from little-endian</param>
        /// <returns>The serialized long</returns>
        long SerializeLong(long value = default(long), int bitCount = sizeof(long) * BSUtility.BITS);
        
        #endregion

        #region Floating point
        
        /// <summary>
        /// Serializes and quantizes a float, using a BoundedRange
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="range">The BoundedRange to quantize with</param>
        /// <param name="value">The float to serialize</param>
        /// <returns>The serialized float</returns>
        float SerializeFloat(BoundedRange range, float value = default(float));

        /// <summary>
        /// Serializes and quantizes a float into a half
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="value">The float to serialize and quantize</param>
        /// <returns>The serialized float</returns>
        float SerializeHalf(float value = default(float));
        
        #endregion

        #region Vectors & Quaternions
        
        /// <summary>
        /// Serializes and quantizes a Vector2, using an array of BoundedRange
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="range">The array of BoundedRange to quantize with</param>
        /// <param name="vec">The Vector2 to serialize</param>
        /// <returns>The serialized Vector2</returns>
        Vector2 SerializeVector2(BoundedRange[] range, Vector2 vec = default(Vector2));

        /// <summary>
        /// Serializes and quantizes a Vector3, using an array of BoundedRange
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="range">The array of BoundedRange to quantize with</param>
        /// <param name="vec">The Vector3 to serialize</param>
        /// <returns>The serialized Vector3</returns>
        Vector3 SerializeVector3(BoundedRange[] range, Vector3 vec = default(Vector3));

        /// <summary>
        /// Serializes and quantizes a Vector4, using an array of BoundedRange
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="range">The array of BoundedRange to quantize with</param>
        /// <param name="vec">The Vector4 to serialize</param>
        /// <returns>The serialized Vector4</returns>
        Vector4 SerializeVector4(BoundedRange[] range, Vector4 vec = default(Vector4));

        /// <summary>
        /// Serializes and quantizes a Quaternion, using the the given bits per element
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="bitsPerElement">The bits per element to quantize with</param>
        /// <param name="quat">The Quaternion to serialize</param>
        /// <returns>The serialized Quaternion</returns>
        Quaternion SerializeQuaternion(int bitsPerElement = 12, Quaternion quat = default(Quaternion));
        
        #endregion

        #region String
        
        /// <summary>
        /// Serializes a string, using a TextEncoding
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="encoding">The encoding to use to serialize the string</param>
        /// <param name="value">The string to serialize</param>
        /// <returns>The serialized string</returns>
        string SerializeString(Encoding encoding, string value = null);
        
        #endregion
        
        #region IPs
        
        /// <summary>
        /// Serializes an IPAddress
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="ipAddress">The IPAddress to serialize</param>
        /// <returns>The serialized IPAddress</returns>
        IPAddress SerializeIPAddress(IPAddress ipAddress = default(IPAddress));

        /// <summary>
        /// Serializes an IPEndPoint
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="endPoint">The IPEndPoint to serialize</param>
        /// <returns>The serialized IPEndPoint</returns>
        IPEndPoint SerializeIPEndPoint(IPEndPoint endPoint = default(IPEndPoint));
        
        #endregion

        #region Bytes
        
        /// <summary>
        /// Serializes a given amount from an array of bytes, and shifts everything to be byte-aligned
        /// </summary>
        /// <exception cref="System.NullReferenceException"/>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="bitCount">The amount of bits to serialize, from little-endian</param>
        /// <param name="bytes">The byte array to serialize</param>
        /// <returns>The serialized byte array</returns>
        byte[] SerializeStream(int bitCount, byte[] bytes = null);

        /// <summary>
        /// Serializes a given amount from an array of bytes
        /// </summary>
        /// <exception cref="System.NullReferenceException"/>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="bitCount">The amount of bits to serialize, from little-endian</param>
        /// <param name="bytes">The byte array to serialize</param>
        /// <returns>The serialized byte array</returns>
        byte[] SerializeBytes(int bitCount, byte[] bytes = null);

        /// <summary>
        /// Serializes an array of bytes
        /// </summary>
        /// <exception cref="System.NullReferenceException"/>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <param name="bytes">The byte array to serialize</param>
        /// <returns>The serialized byte array</returns>
        byte[] SerializeBytes(byte[] bytes = null);
        
        #endregion

        /// <summary>
        /// Return a copy of the internal stream as a byte array without reading/writing
        /// </summary>
        /// <returns>A copy of the internal stream</returns>
        byte[] ToArray();
    }
}
