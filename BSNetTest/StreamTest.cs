using BSNet.Quantization;
using BSNet.Stream;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BSNetTest
{
    [TestClass]
    public class StreamTest
    {
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Test the ability to retrieve a writer
        /// <para/>Fails if the 2 writers refer to the same object
        /// </summary>
        [TestMethod]
        public void WriterPoolTest()
        {
            BSWriter occupy1 = BSWriter.Get(4);
            BSWriter occupy2 = BSWriter.Get(4);

            Assert.AreNotSame(occupy1, occupy2);

            BSWriter.Return(occupy1);
            BSWriter.Return(occupy2);
        }

        /// <summary>
        /// Test the ability to retrieve a reader
        /// <para/>Fails if the 2 readers refer to the same object
        /// </summary>
        [TestMethod]
        public void ReaderPoolTest()
        {
            byte[] rawBytes = new byte[1];

            BSReader occupy1 = BSReader.Get(rawBytes);
            BSReader occupy2 = BSReader.Get(rawBytes);

            Assert.AreNotSame(occupy1, occupy2);

            BSReader.Return(occupy1);
            BSReader.Return(occupy2);
        }

        /// <summary>
        /// Test the ability to export the byte array from one stream and write it directly into another
        /// <para>Fails if the stream size doesn't match, the bitcount doesn't match or the content doesn't match</para>
        /// </summary>
        [TestMethod]
        public void NestedWriterTest()
        {
            using (BSWriter writer1 = BSWriter.Get())
            {
                // Write some bits
                writer1.SerializeUInt(273U, 11);
                writer1.SerializeUInt(273U, 11);
                writer1.SerializeUInt(273U, 11);
                byte[] writer1Bytes = writer1.ToArray();

                using (BSWriter writer2 = BSWriter.Get())
                {
                    // Take that byte array and write it directly int a new writer
                    writer2.SerializeStream(writer1.TotalBits, writer1Bytes);

                    byte[] writer2Bytes = writer2.ToArray();

                    Assert.AreEqual(writer2.TotalBits, writer1.TotalBits);
                    Assert.AreEqual(writer2Bytes.Length, writer1Bytes.Length);

                    for (int i = 0; i < writer1Bytes.Length; i++)
                        Assert.AreEqual(writer1Bytes[i], writer2Bytes[i]);
                }
            }
        }

        /// <summary>
        /// Test the ability to read serialized data from one reader to another
        /// <para/>Fails if the serialized data doesn't match
        /// </summary>
        [TestMethod]
        public void NestedReaderTest()
        {
            BoundedRange range = new BoundedRange(0, 10, 0.0001f);
            byte[] rawBytes;
            using (BSWriter writer = BSWriter.Get())
            {
                // Write some non byte-aligned values
                writer.SerializeUInt(904, 10);
                writer.SerializeFloat(range, 7.123f);

                rawBytes = writer.ToArray();
            }

            byte[] inlineBytes;
            using (BSReader reader = BSReader.Get(rawBytes))
            {
                // Read the first value
                Assert.AreEqual(904U, reader.SerializeUInt(0, 10));

                // Read the second value as a byte array
                inlineBytes = reader.ToArray(); // Serializes stream without reading
                //inlineBytes = reader.SerializeStream(reader.TotalBits); // Serializes stream by reading
            }

            using (BSReader reader = BSReader.Get(inlineBytes))
            {
                // Read the second value from the byte array
                float quantizedFloat = range.Dequantize(range.Quantize(7.123f)); // Quantize float
                Assert.AreEqual(quantizedFloat, reader.SerializeFloat(range));
            }
        }

        /// <summary>
        /// Test the ability to read and write bits precisely
        /// <para/>Fails if the input doesn't match the output
        /// </summary>
        [TestMethod]
        public void PrecisionReadWriteTest()
        {
            byte[] rawBytes;
            using (BSWriter writer = BSWriter.Get())
            {
                writer.SerializeUInt(1U, 1);
                writer.SerializeUInt(273U, 11);
                writer.SerializeUInt(273U, 17);
                writer.SerializeUInt(16383U, 14);
                writer.SerializeUInt(511U, 11);
                writer.SerializeUInt(0b10101010111111111010101000001111, 32);
                writer.SerializeUInt(211U, 10);
                writer.SerializeUInt(126U, 7);
                rawBytes = writer.ToArray();
            }

            using (BSReader reader = BSReader.Get(rawBytes))
            {
                Assert.AreEqual(1U, reader.SerializeUInt(0, 1));
                Assert.AreEqual(273U, reader.SerializeUInt(0, 11));
                Assert.AreEqual(273U, reader.SerializeUInt(0, 17));
                Assert.AreEqual(16383U, reader.SerializeUInt(0, 14));
                Assert.AreEqual(511U, reader.SerializeUInt(0, 11));
                Assert.AreEqual(0b10101010111111111010101000001111, reader.SerializeUInt(0, 32));
                Assert.AreEqual(211U, reader.SerializeUShort(0, 10));
                Assert.AreEqual(126U, reader.SerializeUShort(0, 7));
            }
        }

        /// <summary>
        /// Test whether writing data with overflow produces the correct results
        /// <para/>Fails if the input doesn't match the output
        /// </summary>
        [TestMethod]
        public void OverflowReadWriteTest()
        {
            // Write some random bits
            byte[] rawBytes;
            using (BSWriter writer1 = BSWriter.Get(1))
            {
                writer1.SerializeUInt(uint.MaxValue, 10);
                writer1.SerializeUInt(uint.MaxValue, 3);
                writer1.SerializeUInt(uint.MaxValue, 11);
                writer1.SerializeUInt(uint.MaxValue, 31);
                rawBytes = writer1.ToArray();
            }

            // Read those bits back
            using (BSReader reader = BSReader.Get(rawBytes))
            {
                Assert.AreEqual(1023U, reader.SerializeUInt(0, 10));
                Assert.AreEqual(7U, reader.SerializeUInt(0, 3));
                Assert.AreEqual(2047U, reader.SerializeUInt(0, 11));
                Assert.AreEqual(2147483647U, reader.SerializeUInt(0, 31));
            }
        }

        /// <summary>
        /// Test a range of unsigned integers and compare size and content
        /// <para/>Fails if the size of the array doesn't match or the given integers don't match
        /// </summary>
        [TestMethod]
        public void UIntRangeReadWriteTest()
        {
            byte[] rawBytes;
            using (BSWriter writer = BSWriter.Get())
            {
                uint value = 1;
                for (int i = 1; i <= 32; i++)
                {
                    writer.SerializeUInt(value, i);

                    value <<= 1;
                }

                rawBytes = writer.ToArray();

                int expectedBitSize = 0;
                for (int i = 1; i <= 32; i++)
                    expectedBitSize += i;
                int expectedByteSize = (expectedBitSize - 1) / 8 + 1;

                Assert.AreEqual(expectedBitSize, writer.TotalBits);
                Assert.AreEqual(expectedByteSize, rawBytes.Length);
            }

            using (BSReader reader = BSReader.Get(rawBytes))
            {
                uint value = 1;
                for (int i = 1; i <= 32; i++)
                {
                    Assert.AreEqual(value, reader.SerializeUInt(0, i));

                    value <<= 1;
                }
            }
        }
    }
}